using System.ComponentModel.DataAnnotations;
using Gratia.Application.Common;
using Gratia.Application.DTOs.Invitation;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Gratia.Domain.Repositories;

namespace Gratia.Application.Features.Invitations;

// The checks of a bulk invitation (#54), the same for the preview and the import, so what the preview calls Ready is
// what the import invites (unless something changed in between: the import checks again)
internal static class InvitationImport
{
    public const int MaxRows = 500;
    public const long MaxFileBytes = 1024 * 1024;

    // Role is null when the row's isn't Employee or Admin
    public sealed record CheckedRow(SpreadsheetRow Input, string Email, UserRole? Role, ImportRowStatus Status, string? Message);

    public static async Task<IReadOnlyList<CheckedRow>> CheckAsync(IReadOnlyList<SpreadsheetRow> rows, Company company,
        IUserRepository userRepository, IInvitationRepository invitationRepository, CancellationToken cancellationToken)
    {
        if (rows.Count > MaxRows)
            throw new Exceptions.BusinessRuleException($"An import can have at most {MaxRows} people. Split the list and import each part.");

        var emails = rows.Select(r => EmailAddresses.Normalize(r.Email)).Where(e => e.Length > 0).Distinct().ToList();
        // Only this company's: an address with an account elsewhere is invited like any other, as with one invite (#61)
        var accounts = (await userRepository.GetInCompanyByEmailsAsync(company.Id, emails, cancellationToken)).ToDictionary(u => u.Email);
        var invited = (await invitationRepository.GetActiveByEmailsAsync(company.Id, emails, cancellationToken))
            .GroupBy(i => i.Email)
            .ToDictionary(g => g.Key, g => g.Any(i => i.EmailStatus == InvitationEmailStatus.Failed));

        var firstRowOf = new Dictionary<string, int>();
        var result = new List<CheckedRow>(rows.Count);
        foreach (var row in rows)
        {
            var email = EmailAddresses.Normalize(row.Email);
            var (role, status, message) = Check(row, email, company, accounts, invited, firstRowOf);
            result.Add(new CheckedRow(row, email, role, status, message));
        }

        return result;
    }

    private static (UserRole? Role, ImportRowStatus Status, string? Message) Check(SpreadsheetRow row, string email, Company company,
        Dictionary<string, User> accounts, Dictionary<string, bool> invited, Dictionary<string, int> firstRowOf)
    {
        // By name only, as the API takes roles everywhere (#52); empty means Employee
        UserRole? role = row.Role.Length == 0
            ? UserRole.Employee
            : Enum.GetNames<UserRole>().FirstOrDefault(n => n.Equals(row.Role, StringComparison.OrdinalIgnoreCase)) is { } name
                ? Enum.Parse<UserRole>(name)
                : null;

        string? Problem()
        {
            if (email.Length == 0)
                return "There's no email.";
            if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
                return "This isn't an email address.";
            if (row.FullName.Length > CompanyInvitation.MaxNameLength)
                return $"The full name is longer than {CompanyInvitation.MaxNameLength} characters.";
            if (row.JobTitle.Length > CompanyInvitation.MaxNameLength)
                return $"The job title is longer than {CompanyInvitation.MaxNameLength} characters.";
            if (role is null)
                return "The role must be Employee or Admin.";
            // The same rule as inviting one person (#1, #61)
            if (company.Domain is not null && !company.IsDomainVerified && !EmailAddresses.IsOnDomain(email, company.Domain))
                return $"Until your company's domain is verified, you can only invite @{company.Domain} addresses.";
            return null;
        }

        if (Problem() is { } problem)
            return (role, ImportRowStatus.Error, problem);

        if (firstRowOf.TryGetValue(email, out var first))
            return (role, ImportRowStatus.Duplicate, $"The same email as row {first}.");
        firstRowOf[email] = row.Row;

        if (accounts.TryGetValue(email, out var account))
            return (role, ImportRowStatus.Member, account.DeactivatedAt is null
                ? "Already a member."
                : "A deactivated member: reactivate them instead.");

        if (invited.TryGetValue(email, out var failed))
            return (role, ImportRowStatus.Invited, failed
                ? "Already invited, but the email couldn't be sent: resend it from the list of invitations."
                : "Already invited.");

        return (role, ImportRowStatus.Ready, null);
    }

    public static ImportPreviewRowDto ToDto(this CheckedRow row) => new()
    {
        Row = row.Input.Row,
        Email = row.Email,
        FullName = row.Input.FullName,
        JobTitle = row.Input.JobTitle,
        Role = row.Role?.ToString() ?? row.Input.Role,
        Status = row.Status,
        Message = row.Message
    };
}

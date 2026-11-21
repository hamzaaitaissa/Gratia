using Gratia.Application.DTOs.Invitation;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Application.Settings;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Mediator;
using Microsoft.Extensions.Options;

namespace Gratia.Application.Features.Invitations.Commands;

// Invites the rows of a previewed spreadsheet (#54). Every row is checked again, since things may have changed since
// the preview. The ready ones are invited in one commit, with their emails queued (#55); the others are skipped, with
// why. An unverified workspace's daily cap applies to each row (#65): the rows past it are skipped too.
public sealed record ImportInvitationsCommand(Guid CompanyId, Guid InvitedByUserId, IReadOnlyList<ImportRowDto> Rows) : ICommand<ImportResultDto>;

public sealed class ImportInvitationsCommandHandler(
    IInvitationRepository invitationRepository,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IUnitOfWork unitOfWork,
    IInvitationEmailQueue emailQueue,
    IOptions<OnboardingOptions> onboardingOptions)
    : ICommandHandler<ImportInvitationsCommand, ImportResultDto>
{
    public async ValueTask<ImportResultDto> Handle(ImportInvitationsCommand command, CancellationToken cancellationToken)
    {
        var company = await companyRepository.GetByIdAsync(command.CompanyId)
            ?? throw new NotFoundException("Company not found");
        var inviter = await userRepository.GetByIdAsync(command.InvitedByUserId)
            ?? throw new NotFoundException("User not found");

        var rows = command.Rows
            .Select((r, i) => new SpreadsheetRow(r.Row > 0 ? r.Row : i + 1, Trim(r.Email), Trim(r.FullName), Trim(r.JobTitle), Trim(r.Role)))
            .ToList();
        var checkedRows = await InvitationImport.CheckAsync(rows, company, userRepository, invitationRepository, cancellationToken);

        var result = new ImportResultDto();
        var now = DateTime.UtcNow;
        await using (var dbTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            foreach (var row in checkedRows)
            {
                if (row.Status != ImportRowStatus.Ready)
                {
                    result.Skipped.Add(new ImportSkippedRowDto { Row = row.Input.Row, Email = row.Email, Status = row.Status, Reason = row.Message! });
                    continue;
                }

                if (!await InvitationQuota.TryCountAsync(company, companyRepository, onboardingOptions.Value, now, cancellationToken))
                {
                    result.Skipped.Add(new ImportSkippedRowDto
                    {
                        Row = row.Input.Row,
                        Email = row.Email,
                        Status = ImportRowStatus.Error,
                        Reason = InvitationQuota.Message(onboardingOptions.Value, InvitationQuota.RetryAfter(company, now))
                    });
                    continue;
                }

                var invitation = new CompanyInvitation(row.Email, InvitationTokens.Generate(), now.Add(InvitationTokens.Lifetime), row.Role!.Value,
                    company.Id, inviter.Id)
                {
                    FullName = NullIfEmpty(row.Input.FullName),
                    JobTitle = NullIfEmpty(row.Input.JobTitle)
                };
                invitation.QueueEmail(now);
                invitationRepository.Add(invitation);
                result.Invited++;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }

        if (result.Invited > 0)
            emailQueue.Notify();
        return result;
    }

    private static string Trim(string? value) => value?.Trim() ?? "";

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}

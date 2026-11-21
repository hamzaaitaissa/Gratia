using Gratia.Application.Exceptions;
using Gratia.Application.Settings;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;

namespace Gratia.Application.Features.Invitations;

// #65: until its domain is verified (#61), a workspace can send OnboardingOptions.UnverifiedInvitationsPerDay invitation
// emails a day, invites, resends and imported rows together. Counted per email rather than per invitation, so
// cancelling one doesn't give its place back. Call it inside the transaction that saves the invitation, so an invitation
// that isn't saved isn't counted.
internal static class InvitationQuota
{
    // Counts one email, or throws TooManyAttemptsException (429) at the cap
    public static async Task CountAsync(Company company, ICompanyRepository companyRepository, OnboardingOptions options,
        DateTime now, CancellationToken cancellationToken)
    {
        if (await TryCountAsync(company, companyRepository, options, now, cancellationToken))
            return;

        var retryAfter = RetryAfter(company, now);
        throw new TooManyAttemptsException(Message(options, retryAfter), retryAfter);
    }

    // Counts one email, or returns false at the cap (an import skips the rows past it, #54)
    public static async Task<bool> TryCountAsync(Company company, ICompanyRepository companyRepository, OnboardingOptions options,
        DateTime now, CancellationToken cancellationToken) =>
        company.IsDomainVerified
        || await companyRepository.TryCountInvitationAsync(company.Id, now, options.UnverifiedInvitationsPerDay, cancellationToken);

    // The window as this request read the company. If another request has just started a new one, a minute.
    public static TimeSpan RetryAfter(Company company, DateTime now)
    {
        var retryAfter = (company.InvitationWindowStart ?? now) + Company.InvitationWindow - now;
        return retryAfter < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : retryAfter;
    }

    public static string Message(OnboardingOptions options, TimeSpan retryAfter)
    {
        var hours = (int)Math.Ceiling(retryAfter.TotalHours);
        return $"Until your company's domain is verified, it can send {options.UnverifiedInvitationsPerDay} invitation emails a day. " +
            $"Try again in {hours} hour{(hours == 1 ? "" : "s")}.";
    }
}

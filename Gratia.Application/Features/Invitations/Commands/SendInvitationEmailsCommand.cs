using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Domain.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Gratia.Application.Features.Invitations.Commands;

// Sends the invitation emails that are due (#55): new invitations, resends, and failures whose next try has come. The
// API's InvitationEmailService sends it in the background; the integration tests send it themselves. Every instance may
// run it: each email is claimed in the database first, so it goes out once. Returns how many were sent.
public sealed record SendInvitationEmailsCommand(DateTime Now) : ICommand<int>;

public sealed class SendInvitationEmailsCommandHandler(
    IInvitationRepository invitationRepository,
    IEmailService emailService,
    ILogger<SendInvitationEmailsCommandHandler> logger)
    : ICommandHandler<SendInvitationEmailsCommand, int>
{
    public const int BatchSize = 50;
    public const int MaxAttempts = 5;

    // How long a sender has an email before another may take it, should the first one stop halfway
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

    // After the 1st, 2nd, 3rd and 4th failure; the 5th is the last
    private static readonly TimeSpan[] Backoff =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2)];

    public async ValueTask<int> Handle(SendInvitationEmailsCommand command, CancellationToken cancellationToken)
    {
        var now = command.Now;
        var sent = 0;
        foreach (var id in await invitationRepository.GetDueEmailIdsAsync(now, BatchSize, cancellationToken))
        {
            if (!await invitationRepository.TryClaimEmailAsync(id, now, now + Lease, cancellationToken))
                continue; // another sender has it

            // Gone if it was cancelled since. Founding invitations have no inviter and are never queued.
            var invitation = await invitationRepository.GetForEmailAsync(id, cancellationToken);
            if (invitation?.InvitedByUser is not { } inviter)
                continue;

            var token = invitation.PendingToken ?? invitation.InvitationToken;
            try
            {
                await emailService.SendInvitationEmailAsync(invitation.Email, token, invitation.Company.Name, inviter.FullName,
                    inviter.Email, cancellationToken);
            }
            catch (EmailDeliveryException ex)
            {
                var attempts = invitation.EmailAttempts + 1;
                DateTime? next = attempts < MaxAttempts ? now + Backoff[attempts - 1] : null;
                await invitationRepository.RecordEmailFailureAsync(id, token, next, cancellationToken);
                logger.LogWarning(ex, "Invitation {InvitationId}'s email failed ({Attempts} of {MaxAttempts}); {Next}",
                    id, attempts, MaxAttempts, next is null ? "giving up until it's resent" : $"trying again at {next:u}");
                continue;
            }

            // The link is good for the full 7 days from when it went out
            if (await invitationRepository.MarkEmailSentAsync(id, token, now, now + InvitationTokens.Lifetime, cancellationToken))
                sent++;
        }

        return sent;
    }
}

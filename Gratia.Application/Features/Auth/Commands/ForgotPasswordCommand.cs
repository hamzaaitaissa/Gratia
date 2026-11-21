using Gratia.Application.Common;
using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Interfaces;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Auth.Commands;

// Asks for a forgotten-password link (#50). The controller always answers 202, and every path sends exactly one email,
// so neither the answer nor how long it takes tells whether the email has an active account. Only the mailbox owner
// learns which: they get the link, or a notice that there's no active account.
public sealed record ForgotPasswordCommand(ForgotPasswordDto Request) : ICommand;

public sealed class ForgotPasswordCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork, IEmailService emailService)
    : ICommandHandler<ForgotPasswordCommand>
{
    public async ValueTask<Unit> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        var email = EmailAddresses.Normalize(command.Request.Email!);
        var user = await userRepository.GetByEmailAsync(email);
        if (user is null || user.DeactivatedAt is not null)
        {
            await emailService.SendPasswordResetUnavailableEmailAsync(email, cancellationToken);
            return Unit.Value;
        }

        // A new link replaces the last one. Saved before sending, so it works the moment it arrives; only its hash is stored.
        var token = SecretTokens.Generate();
        var (previousHash, previousExpiry) = (user.PasswordResetTokenHash, user.PasswordResetExpiresAt);
        user.StartPasswordReset(SecretTokens.Hash(token), DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, token, cancellationToken);
        }
        catch
        {
            // #20: a link nobody received would replace one that may already be on its way. Not cancellable: the
            // request may be what was cancelled.
            user.PasswordResetTokenHash = previousHash;
            user.PasswordResetExpiresAt = previousExpiry;
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        return Unit.Value;
    }
}

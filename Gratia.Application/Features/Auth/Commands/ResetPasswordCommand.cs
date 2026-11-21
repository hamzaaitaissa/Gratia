using Gratia.Application.Common;
using Gratia.Application.DTOs.Token;
using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Domain.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Gratia.Application.Features.Auth.Commands;

// Sets a forgotten password with the token from the emailed link (#50), and signs the user in on this device: the link
// proved the mailbox. Every other session ends. The email's failed sign-ins are cleared too (#72): being locked out is
// often why people reset.
public sealed record ResetPasswordCommand(ResetPasswordDto Reset) : ICommand<TokenResponseDto>;

public sealed class ResetPasswordCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ILoginAttemptRepository loginAttemptRepository,
    IUnitOfWork unitOfWork,
    IPasswordHashingService passwordHashingService,
    ITokenService tokenService,
    IEmailService emailService,
    ILogger<ResetPasswordCommandHandler> logger)
    : ICommandHandler<ResetPasswordCommand, TokenResponseDto>
{
    public async ValueTask<TokenResponseDto> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByPasswordResetTokenHashAsync(SecretTokens.Hash(command.Reset.Token!));
        // A deactivated user is sent no link, but could be deactivated after asking for one
        if (user is null || !user.HasPasswordResetUntil(DateTime.UtcNow) || user.DeactivatedAt is not null)
            throw new BusinessRuleException("This link is invalid or has expired. Ask for a new one.");

        // Also uses the link up: ChangePassword clears it
        await using var dbTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        user.ChangePassword(passwordHashingService.HashPassword(command.Reset.NewPassword!));
        await refreshTokenRepository.DeleteForUserAsync(user.Id, cancellationToken);
        await loginAttemptRepository.DeleteForEmailAsync(user.Email, cancellationToken);
        var tokens = Sessions.Start(user, tokenService, refreshTokenRepository);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        try
        {
            await emailService.SendPasswordChangedEmailAsync(user.Email, user.FullName, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Couldn't send the password-changed email for user {UserId}", user.Id);
        }

        return tokens;
    }
}

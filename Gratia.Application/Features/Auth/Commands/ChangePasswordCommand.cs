using Gratia.Application.DTOs.Token;
using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Domain.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Gratia.Application.Features.Auth.Commands;

// The signed-in user's own password (#50). It ends every session, this device's included, and this device gets a new one
// in the response: a password changed because it leaked mustn't leave another device signed in.
public sealed record ChangePasswordCommand(Guid CallerUserId, Guid CallerCompanyId, ChangePasswordDto Passwords) : ICommand<TokenResponseDto>;

public sealed class ChangePasswordCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IPasswordHashingService passwordHashingService,
    ITokenService tokenService,
    IEmailService emailService,
    ILogger<ChangePasswordCommandHandler> logger)
    : ICommandHandler<ChangePasswordCommand, TokenResponseDto>
{
    public async ValueTask<TokenResponseDto> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdInCompanyAsync(command.CallerUserId, command.CallerCompanyId)
            ?? throw new NotFoundException("User not found");

        // A stolen access token alone mustn't be enough to lock the owner out
        if (!passwordHashingService.VerifyPassword(command.Passwords.CurrentPassword!, user.HashedPassword))
            throw new BusinessRuleException("Your current password isn't right.");

        // The new password, the end of every session and this device's new one commit together
        await using var dbTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        user.ChangePassword(passwordHashingService.HashPassword(command.Passwords.NewPassword!));
        await refreshTokenRepository.DeleteForUserAsync(user.Id, cancellationToken);
        var tokens = Sessions.Start(user, tokenService, refreshTokenRepository);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        // So the owner hears of a change they didn't make. The password is changed by now, so a failed email isn't an error.
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

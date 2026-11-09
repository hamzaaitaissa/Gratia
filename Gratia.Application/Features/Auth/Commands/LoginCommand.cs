using Gratia.Application.Common;
using Gratia.Application.DTOs.Token;
using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Application.Mappings;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Auth.Commands;

// Returns null when the email or password is wrong; the controller turns that into a 400
public sealed record LoginCommand(LoginDto Credentials) : ICommand<TokenResponseDto?>;

public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IPasswordHashingService passwordHashingService)
    : ICommandHandler<LoginCommand, TokenResponseDto?>
{
    public async ValueTask<TokenResponseDto?> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        // Emails are stored normalized, so "Ada@Acme.com " finds ada@acme.com (it did on SQL Server's case-insensitive
        // collation, but not with the spaces, and not on other databases)
        var user = await userRepository.GetByEmailAsync(EmailAddresses.Normalize(command.Credentials.Email));
        if (user is null || !passwordHashingService.VerifyPassword(command.Credentials.HashedPassword, user.HashedPassword))
            return null;

        // Checked after the password, so only the account's owner learns it's deactivated (#16)
        if (user.DeactivatedAt is not null)
            throw new ForbiddenException("This account has been deactivated. Ask an admin of your workspace to reactivate it.");

        // A new session for this device. The user's other devices stay signed in (#15).
        var tokens = tokenService.GenerateTokens(user.ToReadDto());
        refreshTokenRepository.Add(new RefreshToken(user.Id, tokenService.HashRefreshToken(tokens.RefreshToken), tokens.RefreshTokenExpiryTime));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Housekeeping: sessions that have already expired are no use to anyone
        await refreshTokenRepository.DeleteExpiredForUserAsync(user.Id, DateTime.UtcNow, cancellationToken);

        return tokens;
    }
}

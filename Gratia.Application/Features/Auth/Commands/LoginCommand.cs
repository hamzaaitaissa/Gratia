using Gratia.Application.DTOs.Token;
using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Interfaces;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Auth.Commands;

// Returns null when the email or password is wrong; the controller turns that into a 400
public sealed record LoginCommand(LoginDto Credentials) : ICommand<TokenResponseDto?>;

public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IPasswordHashingService passwordHashingService)
    : ICommandHandler<LoginCommand, TokenResponseDto?>
{
    public async ValueTask<TokenResponseDto?> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(command.Credentials.Email);
        if (user is null || !passwordHashingService.VerifyPassword(command.Credentials.HashedPassword, user.HashedPassword))
            return null;

        var tokenResponse = tokenService.GenerateTokens(user.ToReadDto());
        user.RefreshToken = tokenResponse.RefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return tokenResponse;
    }
}

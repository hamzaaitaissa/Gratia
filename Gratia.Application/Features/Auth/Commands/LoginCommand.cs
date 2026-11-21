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

// Returns null when the email or password is wrong; the controller turns that into a 400. After
// LoginAttempt.MaxFailures failures with an email in LoginAttempt.Window, throws TooManyAttemptsException (429) (#72).
public sealed record LoginCommand(LoginDto Credentials) : ICommand<TokenResponseDto?>;

public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ILoginAttemptRepository loginAttemptRepository,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IPasswordHashingService passwordHashingService)
    : ICommandHandler<LoginCommand, TokenResponseDto?>
{
    public async ValueTask<TokenResponseDto?> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        // Emails are stored normalized, so "Ada@Acme.com " finds ada@acme.com (it did on SQL Server's case-insensitive
        // collation, but not with the spaces, and not on other databases)
        var email = EmailAddresses.Normalize(command.Credentials.Email);

        // The per-IP limit (#27) doesn't stop many IPs guessing one account's password; this limit is per email
        await StartAttemptAsync(email, DateTime.UtcNow, cancellationToken);

        // Checked even when no account has the email (against a dummy hash), so the answer takes as long either way (#82)
        var user = await userRepository.GetByEmailAsync(email);
        var passwordMatches = passwordHashingService.VerifyPassword(command.Credentials.HashedPassword, user?.HashedPassword);
        if (user is null || !passwordMatches)
            return null; // the attempt stays, as a failure

        // The right password: the email's earlier failures no longer count
        await loginAttemptRepository.DeleteForEmailAsync(email, cancellationToken);

        // Checked after the password, so only the account's owner learns it's deactivated (#16)
        if (user.DeactivatedAt is not null)
            throw new ForbiddenException("This account has been deactivated. Ask an admin of your workspace to reactivate it.");

        // A new session for this device. The user's other devices stay signed in (#15).
        var tokens = Sessions.Start(user, tokenService, refreshTokenRepository);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Housekeeping: sessions that have already expired are no use to anyone
        await refreshTokenRepository.DeleteExpiredForUserAsync(user.Id, DateTime.UtcNow, cancellationToken);

        return tokens;
    }

    // Records the attempt, or refuses it without checking the password when the email has too many recent failures
    private async Task StartAttemptAsync(string email, DateTime now, CancellationToken cancellationToken)
    {
        var windowStart = now - LoginAttempt.Window;
        await loginAttemptRepository.DeleteOlderThanAsync(windowStart, cancellationToken);

        // Saved (a save of its own, before the login's) before the others are counted, so attempts made at the same
        // moment each count the others: however many are sent at once, at most MaxFailures get their password checked
        var attempt = new LoginAttempt(email, now);
        loginAttemptRepository.Add(attempt);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var earlier = await loginAttemptRepository.GetTimesSinceAsync(email, windowStart, attempt.Id, cancellationToken);
        if (earlier.Count < LoginAttempt.MaxFailures)
            return;

        // A refused attempt checks no password, so it isn't a failure: the owner trying again doesn't extend the lock
        await loginAttemptRepository.DeleteAsync(attempt.Id, cancellationToken);

        // When enough of the earlier ones are Window old to leave room for one more
        var retryAfter = earlier[earlier.Count - LoginAttempt.MaxFailures] + LoginAttempt.Window - now;
        var minutes = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes));
        throw new TooManyAttemptsException(
            $"Too many failed sign-ins with this email. Try again in {minutes} minute{(minutes == 1 ? "" : "s")}.", retryAfter);
    }
}

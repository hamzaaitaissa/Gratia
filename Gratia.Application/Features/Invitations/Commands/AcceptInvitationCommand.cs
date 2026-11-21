using Gratia.Application.DTOs.Invitation;
using Gratia.Application.DTOs.Token;
using Gratia.Application.Features.Auth;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Gratia.Application.Features.Invitations.Commands;

// Creates the account and signs it in on this device (#66): the response is the same as login's
public sealed record AcceptInvitationCommand(AcceptInvitationDto Invitation) : ICommand<TokenResponseDto>;

public sealed class AcceptInvitationCommandHandler(
    IInvitationRepository invitationRepository,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IPasswordHashingService passwordHashingService,
    ITokenService tokenService,
    IEmailService emailService,
    ILogger<AcceptInvitationCommandHandler> logger)
    : ICommandHandler<AcceptInvitationCommand, TokenResponseDto>
{
    public async ValueTask<TokenResponseDto> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        var dto = command.Invitation;

        var invitation = await invitationRepository.GetByTokenAsync(dto.Token);
        if (invitation is null || !invitation.IsValid)
            throw new BusinessRuleException("Invalid or expired invitation");

        if (await userRepository.GetByEmailAsync(invitation.Email) is not null)
            throw new ConflictException("User with this email already exists");

        // A new account has this month's allowance to give and nothing received yet. GetByTokenAsync loads the company.
        var monthlyAllowance = invitation.Company.MonthlyPointsAllowance;
        var user = new User(
            dto.FullName,
            invitation.Email,
            passwordHashingService.HashPassword(dto.Password),
            dto.JobTitle ?? string.Empty,
            invitation.InvitedRole,
            numberofpointsacquired: 0,
            numberofpointsavailable: monthlyAllowance,
            invitation.CompanyId);

        userRepository.Add(user);
        invitation.IsUsed = true;

        // The token from the email proved the mailbox, and the password was just chosen, so there's nothing left to
        // check before signing in. The user is new, so the session's company and role are the invitation's.
        var tokens = Sessions.Start(user, tokenService, refreshTokenRepository);

        // Saved together: the account is created, the invitation used up and the session started, or none of them
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // The account exists by now, so a failed welcome email shouldn't turn into an error for the new user
        try
        {
            await emailService.SendWelcomeEmailAsync(user.Email, user.FullName, invitation.Company.Name, monthlyAllowance,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Couldn't send the welcome email for user {UserId}", user.Id);
        }

        return tokens;
    }
}

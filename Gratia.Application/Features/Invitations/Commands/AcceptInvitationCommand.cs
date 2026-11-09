using Gratia.Application.DTOs.Invitation;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Application.Settings;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gratia.Application.Features.Invitations.Commands;

public sealed record AcceptInvitationCommand(AcceptInvitationDto Invitation) : ICommand;

public sealed class AcceptInvitationCommandHandler(
    IInvitationRepository invitationRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IPasswordHashingService passwordHashingService,
    IEmailService emailService,
    IOptions<PointsOptions> pointsOptions,
    ILogger<AcceptInvitationCommandHandler> logger)
    : ICommandHandler<AcceptInvitationCommand>
{
    public async ValueTask<Unit> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        var dto = command.Invitation;

        var invitation = await invitationRepository.GetByTokenAsync(dto.Token);
        if (invitation is null || !invitation.IsValid)
            throw new BusinessRuleException("Invalid or expired invitation");

        if (await userRepository.GetByEmailAsync(invitation.Email) is not null)
            throw new ConflictException("User with this email already exists");

        // A new account has this month's allowance to give and nothing received yet
        var monthlyAllowance = pointsOptions.Value.MonthlyAllowance;
        var user = new User(
            dto.FullName,
            invitation.Email,
            passwordHashingService.HashPassword(dto.Password),
            dto.JobTitle,
            invitation.InvitedRole,
            numberofpointsacquired: 0,
            numberofpointsavailable: monthlyAllowance,
            invitation.CompanyId);

        userRepository.Add(user);
        invitation.IsUsed = true;

        // Saved together: the account is created and the invitation used up, or neither
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // The account exists by now, so a failed welcome email shouldn't turn into an error for the new user
        try
        {
            await emailService.SendWelcomeEmailAsync(user.Email, user.FullName, invitation.Company?.Name ?? "your company", monthlyAllowance,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Couldn't send the welcome email for user {UserId}", user.Id);
        }

        return Unit.Value;
    }
}

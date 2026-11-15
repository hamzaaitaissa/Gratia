using Gratia.Application.Exceptions;
using Gratia.Domain.Enum;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Users.Commands;

// Replaces deleting a user (#16), which failed on their transactions with a 500. Their history stays as it was.
// Admins can only deactivate users in their own company (CallerCompanyId), and not themselves, so a company keeps the
// admin who did it (#63). Doing it twice is fine.
public sealed record DeactivateUserCommand(Guid UserId, Guid CallerUserId, Guid CallerCompanyId) : ICommand;

public sealed class DeactivateUserCommandHandler(
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeactivateUserCommand>
{
    public async ValueTask<Unit> Handle(DeactivateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdInCompanyIncludingDeactivatedAsync(command.UserId, command.CallerCompanyId)
            ?? throw new NotFoundException($"User {command.UserId} was not found.");

        if (user.Id == command.CallerUserId)
            throw new BusinessRuleException("You can't deactivate your own account. Ask another admin to do it.");

        // Signs them out of every device in the same commit. An access token they already hold stops working at once (#73).
        await using var dbTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // The caller is another admin, so one normally remains. Not when two admins deactivate (or demote) each other
        // at the same moment, which this catches (#63).
        if (user.Role == UserRole.Admin && user.DeactivatedAt is null)
            await LastAdmin.EnsureAnotherRemainsAsync(companyRepository, userRepository, user.CompanyId, user.Id, cancellationToken);

        user.Deactivate(DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await refreshTokenRepository.DeleteForUserAsync(user.Id, cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return Unit.Value;
    }
}

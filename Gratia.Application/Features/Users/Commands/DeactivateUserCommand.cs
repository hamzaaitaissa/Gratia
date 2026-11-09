using Gratia.Application.Exceptions;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Users.Commands;

public sealed record DeleteUserCommand(Guid UserId) : ICommand;

public sealed class DeleteUserCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteUserCommand>
{
    public async ValueTask<Unit> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(command.UserId)
            ?? throw new NotFoundException($"User {command.UserId} was not found.");

        userRepository.Remove(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

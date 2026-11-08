using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Users.Commands;

public sealed record UpdateUserCommand(UpdateUserDto User) : ICommand<ReadUserDto>;

public sealed class UpdateUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IPasswordHashingService passwordHashingService)
    : ICommandHandler<UpdateUserCommand, ReadUserDto>
{
    public async ValueTask<ReadUserDto> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var dto = command.User;
        var user = await userRepository.GetByIdAsync(dto.Id)
            ?? throw new NotFoundException($"User {dto.Id} was not found.");

        user.Update(
            dto.FullName,
            dto.Email,
            passwordHashingService.HashPassword(dto.HashedPassword),
            dto.JobTitle,
            dto.Role);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return user.ToReadDto();
    }
}

using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Application.Mappings;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Users.Commands;

public sealed record CreateUserCommand(RegisterUserDto User) : ICommand<ReadUserDto>;

public sealed class CreateUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IPasswordHashingService passwordHashingService)
    : ICommandHandler<CreateUserCommand, ReadUserDto>
{
    public async ValueTask<ReadUserDto> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var dto = command.User;

        if (await userRepository.GetByEmailAsync(dto.Email) is not null)
            throw new ConflictException($"User with this email {dto.Email} already exists");

        var user = new User(
            dto.FullName,
            dto.Email,
            passwordHashingService.HashPassword(dto.HashedPassword),
            dto.JobTitle,
            dto.Role,
            dto.NumberOfPointsAcquired,
            dto.NumberOfPointsAvailable,
            dto.CompanyId);

        userRepository.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return user.ToReadDto();
    }
}

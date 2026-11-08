using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Users.Queries;

public sealed record GetUserByIdQuery(Guid UserId) : IQuery<ReadUserDto>;

public sealed class GetUserByIdQueryHandler(IUserRepository userRepository)
    : IQueryHandler<GetUserByIdQuery, ReadUserDto>
{
    public async ValueTask<ReadUserDto> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(query.UserId)
            ?? throw new NotFoundException($"User {query.UserId} was not found.");

        return user.ToReadDto();
    }
}

using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Users.Queries;

public sealed record GetAllUsersQuery : IQuery<IReadOnlyList<ReadUserDto>>;

public sealed class GetAllUsersQueryHandler(IUserRepository userRepository)
    : IQueryHandler<GetAllUsersQuery, IReadOnlyList<ReadUserDto>>
{
    public async ValueTask<IReadOnlyList<ReadUserDto>> Handle(GetAllUsersQuery query, CancellationToken cancellationToken)
    {
        var users = await userRepository.GetAllAsync();
        return users.Select(u => u.ToReadDto()).ToList();
    }
}

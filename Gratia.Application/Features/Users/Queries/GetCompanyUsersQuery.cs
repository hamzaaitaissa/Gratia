using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Users.Queries;

// The directory: the active users in the caller's company, by name. Deactivated users are left out (#16).
public sealed record GetCompanyUsersQuery(Guid CallerCompanyId) : IQuery<IReadOnlyList<ReadUserDto>>;

public sealed class GetCompanyUsersQueryHandler(IUserRepository userRepository)
    : IQueryHandler<GetCompanyUsersQuery, IReadOnlyList<ReadUserDto>>
{
    public async ValueTask<IReadOnlyList<ReadUserDto>> Handle(GetCompanyUsersQuery query, CancellationToken cancellationToken)
    {
        var users = await userRepository.GetActiveInCompanyAsync(query.CallerCompanyId);
        return users.Select(u => u.ToReadDto()).ToList();
    }
}

using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Users.Queries;

// For admins: the deactivated users in their company, by name, so they can be reactivated (#16)
public sealed record GetDeactivatedUsersQuery(Guid CallerCompanyId) : IQuery<IReadOnlyList<ReadUserDto>>;

public sealed class GetDeactivatedUsersQueryHandler(IUserRepository userRepository)
    : IQueryHandler<GetDeactivatedUsersQuery, IReadOnlyList<ReadUserDto>>
{
    public async ValueTask<IReadOnlyList<ReadUserDto>> Handle(GetDeactivatedUsersQuery query, CancellationToken cancellationToken)
    {
        var users = await userRepository.GetDeactivatedInCompanyAsync(query.CallerCompanyId);
        return users.Select(u => u.ToReadDto()).ToList();
    }
}

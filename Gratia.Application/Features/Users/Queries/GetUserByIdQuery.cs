using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Users.Queries;

// CallerCompanyId is the signed-in user's company: a user in another company, or a deactivated one (#16), gets the
// same 404 as one that doesn't exist
public sealed record GetUserByIdQuery(Guid UserId, Guid CallerCompanyId) : IQuery<ReadUserDto>;

public sealed class GetUserByIdQueryHandler(IUserRepository userRepository)
    : IQueryHandler<GetUserByIdQuery, ReadUserDto>
{
    public async ValueTask<ReadUserDto> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdInCompanyAsync(query.UserId, query.CallerCompanyId)
            ?? throw new NotFoundException($"User {query.UserId} was not found.");

        return user.ToReadDto();
    }
}

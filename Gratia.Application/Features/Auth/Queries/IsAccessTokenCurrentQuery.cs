using Gratia.Domain.Enum;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Auth.Queries;

// Asked on every authenticated request (#73): an access token is honoured only while its user is still active, in the
// company and with the role the token names. Before, a deactivated or demoted admin kept their rights until the token
// expired (AccessTokenMinutes, 60).
// A refused token gets a 401 and the frontend refreshes: a deactivated user's refresh fails, so they're signed out, and
// anyone else gets a token built from who they are now (RefreshTokenCommand).
public sealed record IsAccessTokenCurrentQuery(Guid UserId, Guid CompanyId, UserRole Role, Guid SessionStamp) : IQuery<bool>;

public sealed class IsAccessTokenCurrentQueryHandler(IUserRepository userRepository) : IQueryHandler<IsAccessTokenCurrentQuery, bool>
{
    public async ValueTask<bool> Handle(IsAccessTokenCurrentQuery query, CancellationToken cancellationToken) =>
        await userRepository.IsAccessTokenCurrentAsync(query.UserId, query.CompanyId, query.Role, query.SessionStamp, cancellationToken);
}

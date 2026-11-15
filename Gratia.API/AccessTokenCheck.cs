using System.Security.Claims;
using Gratia.Application.Features.Auth.Queries;
using Gratia.Domain.Enum;
using Mediator;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Gratia.API;

// Runs once the token's signature, issuer, audience and expiry have checked out (JwtBearerEvents.OnTokenValidated):
// the token must also still describe its user (#73, IsAccessTokenCurrentQuery). One that doesn't is refused like an
// expired one, a 401 with no details.
public static class AccessTokenCheck
{
    public static async Task OnTokenValidated(TokenValidatedContext context)
    {
        // The claims TokenService writes; CurrentUserService reads the same ones
        var principal = context.Principal;
        if (!Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(principal?.FindFirstValue("CompanyId"), out var companyId)
            || !Enum.TryParse(principal?.FindFirstValue(ClaimTypes.Role), out UserRole role))
        {
            context.Fail("The access token doesn't name a user, company and role.");
            return;
        }

        var sender = context.HttpContext.RequestServices.GetRequiredService<ISender>();
        if (!await sender.Send(new IsAccessTokenCurrentQuery(userId, companyId, role), context.HttpContext.RequestAborted))
            context.Fail("The access token's user is deactivated or has another role now.");
    }
}

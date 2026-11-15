using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Gratia.Infrastructure.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GratiaTests.Integration;

// The API only accepts access tokens it issued itself, for itself (#25), that still describe their user (#73). Each
// token here is signed with the real key, so the one thing wrong with it is what gets it refused.
public sealed class JwtValidationTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    [Fact]
    public async Task A_token_made_the_way_the_api_makes_them_is_accepted()
    {
        Assert.Equal(HttpStatusCode.OK, await CallWithAsync(await TokenAsync()));
    }

    [Fact]
    public async Task A_token_from_another_issuer_is_refused()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithAsync(await TokenAsync(issuer: "someone-else")));
    }

    [Fact]
    public async Task A_token_for_another_audience_is_refused()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithAsync(await TokenAsync(audience: "another-api")));
    }

    [Fact]
    public async Task A_token_signed_with_another_algorithm_is_refused()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithAsync(await TokenAsync(algorithm: SecurityAlgorithms.HmacSha256)));
    }

    [Fact]
    public async Task A_token_that_expired_two_minutes_ago_is_refused()
    {
        // The default 5-minute clock skew would still have let it in
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithAsync(await TokenAsync(expires: DateTime.UtcNow.AddMinutes(-2))));
    }

    [Fact]
    public async Task A_token_that_no_longer_describes_its_user_is_refused()
    {
        // Correctly signed, but the employee isn't an admin, isn't in Globex, or the user doesn't exist (#73)
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithAsync(await TokenAsync(role: "Admin")));
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithAsync(await TokenAsync(companyId: factory.Globex.Id)));
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithAsync(await TokenAsync(userId: Guid.NewGuid())));
    }

    private async Task<string> TokenAsync(string? issuer = null, string? audience = null, string algorithm = SecurityAlgorithms.HmacSha512,
        DateTime? expires = null, Guid? userId = null, string role = "Employee", Guid? companyId = null)
    {
        var options = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var subject = userId ?? await factory.UserIdAsync(factory.AcmeEmployeeEmail);
        var expiry = expires ?? DateTime.UtcNow.AddMinutes(10);

        var token = new JwtSecurityToken(
            issuer: issuer ?? options.Issuer,
            audience: audience ?? options.Audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, subject.ToString()),
                new Claim(ClaimTypes.Email, factory.AcmeEmployeeEmail),
                new Claim(ClaimTypes.Role, role),
                new Claim("CompanyId", (companyId ?? factory.Acme.Id).ToString())
            ],
            notBefore: expiry.AddHours(-1),
            expires: expiry,
            signingCredentials: new SigningCredentials(options.CreateSigningKey(), algorithm));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<HttpStatusCode> CallWithAsync(string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return (await client.GetAsync("/api/company/me")).StatusCode;
    }
}

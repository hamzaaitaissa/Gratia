using System.Net;
using System.Net.Http.Json;
using Gratia.Application.DTOs.CompanyDTO;
using Microsoft.EntityFrameworkCore;

namespace GratiaTests.Integration;

// Callers only ever see or change their own company (TASKS.md #6 and #22)
public sealed class CompanyEndpointsTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    [Fact]
    public async Task Anonymous_callers_get_401()
    {
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/company/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/company/{factory.Acme.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync($"/api/company/{factory.Acme.Id}", UpdateFor(factory.Acme.Id))).StatusCode);
    }

    [Fact]
    public async Task There_is_no_list_of_companies()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/company")).StatusCode);
    }

    [Fact]
    public async Task Users_see_their_own_company()
    {
        var employee = await factory.CreateClientForAsync(factory.AcmeEmployeeEmail);

        var mine = await employee.GetFromJsonAsync<ReadCompanyDto>("/api/company/me");
        Assert.Equal(factory.Acme.Id, mine!.Id);
        Assert.Equal("acme.test", mine.Domain);
        Assert.False(mine.IsDomainVerified);

        var byId = await employee.GetFromJsonAsync<ReadCompanyDto>($"/api/company/{factory.Acme.Id}");
        Assert.Equal(factory.Acme.Id, byId!.Id);
    }

    [Fact]
    public async Task Another_company_looks_the_same_as_one_that_does_not_exist()
    {
        var employee = await factory.CreateClientForAsync(factory.AcmeEmployeeEmail);

        Assert.Equal(HttpStatusCode.NotFound, (await employee.GetAsync($"/api/company/{factory.Globex.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await employee.GetAsync($"/api/company/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Admins_can_update_their_own_company()
    {
        // Globex, so the Acme tests in this class don't depend on the order the tests run in
        var admin = await factory.CreateClientForAsync(factory.GlobexAdminEmail);

        var response = await admin.PutAsJsonAsync($"/api/company/{factory.Globex.Id}", UpdateFor(factory.Globex.Id, "Globex Renamed"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Globex Renamed", (await response.Content.ReadFromJsonAsync<ReadCompanyDto>())!.Name);

        var mine = await admin.GetFromJsonAsync<ReadCompanyDto>("/api/company/me");
        Assert.Equal("Globex Renamed", mine!.Name);
        Assert.Equal("#000000", mine.PrimaryColor);
    }

    [Fact]
    public async Task Employees_cannot_update_the_company()
    {
        var employee = await factory.CreateClientForAsync(factory.AcmeEmployeeEmail);

        var response = await employee.PutAsJsonAsync($"/api/company/{factory.Acme.Id}", UpdateFor(factory.Acme.Id));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admins_cannot_update_another_company()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var nameBefore = await GlobexNameAsync();

        var response = await admin.PutAsJsonAsync($"/api/company/{factory.Globex.Id}", UpdateFor(factory.Globex.Id, "Hijacked"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(nameBefore, await GlobexNameAsync());
    }

    [Fact]
    public async Task Different_ids_in_route_and_body_is_a_bad_request()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);

        var response = await admin.PutAsJsonAsync($"/api/company/{factory.Acme.Id}", UpdateFor(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task The_logo_is_optional(string? logoUrl)
    {
        // Companies created by signup have no logo, and their admins must still be able to save their branding (#67)
        var admin = await factory.CreateClientForAsync(factory.GlobexAdminEmail);

        var response = await admin.PutAsJsonAsync($"/api/company/{factory.Globex.Id}", UpdateFor(factory.Globex.Id, "Globex", logoUrl));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("", (await response.Content.ReadFromJsonAsync<ReadCompanyDto>())!.LogoUrl);
    }

    [Theory]
    [InlineData("javascript:alert(1)", "#000000", "#FFFFFF")]
    [InlineData("ftp://example.com/logo.png", "#000000", "#FFFFFF")]
    [InlineData("https://example.com/logo.png", "red", "#FFFFFF")]
    [InlineData("https://example.com/logo.png", "#FFF", "#FFFFFF")]
    [InlineData("https://example.com/logo.png", "#000000", "#12345G")]
    [InlineData("https://example.com/logo.png", "#000000", null)]
    public async Task Invalid_branding_is_rejected(string logoUrl, string primaryColor, string? secondaryColor)
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);

        var response = await admin.PutAsJsonAsync($"/api/company/{factory.Acme.Id}",
            UpdateFor(factory.Acme.Id, "Acme", logoUrl, primaryColor, secondaryColor));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private Task<string> GlobexNameAsync() =>
        factory.WithDbAsync(db => db.Companies.Where(c => c.Id == factory.Globex.Id).Select(c => c.Name).SingleAsync());

    private static object UpdateFor(Guid id, string name = "Acme", string? logoUrl = "https://example.com/logo.png",
        string primaryColor = "#000000", string? secondaryColor = "#FFFFFF") => new
    {
        id,
        name,
        logoUrl,
        primaryColor,
        secondaryColor
        // No points cycle, like the frontend's branding form; PointsCycleTests covers changing it
    };
}

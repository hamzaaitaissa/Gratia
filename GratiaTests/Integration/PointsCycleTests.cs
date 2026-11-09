using System.Net;
using System.Net.Http.Json;
using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Features.Points.Commands;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Gratia.Infrastructure.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GratiaTests.Integration;

// The monthly refill (#19), driven by sending ResetDuePointsCommand at chosen times; the background service that sends
// it in the real API is off in tests. The command looks at every company in the database, so each test makes its own
// companies and only checks those.
public sealed class PointsCycleTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    [Fact]
    public async Task The_first_run_records_the_period_without_refilling()
    {
        var (company, _) = await AddCompanyAsync(allowance: 40, resetDay: 21, periodStartedAt: null, members: 2, pointsLeft: 3);

        await ResetAsync(Utc(2026, 11, 25));

        // Members got the allowance when they joined; refilling now would give them an extra month's worth
        Assert.All(await PointsLeftAsync(company), points => Assert.Equal(3, points));
        Assert.Equal(Utc(2026, 11, 21), await PeriodStartedAtAsync(company));
    }

    [Fact]
    public async Task Each_company_is_refilled_to_its_own_allowance_on_its_own_day_and_only_once()
    {
        var (on21st, _) = await AddCompanyAsync(allowance: 40, resetDay: 21, periodStartedAt: Utc(2026, 10, 21), members: 2, pointsLeft: 3);
        var (on25th, _) = await AddCompanyAsync(allowance: 10, resetDay: 25, periodStartedAt: Utc(2026, 10, 25), members: 1, pointsLeft: 3);

        await ResetAsync(Utc(2026, 11, 21, 0, 5));
        Assert.All(await PointsLeftAsync(on21st), points => Assert.Equal(40, points));
        Assert.All(await PointsLeftAsync(on25th), points => Assert.Equal(3, points));

        // Points given after the refill stay given when the job runs again: after a restart, or on another instance.
        // The old Worker refilled again whenever it restarted on the 21st.
        await SetPointsLeftAsync(on21st, 7);
        await ResetAsync(Utc(2026, 11, 21, 0, 15));
        Assert.All(await PointsLeftAsync(on21st), points => Assert.Equal(7, points));

        await ResetAsync(Utc(2026, 11, 25, 0, 5));
        Assert.All(await PointsLeftAsync(on25th), points => Assert.Equal(10, points));
        Assert.All(await PointsLeftAsync(on21st), points => Assert.Equal(7, points));
    }

    [Fact]
    public async Task A_refill_missed_while_the_api_was_down_happens_on_the_next_run()
    {
        var (company, _) = await AddCompanyAsync(allowance: 25, resetDay: 21, periodStartedAt: Utc(2026, 9, 21), members: 1, pointsLeft: 0);

        // The old Worker only acted on the 21st itself, so a day of downtime skipped the month
        await ResetAsync(Utc(2026, 11, 9));

        Assert.All(await PointsLeftAsync(company), points => Assert.Equal(25, points));
        Assert.Equal(Utc(2026, 10, 21), await PeriodStartedAtAsync(company));
    }

    [Fact]
    public async Task The_company_shows_its_cycle()
    {
        var (_, adminEmail) = await AddCompanyAsync(allowance: 25, resetDay: 21, periodStartedAt: null);
        var admin = await factory.CreateClientForAsync(adminEmail);
        var now = DateTime.UtcNow;

        var company = (await admin.GetFromJsonAsync<ReadCompanyDto>("/api/company/me"))!;

        Assert.Equal((25, 21), (company.MonthlyPointsAllowance, company.PointsResetDay));
        Assert.Equal((21, 21), (company.LastPointsResetAt.Day, company.NextPointsResetAt.Day));
        Assert.InRange(now, company.LastPointsResetAt, company.NextPointsResetAt);
        Assert.Equal(company.LastPointsResetAt.AddMonths(1), company.NextPointsResetAt);
    }

    [Fact]
    public async Task Admins_change_the_cycle_and_a_new_day_does_not_refill_anyone()
    {
        var (company, adminEmail) = await AddCompanyAsync(allowance: 25, resetDay: 21, periodStartedAt: null, members: 2, pointsLeft: 3);
        await ResetAsync(DateTime.UtcNow); // records the current period, as the running API would have
        var admin = await factory.CreateClientForAsync(adminEmail);

        var response = await admin.PutAsJsonAsync($"/api/company/{company}", Settings(company, allowance: 30, resetDay: 5));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ReadCompanyDto>())!;
        Assert.Equal((30, 5, 5), (updated.MonthlyPointsAllowance, updated.PointsResetDay, updated.NextPointsResetAt.Day));

        await ResetAsync(DateTime.UtcNow);
        Assert.All(await PointsLeftAsync(company), points => Assert.Equal(3, points));

        // The new allowance comes with the next refill, on the new day
        await ResetAsync(updated.NextPointsResetAt.AddMinutes(1));
        Assert.All(await PointsLeftAsync(company), points => Assert.Equal(30, points));
    }

    [Fact]
    public async Task Saving_branding_without_the_cycle_keeps_it()
    {
        var (company, adminEmail) = await AddCompanyAsync(allowance: 40, resetDay: 5, periodStartedAt: null);
        var admin = await factory.CreateClientForAsync(adminEmail);

        // What the frontend's Company settings page sends
        var response = await admin.PutAsJsonAsync($"/api/company/{company}",
            new { id = company, name = "Renamed Co", primaryColor = "#112233", secondaryColor = "#445566" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ReadCompanyDto>())!;
        Assert.Equal(("Renamed Co", 40, 5), (updated.Name, updated.MonthlyPointsAllowance, updated.PointsResetDay));
    }

    [Theory]
    [InlineData(0, 21)]
    [InlineData(1001, 21)]
    [InlineData(25, 0)]
    [InlineData(25, 29)] // not in every month
    public async Task A_cycle_outside_the_limits_is_refused(int allowance, int resetDay)
    {
        var (company, adminEmail) = await AddCompanyAsync(allowance: 25, resetDay: 21, periodStartedAt: null);
        var admin = await factory.CreateClientForAsync(adminEmail);

        var response = await admin.PutAsJsonAsync($"/api/company/{company}", Settings(company, allowance, resetDay));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_new_member_starts_with_their_company_allowance()
    {
        var (company, adminEmail) = await AddCompanyAsync(allowance: 40, resetDay: 21, periodStartedAt: null);
        var email = $"joiner@{adminEmail.Split('@')[1]}";
        var token = Guid.NewGuid().ToString("N");
        await factory.WithDbAsync(db =>
        {
            db.CompanyInvitations.Add(new CompanyInvitation(email, token, DateTime.UtcNow.AddDays(1), UserRole.Employee, company, invitedByUserId: null));
            return db.SaveChangesAsync();
        });

        var accept = await factory.CreateClient().PostAsJsonAsync("/api/invitation/accept",
            new { token, fullName = "New Joiner", password = GratiaApiFactory.Password });

        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);
        Assert.Equal(40, await factory.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.NumberOfPointsAvailable).SingleAsync()));
    }

    private async Task<(Guid Id, string AdminEmail)> AddCompanyAsync(int allowance, int resetDay, DateTime? periodStartedAt,
        int members = 1, int pointsLeft = 0)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GratiaDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();

        var domain = $"{Guid.NewGuid():N}.test";
        var company = new Company("Cycle Co", "", "#112233", "#445566")
        {
            Domain = domain,
            MonthlyPointsAllowance = allowance,
            PointsResetDay = resetDay,
            PointsPeriodStartedAt = periodStartedAt
        };
        db.Companies.Add(company);

        // The first member is an admin, who can sign in and change the cycle
        var adminEmail = $"admin@{domain}";
        for (var i = 0; i < members; i++)
        {
            db.Users.Add(new User("Member", i == 0 ? adminEmail : $"member{i}@{domain}", hasher.HashPassword(GratiaApiFactory.Password),
                "Tester", i == 0 ? UserRole.Admin : UserRole.Employee, 0, pointsLeft, company.Id));
        }

        await db.SaveChangesAsync();
        return (company.Id, adminEmail);
    }

    private async Task<int> ResetAsync(DateTime now)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ResetDuePointsCommand(now));
    }

    private Task<List<int>> PointsLeftAsync(Guid companyId) =>
        factory.WithDbAsync(db => db.Users.Where(u => u.CompanyId == companyId).Select(u => u.NumberOfPointsAvailable).ToListAsync());

    private Task<int> SetPointsLeftAsync(Guid companyId, int points) =>
        factory.WithDbAsync(db => db.Users.Where(u => u.CompanyId == companyId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.NumberOfPointsAvailable, points)));

    private Task<DateTime?> PeriodStartedAtAsync(Guid companyId) =>
        factory.WithDbAsync(db => db.Companies.Where(c => c.Id == companyId).Select(c => c.PointsPeriodStartedAt).SingleAsync());

    private static object Settings(Guid id, int allowance, int resetDay) => new
    {
        id,
        name = "Cycle Co",
        primaryColor = "#112233",
        secondaryColor = "#445566",
        monthlyPointsAllowance = allowance,
        pointsResetDay = resetDay
    };

    private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);
}

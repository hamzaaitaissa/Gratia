using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Gratia.Application.DTOs.Transaction;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GratiaTests.Integration;

public sealed class TransactionEndpointsTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    [Fact]
    public async Task Points_can_be_sent_to_a_colleague()
    {
        var (_, senderEmail) = await factory.AddUserAsync(factory.Acme, pointsAvailable: 25);
        var sender = await factory.CreateClientForAsync(senderEmail);
        var receiverBefore = await BalanceAsync(factory.AcmeAdminEmail);

        var response = await sender.PostAsJsonAsync("/api/transaction", Gift(await factory.UserIdAsync(factory.AcmeAdminEmail), 5));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = await response.Content.ReadFromJsonAsync<ReadTransactionDto>(GratiaApiFactory.Json);
        Assert.Equal(("Temp User", "Acme Admin", "Acme"), (sent!.SenderName, sent.ReceiverName, sent.CompanyName)); // CompanyName was empty (#14)
        Assert.Equal(20, (await BalanceAsync(senderEmail)).Available);
        Assert.Equal(receiverBefore.Acquired + 5, (await BalanceAsync(factory.AcmeAdminEmail)).Acquired);
    }

    [Fact]
    public async Task You_cannot_send_more_than_you_have_left()
    {
        var (_, senderEmail) = await factory.AddUserAsync(factory.Acme, pointsAvailable: 3);
        var sender = await factory.CreateClientForAsync(senderEmail);
        var receiverId = await factory.UserIdAsync(factory.AcmeAdminEmail);
        var receiverBefore = await BalanceAsync(factory.AcmeAdminEmail);
        var transactionsBefore = await TransactionCountAsync();

        var response = await sender.PostAsJsonAsync("/api/transaction", Gift(receiverId, 4));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("3 points left", await response.Content.ReadAsStringAsync());
        Assert.Equal((3, 0), await BalanceAsync(senderEmail));
        Assert.Equal(receiverBefore, await BalanceAsync(factory.AcmeAdminEmail));
        Assert.Equal(transactionsBefore, await TransactionCountAsync());
    }

    [Fact]
    public async Task You_can_spend_everything_and_then_nothing_more()
    {
        var (_, senderEmail) = await factory.AddUserAsync(factory.Acme, pointsAvailable: 3);
        var sender = await factory.CreateClientForAsync(senderEmail);
        var receiverId = await factory.UserIdAsync(factory.AcmeAdminEmail);

        Assert.Equal(HttpStatusCode.OK, (await sender.PostAsJsonAsync("/api/transaction", Gift(receiverId, 3))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sender.PostAsJsonAsync("/api/transaction", Gift(receiverId, 1))).StatusCode);
        Assert.Equal(0, (await BalanceAsync(senderEmail)).Available);
    }

    [Fact]
    public async Task A_receiver_in_another_company_looks_like_one_that_does_not_exist()
    {
        var employee = await factory.CreateClientForAsync(factory.AcmeEmployeeEmail);
        var (senderBefore, receiverBefore) = (await BalanceAsync(factory.AcmeEmployeeEmail), await BalanceAsync(factory.GlobexAdminEmail));

        var response = await employee.PostAsJsonAsync("/api/transaction", Gift(await factory.UserIdAsync(factory.GlobexAdminEmail), 5));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(senderBefore, await BalanceAsync(factory.AcmeEmployeeEmail));
        Assert.Equal(receiverBefore, await BalanceAsync(factory.GlobexAdminEmail));
    }

    [Fact]
    public async Task Times_are_utc_and_say_so()
    {
        var (_, senderEmail) = await factory.AddUserAsync(factory.Acme);
        var (receiverId, receiverEmail) = await factory.AddUserAsync(factory.Acme);

        var created = await (await factory.CreateClientForAsync(senderEmail)).PostAsJsonAsync("/api/transaction", Gift(receiverId, 1));
        // Read back from the database, which doesn't keep a DateTime's Kind, so this is the case #40 was about
        var received = await (await factory.CreateClientForAsync(receiverEmail)).GetStringAsync("/api/transaction/received");

        foreach (var json in new[] { await created.Content.ReadAsStringAsync(), received })
        {
            var createdAt = Regex.Match(json, "\"createdAt\":\"([^\"]+)\"").Groups[1].Value;
            Assert.EndsWith("Z", createdAt);
            Assert.InRange(DateTimeOffset.Parse(createdAt, CultureInfo.InvariantCulture), DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
        }
    }

    [Fact]
    public async Task Only_the_sender_and_receiver_can_open_a_recognition_and_other_companies_cannot_tell_it_exists()
    {
        var (_, senderEmail) = await factory.AddUserAsync(factory.Acme);
        var (receiverId, receiverEmail) = await factory.AddUserAsync(factory.Acme);
        var sent = await (await factory.CreateClientForAsync(senderEmail)).PostAsJsonAsync("/api/transaction", Gift(receiverId, 1));
        var path = $"/api/transaction/{(await sent.Content.ReadFromJsonAsync<ReadTransactionDto>(GratiaApiFactory.Json))!.Id}";

        Assert.Equal(HttpStatusCode.OK, (await (await factory.CreateClientForAsync(senderEmail)).GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await factory.CreateClientForAsync(receiverEmail)).GetAsync(path)).StatusCode);
        // A colleague who wasn't part of it
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateClientForAsync(factory.AcmeEmployeeEmail)).GetAsync(path)).StatusCode);

        // Another company gets the same answer as for an id that doesn't exist; it used to get the 403 (#71)
        var outsider = await factory.CreateClientForAsync(factory.GlobexAdminEmail);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/transaction/{Guid.NewGuid()}")).StatusCode);
    }

    // #11, at the repository level: SQLite in tests can't run two requests truly at once, so these check the property
    // that makes concurrent sends safe instead. The balance is checked and changed in the database, in one statement,
    // never from a copy the handler read earlier.

    [Fact]
    public async Task Spending_checks_the_balance_in_the_database_not_a_stale_copy()
    {
        var (id, _) = await factory.AddUserAsync(factory.Acme, pointsAvailable: 10);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        // This request read the balance, then another send spent 6 of the 10 points before it got to spend them
        var stale = await users.GetByIdAsync(id);
        await SetAvailableAsync(id, 4);
        Assert.Equal(10, stale!.NumberOfPointsAvailable);

        Assert.False(await users.TrySpendAvailablePointsAsync(id, 5));
        Assert.Equal(4, await AvailableAsync(id));
        Assert.True(await users.TrySpendAvailablePointsAsync(id, 4));
        Assert.Equal(0, await AvailableAsync(id));
    }

    [Fact]
    public async Task Received_points_are_added_not_overwritten()
    {
        var (id, _) = await factory.AddUserAsync(factory.Acme);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var stale = await users.GetByIdAsync(id); // reads 0 received, like two gifts arriving at the same moment

        await users.AddAcquiredPointsAsync(id, 5);
        await users.AddAcquiredPointsAsync(id, 7);

        Assert.Equal(0, stale!.NumberOfPointsAcquired);
        Assert.Equal(12, await factory.WithDbAsync(db => db.Users.Where(u => u.Id == id).Select(u => u.NumberOfPointsAcquired).SingleAsync()));
    }

    private Task<(int Available, int Acquired)> BalanceAsync(string email) =>
        factory.WithDbAsync(async db =>
        {
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
            return (user.NumberOfPointsAvailable, user.NumberOfPointsAcquired);
        });

    private Task<int> AvailableAsync(Guid id) =>
        factory.WithDbAsync(db => db.Users.Where(u => u.Id == id).Select(u => u.NumberOfPointsAvailable).SingleAsync());

    private Task<int> SetAvailableAsync(Guid id, int points) =>
        factory.WithDbAsync(db => db.Users.Where(u => u.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.NumberOfPointsAvailable, points)));

    private Task<int> TransactionCountAsync() => factory.WithDbAsync((GratiaDbContext db) => db.Transactions.CountAsync());

    private static object Gift(Guid receiverId, int amount) =>
        new { receiverId, amount, message = "Thanks for the help!", typeOfDonation = "Teamwork" };
}

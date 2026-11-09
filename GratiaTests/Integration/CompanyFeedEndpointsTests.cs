using System.Net;
using System.Net.Http.Json;
using Gratia.Application.DTOs.Transaction;

namespace GratiaTests.Integration;

// GET /api/transaction/company-history, the Home screen's company feed (#13 / #14)
public sealed class CompanyFeedEndpointsTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    [Fact]
    public async Task The_feed_is_newest_first_paged_with_names_and_the_company_total()
    {
        var (_, senderEmail) = await factory.AddUserAsync(factory.Acme);
        var sender = await factory.CreateClientForAsync(senderEmail);
        var receiverId = await factory.UserIdAsync(factory.AcmeEmployeeEmail);
        var before = (await FeedAsync(sender, page: 1, pageSize: 1)).TotalCount;

        foreach (var message in new[] { "first", "second", "third" })
            (await sender.PostAsJsonAsync("/api/transaction", Gift(receiverId, message))).EnsureSuccessStatusCode();

        var page1 = await FeedAsync(sender, page: 1, pageSize: 2);
        var page2 = await FeedAsync(sender, page: 2, pageSize: 2);

        Assert.Equal(["third", "second"], page1.Transactions.Select(t => t.Message));
        Assert.Equal("first", page2.Transactions.First().Message);
        Assert.Equal(before + 3, page1.TotalCount);
        Assert.Equal((int)Math.Ceiling((before + 3) / 2.0), page1.TotalPages);

        var latest = page1.Transactions.First();
        Assert.Equal(("Temp User", "Acme Employee", "Acme"), (latest.SenderName, latest.ReceiverName, latest.CompanyName));
    }

    [Fact]
    public async Task The_feed_only_shows_the_callers_company()
    {
        var (_, acmeEmail) = await factory.AddUserAsync(factory.Acme);
        var (_, globexEmail) = await factory.AddUserAsync(factory.Globex);
        var acme = await factory.CreateClientForAsync(acmeEmail);
        var globex = await factory.CreateClientForAsync(globexEmail);
        (await acme.PostAsJsonAsync("/api/transaction", Gift(await factory.UserIdAsync(factory.AcmeAdminEmail), "acme only"))).EnsureSuccessStatusCode();
        (await globex.PostAsJsonAsync("/api/transaction", Gift(await factory.UserIdAsync(factory.GlobexAdminEmail), "globex only"))).EnsureSuccessStatusCode();

        var feed = await FeedAsync(await factory.CreateClientForAsync(factory.GlobexAdminEmail), page: 1, pageSize: 100);

        Assert.Contains(feed.Transactions, t => t.Message == "globex only");
        Assert.DoesNotContain(feed.Transactions, t => t.Message == "acme only");
        Assert.All(feed.Transactions, t => Assert.Equal("Globex", t.CompanyName));
        Assert.Equal(feed.Transactions.Count(), feed.TotalCount);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Invalid_paging_is_rejected(int page, int pageSize)
    {
        var client = await factory.CreateClientForAsync(factory.AcmeEmployeeEmail);

        var response = await client.GetAsync($"/api/transaction/company-history?page={page}&pageSize={pageSize}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<ReadTransactionHistoryDto> FeedAsync(HttpClient client, int page, int pageSize)
    {
        var response = await client.GetAsync($"/api/transaction/company-history?page={page}&pageSize={pageSize}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReadTransactionHistoryDto>(GratiaApiFactory.Json))!;
    }

    private static object Gift(Guid receiverId, string message) =>
        new { receiverId, amount = 1, message, typeOfDonation = "Teamwork" };
}

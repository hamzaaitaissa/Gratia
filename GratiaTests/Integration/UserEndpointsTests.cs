using System.Net;
using System.Net.Http.Json;
using Gratia.Application.DTOs.Transaction;
using Gratia.Application.DTOs.UserDTO;
using Microsoft.EntityFrameworkCore;

namespace GratiaTests.Integration;

// Callers only ever see or change users in their own company (TASKS.md #22). Users are deactivated, not deleted (#16),
// and an admin's edit leaves the password alone (#17).
public sealed class UserEndpointsTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    [Fact]
    public async Task The_directory_only_lists_your_own_company()
    {
        var employee = await factory.CreateClientForAsync(factory.AcmeEmployeeEmail);

        var users = (await employee.GetFromJsonAsync<List<ReadUserDto>>("/api/user", GratiaApiFactory.Json))!;

        Assert.All(users, u => Assert.Equal(factory.Acme.Id, u.CompanyId));
        Assert.Contains(users, u => u.Email == factory.AcmeAdminEmail);
        Assert.DoesNotContain(users, u => u.Email == factory.GlobexAdminEmail);
    }

    [Fact]
    public async Task A_user_in_another_company_looks_like_one_that_does_not_exist()
    {
        var employee = await factory.CreateClientForAsync(factory.AcmeEmployeeEmail);

        var colleague = await employee.GetFromJsonAsync<ReadUserDto>($"/api/user/{await factory.UserIdAsync(factory.AcmeAdminEmail)}", GratiaApiFactory.Json);
        Assert.Equal(factory.AcmeAdminEmail, colleague!.Email);

        var outsider = await employee.GetAsync($"/api/user/{await factory.UserIdAsync(factory.GlobexAdminEmail)}");
        Assert.Equal(HttpStatusCode.NotFound, outsider.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await employee.GetAsync($"/api/user/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Admins_can_update_users_in_their_company_without_touching_the_password()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (id, email) = await factory.AddUserAsync(factory.Acme);

        var update = await admin.PutAsJsonAsync($"/api/user/{id}", UpdateFor(id, email, "Engineer"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode); // #5: this was always 400
        Assert.Equal("Engineer", (await update.Content.ReadFromJsonAsync<ReadUserDto>(GratiaApiFactory.Json))!.JobTitle);

        // #17: the edit used to require a password and replace theirs with it
        await factory.CreateClientForAsync(email);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task A_member_can_be_saved_without_a_job_title(string? jobTitle)
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (id, email) = await factory.AddUserAsync(factory.Acme);

        // #74: accepting an invitation allows no job title, but the admin's edit refused one
        var update = await admin.PutAsJsonAsync($"/api/user/{id}", new { id, fullName = "Temp User", email, jobTitle, role = "Employee" });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("", await factory.WithDbAsync(db => db.Users.Where(u => u.Id == id).Select(u => u.JobTitle).SingleAsync()));
    }

    [Fact]
    public async Task A_changed_email_is_normalized_and_becomes_the_login()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (id, _) = await factory.AddUserAsync(factory.Acme);
        var newEmail = $"renamed-{Guid.NewGuid():N}@acme.test";

        var update = await admin.PutAsJsonAsync($"/api/user/{id}", UpdateFor(id, $"  {newEmail.ToUpperInvariant()} ", "Engineer"));

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(newEmail, (await update.Content.ReadFromJsonAsync<ReadUserDto>(GratiaApiFactory.Json))!.Email);
        await factory.CreateClientForAsync(newEmail);
    }

    [Theory]
    [InlineData("employee@acme.test")]
    [InlineData("EMPLOYEE@Acme.test")]
    public async Task An_email_that_another_account_uses_is_a_conflict(string takenEmail)
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (id, email) = await factory.AddUserAsync(factory.Acme);

        // #17: this hit the unique index and answered 500
        var response = await admin.PutAsJsonAsync($"/api/user/{id}", UpdateFor(id, takenEmail, "Engineer"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(email, await factory.WithDbAsync(db => db.Users.Where(u => u.Id == id).Select(u => u.Email).SingleAsync()));
    }

    [Fact]
    public async Task An_unverified_workspace_cannot_move_a_member_off_its_domain()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (id, _) = await factory.AddUserAsync(factory.Acme);

        // The same rule as for invitations, which this would otherwise get around
        var response = await admin.PutAsJsonAsync($"/api/user/{id}", UpdateFor(id, $"{Guid.NewGuid():N}@elsewhere.test", "Engineer"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admins_cannot_update_deactivate_or_reactivate_users_in_another_company()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (id, email) = await factory.AddUserAsync(factory.Globex);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/user/{id}", UpdateFor(id, email, "Hijacked"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/user/{id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/user/{id}/reactivate", null)).StatusCode);

        var user = await factory.WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id));
        Assert.Equal("Tester", user.JobTitle);
        Assert.Null(user.DeactivatedAt);
    }

    [Fact]
    public async Task Employees_cannot_manage_users()
    {
        var employee = await factory.CreateClientForAsync(factory.AcmeEmployeeEmail);
        var (id, email) = await factory.AddUserAsync(factory.Acme);

        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PutAsJsonAsync($"/api/user/{id}", UpdateFor(id, email, "Hijacked"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsync($"/api/user/{id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsync($"/api/user/{id}/reactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/user/deactivated")).StatusCode);
    }

    [Fact]
    public async Task Roles_go_both_ways_by_name()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (id, email) = await factory.AddUserAsync(factory.Acme);

        var promote = await admin.PutAsJsonAsync($"/api/user/{id}", UpdateFor(id, email, "Lead", role: "Admin"));
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);
        Assert.Contains("\"role\":\"Admin\"", await promote.Content.ReadAsStringAsync());

        Assert.Contains("\"role\":\"Admin\"", await admin.GetStringAsync($"/api/user/{await factory.UserIdAsync(factory.AcmeAdminEmail)}"));
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData(1)] // numbers used to be the only way in (#52)
    public async Task Roles_that_are_not_a_known_name_are_rejected(object role)
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (id, email) = await factory.AddUserAsync(factory.Acme);

        var response = await admin.PutAsJsonAsync($"/api/user/{id}", UpdateFor(id, email, "Engineer", role));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Deactivation (#16)

    [Fact]
    public async Task A_user_with_recognitions_can_be_deactivated_and_their_history_stays()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (id, email) = await factory.AddUserAsync(factory.Acme);
        var gift = await (await factory.CreateClientForAsync(email))
            .PostAsJsonAsync("/api/transaction", Gift(await factory.UserIdAsync(factory.AcmeAdminEmail), 2));
        var giftId = (await gift.Content.ReadFromJsonAsync<ReadTransactionDto>(GratiaApiFactory.Json))!.Id;

        // Deleting them failed on this transaction's foreign key with a 500
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/user/{id}/deactivate", null)).StatusCode);

        var received = (await admin.GetFromJsonAsync<List<ReadTransactionDto>>("/api/transaction/received", GratiaApiFactory.Json))!;
        Assert.Equal("Temp User", Assert.Single(received, t => t.Id == giftId).SenderName);

        // Gone from the directory, and listed for admins instead
        Assert.DoesNotContain((await admin.GetFromJsonAsync<List<ReadUserDto>>("/api/user", GratiaApiFactory.Json))!, u => u.Id == id);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/user/{id}")).StatusCode);
        var deactivated = (await admin.GetFromJsonAsync<List<ReadUserDto>>("/api/user/deactivated", GratiaApiFactory.Json))!;
        Assert.InRange(Assert.Single(deactivated, u => u.Id == id).DeactivatedAt!.Value, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1));
        Assert.All(deactivated, u => Assert.Equal(factory.Acme.Id, u.CompanyId));
    }

    [Fact]
    public async Task A_deactivated_user_can_neither_give_nor_receive_points()
    {
        var (giverId, giverEmail) = await factory.AddUserAsync(factory.Acme);
        var (receiverId, _) = await factory.AddUserAsync(factory.Acme);
        var giver = await factory.CreateClientForAsync(giverEmail);
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        await admin.PostAsync($"/api/user/{giverId}/deactivate", null);
        await admin.PostAsync($"/api/user/{receiverId}/deactivate", null);

        // The giver's access token is still valid, but not for this
        Assert.Equal(HttpStatusCode.Forbidden, (await giver.PostAsJsonAsync("/api/transaction", Gift(await factory.UserIdAsync(factory.AcmeAdminEmail), 1))).StatusCode);
        // The frontend says "they may have left the company" for this 404
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/transaction", Gift(receiverId, 1))).StatusCode);

        Assert.Equal(0, await factory.WithDbAsync(db => db.Transactions.CountAsync(t => t.SenderId == giverId || t.ReceiverId == receiverId)));
    }

    [Fact]
    public async Task A_reactivated_user_signs_in_with_their_old_password_and_is_back_in_the_directory()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (id, email) = await factory.AddUserAsync(factory.Acme);

        // Both are safe to repeat
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/user/{id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/user/{id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/user/{id}/reactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/user/{id}/reactivate", null)).StatusCode);

        await factory.CreateClientForAsync(email);
        Assert.Contains((await admin.GetFromJsonAsync<List<ReadUserDto>>("/api/user", GratiaApiFactory.Json))!, u => u.Id == id);
    }

    [Fact]
    public async Task Admins_cannot_deactivate_themselves()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);

        var response = await admin.PostAsync($"/api/user/{await factory.UserIdAsync(factory.AcmeAdminEmail)}/deactivate", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await factory.CreateClientForAsync(factory.AcmeAdminEmail);
    }

    [Fact]
    public async Task Inviting_a_deactivated_user_again_says_to_reactivate_them()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (id, email) = await factory.AddUserAsync(factory.Acme);
        await admin.PostAsync($"/api/user/{id}/deactivate", null);

        var response = await admin.PostAsJsonAsync("/api/invitation", new { email, role = "Employee" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Reactivate", await response.Content.ReadAsStringAsync());
    }

    private static object UpdateFor(Guid id, string email, string jobTitle, object? role = null) => new
    {
        id,
        fullName = "Temp User",
        email,
        jobTitle,
        role = role ?? "Employee"
    };

    private static object Gift(Guid receiverId, int amount) =>
        new { receiverId, amount, message = "Thanks!", typeOfDonation = "Teamwork" };
}

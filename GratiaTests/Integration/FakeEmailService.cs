using System.Collections.Concurrent;
using Gratia.Application.Interfaces;

namespace GratiaTests.Integration;

public enum EmailKind { Invitation, CompanySetup, AccountExists, WorkspaceExists, Welcome }

public sealed record SentEmail(EmailKind Kind, string To, string? Token = null, string? CompanyName = null);

// Records emails instead of sending them, so a test can check what went out and "click" the link it contained
public sealed class FakeEmailService : IEmailService
{
    private readonly ConcurrentQueue<SentEmail> _sent = new();

    public IReadOnlyList<SentEmail> SentTo(string email) =>
        _sent.Where(e => string.Equals(e.To, email, StringComparison.OrdinalIgnoreCase)).ToList();

    // The token from the latest invitation or setup link sent to this address
    public string LastTokenFor(string email) =>
        SentTo(email).LastOrDefault(e => e.Token is not null)?.Token
            ?? throw new InvalidOperationException($"No link was emailed to {email}");

    public Task SendInvitationEmailAsync(string toEmail, string token, string companyName, string inviterName, string inviterEmail,
        CancellationToken cancellationToken = default) => Record(new(EmailKind.Invitation, toEmail, token, companyName));

    public Task SendCompanySetupEmailAsync(string toEmail, string founderName, string companyName, string token,
        CancellationToken cancellationToken = default) => Record(new(EmailKind.CompanySetup, toEmail, token, companyName));

    public Task SendAccountExistsEmailAsync(string toEmail, CancellationToken cancellationToken = default) =>
        Record(new(EmailKind.AccountExists, toEmail));

    public Task SendWorkspaceExistsEmailAsync(string toEmail, string companyName, CancellationToken cancellationToken = default) =>
        Record(new(EmailKind.WorkspaceExists, toEmail, CompanyName: companyName));

    public Task SendWelcomeEmailAsync(string toEmail, string fullName, string companyName, int monthlyAllowance,
        CancellationToken cancellationToken = default) => Record(new(EmailKind.Welcome, toEmail, CompanyName: companyName));

    private Task Record(SentEmail email)
    {
        _sent.Enqueue(email);
        return Task.CompletedTask;
    }
}

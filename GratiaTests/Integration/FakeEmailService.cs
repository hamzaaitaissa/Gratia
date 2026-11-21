using System.Collections.Concurrent;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;

namespace GratiaTests.Integration;

public enum EmailKind
{
    Invitation, CompanySetup, AccountExists, WorkspaceExists, Welcome,
    PasswordReset, PasswordResetUnavailable, PasswordChanged, EmailChanged
}

// NewEmail is the address an EmailChanged notice names
public sealed record SentEmail(EmailKind Kind, string To, string? Token = null, string? CompanyName = null, string? NewEmail = null);

// Records emails instead of sending them, so a test can check what went out and "click" the link it contained
public sealed class FakeEmailService : IEmailService
{
    private readonly ConcurrentQueue<SentEmail> _sent = new();

    public IReadOnlyList<SentEmail> SentTo(string email) =>
        _sent.Where(e => string.Equals(e.To, email, StringComparison.OrdinalIgnoreCase)).ToList();

    // The token from the latest link sent to this address: an invitation, a setup link or a password reset
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

    public Task SendPasswordResetEmailAsync(string toEmail, string fullName, string token, CancellationToken cancellationToken = default) =>
        Record(new(EmailKind.PasswordReset, toEmail, token));

    public Task SendPasswordResetUnavailableEmailAsync(string toEmail, CancellationToken cancellationToken = default) =>
        Record(new(EmailKind.PasswordResetUnavailable, toEmail));

    public Task SendPasswordChangedEmailAsync(string toEmail, string fullName, CancellationToken cancellationToken = default) =>
        Record(new(EmailKind.PasswordChanged, toEmail));

    public Task SendEmailChangedEmailAsync(string oldEmail, string newEmail, string companyName, CancellationToken cancellationToken = default) =>
        Record(new(EmailKind.EmailChanged, oldEmail, CompanyName: companyName, NewEmail: newEmail));

    private readonly ConcurrentDictionary<string, bool> _failing = new(StringComparer.OrdinalIgnoreCase);

    // Makes every email to this address fail the way EmailService reports an SMTP failure (#20)
    public void FailFor(string email) => _failing[email] = true;
    public void StopFailingFor(string email) => _failing.TryRemove(email, out _);

    private Task Record(SentEmail email)
    {
        if (_failing.ContainsKey(email.To))
            return Task.FromException(new EmailDeliveryException("The email couldn't be sent. Try again in a moment."));

        _sent.Enqueue(email);
        return Task.CompletedTask;
    }
}

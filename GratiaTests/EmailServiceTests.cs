using System.Net.Mail;
using Gratia.Application.Exceptions;
using Gratia.Application.Settings;
using Gratia.Infrastructure.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GratiaTests
{
    public class EmailServiceTests
    {
        private readonly CapturingSender _sender = new();

        private EmailService CreateService(string frontendUrl) =>
            new(_sender, Options.Create(new FrontendOptions { FrontendUrl = frontendUrl }));

        [Theory]
        [InlineData("https://app.gratia.test")]
        [InlineData("https://app.gratia.test/")]
        public async Task Invitation_links_point_at_the_frontend(string frontendUrl)
        {
            await CreateService(frontendUrl).SendInvitationEmailAsync("bob@acme.test", "abc123", "Acme", "Ada", "ada@acme.test");

            var (to, content) = Assert.Single(_sender.Sent);
            Assert.Equal("bob@acme.test", to);
            Assert.Contains("https://app.gratia.test/accept-invitation/abc123", content.HtmlBody);
            Assert.Equal("Ada invited you to join Acme on Gratia", content.Subject);
        }

        [Fact]
        public async Task The_setup_email_links_to_the_accept_page()
        {
            await CreateService("http://localhost:5173").SendCompanySetupEmailAsync("peter@initech.test", "Peter", "Initech", "tok");

            Assert.Contains("http://localhost:5173/accept-invitation/tok", Assert.Single(_sender.Sent).Content.HtmlBody);
        }

        [Fact]
        public async Task The_development_log_sender_writes_the_link_as_text()
        {
            var logger = new CapturingLogger<LogEmailSender>();
            var service = new EmailService(new LogEmailSender(logger), Options.Create(new FrontendOptions { FrontendUrl = "http://localhost:5173" }));

            await service.SendInvitationEmailAsync("bob@acme.test", "abc123", "Acme", "Ada", "ada@acme.test");

            var message = Assert.Single(logger.Messages);
            Assert.Contains("To: bob@acme.test", message);
            Assert.Contains("Accept invitation (http://localhost:5173/accept-invitation/abc123)", message);
            Assert.DoesNotContain("<a ", message);
        }

        [Fact]
        public async Task A_failed_send_becomes_an_EmailDeliveryException()
        {
            var service = new EmailService(new FailingSender(new SmtpException(SmtpStatusCode.ServiceNotAvailable, "Service not available")),
                Options.Create(new FrontendOptions { FrontendUrl = "http://localhost:5173" }));

            // The API answers 503 for it, and the invite and resend handlers undo what the email was about (#20)
            var error = await Assert.ThrowsAsync<EmailDeliveryException>(() =>
                service.SendInvitationEmailAsync("bob@acme.test", "abc123", "Acme", "Ada", "ada@acme.test"));
            Assert.IsType<SmtpException>(error.InnerException);
        }

        [Fact]
        public async Task A_send_the_caller_cancelled_stays_a_cancellation()
        {
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();
            var service = new EmailService(new FailingSender(new OperationCanceledException(cancelled.Token)),
                Options.Create(new FrontendOptions { FrontendUrl = "http://localhost:5173" }));

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                service.SendInvitationEmailAsync("bob@acme.test", "abc123", "Acme", "Ada", "ada@acme.test", cancelled.Token));
        }

        private sealed class FailingSender(Exception failure) : IEmailSender
        {
            public Task SendAsync(string toEmail, EmailContent content, CancellationToken cancellationToken) => Task.FromException(failure);
        }

        private sealed class CapturingSender : IEmailSender
        {
            public List<(string To, EmailContent Content)> Sent { get; } = [];

            public Task SendAsync(string toEmail, EmailContent content, CancellationToken cancellationToken)
            {
                Sent.Add((toEmail, content));
                return Task.CompletedTask;
            }
        }

        private sealed class CapturingLogger<T> : ILogger<T>
        {
            public List<string> Messages { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                Messages.Add(formatter(state, exception));
        }
    }
}

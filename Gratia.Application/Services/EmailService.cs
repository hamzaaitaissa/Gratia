using Gratia.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task SendInvitationEmailAsync(string email, string token, Guid companyId)
        {
            var frontendUrl = _configuration["FrontendUrl"];
            var inviteUrl = $"{frontendUrl}/accept-invitation/{token}";

            var subject = "You're invited to join Gratia!";
            var body = $@"
                <h2>You've been invited to join Gratia!</h2>
                <p>Click the link below to accept your invitation and create your account:</p>
                <a href='{inviteUrl}' style='background-color: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>
                    Accept Invitation
                </a>
                <p>This invitation will expire in 7 days.</p>
                <p>If the button doesn't work, copy and paste this URL into your browser:</p>
                <p>{inviteUrl}</p>
            ";

            await SendEmailAsync(email, subject, body);
        }
        public async Task SendWelcomeEmailAsync(string email, string fullName, string companyName)
        {
            var subject = $"Welcome to {companyName} on Gratia!";
            var body = $@"
                <h2>Welcome to Gratia, {fullName}!</h2>
                <p>Your account has been created successfully for {companyName}.</p>
                <p>You can now start sharing and receiving gratitude points with your colleagues!</p>
                <p>You've been given 25 points to start with.</p>
            ";

            await SendEmailAsync(email, subject, body);
        }
        public async Task SendEmailAsync(string receptor, string subject, string body)
        {
            var email = _configuration.GetValue<string>("EMAIL_CONFIGURATION:EMAIL");
            var password = _configuration.GetValue<string>("EMAIL_CONFIGURATION:PASSWORD");
            var host = _configuration.GetValue<string>("EMAIL_CONFIGURATION:HOST");
            var port = _configuration.GetValue<int>("EMAIL_CONFIGURATION:PORT");

            var smtpClient = new SmtpClient(host, port);
            smtpClient.EnableSsl = true;
            smtpClient.UseDefaultCredentials = false;

            smtpClient.Credentials = new NetworkCredential(email, password);
            var message = new MailMessage(email,receptor,subject,body);
            await smtpClient.SendMailAsync(message);
        }
    }
}

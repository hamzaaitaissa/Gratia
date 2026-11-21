using System.Net;

namespace Gratia.Infrastructure.Email
{
    public sealed record EmailContent(string Subject, string HtmlBody);

    /// <summary>
    /// The text of every email. Names and company names come from users, so they're HTML-encoded in bodies
    /// and kept to one line in subjects.
    /// </summary>
    public static class EmailTemplates
    {
        public static EmailContent Invitation(string companyName, string inviterName, string inviterEmail, string acceptUrl) => new(
            Subject: OneLine($"{inviterName} invited you to join {companyName} on Gratia"),
            HtmlBody: $"""
                <h2>Join {H(companyName)} on Gratia</h2>
                <p>{H(inviterName)} ({H(inviterEmail)}) invited you to join {H(companyName)} on Gratia, where colleagues thank each other with points.</p>
                {Button(acceptUrl, "Accept invitation")}
                <p>The link expires in 7 days. If you weren't expecting this invitation, you can ignore this email.</p>
                {LinkFallback(acceptUrl)}
                """);

        public static EmailContent CompanySetup(string founderName, string companyName, string setupUrl) => new(
            Subject: OneLine($"Finish setting up {companyName} on Gratia"),
            HtmlBody: $"""
                <h2>Hi {H(founderName)},</h2>
                <p>Choose a password to finish setting up {H(companyName)} on Gratia. You'll be its first admin.</p>
                {Button(setupUrl, "Set up your workspace")}
                <p>The link expires in 7 days. If you didn't sign up, you can ignore this email: no account is created unless the link is used.</p>
                {LinkFallback(setupUrl)}
                """);

        public static EmailContent AccountExists(string signInUrl) => new(
            Subject: "You already have a Gratia account",
            HtmlBody: $"""
                <p>Someone tried to create a new Gratia workspace with this email address, but it already has an account.</p>
                {Button(signInUrl, "Sign in")}
                <p>If that wasn't you, you can ignore this email. Nothing was changed.</p>
                """);

        public static EmailContent WorkspaceExists(string companyName) => new(
            Subject: OneLine($"{companyName} is already on Gratia"),
            HtmlBody: $"""
                <p>Someone tried to create a new Gratia workspace with this email address, but your company already has one: {H(companyName)}.</p>
                <p>Ask an admin there to invite you.</p>
                <p>If that wasn't you, you can ignore this email. Nothing was changed.</p>
                """);

        public static EmailContent Welcome(string fullName, string companyName, int monthlyAllowance, string signInUrl) => new(
            Subject: OneLine($"Welcome to {companyName} on Gratia"),
            HtmlBody: $"""
                <h2>Welcome to Gratia, {H(fullName)}!</h2>
                <p>Your account for {H(companyName)} is ready.</p>
                <p>You have {monthlyAllowance} points to give to colleagues this month. They refill every month, and points you haven't given don't carry over.</p>
                {Button(signInUrl, "Sign in")}
                """);

        public static EmailContent PasswordReset(string fullName, string resetUrl) => new(
            Subject: "Reset your Gratia password",
            HtmlBody: $"""
                <h2>Hi {H(fullName)},</h2>
                <p>Someone asked to reset the password of your Gratia account. Choose a new one with this link:</p>
                {Button(resetUrl, "Choose a new password")}
                <p>The link expires in 1 hour and works once. Choosing a new password signs you out on every device.</p>
                <p>If you didn't ask for this, you can ignore this email: your password stays as it is.</p>
                {LinkFallback(resetUrl)}
                """);

        public static EmailContent PasswordResetUnavailable() => new(
            Subject: "Your Gratia password reset",
            HtmlBody: """
                <p>Someone asked to reset the password of a Gratia account with this email address, but there's no active account with it.</p>
                <p>If you think there should be, ask an admin of your workspace: they may have deactivated your account, or invited you with another address.</p>
                <p>If you didn't ask for this, you can ignore this email. Nothing was changed.</p>
                """);

        public static EmailContent PasswordChanged(string fullName, string forgotPasswordUrl) => new(
            Subject: "Your Gratia password was changed",
            HtmlBody: $"""
                <h2>Hi {H(fullName)},</h2>
                <p>The password of your Gratia account was just changed, and every device that was signed in has been signed out.</p>
                <p>If you didn't do this, choose a new password now, then tell an admin of your workspace:</p>
                {Button(forgotPasswordUrl, "Reset your password")}
                """);

        public static EmailContent EmailChanged(string newEmail, string companyName) => new(
            Subject: "Your Gratia sign-in email was changed",
            HtmlBody: $"""
                <p>An admin of {H(companyName)} changed the email address you sign in to Gratia with, from this address to {H(newEmail)}.</p>
                <p>From now on, sign in and reset your password with the new address.</p>
                <p>If you didn't expect this, contact an admin of your workspace.</p>
                """);

        private static string H(string value) => WebUtility.HtmlEncode(value);

        // A subject can't contain line breaks
        private static string OneLine(string value) => value.ReplaceLineEndings(" ");

        private static string Button(string url, string label) =>
            $"""<p><a href="{H(url)}" style="background-color:#4F46E5;color:#ffffff;padding:10px 20px;text-decoration:none;border-radius:6px;display:inline-block">{H(label)}</a></p>""";

        private static string LinkFallback(string url) =>
            $"""<p style="color:#6b7280">If the button doesn't work, copy this address into your browser:<br>{H(url)}</p>""";
    }
}

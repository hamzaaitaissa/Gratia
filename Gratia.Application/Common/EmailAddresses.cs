namespace Gratia.Application.Common;

internal static class EmailAddresses
{
    // Emails are stored and compared lowercase, so "Ada@Acme.com" and "ada@acme.com" are one account
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    // "ada@acme.com" -> "acme.com"
    public static string DomainOf(string normalizedEmail) => normalizedEmail[(normalizedEmail.LastIndexOf('@') + 1)..];

    public static bool IsOnDomain(string normalizedEmail, string domain) => DomainOf(normalizedEmail) == domain;
}

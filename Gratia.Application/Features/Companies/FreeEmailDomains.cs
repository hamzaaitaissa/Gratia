namespace Gratia.Application.Features.Companies;

// Personal email providers. Their domains are shared by strangers, so they can't stand for a company.
internal static class FreeEmailDomains
{
    private static readonly HashSet<string> Domains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com",
        "outlook.com", "outlook.fr", "hotmail.com", "hotmail.fr", "hotmail.co.uk", "live.com", "live.fr", "msn.com",
        "yahoo.com", "yahoo.fr", "yahoo.co.uk", "ymail.com", "rocketmail.com",
        "icloud.com", "me.com", "mac.com",
        "aol.com", "proton.me", "protonmail.com", "pm.me", "tutanota.com", "tuta.io", "fastmail.com", "hey.com",
        "gmx.com", "gmx.net", "gmx.de", "gmx.fr", "web.de", "t-online.de", "mail.com", "zoho.com", "yandex.com", "yandex.ru",
        "mail.ru", "qq.com", "163.com", "126.com",
        "laposte.net", "orange.fr", "wanadoo.fr", "free.fr", "sfr.fr", "menara.ma"
    };

    public static bool Contains(string domain) => Domains.Contains(domain);
}

namespace Gratia.Application.Settings;

public sealed class FrontendOptions
{
    // Shares the existing "AppSettings" section, so the key is AppSettings:FrontendUrl
    // (the AppSettings__FrontendUrl environment variable outside Development)
    public const string SectionName = "AppSettings";

    // The web app's base address, used for links in emails such as /accept-invitation/{token}
    public string FrontendUrl { get; set; } = string.Empty;

    public bool IsValid() =>
        Uri.TryCreate(FrontendUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}

namespace Gratia.Application.Settings;

public sealed class OnboardingOptions
{
    public const string SectionName = "Onboarding";

    // Off in production: a company must sign up with a work email, which ties the workspace to its domain.
    // On in Development, so you can try the signup flow with a personal address; such a workspace has no domain.
    public bool AllowFreeEmailDomains { get; set; }
}

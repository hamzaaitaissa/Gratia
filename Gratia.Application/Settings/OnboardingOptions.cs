namespace Gratia.Application.Settings;

public sealed class OnboardingOptions
{
    public const string SectionName = "Onboarding";

    // Off in production: a company must sign up with a work email, which ties the workspace to its domain.
    // On in Development, so you can try the signup flow with a personal address; such a workspace has no domain.
    public bool AllowFreeEmailDomains { get; set; }

    // How many invitation emails (invites and resends) a workspace can send a day until its domain is verified (#65,
    // #61). Its admins can only invite its own domain meanwhile; this also limits how many mailboxes there it can fill.
    // Verification isn't built yet, so for now it applies to every workspace.
    public int UnverifiedInvitationsPerDay { get; set; } = 50;
}

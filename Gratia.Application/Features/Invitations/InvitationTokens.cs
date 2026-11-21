using Gratia.Application.Common;

namespace Gratia.Application.Features.Invitations;

// Shared by inviting and resending, so both create the same kind of link
internal static class InvitationTokens
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    // URL-safe, so it can go straight into the /accept-invitation/{token} link
    public static string Generate() => SecretTokens.Generate();
}

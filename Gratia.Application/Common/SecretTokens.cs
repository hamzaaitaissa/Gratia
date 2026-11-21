using System.Security.Cryptography;
using System.Text;

namespace Gratia.Application.Common;

// The random tokens that go out in email links: invitations, forgotten passwords
internal static class SecretTokens
{
    // 256 random bits, URL-safe, so a token can go straight into a link
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "");
    }

    // What's stored instead of a token that's as good as a password (a reset link). It's random, so a plain SHA-256 is
    // enough: there's nothing to guess.
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

namespace Gratia.API
{
    // Names of the rate-limiting policies registered in Program.cs. Each one counts per client IP.
    public static class RateLimitPolicies
    {
        // Signup: RateLimiting:SignupPerHour attempts per hour (default 5)
        public const string Signup = "signup";

        // Login: RateLimiting:LoginPerMinute attempts per minute (default 20), against password guessing (#27). Kept
        // generous because a whole office can share one IP.
        public const string Login = "login";

        // Refreshing tokens: RateLimiting:RefreshPerMinute per minute (default 60). A refresh token can't be guessed,
        // so this only limits load; a device normally refreshes once per access-token lifetime.
        public const string Refresh = "refresh";

        // Asking for a forgotten-password link: RateLimiting:PasswordResetPerHour per hour (default 5). Each request
        // sends an email (#50).
        public const string PasswordReset = "password-reset";

        // Checking a domain's DNS for its verification record (#61): RateLimiting:DomainChecksPerMinute per minute (default
        // 10). Each check makes a DNS lookup.
        public const string DomainCheck = "domain-check";
    }
}

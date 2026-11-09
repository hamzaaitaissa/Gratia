namespace Gratia.API
{
    // Names of the rate-limiting policies registered in Program.cs
    public static class RateLimitPolicies
    {
        // Signup: RateLimiting:SignupPerHour attempts per client IP per hour (default 5)
        public const string Signup = "signup";
    }
}

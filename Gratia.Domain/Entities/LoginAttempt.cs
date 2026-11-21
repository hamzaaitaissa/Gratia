using Gratia.Domain.Common;

namespace Gratia.Domain.Entities
{
    /// <summary>
    /// A sign-in with an email (#72), recorded when it starts. A successful sign-in deletes the email's rows and a
    /// refused one deletes its own, so what's left are failures: a wrong password, or an email no account has. Both
    /// count alike, so the limit can't be used to find out which emails have accounts. CreatedDate is when it was made;
    /// rows older than <see cref="Window"/> are deleted as sign-ins happen.
    /// </summary>
    public class LoginAttempt : BaseEntity
    {
        // At most MaxFailures failed sign-ins per email in any Window. Past that, sign-ins with the email are refused,
        // the right password included, until the oldest failure is Window old. Anyone can lock an email this way, so
        // the lock is short.
        public const int MaxFailures = 5;
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

        public string Email { get; set; } = string.Empty;

        public LoginAttempt()
        {
        }

        public LoginAttempt(string email, DateTime attemptedAt)
        {
            Email = email;
            CreatedDate = attemptedAt;
        }
    }
}

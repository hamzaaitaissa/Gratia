using System.Diagnostics;
using Gratia.Infrastructure.Authentication;

namespace GratiaTests
{
    public class PasswordHashingServiceTests
    {
        private const string Password = "Correct-horse-1";
        private readonly PasswordHashingService _service = new();

        [Fact]
        public void A_password_matches_only_its_own_hash()
        {
            var hash = _service.HashPassword(Password);

            Assert.True(_service.VerifyPassword(Password, hash));
            Assert.False(_service.VerifyPassword("Wrong-password-1", hash));
            Assert.False(_service.VerifyPassword(Password, "not a hash"));
        }

        [Fact]
        public void With_no_hash_the_answer_is_no_and_takes_as_long_as_a_wrong_password()
        {
            // No account has the email (#82). Checking a password is slow on purpose (PBKDF2), so answering at once would
            // tell anyone which emails have accounts.
            var hash = _service.HashPassword(Password);

            Assert.False(_service.VerifyPassword(Password, null));

            // The fastest of a few runs each, against a generous bound: answering at once is thousands of times faster
            var wrongPassword = Fastest(() => _service.VerifyPassword("Wrong-password-1", hash));
            var noHash = Fastest(() => _service.VerifyPassword("Wrong-password-1", null));
            Assert.True(noHash > wrongPassword / 4, $"No hash took {noHash.TotalMilliseconds} ms, a wrong password {wrongPassword.TotalMilliseconds} ms");
        }

        private static TimeSpan Fastest(Action check)
        {
            var fastest = TimeSpan.MaxValue;
            for (var i = 0; i < 3; i++)
            {
                var watch = Stopwatch.StartNew();
                check();
                if (watch.Elapsed < fastest)
                    fastest = watch.Elapsed;
            }

            return fastest;
        }
    }
}

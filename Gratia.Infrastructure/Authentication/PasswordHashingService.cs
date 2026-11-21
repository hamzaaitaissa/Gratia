using Gratia.Application.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Gratia.Infrastructure.Authentication
{
    /// <summary>
    /// The one password hasher for the whole app (ASP.NET Core Identity's PBKDF2 format).
    /// Users created before this change were hashed with it too, so their passwords keep working.
    /// </summary>
    public class PasswordHashingService : IPasswordHashingService
    {
        // The default PasswordHasher never looks at the user argument
        private static readonly object NoUser = new();
        private readonly PasswordHasher<object> _hasher = new();

        // Made the same way as every user's hash, so checking against it costs the same. The service is a singleton, so
        // it's made once.
        private readonly string _dummyHash;

        public PasswordHashingService()
        {
            _dummyHash = _hasher.HashPassword(NoUser, Guid.NewGuid().ToString());
        }

        public string HashPassword(string password)
        {
            return _hasher.HashPassword(NoUser, password);
        }

        public bool VerifyPassword(string password, string? hashedPassword)
        {
            if (hashedPassword is null)
            {
                // Only for the time it takes; the answer is no whatever was typed
                _hasher.VerifyHashedPassword(NoUser, _dummyHash, password);
                return false;
            }

            try
            {
                // SuccessRehashNeeded still means the password is correct
                return _hasher.VerifyHashedPassword(NoUser, hashedPassword, password) != PasswordVerificationResult.Failed;
            }
            catch (FormatException)
            {
                // The stored value is not in this hasher's format
                return false;
            }
        }
    }
}

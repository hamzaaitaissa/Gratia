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

        public string HashPassword(string password)
        {
            return _hasher.HashPassword(NoUser, password);
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
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

using Gratia.Domain.Common;

namespace Gratia.Domain.Entities
{
    /// <summary>
    /// One signed-in device or browser (#15), so signing in somewhere else doesn't sign this one out. Only a hash of
    /// the token is stored (#27): someone who reads the database can't use what they find there.
    /// </summary>
    public class RefreshToken : BaseEntity
    {
        public Guid UserId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }

        //navigation
        public User User { get; set; } = null!;

        public RefreshToken()
        {
        }

        public RefreshToken(Guid userId, string tokenHash, DateTime expiresAt)
        {
            UserId = userId;
            TokenHash = tokenHash;
            ExpiresAt = expiresAt;
        }
    }
}

using Gratia.Domain.Common;
using Gratia.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Domain.Entities
{
    public class User: BaseEntity
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string HashedPassword { get; set; }
        public string JobTitle { get; set; }
        public UserRole Role { get; set; }
        // Lifetime points received (only grows) and points left to give this month (refilled to the allowance).
        // A new account starts with nothing received; whoever creates it sets the allowance to give.
        public int NumberOfPointsAcquired { get; set; }
        public int NumberOfPointsAvailable { get; set; }
        // Signed-in devices are RefreshToken rows (#15), so a user can be signed in on several at once
        public Guid CompanyId { get; set; }
        // Set when an admin deactivates the account (#16). Users are never deleted, because their transactions keep
        // pointing at them. A deactivated user can't sign in and is left out of the directory.
        public DateTime? DeactivatedAt { get; set; }

        //navigation
        [ForeignKey(nameof(CompanyId))]
        public Company Company { get; set; }
        public ICollection<Transaction> SentTransactions { get; set; }
        public ICollection<Transaction> ReceivedTransactions { get; set; }

        public User()
        {
            
        }

        public User(string fullname, string email, string hashedpassword, string jobtitle, UserRole role, int numberofpointsacquired, int numberofpointsavailable, Guid companyId)
        {
            FullName = fullname;
            Email = email;
            HashedPassword = hashedpassword;
            JobTitle = jobtitle;
            Role = role;
            NumberOfPointsAcquired = numberofpointsacquired;
            NumberOfPointsAvailable = numberofpointsavailable;
            CompanyId = companyId;
        }

        // An admin's edit. The password isn't part of it (#17): it gets its own change-password flow (#50).
        public void Update(string fullname, string email, string jobtitle, UserRole role)
        {
            FullName = fullname;
            Email = email;
            JobTitle = jobtitle;
            Role = role;
        }

        // Keeps the first date if it's already deactivated
        public void Deactivate(DateTime now) => DeactivatedAt ??= now;

        public void Reactivate() => DeactivatedAt = null;



    }
    
}

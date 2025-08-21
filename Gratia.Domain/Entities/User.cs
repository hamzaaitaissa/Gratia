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
        public int NumberOfPointsAcquired { get; set; } = 25;
        public int NumberOfPointsAvailable { get; set; } = 0;
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }
        public Guid CompanyId { get; set; }

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

        public void Update(string fullname, string email, string hashedpassword, string jobtitle, UserRole role)
        {
            FullName = fullname;
            Email = email;
            HashedPassword = hashedpassword;
            JobTitle = jobtitle;
            Role = role;
        }



    }
    
}

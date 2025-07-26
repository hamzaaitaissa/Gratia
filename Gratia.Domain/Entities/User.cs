using Gratia.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Domain.Entities
{
    internal class User: BaseEntity
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string HashedPassword { get; set; }
        public string JobTitle { get; set; }
        public string Role { get; set; }
        public int NumberOfPointsAcquired { get; set; }
        public int NumberOfPointsAvailable { get; set; }
        public int CompanyId { get; set; }
        public Company Company { get; set; }

        public User(string fullname, string email, string hashedpassword, string jobtitle, string role, int numberofpointsacquired, int numberofpointsavailable)
        {
            FullName = fullname;
            Email = email;
            HashedPassword = hashedpassword;
            JobTitle = jobtitle;
            Role = role;
            NumberOfPointsAcquired = numberofpointsacquired;
            NumberOfPointsAvailable = numberofpointsavailable;
        }

        public void Update(string fullname, string email, string hashedpassword, string jobtitle, string role, int numberofpointsacquired, int numberofpointsavailable)
        {
            FullName = fullname;
            Email = email;
            HashedPassword = hashedpassword;
            JobTitle = jobtitle;
            Role = role;
            NumberOfPointsAcquired = numberofpointsacquired;
            NumberOfPointsAvailable = numberofpointsavailable;
        }



    }
}

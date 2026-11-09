using Gratia.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Domain.Entities
{
    public class Company : BaseEntity
    {
        public string Name { get; set; } 
        public string LogoUrl { get; set; }
        public string PrimaryColor { get; set; }
        public string SecondaryColor { get; set; }

        // The founder's email domain (lowercase), e.g. "acme.com". Null for a workspace founded with a free email
        // address (only allowed when Onboarding:AllowFreeEmailDomains is on), since such a domain is shared by strangers.
        public string? Domain { get; set; }

        // Set once the company proves it controls Domain through DNS (TASKS.md #61). Until then the workspace is
        // provisional: its admins can only invite people on Domain.
        public DateTime? DomainVerifiedAt { get; set; }

        public bool IsDomainVerified => DomainVerifiedAt is not null;

        // Used until an admin sets the company's own branding
        public const string DefaultPrimaryColor = "#4F46E5";
        public const string DefaultSecondaryColor = "#F59E0B";

        public ICollection<User> Users { get; set; }

        public Company()
        {
            
        }
        public Company(string name, string logoUrl, string primaryColor, string secondaryColor)
        {
            Name = name;
            LogoUrl = logoUrl;
            PrimaryColor = primaryColor;
            SecondaryColor = secondaryColor;
        }

        public void Update(string name, string logoUrl, string primaryColor, string secondaryColor)
        {
            Name = name;
            LogoUrl = logoUrl;
            PrimaryColor = primaryColor;
            SecondaryColor = secondaryColor;
        }
    }
}

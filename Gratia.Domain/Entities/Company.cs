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

using Gratia.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Domain.Entities
{
    internal class Company : BaseEntity
    {
        public string Name { get; set; }
        public string LogoUrl { get; set; }
        public string PrimaryColor { get; set; }
        public string SecondaryCololr { get; set; }

        public Company(string name, string logoUrl, string primaryColor, string secondaryColor)
        {
            Name = name;
            LogoUrl = logoUrl;
            PrimaryColor = primaryColor;
            SecondaryCololr = secondaryColor;
        }

        public void Update(string name, string logoUrl, string primaryColor, string secondaryColor)
        {
            Name = name;
            LogoUrl = logoUrl;
            PrimaryColor = primaryColor;
            SecondaryCololr = secondaryColor;
        }
    }
}

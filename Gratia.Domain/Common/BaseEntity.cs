using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Domain.Common
{
    public class BaseEntity
    {
        public Guid Id { get; set; }
        // UTC, like every DateTime in the app (#40)
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}

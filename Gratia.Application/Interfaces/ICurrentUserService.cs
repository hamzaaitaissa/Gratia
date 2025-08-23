using Gratia.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Interfaces
{
    public interface ICurrentUserService
    {
        Guid UserId { get; }
        Guid CompanyId { get; }
        UserRole? Role { get; }
    }
}

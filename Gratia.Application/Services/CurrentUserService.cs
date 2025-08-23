using Gratia.Application.Interfaces;
using Gratia.Domain.Enum;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid UserId => Guid.TryParse(_httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid id) ? id : Guid.Empty;

        public Guid CompanyId => Guid.TryParse(_httpContextAccessor.HttpContext.User.FindFirstValue("CompanyId"), out Guid id) ? id : Guid.Empty;

        public UserRole? Role => Enum.TryParse(_httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Role), out UserRole role) ? role : null;
    }
}

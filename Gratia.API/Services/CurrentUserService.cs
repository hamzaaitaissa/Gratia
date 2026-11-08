using Gratia.Application.Interfaces;
using Gratia.Domain.Enum;
using System.Security.Claims;

namespace Gratia.API.Services
{
    // Reads the caller from the JWT claims of the current HTTP request
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

        public Guid UserId => Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out Guid id) ? id : Guid.Empty;
        public Guid CompanyId => Guid.TryParse(User?.FindFirstValue("CompanyId"), out Guid id) ? id : Guid.Empty;
        public UserRole? Role => Enum.TryParse(User?.FindFirstValue(ClaimTypes.Role), out UserRole role) ? role : null;
    }
}

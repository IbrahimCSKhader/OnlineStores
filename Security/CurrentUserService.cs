using System.Security.Claims;

namespace onlineStore.Security
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

        public Guid? UserId
        {
            get
            {
                var value = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return Guid.TryParse(value, out var id) ? id : null;
            }
        }

        public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

        public bool IsSuperAdmin => User?.IsInRole("SuperAdmin") == true;

        public bool IsStoreOwner => User?.IsInRole("StoreOwner") == true;
    }
}
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;

namespace onlineStore.Security
{
    public class StoreAuthorizationService : IStoreAuthorizationService
    {
        private const string SuperAdminRole = "SuperAdmin";
        private const string StoreOwnerRole = "StoreOwner";

        private readonly AppDbContext _context;

        public StoreAuthorizationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsSuperAdminAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await UserHasRoleAsync(userId, SuperAdminRole, cancellationToken);
        }

        public async Task<bool> CanManageStoreAsync(Guid userId, Guid storeId, CancellationToken cancellationToken = default)
        {
            if (await IsSuperAdminAsync(userId, cancellationToken))
                return true;

            var isStoreOwner = await UserHasRoleAsync(userId, StoreOwnerRole, cancellationToken);
            if (!isStoreOwner)
                return false;

            return await _context.Stores
                .AsNoTracking()
                .AnyAsync(s => s.Id == storeId && s.OwnerId == userId, cancellationToken);
        }

        public async Task<bool> CanManageProductAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default)
        {
            if (await IsSuperAdminAsync(userId, cancellationToken))
                return true;

            var productStoreId = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => (Guid?)p.StoreId)
                .FirstOrDefaultAsync(cancellationToken);

            return productStoreId.HasValue
                && await CanManageStoreAsync(userId, productStoreId.Value, cancellationToken);
        }

        public async Task<bool> CanManageCategoryAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken = default)
        {
            if (await IsSuperAdminAsync(userId, cancellationToken))
                return true;

            var categoryStoreId = await _context.Categories
                .AsNoTracking()
                .Where(c => c.Id == categoryId)
                .Select(c => (Guid?)c.StoreId)
                .FirstOrDefaultAsync(cancellationToken);

            return categoryStoreId.HasValue
                && await CanManageStoreAsync(userId, categoryStoreId.Value, cancellationToken);
        }

        public async Task<bool> CanManageSectionAsync(Guid userId, Guid sectionId, CancellationToken cancellationToken = default)
        {
            if (await IsSuperAdminAsync(userId, cancellationToken))
                return true;

            var sectionStoreId = await _context.Sections
                .AsNoTracking()
                .Where(s => s.Id == sectionId)
                .Select(s => (Guid?)s.StoreId)
                .FirstOrDefaultAsync(cancellationToken);

            return sectionStoreId.HasValue
                && await CanManageStoreAsync(userId, sectionStoreId.Value, cancellationToken);
        }

        private async Task<bool> UserHasRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken)
        {
            return await _context.UserRoles
                .AsNoTracking()
                .Join(
                    _context.Roles.AsNoTracking(),
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, role) => new { userRole.UserId, role.Name })
                .AnyAsync(x => x.UserId == userId && x.Name == roleName, cancellationToken);
        }
    }
}

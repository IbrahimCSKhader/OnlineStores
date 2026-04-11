using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.Models.Identity;

namespace onlineStore.Security
{
    public class StoreAccountBoundaryService : IStoreAccountBoundaryService
    {
        private const string StoreOwnerRole = "StoreOwner";

        private readonly AppDbContext _context;

        public StoreAccountBoundaryService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsStoreOwnerEmailAsync(
            Guid storeId,
            string email,
            CancellationToken cancellationToken = default)
        {
            if (storeId == Guid.Empty || string.IsNullOrWhiteSpace(email))
                return false;

            var normalizedEmail = NormalizeEmail(email);

            return await _context.Stores
                .AsNoTracking()
                .Where(store => store.Id == storeId)
                .Join(
                    _context.Users.AsNoTracking(),
                    store => store.OwnerId,
                    user => user.Id,
                    (_, user) => new
                    {
                        user.Email,
                        user.UserName
                    })
                .AnyAsync(
                    user =>
                        (user.Email != null && user.Email.ToLower() == normalizedEmail) ||
                        (user.UserName != null && user.UserName.ToLower() == normalizedEmail),
                    cancellationToken);
        }

        public async Task<AppUser?> GetActiveStoreOwnerAsync(
            Guid ownerId,
            CancellationToken cancellationToken = default)
        {
            if (ownerId == Guid.Empty)
                return null;

            var storeOwnerUserIds = _context.UserRoles
                .AsNoTracking()
                .Join(
                    _context.Roles.AsNoTracking().Where(role => role.Name == StoreOwnerRole),
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, _) => userRole.UserId);

            return await _context.Users
                .AsNoTracking()
                .Where(user => user.Id == ownerId && user.IsActive && !user.IsDeleted)
                .Join(
                    storeOwnerUserIds,
                    user => user.Id,
                    storeOwnerUserId => storeOwnerUserId,
                    (user, _) => user)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static string NormalizeEmail(string email) =>
            email.Trim().ToLowerInvariant();
    }
}

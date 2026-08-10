using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.SuperAdminDashboard;
using onlineStore.Models;
using onlineStore.Models.Identity;

namespace onlineStore.Services.SuperAdminDashboard
{
    public class SuperAdminDashboardService : ISuperAdminDashboardService
    {
        private const string StoreOwnerRoleName = "StoreOwner";
        private readonly AppDbContext _context;

        public SuperAdminDashboardService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SuperAdminDashboardSummaryDto> GetSummaryAsync()
        {
            var storeOwnerRoleId = await GetRoleIdAsync(StoreOwnerRoleName);

            var totalStores = await _context.Stores.AsNoTracking().CountAsync();
            var activeStores = await _context.Stores.AsNoTracking().CountAsync(x => x.IsActive);
            var totalStoreCustomers = await _context.StoreCustomers.AsNoTracking().CountAsync();
            var activeStoreCustomers = await _context.StoreCustomers.AsNoTracking().CountAsync(x => x.IsActive);
            var totalContactAccounts = await _context.StoreContactAccounts.AsNoTracking().CountAsync();

            var totalStoreOwners = 0;
            var activeStoreOwners = 0;

            if (storeOwnerRoleId.HasValue)
            {
                var storeOwnersQuery = GetStoreOwnersQuery(storeOwnerRoleId.Value);
                totalStoreOwners = await storeOwnersQuery.CountAsync();
                activeStoreOwners = await storeOwnersQuery.CountAsync(x => x.IsActive);
            }

            return new SuperAdminDashboardSummaryDto
            {
                TotalStores = totalStores,
                ActiveStores = activeStores,
                InactiveStores = totalStores - activeStores,
                TotalStoreOwners = totalStoreOwners,
                ActiveStoreOwners = activeStoreOwners,
                InactiveStoreOwners = totalStoreOwners - activeStoreOwners,
                TotalStoreCustomers = totalStoreCustomers,
                ActiveStoreCustomers = activeStoreCustomers,
                InactiveStoreCustomers = totalStoreCustomers - activeStoreCustomers,
                TotalContactAccounts = totalContactAccounts,
            };
        }

        public async Task<List<SuperAdminOwnerListItemDto>> GetStoreOwnersAsync()
        {
            var storeOwnerRoleId = await GetRoleIdAsync(StoreOwnerRoleName);
            if (!storeOwnerRoleId.HasValue)
                return new List<SuperAdminOwnerListItemDto>();

            var owners = await GetStoreOwnersQuery(storeOwnerRoleId.Value)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new SuperAdminOwnerDetailsDto
                {
                    Id = x.Id,
                    Email = x.Email ?? string.Empty,
                    FirstName = x.FirstName ?? string.Empty,
                    LastName = x.LastName ?? string.Empty,
                    FullName = BuildFullName(x.FirstName, x.LastName),
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                })
                .ToListAsync();

            await AttachStoresAsync(owners);

            return owners
                .Select(owner => new SuperAdminOwnerListItemDto
                {
                    Id = owner.Id,
                    Email = owner.Email,
                    FirstName = owner.FirstName,
                    LastName = owner.LastName,
                    FullName = owner.FullName,
                    IsActive = owner.IsActive,
                    CreatedAt = owner.CreatedAt,
                    StoreCount = owner.StoreCount,
                    ActiveStoreCount = owner.ActiveStoreCount,
                    InactiveStoreCount = owner.InactiveStoreCount,
                    Stores = owner.Stores,
                })
                .ToList();
        }

        public async Task<SuperAdminOwnerDetailsDto?> GetStoreOwnerDetailsAsync(Guid ownerId)
        {
            var storeOwnerRoleId = await GetRoleIdAsync(StoreOwnerRoleName);
            if (!storeOwnerRoleId.HasValue)
                return null;

            var owner = await GetStoreOwnersQuery(storeOwnerRoleId.Value)
                .Where(x => x.Id == ownerId)
                .Select(x => new SuperAdminOwnerDetailsDto
                {
                    Id = x.Id,
                    Email = x.Email ?? string.Empty,
                    FirstName = x.FirstName ?? string.Empty,
                    LastName = x.LastName ?? string.Empty,
                    FullName = BuildFullName(x.FirstName, x.LastName),
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                })
                .FirstOrDefaultAsync();

            if (owner == null)
                return null;

            await AttachStoresAsync(new List<SuperAdminOwnerDetailsDto> { owner });
            return owner;
        }

        public async Task<bool> SetStoreOwnerStatusAsync(Guid ownerId, bool isActive)
        {
            var storeOwnerRoleId = await GetRoleIdAsync(StoreOwnerRoleName);
            if (!storeOwnerRoleId.HasValue)
                return false;

            var owner = await _context.Users
                .FirstOrDefaultAsync(x =>
                    x.Id == ownerId &&
                    !x.IsDeleted &&
                    _context.UserRoles.Any(ur => ur.UserId == x.Id && ur.RoleId == storeOwnerRoleId.Value));

            if (owner == null)
                return false;

            owner.IsActive = isActive;
            owner.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<SuperAdminStoreListItemDto>> GetStoresAsync()
        {
            var stores = await _context.Stores
                .AsNoTracking()
                .Include(x => x.ContactAccounts)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            if (stores.Count == 0)
                return new List<SuperAdminStoreListItemDto>();

            var ownerMap = await GetOwnersMapAsync(stores.Select(x => x.OwnerId));
            var customerCounts = await GetStoreCustomerCountsAsync(stores.Select(x => x.Id));

            return stores
                .Select(store => MapStoreListItem(
                    store,
                    ownerMap.TryGetValue(store.OwnerId, out var owner) ? owner : null,
                    customerCounts))
                .ToList();
        }

        public async Task<SuperAdminStoreDetailsDto?> GetStoreDetailsAsync(Guid storeId)
        {
            var store = await _context.Stores
                .AsNoTracking()
                .Include(x => x.ContactAccounts)
                .FirstOrDefaultAsync(x => x.Id == storeId);

            if (store == null)
                return null;

            var owner = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == store.OwnerId && !x.IsDeleted);

            var customerCount = await _context.StoreCustomers
                .AsNoTracking()
                .CountAsync(x => x.StoreId == storeId);

            return new SuperAdminStoreDetailsDto
            {
                Id = store.Id,
                Name = store.Name,
                Slug = store.Slug,
                CustomDomain = store.CustomDomain,
                ThemeTemplate = StoreThemeTemplates.NormalizeForResponse(store.ThemeTemplate),
                Description = store.Description,
                BusinessType = store.BusinessType,
                IsActive = store.IsActive,
                StoreStory = store.StoreStory,
                WhatsAppNumber = store.WhatsAppNumber,
                CustomerCount = customerCount,
                VisitCount = store.VisitCount,
                CreatedAt = store.CreatedAt,
                UpdatedAt = store.UpdatedAt,
                Owner = owner == null ? null : MapOwnerInfo(owner),
                ContactAccounts = MapContactAccounts(store.ContactAccounts),
            };
        }

        public async Task<bool> SetStoreStatusAsync(Guid storeId, bool isActive)
        {
            var store = await _context.Stores.FirstOrDefaultAsync(x => x.Id == storeId);
            if (store == null)
                return false;

            store.IsActive = isActive;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<SuperAdminStoreCustomerDto>?> GetStoreCustomersAsync(Guid storeId)
        {
            var storeExists = await _context.Stores
                .AsNoTracking()
                .AnyAsync(x => x.Id == storeId);

            if (!storeExists)
                return null;

            return await _context.StoreCustomers
                .AsNoTracking()
                .Where(x => x.StoreId == storeId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new SuperAdminStoreCustomerDto
                {
                    Id = x.Id,
                    StoreId = x.StoreId,
                    FirstName = x.FirstName,
                    LastName = x.LastName,
                    FullName = (x.FirstName + " " + x.LastName).Trim(),
                    Email = x.Email,
                    Phone = x.Phone,
                    DiscountPercentage = x.DiscountPercentage,
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                })
                .ToListAsync();
        }

        private async Task AttachStoresAsync(List<SuperAdminOwnerDetailsDto> owners)
        {
            if (owners.Count == 0)
                return;

            var ownerIds = owners.Select(x => x.Id).ToList();
            var stores = await _context.Stores
                .AsNoTracking()
                .Include(x => x.ContactAccounts)
                .Where(x => ownerIds.Contains(x.OwnerId))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            var customerCounts = await GetStoreCustomerCountsAsync(stores.Select(x => x.Id));
            var storesByOwner = stores
                .GroupBy(x => x.OwnerId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(store => new SuperAdminOwnerManagedStoreDto
                        {
                            Id = store.Id,
                            Name = store.Name,
                            Slug = store.Slug,
                            CustomDomain = store.CustomDomain,
                            ThemeTemplate = StoreThemeTemplates.NormalizeForResponse(store.ThemeTemplate),
                            IsActive = store.IsActive,
                            StoreStory = store.StoreStory,
                            WhatsAppNumber = store.WhatsAppNumber,
                            CustomerCount = customerCounts.TryGetValue(store.Id, out var customerCount)
                                ? customerCount
                                : 0,
                            CreatedAt = store.CreatedAt,
                            UpdatedAt = store.UpdatedAt,
                            ContactAccounts = MapContactAccounts(store.ContactAccounts),
                        })
                        .ToList());

            foreach (var owner in owners)
            {
                owner.Stores = storesByOwner.TryGetValue(owner.Id, out var ownerStores)
                    ? ownerStores
                    : new List<SuperAdminOwnerManagedStoreDto>();
                owner.StoreCount = owner.Stores.Count;
                owner.ActiveStoreCount = owner.Stores.Count(x => x.IsActive);
                owner.InactiveStoreCount = owner.StoreCount - owner.ActiveStoreCount;
            }
        }

        private async Task<Dictionary<Guid, AppUser>> GetOwnersMapAsync(IEnumerable<Guid> ownerIds)
        {
            var uniqueOwnerIds = ownerIds.Distinct().ToList();
            if (uniqueOwnerIds.Count == 0)
                return new Dictionary<Guid, AppUser>();

            return await _context.Users
                .AsNoTracking()
                .Where(x => uniqueOwnerIds.Contains(x.Id) && !x.IsDeleted)
                .ToDictionaryAsync(x => x.Id);
        }

        private async Task<Dictionary<Guid, int>> GetStoreCustomerCountsAsync(IEnumerable<Guid> storeIds)
        {
            var uniqueStoreIds = storeIds.Distinct().ToList();
            if (uniqueStoreIds.Count == 0)
                return new Dictionary<Guid, int>();

            return await _context.StoreCustomers
                .AsNoTracking()
                .Where(x => uniqueStoreIds.Contains(x.StoreId))
                .GroupBy(x => x.StoreId)
                .Select(group => new { StoreId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(x => x.StoreId, x => x.Count);
        }

        private IQueryable<AppUser> GetStoreOwnersQuery(Guid storeOwnerRoleId)
        {
            return _context.Users
                .AsNoTracking()
                .Where(x =>
                    !x.IsDeleted &&
                    _context.UserRoles.Any(ur => ur.UserId == x.Id && ur.RoleId == storeOwnerRoleId));
        }

        private async Task<Guid?> GetRoleIdAsync(string roleName)
        {
            return await _context.Roles
                .AsNoTracking()
                .Where(x => x.Name == roleName)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync();
        }

        private static SuperAdminStoreListItemDto MapStoreListItem(
            onlineStore.Models.Store store,
            AppUser? owner,
            IReadOnlyDictionary<Guid, int> customerCounts)
        {
            return new SuperAdminStoreListItemDto
            {
                Id = store.Id,
                Name = store.Name,
                Slug = store.Slug,
                CustomDomain = store.CustomDomain,
                ThemeTemplate = StoreThemeTemplates.NormalizeForResponse(store.ThemeTemplate),
                Description = store.Description,
                BusinessType = store.BusinessType,
                IsActive = store.IsActive,
                StoreStory = store.StoreStory,
                WhatsAppNumber = store.WhatsAppNumber,
                CustomerCount = customerCounts.TryGetValue(store.Id, out var customerCount)
                    ? customerCount
                    : 0,
                VisitCount = store.VisitCount,
                CreatedAt = store.CreatedAt,
                UpdatedAt = store.UpdatedAt,
                Owner = owner == null ? null : MapOwnerInfo(owner),
                ContactAccounts = MapContactAccounts(store.ContactAccounts),
            };
        }

        private static SuperAdminStoreOwnerInfoDto MapOwnerInfo(AppUser owner)
        {
            return new SuperAdminStoreOwnerInfoDto
            {
                Id = owner.Id,
                Email = owner.Email ?? string.Empty,
                FirstName = owner.FirstName ?? string.Empty,
                LastName = owner.LastName ?? string.Empty,
                FullName = BuildFullName(owner.FirstName, owner.LastName),
                IsActive = owner.IsActive,
            };
        }

        private static List<SuperAdminStoreContactAccountDto> MapContactAccounts(IEnumerable<StoreContactAccount> contactAccounts)
        {
            return contactAccounts
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.CreatedAt)
                .Select(x => new SuperAdminStoreContactAccountDto
                {
                    Id = x.Id,
                    Platform = x.Platform,
                    Username = x.Username,
                    Label = x.Label,
                    Url = StoreContactPlatforms.BuildUrl(x.Platform, x.Username),
                    SortOrder = x.SortOrder,
                })
                .ToList();
        }

        private static string BuildFullName(string? firstName, string? lastName)
        {
            return $"{firstName ?? string.Empty} {lastName ?? string.Empty}".Trim();
        }
    }
}


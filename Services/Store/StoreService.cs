using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Store;
using onlineStore.Models;
using onlineStore.Security;

namespace onlineStore.Services.Store
{
    public class StoreService : IStoreService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StoreService> _logger;
        private readonly ICurrentUserService _currentUser;

        public StoreService(
            AppDbContext context,
            ILogger<StoreService> logger,
            ICurrentUserService currentUser)
        {
            _context = context;
            _logger = logger;
            _currentUser = currentUser;
        }

        public async Task<List<StoreDto>> GetAllStoresAsync()
        {
            if (_currentUser.IsSuperAdmin)
            {
                return await _context.Stores
                    .AsNoTracking()
                    .Select(s => ToDto(s))
                    .ToListAsync();
            }

            if (!_currentUser.UserId.HasValue)
                return new List<StoreDto>();

            return await _context.Stores
                .AsNoTracking()
                .Where(s => s.OwnerId == _currentUser.UserId.Value)
                .Select(s => ToDto(s))
                .ToListAsync();
        }

        public async Task<StoreDto?> GetStoreByIdAsync(Guid id)
        {
            var store = await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    (_currentUser.IsSuperAdmin ||
                     s.OwnerId == _currentUser.UserId));

            return store == null ? null : ToDto(store);
        }

        public async Task<StoreDto?> GetStoreBySlugAsync(string slug)
        {
            var normalizedSlug = slug.Trim().ToLower();

            var store = await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Slug == normalizedSlug &&
                    (_currentUser.IsSuperAdmin ||
                     s.OwnerId == _currentUser.UserId));

            return store == null ? null : ToDto(store);
        }

        public async Task<StoreDto> CreateStoreAsync(CreateStoreDto dto)
        {
            var normalizedSlug = dto.Slug.Trim().ToLower();

            var slugExists = await _context.Stores
                .AsNoTracking()
                .AnyAsync(s => s.Slug == normalizedSlug);

            if (slugExists)
                throw new Exception("sorry this link is used already");

            var store = new Models.Store
            {
                Name = dto.Name.Trim(),
                Slug = normalizedSlug,
                Description = dto.Description,
                BusinessType = dto.BusinessType,
                LogoUrl = dto.LogoUrl,
                CoverImageUrl = dto.CoverImageUrl,
                WhatsAppNumber = dto.WhatsAppNumber,
                ThemeTemplate = dto.ThemeTemplate,
                OwnerId = dto.OwnerId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Stores.Add(store);
            await _context.SaveChangesAsync();

            CreateStoreFolders(store.Id);

            _logger.LogInformation(
                "Store created: {StoreName}, OwnerId: {OwnerId}",
                store.Name, store.OwnerId);

            return ToDto(store);
        }

        public async Task<StoreDto?> UpdateStoreAsync(Guid id, UpdateStoreDto dto)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    (_currentUser.IsSuperAdmin ||
                     s.OwnerId == _currentUser.UserId));

            if (store == null)
                return null;

            if (dto.Name != null)
                store.Name = dto.Name.Trim();

            if (dto.Description != null)
                store.Description = dto.Description;

            if (dto.BusinessType != null)
                store.BusinessType = dto.BusinessType;

            if (dto.LogoUrl != null)
                store.LogoUrl = dto.LogoUrl;

            if (dto.CoverImageUrl != null)
                store.CoverImageUrl = dto.CoverImageUrl;

            if (dto.IsActive != null)
                store.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Store updated: {StoreId}", id);

            return ToDto(store);
        }

        public async Task<bool> DeleteStoreAsync(Guid id)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    (_currentUser.IsSuperAdmin ||
                     s.OwnerId == _currentUser.UserId));

            if (store == null)
                return false;

            store.IsDeleted = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "just soft delete Store deleted: {StoreId}", id);

            return true;
        }

        private static StoreDto ToDto(Models.Store s) => new()
        {
            Id = s.Id,
            Name = s.Name,
            Slug = s.Slug,
            Description = s.Description,
            BusinessType = s.BusinessType,
            LogoUrl = s.LogoUrl,
            CoverImageUrl = s.CoverImageUrl,
            IsActive = s.IsActive,
            VisitCount = s.VisitCount,
            CreatedAt = s.CreatedAt
        };

        private void CreateStoreFolders(Guid storeId)
        {
            var basePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "uploads", "stores", storeId.ToString()
            );

            var folders = new[]
            {
                Path.Combine(basePath, "branding"),
                Path.Combine(basePath, "products")
            };

            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
            }

            _logger.LogInformation(
                "Store folders created for: {StoreId}", storeId);
        }
        public async Task<int?> IncrementStoreVisitAsync(Guid storeId)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.Id == storeId);

            if (store == null)
                return null;

            store.VisitCount += 1;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Store visit incremented: {StoreId}, Count: {VisitCount}",
                storeId, store.VisitCount);

            return store.VisitCount;
        }

        public async Task<int?> GetStoreVisitCountAsync(Guid storeId)
        {
            return await _context.Stores
                .AsNoTracking()
                .Where(s => s.Id == storeId)
                .Select(s => (int?)s.VisitCount)
                .FirstOrDefaultAsync();
        }
    }
}
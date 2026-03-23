using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Store;

namespace onlineStore.Services.Store
{
    public class StoreService : IStoreService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StoreService> _logger;

        public StoreService(
            AppDbContext context,
            ILogger<StoreService> logger)
        {
            _context = context;
            _logger = logger;
        }


        public async Task<List<StoreDto>> GetAllStoresAsync()
        {
            return await _context.Stores
                .AsNoTracking()
                .Select(s => ToDto(s))
                .ToListAsync();
        }


        public async Task<StoreDto?> GetStoreByIdAsync(Guid id)
        {
            var store = await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            return store == null ? null : ToDto(store);
        }



        public async Task<StoreDto?> GetStoreBySlugAsync(string slug)
        {
            var store = await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Slug == slug.ToLower());

            return store == null ? null : ToDto(store);
        }



        public async Task<StoreDto> CreateStoreAsync(
            CreateStoreDto dto, string ownerId)
        {
            var slugExists = await _context.Stores
                .AnyAsync(s => s.Slug == dto.Slug.ToLower());

            if (slugExists)
                throw new Exception("sorry this link is used already");

            var store = new Models.Store
            {
                Name = dto.Name.Trim(),
                Slug = dto.Slug.Trim().ToLower(),
                Description = dto.Description,
                BusinessType = dto.BusinessType,
                LogoUrl = dto.LogoUrl,
                CoverImageUrl = dto.CoverImageUrl,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Stores.Add(store);
            await _context.SaveChangesAsync();
            CreateStoreFolders(store.Id);

            _logger.LogInformation(
                "Store created: {StoreName}", store.Name);

            return ToDto(store);
        }



        public async Task<StoreDto?> UpdateStoreAsync(
            Guid id, UpdateStoreDto dto)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.Id == id);

            if (store == null) return null;

            if (dto.Name != null) store.Name = dto.Name.Trim();
            if (dto.Description != null) store.Description = dto.Description;
            if (dto.BusinessType != null) store.BusinessType = dto.BusinessType;
            if (dto.LogoUrl != null) store.LogoUrl = dto.LogoUrl;
            if (dto.CoverImageUrl != null) store.CoverImageUrl = dto.CoverImageUrl;
            if (dto.IsActive != null) store.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Store updated: {StoreId}", id);

            return ToDto(store);
        }


        
        public async Task<bool> DeleteStoreAsync(Guid id)
        {
            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.Id == id);

            if (store == null) return false;

            
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
            CreatedAt = s.CreatedAt
        };
        //HELPER
        
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
    }
}
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
        private readonly IWebHostEnvironment _environment;

        public StoreService(
            AppDbContext context,
            ILogger<StoreService> logger,
            ICurrentUserService currentUser,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _currentUser = currentUser;
            _environment = environment;
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

        //public async Task<StoreDto> CreateStoreAsync(CreateStoreDto dto)
        //{
        //    var normalizedSlug = dto.Slug.Trim().ToLower();

        //    var slugExists = await _context.Stores
        //        .AsNoTracking()
        //        .AnyAsync(s => s.Slug == normalizedSlug);

        //    if (slugExists)
        //        throw new Exception("sorry this link is used already");

        //    var store = new Models.Store
        //    {
        //        Name = dto.Name.Trim(),
        //        Slug = normalizedSlug,
        //        Description = dto.Description?.Trim(),
        //        BusinessType = dto.BusinessType?.Trim(),
        //        WhatsAppNumber = dto.WhatsAppNumber?.Trim(),
        //        ThemeTemplate = string.IsNullOrWhiteSpace(dto.ThemeTemplate)
        //            ? "default"
        //            : dto.ThemeTemplate.Trim(),
        //        OwnerId = dto.OwnerId,
        //        IsActive = true,
        //        CreatedAt = DateTime.UtcNow
        //    };

        //    _context.Stores.Add(store);
        //    await _context.SaveChangesAsync();

        //    CreateStoreFolders(store.Id);

        //    if (dto.Logo != null)
        //    {
        //        var logoRelativeUrl = await SaveBrandingImageAsync(
        //            dto.Logo,
        //            store.Id,
        //            "Logo"
        //        );

        //        store.LogoUrl = logoRelativeUrl;
        //    }

        //    if (dto.CoverPage != null)
        //    {
        //        var coverRelativeUrl = await SaveBrandingImageAsync(
        //            dto.CoverPage,
        //            store.Id,
        //            "CoverPage"
        //        );

        //        store.CoverImageUrl = coverRelativeUrl;
        //    }

        //    await _context.SaveChangesAsync();

        //    _logger.LogInformation(
        //        "Store created: {StoreName}, OwnerId: {OwnerId}",
        //        store.Name, store.OwnerId);

        //    return ToDto(store);
        //}

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
        private async Task<string> SaveBrandingImageAsync(
     IFormFile file,
     Guid storeId,
     string fileBaseName)
        {
            if (file == null || file.Length == 0)
                throw new Exception("Invalid image file");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
                throw new Exception("Only .jpg, .jpeg, .png, .webp files are allowed");

            const long maxFileSize = 5 * 1024 * 1024;
            if (file.Length > maxFileSize)
                throw new Exception("Image size must not exceed 5 MB");

            var brandingPath = GetBrandingFolderPath(storeId);

            if (!Directory.Exists(brandingPath))
                Directory.CreateDirectory(brandingPath);

            DeleteExistingBrandingFileIfExists(brandingPath, fileBaseName);

            var fileName = $"{fileBaseName}{extension}";
            var fullPath = Path.Combine(brandingPath, fileName);

            using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            return GetBrandingFileRelativeUrl(storeId, fileName);
        }
        private string GetBrandingFolderPath(Guid storeId)
        {
            var rootPath = _environment.WebRootPath ?? _environment.ContentRootPath;

            return Path.Combine(
                rootPath,
                "uploads",
                "stores",
                storeId.ToString(),
                "branding"
            );
        }
        private void CreateStoreFolders(Guid storeId)
        {
            var rootPath = _environment.WebRootPath ?? _environment.ContentRootPath;

            var basePath = Path.Combine(
                rootPath,
                "uploads",
                "stores",
                storeId.ToString()
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
        private void DeleteExistingBrandingFileIfExists(
      string brandingPath,
      string fileBaseName)
        {
            if (!Directory.Exists(brandingPath))
                return;

            var existingFiles = Directory.GetFiles(brandingPath, $"{fileBaseName}.*");

            foreach (var file in existingFiles)
            {
                File.Delete(file);
            }
        }
        private static string GetBrandingFileRelativeUrl(Guid storeId, string fileName)
        {
            return $"/uploads/stores/{storeId}/branding/{fileName}";
        }

        public async Task<StoreDto> CreateStoreAsync(CreateStoreDto dto, string userId)
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
        Description = dto.Description?.Trim(),
        BusinessType = dto.BusinessType?.Trim(),
        WhatsAppNumber = dto.WhatsAppNumber?.Trim(),
        ThemeTemplate = string.IsNullOrWhiteSpace(dto.ThemeTemplate)
            ? "default"
            : dto.ThemeTemplate.Trim(),
        OwnerId = Guid.Parse(userId), // 🔥 هون الفرق
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    _context.Stores.Add(store);
    await _context.SaveChangesAsync();

    CreateStoreFolders(store.Id);

    if (dto.Logo != null)
    {
        var logoUrl = await SaveBrandingImageAsync(dto.Logo, store.Id, "Logo");
        store.LogoUrl = logoUrl;
    }

    if (dto.CoverPage != null)
    {
        var coverUrl = await SaveBrandingImageAsync(dto.CoverPage, store.Id, "CoverPage");
        store.CoverImageUrl = coverUrl;
    }

    await _context.SaveChangesAsync();

    _logger.LogInformation(
        "Store created: {StoreName}, OwnerId: {OwnerId}",
        store.Name, store.OwnerId);

    return ToDto(store);
}
    }
}
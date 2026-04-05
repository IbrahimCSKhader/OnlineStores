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
                var stores = await GetStoresWithContacts(asNoTracking: true)
                    .ToListAsync();

                return stores
                    .Select(ToDto)
                    .ToList();


        }

        public async Task<StoreDto?> GetStoreByIdAsync(Guid id)
        {
            var store = await _context.Stores
                .Include(s => s.ContactAccounts)
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
                .Include(s => s.ContactAccounts)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Slug == normalizedSlug);

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
                .Include(s => s.ContactAccounts)
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    (_currentUser.IsSuperAdmin ||
                     s.OwnerId == _currentUser.UserId));

            if (store == null)
                return null;

            if (dto.Name != null)
                store.Name = dto.Name.Trim();

            if (dto.Description != null)
                store.Description = NormalizeOptional(dto.Description);

            if (dto.BusinessType != null)
                store.BusinessType = NormalizeOptional(dto.BusinessType);

            if (dto.LogoUrl != null)
                store.LogoUrl = NormalizeOptional(dto.LogoUrl);

            if (dto.CoverImageUrl != null)
                store.CoverImageUrl = NormalizeOptional(dto.CoverImageUrl);

            if (dto.WhatsAppNumber != null)
                store.WhatsAppNumber = NormalizeOptional(dto.WhatsAppNumber);

            if (dto.StoreStory != null)
                store.StoreStory = NormalizeOptional(dto.StoreStory);

            if (dto.ThemeTemplate != null)
                store.ThemeTemplate = string.IsNullOrWhiteSpace(dto.ThemeTemplate)
                    ? "default"
                    : dto.ThemeTemplate.Trim();

            if (dto.IsActive != null)
                store.IsActive = dto.IsActive.Value;

            if (dto.ContactAccounts != null)
                ReplaceContactAccounts(store, dto.ContactAccounts);

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
            WhatsAppNumber = s.WhatsAppNumber,
            StoreStory = s.StoreStory,
            ThemeTemplate = s.ThemeTemplate,
            ContactAccounts = s.ContactAccounts
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.CreatedAt)
                .Select(c => new StoreContactAccountDto
                {
                    Id = c.Id,
                    Platform = c.Platform,
                    Username = c.Username,
                    Label = c.Label,
                    Url = StoreContactPlatforms.BuildUrl(c.Platform, c.Username),
                    SortOrder = c.SortOrder
                })
                .ToList(),
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
            return Path.Combine(
                GetStoreFolderPath(storeId),
                "branding"
            );
        }
        private string GetStoreFolderPath(Guid storeId)
        {
            return Path.Combine(
                _environment.ContentRootPath,
                "uploads",
                "stores",
                storeId.ToString()
            );
        }
        private void CreateStoreFolders(Guid storeId)
        {
            var basePath = GetStoreFolderPath(storeId);

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
                "Store folders created for: {StoreId} at {BasePath}",
                storeId,
                basePath);
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
        Description = NormalizeOptional(dto.Description),
        BusinessType = NormalizeOptional(dto.BusinessType),
        WhatsAppNumber = NormalizeOptional(dto.WhatsAppNumber),
        StoreStory = NormalizeOptional(dto.StoreStory),
        ThemeTemplate = string.IsNullOrWhiteSpace(dto.ThemeTemplate)
            ? "default"
            : dto.ThemeTemplate.Trim(),
        OwnerId = Guid.Parse(userId), // 🔥 هون الفرق
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        ContactAccounts = BuildContactAccounts(dto.ContactAccounts)
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

        private IQueryable<Models.Store> GetStoresWithContacts(bool asNoTracking = false)
        {
            var query = _context.Stores
                .Include(s => s.ContactAccounts)
                .AsQueryable();

            return asNoTracking ? query.AsNoTracking() : query;
        }

        private static List<StoreContactAccount> BuildContactAccounts(
            IEnumerable<StoreContactAccountInputDto>? contactAccounts)
        {
            if (contactAccounts == null)
                return new List<StoreContactAccount>();

            return contactAccounts
                .Where(c => !string.IsNullOrWhiteSpace(c.Platform) && !string.IsNullOrWhiteSpace(c.Username))
                .Select(c => new StoreContactAccount
                {
                    Platform = StoreContactPlatforms.NormalizePlatform(c.Platform),
                    Username = StoreContactPlatforms.NormalizeUsername(c.Platform, c.Username),
                    Label = NormalizeOptional(c.Label),
                    SortOrder = c.SortOrder
                })
                .ToList();
        }

        private static void ReplaceContactAccounts(
            Models.Store store,
            IEnumerable<StoreContactAccountInputDto> contactAccounts)
        {
            foreach (var existingContact in store.ContactAccounts.Where(c => !c.IsDeleted))
                existingContact.IsDeleted = true;

            foreach (var contactAccount in BuildContactAccounts(contactAccounts))
                store.ContactAccounts.Add(contactAccount);
        }

        private static string? NormalizeOptional(string? value)
        {
            if (value == null)
                return null;

            var trimmedValue = value.Trim();
            return string.IsNullOrWhiteSpace(trimmedValue) ? null : trimmedValue;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Store;
using onlineStore.Models;
using onlineStore.Models.Subscriptions;
using onlineStore.Security;
using onlineStore.Services.Subscription;

namespace onlineStore.Services.Store
{
    public class StoreService : IStoreService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StoreService> _logger;
        private readonly ICurrentUserService _currentUser;
        private readonly IStoreAuthorizationService _storeAuthorizationService;
        private readonly IStoreAccountBoundaryService _storeAccountBoundaryService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IWebHostEnvironment _environment;

        public StoreService(
            AppDbContext context,
            ILogger<StoreService> logger,
            ICurrentUserService currentUser,
            IStoreAuthorizationService storeAuthorizationService,
            IStoreAccountBoundaryService storeAccountBoundaryService,
            ISubscriptionService subscriptionService,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _currentUser = currentUser;
            _storeAuthorizationService = storeAuthorizationService;
            _storeAccountBoundaryService = storeAccountBoundaryService;
            _subscriptionService = subscriptionService;
            _environment = environment;
        }

        public async Task<List<StoreDto>> GetAllStoresAsync()
        {
            var query = GetStoresWithContacts(asNoTracking: true);

            if (_currentUser.IsSuperAdmin)
            {
                // Super admin can view all stores.
            }
            else if (_currentUser.IsStoreOwner && _currentUser.UserId.HasValue)
            {
                var currentOwnerId = _currentUser.UserId.Value;
                query = query.Where(s => s.IsActive || s.OwnerId == currentOwnerId);
            }
            else
            {
                query = query.Where(s => s.IsActive);
            }

            var stores = await query.ToListAsync();

            return stores
                .Select(ToDto)
                .ToList();


        }

        public async Task<StoreDto?> GetStoreByIdAsync(Guid id)
        {
            IQueryable<Models.Store> query = GetStoresWithContacts(asNoTracking: true)
                .Where(s => s.Id == id);

            if (_currentUser.IsSuperAdmin)
            {
                // Super admin can view any store.
            }
            else if (_currentUser.IsStoreOwner && _currentUser.UserId.HasValue)
            {
                var currentOwnerId = _currentUser.UserId.Value;
                query = query.Where(s => s.IsActive || s.OwnerId == currentOwnerId);
            }
            else
            {
                query = query.Where(s => s.IsActive);
            }

            var store = await query.FirstOrDefaultAsync();

            return store == null ? null : ToDto(store);
        }

        public async Task<StoreDto?> GetStoreBySlugAsync(string slug)
        {
            var normalizedSlug = slug.Trim().ToLower();

            IQueryable<Models.Store> query = GetStoresWithContacts(asNoTracking: true)
                .Where(s => s.Slug == normalizedSlug);

            if (_currentUser.IsSuperAdmin)
            {
                // Super admin can view any store.
            }
            else if (_currentUser.IsStoreOwner && _currentUser.UserId.HasValue)
            {
                var currentOwnerId = _currentUser.UserId.Value;
                query = query.Where(s => s.IsActive || s.OwnerId == currentOwnerId);
            }
            else
            {
                query = query.Where(s => s.IsActive);
            }

            var store = await query.FirstOrDefaultAsync();

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
                .FirstOrDefaultAsync(s => s.Id == id);

            if (store == null)
                return null;

            await EnsureCanManageStoreAsync(id);

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
                store.ThemeTemplate = StoreThemeTemplates.NormalizeOrThrow(
                    dto.ThemeTemplate,
                    nameof(dto.ThemeTemplate));

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
                .FirstOrDefaultAsync(s => s.Id == id);

            if (store == null)
                return false;

            await EnsureCanManageStoreAsync(id);

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
            ThemeTemplate = StoreThemeTemplates.NormalizeOrDefault(s.ThemeTemplate),
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
            {
                _logger.LogWarning(
                    "SaveBrandingImageAsync rejected empty file. StoreId: {StoreId}, FileBaseName: {FileBaseName}",
                    storeId,
                    fileBaseName);
                throw new Exception("Invalid image file");
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
            {
                _logger.LogWarning(
                    "SaveBrandingImageAsync rejected file extension. StoreId: {StoreId}, FileBaseName: {FileBaseName}, FileName: {FileName}, Extension: {Extension}",
                    storeId,
                    fileBaseName,
                    file.FileName,
                    extension);
                throw new Exception("Only .jpg, .jpeg, .png, .webp files are allowed");
            }

            const long maxFileSize = 5 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                _logger.LogWarning(
                    "SaveBrandingImageAsync rejected oversized file. StoreId: {StoreId}, FileBaseName: {FileBaseName}, FileName: {FileName}, FileSize: {FileSize}",
                    storeId,
                    fileBaseName,
                    file.FileName,
                    file.Length);
                throw new Exception("Image size must not exceed 5 MB");
            }

            var brandingPath = GetBrandingFolderPath(storeId);

            _logger.LogInformation(
                "SaveBrandingImageAsync started. StoreId: {StoreId}, FileBaseName: {FileBaseName}, FileName: {FileName}, FileSize: {FileSize}, BrandingPath: {BrandingPath}",
                storeId,
                fileBaseName,
                file.FileName,
                file.Length,
                brandingPath);

            if (!Directory.Exists(brandingPath))
                Directory.CreateDirectory(brandingPath);

            DeleteExistingBrandingFileIfExists(brandingPath, fileBaseName);

            var fileName = $"{fileBaseName}{extension}";
            var fullPath = Path.Combine(brandingPath, fileName);

            using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            _logger.LogInformation(
                "SaveBrandingImageAsync completed. StoreId: {StoreId}, FileBaseName: {FileBaseName}, SavedPath: {SavedPath}",
                storeId,
                fileBaseName,
                fullPath);

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
            var requestedContactCount = dto.ContactAccounts?.Count ?? 0;

            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["RequestedOwnerId"] = dto.OwnerId,
                ["AuthenticatedUserId"] = userId,
                ["RequestedSlug"] = dto.Slug,
                ["RequestedContactAccountsCount"] = requestedContactCount
            });

            _logger.LogInformation(
                "StoreService.CreateStoreAsync started. Name: {StoreName}, RequestedOwnerId: {RequestedOwnerId}, HasLogo: {HasLogo}, HasCoverPage: {HasCoverPage}, ThemeTemplate: {ThemeTemplate}",
                dto.Name,
                dto.OwnerId,
                dto.Logo != null,
                dto.CoverPage != null,
                dto.ThemeTemplate);

            if (!Guid.TryParse(userId, out var authenticatedUserId))
            {
                _logger.LogError(
                    "StoreService.CreateStoreAsync received invalid authenticated user id. UserId: {UserId}",
                    userId);
                throw new ArgumentException("Authenticated user id is invalid.");
            }

            var owner = await _storeAccountBoundaryService.GetActiveStoreOwnerAsync(dto.OwnerId);
            if (owner == null)
            {
                _logger.LogWarning(
                    "StoreService.CreateStoreAsync rejected invalid owner. RequestedOwnerId: {RequestedOwnerId}, AuthenticatedUserId: {AuthenticatedUserId}",
                    dto.OwnerId,
                    authenticatedUserId);
                throw new ArgumentException("The provided OwnerId does not belong to an active StoreOwner account.");
            }

            var normalizedSlug = dto.Slug.Trim().ToLower();

            _logger.LogInformation(
                "StoreService.CreateStoreAsync normalized identifiers. NormalizedSlug: {NormalizedSlug}, AuthenticatedUserId: {AuthenticatedUserId}, OwnerId: {OwnerId}",
                normalizedSlug,
                authenticatedUserId,
                owner.Id);

            var slugExists = await _context.Stores
                .AsNoTracking()
                .AnyAsync(s => s.Slug == normalizedSlug);

            if (slugExists)
            {
                _logger.LogWarning(
                    "StoreService.CreateStoreAsync found duplicate slug. NormalizedSlug: {NormalizedSlug}",
                    normalizedSlug);
                throw new ArgumentException("sorry this link is used already");
            }

            List<StoreContactAccount> normalizedContactAccounts;
            try
            {
                normalizedContactAccounts = BuildContactAccounts(dto.ContactAccounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "StoreService.CreateStoreAsync failed while preparing contact accounts. RequestedContacts: {RequestedContacts}",
                    DescribeRequestedContactAccounts(dto.ContactAccounts));
                throw;
            }

            var store = new Models.Store
            {
                Name = dto.Name.Trim(),
                Slug = normalizedSlug,
                Description = NormalizeOptional(dto.Description),
                BusinessType = NormalizeOptional(dto.BusinessType),
                WhatsAppNumber = NormalizeOptional(dto.WhatsAppNumber),
                StoreStory = NormalizeOptional(dto.StoreStory),
                ThemeTemplate = StoreThemeTemplates.NormalizeOrThrow(
                    dto.ThemeTemplate,
                    nameof(dto.ThemeTemplate)),
                OwnerId = owner.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ContactAccounts = normalizedContactAccounts
            };

            _logger.LogInformation(
                "StoreService.CreateStoreAsync prepared store entity. StoreName: {StoreName}, Slug: {Slug}, NormalizedContactAccountsCount: {NormalizedContactAccountsCount}, ContactAccounts: {ContactAccounts}",
                store.Name,
                store.Slug,
                normalizedContactAccounts.Count,
                DescribeNormalizedContactAccounts(normalizedContactAccounts));

            _context.Stores.Add(store);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "StoreService.CreateStoreAsync failed during initial store save. StoreName: {StoreName}, Slug: {Slug}, ContactAccounts: {ContactAccounts}",
                    store.Name,
                    store.Slug,
                    DescribeNormalizedContactAccounts(normalizedContactAccounts));
                throw;
            }

            _logger.LogInformation(
                "StoreService.CreateStoreAsync initial save completed. StoreId: {StoreId}, ContactAccountsCount: {ContactAccountsCount}",
                store.Id,
                normalizedContactAccounts.Count);

            try
            {
                CreateStoreFolders(store.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "StoreService.CreateStoreAsync failed while creating store folders. StoreId: {StoreId}",
                    store.Id);
                throw;
            }

            if (dto.Logo != null)
            {
                _logger.LogInformation(
                    "StoreService.CreateStoreAsync saving logo. StoreId: {StoreId}, FileName: {FileName}, FileSize: {FileSize}",
                    store.Id,
                    dto.Logo.FileName,
                    dto.Logo.Length);

                var logoUrl = await SaveBrandingImageAsync(dto.Logo, store.Id, "Logo");
                store.LogoUrl = logoUrl;
            }

            if (dto.CoverPage != null)
            {
                _logger.LogInformation(
                    "StoreService.CreateStoreAsync saving cover page. StoreId: {StoreId}, FileName: {FileName}, FileSize: {FileSize}",
                    store.Id,
                    dto.CoverPage.FileName,
                    dto.CoverPage.Length);

                var coverUrl = await SaveBrandingImageAsync(dto.CoverPage, store.Id, "CoverPage");
                store.CoverImageUrl = coverUrl;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "StoreService.CreateStoreAsync failed during branding save. StoreId: {StoreId}, LogoUrl: {LogoUrl}, CoverImageUrl: {CoverImageUrl}",
                    store.Id,
                    store.LogoUrl,
                    store.CoverImageUrl);
                throw;
            }

            _logger.LogInformation(
                "StoreService.CreateStoreAsync branding save completed. StoreId: {StoreId}, LogoUrl: {LogoUrl}, CoverImageUrl: {CoverImageUrl}",
                store.Id,
                store.LogoUrl,
                store.CoverImageUrl);

            var plans = await _subscriptionService.GetAllPlansAsync();
            var defaultPlan = plans.FirstOrDefault(p => string.Equals(
                p.Code,
                SubscriptionPlanCodes.DefaultStorePlan,
                StringComparison.OrdinalIgnoreCase));

            _logger.LogInformation(
                "StoreService.CreateStoreAsync loaded subscription plans. StoreId: {StoreId}, PlansCount: {PlansCount}, DefaultPlanFound: {DefaultPlanFound}",
                store.Id,
                plans.Count,
                defaultPlan != null);

            if (defaultPlan == null)
            {
                _logger.LogError(
                    "StoreService.CreateStoreAsync could not find default subscription plan. StoreId: {StoreId}, ExpectedPlanCode: {ExpectedPlanCode}",
                    store.Id,
                    SubscriptionPlanCodes.DefaultStorePlan);
                throw new InvalidOperationException($"{SubscriptionPlanCodes.DefaultStorePlan} plan is not configured");
            }

            try
            {
                await _subscriptionService.AssignPlanToStoreAsync(store.Id, defaultPlan.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "StoreService.CreateStoreAsync failed while assigning default plan. StoreId: {StoreId}, PlanId: {PlanId}, PlanCode: {PlanCode}",
                    store.Id,
                    defaultPlan.Id,
                    defaultPlan.Code);
                throw;
            }

            _logger.LogInformation(
                "StoreService.CreateStoreAsync completed successfully. StoreId: {StoreId}, StoreName: {StoreName}, OwnerId: {OwnerId}, ContactAccountsCount: {ContactAccountsCount}, DefaultPlanId: {DefaultPlanId}",
                store.Id,
                store.Name,
                store.OwnerId,
                normalizedContactAccounts.Count,
                defaultPlan.Id);

            return ToDto(store);
        }

        private IQueryable<Models.Store> GetStoresWithContacts(bool asNoTracking = false)
        {
            var query = _context.Stores
                .Include(s => s.ContactAccounts)
                .AsQueryable();

            return asNoTracking ? query.AsNoTracking() : query;
        }

        private List<StoreContactAccount> BuildContactAccounts(
            IEnumerable<StoreContactAccountInputDto>? contactAccounts)
        {
            if (contactAccounts == null)
            {
                _logger.LogInformation(
                    "StoreService.BuildContactAccounts skipped because request contains no contact accounts.");
                return new List<StoreContactAccount>();
            }

            var requestedAccounts = contactAccounts.ToList();
            var normalizedAccounts = new List<StoreContactAccount>();

            _logger.LogInformation(
                "StoreService.BuildContactAccounts started. RequestedCount: {RequestedCount}",
                requestedAccounts.Count);

            for (var index = 0; index < requestedAccounts.Count; index++)
            {
                var account = requestedAccounts[index];

                if (string.IsNullOrWhiteSpace(account.Platform) || string.IsNullOrWhiteSpace(account.Username))
                {
                    _logger.LogWarning(
                        "StoreService.BuildContactAccounts skipped invalid raw account. Index: {Index}, Platform: {Platform}, Username: {Username}, SortOrder: {SortOrder}",
                        index,
                        account.Platform,
                        account.Username,
                        account.SortOrder);
                    continue;
                }

                try
                {
                    var normalizedPlatform = StoreContactPlatforms.NormalizePlatform(account.Platform);
                    var normalizedUsername = StoreContactPlatforms.NormalizeUsername(normalizedPlatform, account.Username);
                    var normalizedLabel = NormalizeOptional(account.Label);

                    normalizedAccounts.Add(new StoreContactAccount
                    {
                        Platform = normalizedPlatform,
                        Username = normalizedUsername,
                        Label = normalizedLabel,
                        SortOrder = account.SortOrder
                    });

                    _logger.LogInformation(
                        "StoreService.BuildContactAccounts normalized account. Index: {Index}, Platform: {Platform}, Username: {Username}, SortOrder: {SortOrder}, HasLabel: {HasLabel}",
                        index,
                        normalizedPlatform,
                        normalizedUsername,
                        account.SortOrder,
                        normalizedLabel != null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "StoreService.BuildContactAccounts failed to normalize account. Index: {Index}, Platform: {Platform}, Username: {Username}, Label: {Label}, SortOrder: {SortOrder}",
                        index,
                        account.Platform,
                        account.Username,
                        account.Label,
                        account.SortOrder);
                    throw;
                }
            }

            _logger.LogInformation(
                "StoreService.BuildContactAccounts completed. AcceptedCount: {AcceptedCount}, SkippedCount: {SkippedCount}, Accounts: {Accounts}",
                normalizedAccounts.Count,
                requestedAccounts.Count - normalizedAccounts.Count,
                DescribeNormalizedContactAccounts(normalizedAccounts));

            return normalizedAccounts;
        }

        private void ReplaceContactAccounts(
            Models.Store store,
            IEnumerable<StoreContactAccountInputDto> contactAccounts)
        {
            var activeContacts = store.ContactAccounts
                .Where(c => !c.IsDeleted)
                .ToList();

            _logger.LogInformation(
                "StoreService.ReplaceContactAccounts started. StoreId: {StoreId}, ExistingActiveCount: {ExistingActiveCount}",
                store.Id,
                activeContacts.Count);

            foreach (var existingContact in activeContacts)
                existingContact.IsDeleted = true;

            foreach (var contactAccount in BuildContactAccounts(contactAccounts))
                store.ContactAccounts.Add(contactAccount);

            _logger.LogInformation(
                "StoreService.ReplaceContactAccounts completed. StoreId: {StoreId}, NewActiveCount: {NewActiveCount}",
                store.Id,
                store.ContactAccounts.Count(c => !c.IsDeleted));
        }

        private static string? NormalizeOptional(string? value)
        {
            if (value == null)
                return null;

            var trimmedValue = value.Trim();
            return string.IsNullOrWhiteSpace(trimmedValue) ? null : trimmedValue;
        }

        private static string DescribeRequestedContactAccounts(
            IEnumerable<StoreContactAccountInputDto>? contactAccounts)
        {
            if (contactAccounts == null)
                return "[]";

            var accounts = contactAccounts.ToList();
            if (accounts.Count == 0)
                return "[]";

            return string.Join(
                " | ",
                accounts.Select((account, index) =>
                    $"#{index}:Platform={account.Platform ?? "<null>"},Username={account.Username ?? "<null>"},SortOrder={account.SortOrder},Label={account.Label ?? "<null>"}"));
        }

        private static string DescribeNormalizedContactAccounts(
            IEnumerable<StoreContactAccount> contactAccounts)
        {
            var accounts = contactAccounts.ToList();
            if (accounts.Count == 0)
                return "[]";

            return string.Join(
                " | ",
                accounts.Select((account, index) =>
                    $"#{index}:Platform={account.Platform},Username={account.Username},SortOrder={account.SortOrder},Label={account.Label ?? "<null>"}"));
        }

        private async Task EnsureCanManageStoreAsync(Guid storeId, CancellationToken cancellationToken = default)
        {
            if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
                throw new UnauthorizedAccessException("غير مصرح لك بإدارة هذا المتجر");

            var canManageStore = await _storeAuthorizationService.CanManageStoreAsync(
                _currentUser.UserId.Value,
                storeId,
                cancellationToken);

            if (!canManageStore)
            {
                _logger.LogWarning(
                    "Unauthorized store management attempt. UserId: {UserId}, StoreId: {StoreId}",
                    _currentUser.UserId,
                    storeId);

                throw new UnauthorizedAccessException("غير مصرح لك بإدارة هذا المتجر");
            }
        }
    }
}

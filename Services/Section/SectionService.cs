using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Section;
using onlineStore.Security;
using onlineStore.Services.Store;

namespace onlineStore.Services.Section
{
    public class SectionService : ISectionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SectionService> _logger;
        private readonly ICurrentUserService _currentUser;
        private readonly IStoreAuthorizationService _storeAuthorizationService;
        private readonly IStorefrontTranslationService _translationService;

        public SectionService(
            AppDbContext context,
            ILogger<SectionService> logger,
            ICurrentUserService currentUser,
            IStoreAuthorizationService storeAuthorizationService,
            IStorefrontTranslationService translationService)
        {
            _context = context;
            _logger = logger;
            _currentUser = currentUser;
            _storeAuthorizationService = storeAuthorizationService;
            _translationService = translationService;
        }

        public async Task<List<SectionDto>> GetStoreSectionsAsync(
            Guid storeId,
            string? language = null,
            CancellationToken cancellationToken = default)
        {
            var sections = await _context.Sections
                .AsNoTracking()
                .Where(s => s.StoreId == storeId)
                .OrderBy(s => s.DisplayOrder)
                .ThenBy(s => s.Name)
                .Select(s => ToDto(s))
                .ToListAsync(cancellationToken);

            await _translationService.ApplySectionsAsync(storeId, sections, language, cancellationToken);
            return sections;
        }

        public async Task<SectionDto?> GetSectionByIdAsync(
            Guid id,
            string? language = null,
            CancellationToken cancellationToken = default)
        {
            var section = await _context.Sections
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            if (section == null)
                return null;

            var dto = ToDto(section);
            await _translationService.ApplySectionsAsync(section.StoreId, [dto], language, cancellationToken);
            return dto;
        }

        public async Task<SectionDto> CreateSectionAsync(
            CreateSectionDto dto,
            CancellationToken cancellationToken = default)
        {
            await EnsureCanManageStoreAsync(dto.StoreId, cancellationToken);

            var storeExists = await _context.Stores
                .AsNoTracking()
                .AnyAsync(s => s.Id == dto.StoreId, cancellationToken);

            if (!storeExists)
                throw new InvalidOperationException("Store not found.");

            var name = dto.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Section name is required.");

            var slug = dto.Slug?.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(slug))
                throw new InvalidOperationException("Section slug is required.");

            var slugExists = await _context.Sections
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(s => s.Slug == slug && s.StoreId == dto.StoreId, cancellationToken);

            if (slugExists)
                throw new InvalidOperationException("This slug is already used in this store.");

            var section = new Models.Section
            {
                Name = name,
                Slug = slug,
                Description = dto.Description?.Trim(),
                DisplayOrder = dto.DisplayOrder,
                StoreId = dto.StoreId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Sections.Add(section);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Section created: {SectionName}", section.Name);

            return ToDto(section);
        }

        public async Task<SectionDto?> UpdateSectionAsync(
            Guid id,
            UpdateSectionDto dto,
            CancellationToken cancellationToken = default)
        {
            var section = await _context.Sections
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            if (section == null)
                return null;

            await EnsureCanManageStoreAsync(section.StoreId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(dto.Name))
                section.Name = dto.Name.Trim();

            if (dto.Description != null)
                section.Description = dto.Description.Trim();

            if (dto.DisplayOrder.HasValue)
                section.DisplayOrder = dto.DisplayOrder.Value;

            if (dto.IsActive.HasValue)
                section.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Section updated: {SectionId}", id);

            return ToDto(section);
        }

        public async Task<bool> DeleteSectionAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var section = await _context.Sections
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            if (section == null)
                return false;

            await EnsureCanManageStoreAsync(section.StoreId, cancellationToken);

            section.IsDeleted = true;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Section soft deleted: {SectionId}", id);

            return true;
        }

        private static SectionDto ToDto(Models.Section s) => new()
        {
            Id = s.Id,
            Name = s.Name,
            Slug = s.Slug,
            Description = s.Description,
            DisplayOrder = s.DisplayOrder,
            IsActive = s.IsActive,
            StoreId = s.StoreId,
            CreatedAt = s.CreatedAt
        };

        private async Task EnsureCanManageStoreAsync(Guid storeId, CancellationToken cancellationToken)
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
                    "Unauthorized section management attempt. UserId: {UserId}, StoreId: {StoreId}",
                    _currentUser.UserId,
                    storeId);

                throw new UnauthorizedAccessException("غير مصرح لك بإدارة هذا المورد");
            }
        }
    }
}

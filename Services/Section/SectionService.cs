using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Section;

namespace onlineStore.Services.Section
{
    public class SectionService : ISectionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SectionService> _logger;

        public SectionService(
            AppDbContext context,
            ILogger<SectionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<SectionDto>> GetStoreSectionsAsync(
            Guid storeId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Sections
                .AsNoTracking()
                .Where(s => s.StoreId == storeId)
                .OrderBy(s => s.DisplayOrder)
                .ThenBy(s => s.Name)
                .Select(s => ToDto(s))
                .ToListAsync(cancellationToken);
        }

        public async Task<SectionDto?> GetSectionByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var section = await _context.Sections
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            return section == null ? null : ToDto(section);
        }

        public async Task<SectionDto> CreateSectionAsync(
            CreateSectionDto dto,
            CancellationToken cancellationToken = default)
        {
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
    }
}
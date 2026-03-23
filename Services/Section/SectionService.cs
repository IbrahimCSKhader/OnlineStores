// Services/Section/SectionService.cs
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


        public async Task<List<SectionDto>> GetStoreSectionsAsync(Guid storeId)
        {
            return await _context.Sections
                .AsNoTracking()
                .Where(s => s.StoreId == storeId)
                .OrderBy(s => s.DisplayOrder)
                .Select(s => ToDto(s))
                .ToListAsync();
        }


        public async Task<SectionDto?> GetSectionByIdAsync(Guid id)
        {
            var section = await _context.Sections
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            return section == null ? null : ToDto(section);
        }



        public async Task<SectionDto> CreateSectionAsync(CreateSectionDto dto)
        {
            var slugExists = await _context.Sections
                .AnyAsync(s => s.Slug == dto.Slug.ToLower()
                            && s.StoreId == dto.StoreId);

            if (slugExists)
                throw new Exception("this link already used");

            var section = new Models.Section
            {
                Name = dto.Name.Trim(),
                Slug = dto.Slug.Trim().ToLower(),
                Description = dto.Description,
                ImageUrl = dto.ImageUrl,
                DisplayOrder = dto.DisplayOrder,
                StoreId = dto.StoreId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Sections.Add(section);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Section created: {SectionName}", section.Name);

            return ToDto(section);
        }


        public async Task<SectionDto?> UpdateSectionAsync(
            Guid id, UpdateSectionDto dto)
        {
            var section = await _context.Sections
                .FirstOrDefaultAsync(s => s.Id == id);

            if (section == null) return null;

            if (dto.Name != null) section.Name = dto.Name.Trim();
            if (dto.Description != null) section.Description = dto.Description;
            if (dto.ImageUrl != null) section.ImageUrl = dto.ImageUrl;
            if (dto.DisplayOrder != null) section.DisplayOrder = dto.DisplayOrder.Value;
            if (dto.IsActive != null) section.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Section updated: {SectionId}", id);

            return ToDto(section);
        }



        public async Task<bool> DeleteSectionAsync(Guid id)
        {
            var section = await _context.Sections
                .FirstOrDefaultAsync(s => s.Id == id);

            if (section == null) return false;

            section.IsDeleted = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("(Soft) Section deleted: {SectionId}", id);

            return true;
        }



        private static SectionDto ToDto(Models.Section s) => new()
        {
            Id = s.Id,
            Name = s.Name,
            Slug = s.Slug,
            Description = s.Description,
            ImageUrl = s.ImageUrl,
            DisplayOrder = s.DisplayOrder,
            IsActive = s.IsActive,
            StoreId = s.StoreId,
            CreatedAt = s.CreatedAt
        };
    }
}
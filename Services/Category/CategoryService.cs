// Services/Category/CategoryService.cs
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Category;

namespace onlineStore.Services.Category
{
    public class CategoryService : ICategoryService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(
            AppDbContext context,
            ILogger<CategoryService> logger)
        {
            _context = context;
            _logger = logger;
        }


        public async Task<List<CategoryDto>> GetStoreCategoriesAsync(Guid storeId)
        {
            return await _context.Categories
                .AsNoTracking()
                .Where(c => c.StoreId == storeId)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => ToDto(c))
                .ToListAsync();
        }



        public async Task<CategoryDto?> GetCategoryByIdAsync(Guid id)
        {
            var category = await _context.Categories
                .AsNoTracking()
                .Include(c => c.ParentCategory)
                .FirstOrDefaultAsync(c => c.Id == id);

            return category == null ? null : ToDto(category);
        }


        public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto dto)
        {
            
            var slugExists = await _context.Categories
                .AnyAsync(c => c.Slug == dto.Slug.ToLower()
                            && c.StoreId == dto.StoreId);

            if (slugExists)
                throw new Exception("this link already used");

            var category = new Models.Category
            {
                Name = dto.Name.Trim(),
                Slug = dto.Slug.Trim().ToLower(),
                Description = dto.Description,
                ImageUrl = dto.ImageUrl,
                DisplayOrder = dto.DisplayOrder,
                ParentCategoryId = dto.ParentCategoryId,
                StoreId = dto.StoreId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Category created: {CategoryName}", category.Name);

            return ToDto(category);
        }


   
        public async Task<CategoryDto?> UpdateCategoryAsync(
            Guid id, UpdateCategoryDto dto)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id);
            if (category == null) return null;
            if (dto.Name != null) category.Name = dto.Name.Trim();
            if (dto.Description != null) category.Description = dto.Description;
            if (dto.ImageUrl != null) category.ImageUrl = dto.ImageUrl;
            if (dto.DisplayOrder != null) category.DisplayOrder = dto.DisplayOrder.Value;
            if (dto.IsActive != null) category.IsActive = dto.IsActive.Value;
            if (dto.ParentCategoryId != null) category.ParentCategoryId = dto.ParentCategoryId;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Category updated: {CategoryId}", id);

            return ToDto(category);
        }


        public async Task<bool> DeleteCategoryAsync(Guid id)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null) return false;

            category.IsDeleted = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Category deleted: {CategoryId}", id);

            return true;
        }


        private static CategoryDto ToDto(Models.Category c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug,
            Description = c.Description,
            ImageUrl = c.ImageUrl,
            DisplayOrder = c.DisplayOrder,
            IsActive = c.IsActive,
            ParentCategoryId = c.ParentCategoryId,
            ParentCategoryName = c.ParentCategory?.Name,
            StoreId = c.StoreId,
            CreatedAt = c.CreatedAt
        };
    }
}
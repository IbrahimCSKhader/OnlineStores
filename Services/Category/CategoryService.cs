using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Category;
using onlineStore.Security;

namespace onlineStore.Services.Category
{
    public class CategoryService : ICategoryService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CategoryService> _logger;
        private readonly ICurrentUserService _currentUser;
        private readonly IStoreOwnershipService _storeOwnershipService;

        public CategoryService(
            AppDbContext context,
            ILogger<CategoryService> logger,
            ICurrentUserService currentUser,
            IStoreOwnershipService storeOwnershipService)
        {
            _context = context;
            _logger = logger;
            _currentUser = currentUser;
            _storeOwnershipService = storeOwnershipService;
        }

        public async Task<List<CategoryDto>> GetStoreCategoriesAsync(Guid storeId, CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AsNoTracking()
                .Where(c => c.StoreId == storeId)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => ToDto(c))
                .ToListAsync(cancellationToken);
        }

        public async Task<CategoryDto?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var category = await _context.Categories
                .AsNoTracking()
                .Include(c => c.ParentCategory)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            return category == null ? null : ToDto(category);
        }

        public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto dto, CancellationToken cancellationToken = default)
        {
            await EnsureCanManageStoreAsync(dto.StoreId, cancellationToken);

            var slug = dto.Slug.Trim().ToLower();

            var slugExists = await _context.Categories
                .AsNoTracking()
                .AnyAsync(c => c.StoreId == dto.StoreId && c.Slug == slug, cancellationToken);

            if (slugExists)
                throw new InvalidOperationException("This slug is already used in this store.");

            if (dto.ParentCategoryId.HasValue)
            {
                var validParent = await _context.Categories
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == dto.ParentCategoryId.Value && c.StoreId == dto.StoreId, cancellationToken);

                if (!validParent)
                    throw new InvalidOperationException("Parent category is invalid for this store.");
            }

            var category = new Models.Category
            {
                Name = dto.Name.Trim(),
                Slug = slug,
                Description = dto.Description,
                ImageUrl = dto.ImageUrl,
                DisplayOrder = dto.DisplayOrder,
                ParentCategoryId = dto.ParentCategoryId,
                StoreId = dto.StoreId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Category created: {CategoryId} in Store {StoreId}", category.Id, category.StoreId);

            return ToDto(category);
        }

        public async Task<CategoryDto?> UpdateCategoryAsync(Guid id, UpdateCategoryDto dto, CancellationToken cancellationToken = default)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    (_currentUser.IsSuperAdmin || c.Store.OwnerId == _currentUser.UserId),
                    cancellationToken);

            if (category == null)
                return null;

            if (dto.ParentCategoryId.HasValue)
            {
                var validParent = await _context.Categories
                    .AsNoTracking()
                    .AnyAsync(c =>
                        c.Id == dto.ParentCategoryId.Value &&
                        c.StoreId == category.StoreId &&
                        c.Id != category.Id,
                        cancellationToken);

                if (!validParent)
                    throw new InvalidOperationException("Parent category is invalid for this store.");
            }

            if (dto.Name != null) category.Name = dto.Name.Trim();
            if (dto.Description != null) category.Description = dto.Description;
            if (dto.ImageUrl != null) category.ImageUrl = dto.ImageUrl;
            if (dto.DisplayOrder != null) category.DisplayOrder = dto.DisplayOrder.Value;
            if (dto.IsActive != null) category.IsActive = dto.IsActive.Value;
            if (dto.ParentCategoryId != null) category.ParentCategoryId = dto.ParentCategoryId;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Category updated: {CategoryId}", category.Id);

            return ToDto(category);
        }

        public async Task<bool> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    (_currentUser.IsSuperAdmin || c.Store.OwnerId == _currentUser.UserId),
                    cancellationToken);

            if (category == null)
                return false;

            category.IsDeleted = true;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Category soft deleted: {CategoryId}", category.Id);

            return true;
        }

        private async Task EnsureCanManageStoreAsync(Guid storeId, CancellationToken cancellationToken)
        {
            if (_currentUser.IsSuperAdmin)
                return;

            if (!_currentUser.UserId.HasValue)
                throw new UnauthorizedAccessException("User is not authenticated.");

            var ownsStore = await _storeOwnershipService.UserOwnsStoreAsync(
                storeId,
                _currentUser.UserId.Value,
                cancellationToken);

            if (!ownsStore)
                throw new KeyNotFoundException("Store not found.");
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
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Category;
using onlineStore.Security;
using System.Text.RegularExpressions;

namespace onlineStore.Services.Category
{
    public class CategoryService : ICategoryService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly IStoreOwnershipService _storeOwnershipService;

        public CategoryService(
            AppDbContext context,
            ICurrentUserService currentUser,
            IStoreOwnershipService storeOwnershipService)
        {
            _context = context;
            _currentUser = currentUser;
            _storeOwnershipService = storeOwnershipService;
        }

        public async Task<List<CategoryDto>> GetStoreCategoriesAsync(
            Guid storeId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AsNoTracking()
                .Where(c => c.StoreId == storeId)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .Select(c => ToDto(c))
                .ToListAsync(cancellationToken);
        }

        public async Task<CategoryDto?> GetCategoryByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            return category == null ? null : ToDto(category);
        }

        public async Task<CategoryDto> CreateCategoryAsync(
            CreateCategoryDto dto,
            CancellationToken cancellationToken = default)
        {
            await EnsureCanManageStoreAsync(dto.StoreId, cancellationToken);

            var store = await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == dto.StoreId, cancellationToken);

            if (store == null)
                throw new InvalidOperationException("Store not found.");

            var name = dto.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Category name is required.");

            var finalSlug = $"{NormalizeSlugSegment(store.Slug)}-{NormalizeSlugSegment(dto.Slug)}";

            var slugExists = await _context.Categories
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(c => c.Slug == finalSlug, cancellationToken);

            if (slugExists)
                throw new InvalidOperationException("This slug is already used.");

            var category = new Models.Category
            {
                Name = name,
                Slug = finalSlug,
                Description = dto.Description?.Trim(),
                DisplayOrder = dto.DisplayOrder,
                StoreId = dto.StoreId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync(cancellationToken);

            return ToDto(category);
        }

        public async Task<CategoryDto?> UpdateCategoryAsync(
            Guid id,
            UpdateCategoryDto dto,
            CancellationToken cancellationToken = default)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (category == null)
                return null;

            await EnsureCanManageStoreAsync(category.StoreId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(dto.Name))
                category.Name = dto.Name.Trim();

            if (dto.Description != null)
                category.Description = dto.Description.Trim();

            if (dto.DisplayOrder.HasValue)
                category.DisplayOrder = dto.DisplayOrder.Value;

            if (dto.IsActive.HasValue)
                category.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync(cancellationToken);

            return ToDto(category);
        }

        public async Task<bool> DeleteCategoryAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (category == null)
                return false;

            await EnsureCanManageStoreAsync(category.StoreId, cancellationToken);

            category.IsDeleted = true;
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }

        private async Task EnsureCanManageStoreAsync(
            Guid storeId,
            CancellationToken cancellationToken)
        {
                return;

        

            var ownsStore = await _storeOwnershipService.UserOwnsStoreAsync(
                storeId,
                _currentUser.UserId.Value,
                cancellationToken);

            if (!ownsStore)
                throw new KeyNotFoundException("Store not found.");
        }

        private static string NormalizeSlugSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("Slug is required.");

            var normalized = value.Trim().ToLowerInvariant();
            normalized = Regex.Replace(normalized, @"\s+", "-");
            normalized = Regex.Replace(normalized, @"-+", "-");
            normalized = normalized.Trim('-');

            if (string.IsNullOrWhiteSpace(normalized))
                throw new InvalidOperationException("Slug is invalid.");

            return normalized;
        }

        private static CategoryDto ToDto(Models.Category c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug,
            Description = c.Description,
            DisplayOrder = c.DisplayOrder,
            IsActive = c.IsActive,
            StoreId = c.StoreId,
            CreatedAt = c.CreatedAt
        };
    }
}
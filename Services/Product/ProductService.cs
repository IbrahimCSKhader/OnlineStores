using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Common;
using onlineStore.DTOs.Product;
using onlineStore.Models;
using onlineStore.Models.Enums;
using onlineStore.Security;
using onlineStore.Services.Subscription;
using System.Text.RegularExpressions;

namespace onlineStore.Services.Product
{
    public class ProductService : IProductService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProductService> _logger;
        private readonly ICurrentUserService _currentUser;
        private readonly IStoreAuthorizationService _storeAuthorizationService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IWebHostEnvironment _environment;

        public ProductService(
            AppDbContext context,
            ILogger<ProductService> logger,
            ICurrentUserService currentUser,
            IStoreAuthorizationService storeAuthorizationService,
            ISubscriptionService subscriptionService,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _currentUser = currentUser;
            _storeAuthorizationService = storeAuthorizationService;
            _subscriptionService = subscriptionService;
            _environment = environment;
        }

        // ════════════════════════════════════════════════════
        // Get Store Products
        // ════════════════════════════════════════════════════
        public async Task<List<ProductDto>> GetStoreProductsAsync(
    Guid storeId,
    Guid? userId = null)
        {
            var discountPercentage = await GetCustomerDiscountPercentageAsync(storeId, userId);

            IQueryable<Models.Product> query = IncludeProductReadGraph(
                _context.Products
                    .AsNoTracking()
                    .Where(p => p.StoreId == storeId &&
                                p.Status == ProductStatus.Active));

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return products.Select(p => ToDto(p, discountPercentage)).ToList();
        }

        public async Task<PagedResultDto<ProductDto>> GetStoreProductsPageAsync(
            Guid storeId,
            ProductQueryDto query,
            Guid? userId = null)
        {
            var discountPercentage = await GetCustomerDiscountPercentageAsync(storeId, userId);
            var productQuery = ApplyPublicProductFilters(
                _context.Products
                    .AsNoTracking()
                    .Where(p => p.StoreId == storeId && p.Status == ProductStatus.Active),
                query);

            return await BuildPagedProductsAsync(productQuery, query, discountPercentage);
        }

        public async Task<List<ProductDto>> GetStoreProductsForManagementAsync(Guid storeId)
        {
            await EnsureCanManageStoreAsync(storeId);

            var products = await IncludeProductReadGraph(
                _context.Products
                    .AsNoTracking()
                    .Where(p => p.StoreId == storeId))
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return products.Select(p => ToDto(p, 0m, includeManagementFields: true)).ToList();
        }

        // ════════════════════════════════════════════════════
        // Get Featured Products
        // ════════════════════════════════════════════════════
        public async Task<List<ProductDto>> GetFeaturedProductsAsync(
            Guid storeId,
            Guid? userId = null)
        {
            var discountPercentage = await GetCustomerDiscountPercentageAsync(storeId, userId);

            var products = await IncludeProductReadGraph(
                _context.Products
                    .AsNoTracking()
                    .Where(p => p.StoreId == storeId &&
                                p.IsFeatured))
                .ToListAsync();

            products = products
                .Where(p => p.Status == ProductStatus.Active)
                .ToList();

            return products.Select(p => ToDto(p, discountPercentage)).ToList();
        }

        // ════════════════════════════════════════════════════
        // Get Products By Category
        // ════════════════════════════════════════════════════
        public async Task<List<ProductDto>> GetProductsByCategoryAsync(
            Guid categoryId,
            Guid? userId = null)
        {
            var products = await IncludeProductReadGraph(
                _context.Products
                    .AsNoTracking()
                    .Where(p => p.CategoryId == categoryId))
                .ToListAsync();

            var activeProducts = products
                .Where(p => p.Status == ProductStatus.Active)
                .ToList();

            if (!activeProducts.Any())
                return new List<ProductDto>();

            var storeId = activeProducts.First().StoreId;
            var discountPercentage = await GetCustomerDiscountPercentageAsync(storeId, userId);

            return activeProducts.Select(p => ToDto(p, discountPercentage)).ToList();
        }

        // ════════════════════════════════════════════════════
        // Get Products By Section
        // ════════════════════════════════════════════════════
        public async Task<PagedResultDto<ProductDto>> GetProductsByCategoryPageAsync(
            Guid categoryId,
            ProductQueryDto query,
            Guid? userId = null)
        {
            var productQuery = ApplyPublicProductFilters(
                _context.Products
                    .AsNoTracking()
                    .Where(p => p.CategoryId == categoryId && p.Status == ProductStatus.Active),
                query);
            var storeId = await productQuery
                .Select(p => (Guid?)p.StoreId)
                .FirstOrDefaultAsync();
            var discountPercentage = storeId.HasValue
                ? await GetCustomerDiscountPercentageAsync(storeId.Value, userId)
                : 0m;

            return await BuildPagedProductsAsync(productQuery, query, discountPercentage);
        }

        public async Task<List<ProductDto>> GetProductsBySectionAsync(
            Guid sectionId,
            Guid? userId = null)
        {
            var products = await IncludeProductReadGraph(
                _context.Products
                    .AsNoTracking()
                    .Where(p => p.SectionId == sectionId))
                .ToListAsync();

            var activeProducts = products
                .Where(p => p.Status == ProductStatus.Active)
                .ToList();

            if (!activeProducts.Any())
                return new List<ProductDto>();

            var storeId = activeProducts.First().StoreId;
            var discountPercentage = await GetCustomerDiscountPercentageAsync(storeId, userId);

            return activeProducts.Select(p => ToDto(p, discountPercentage)).ToList();
        }

        // ════════════════════════════════════════════════════
        // Get Product By Id
        // ════════════════════════════════════════════════════
        public async Task<PagedResultDto<ProductDto>> GetProductsBySectionPageAsync(
            Guid sectionId,
            ProductQueryDto query,
            Guid? userId = null)
        {
            var productQuery = ApplyPublicProductFilters(
                _context.Products
                    .AsNoTracking()
                    .Where(p => p.SectionId == sectionId && p.Status == ProductStatus.Active),
                query);
            var storeId = await productQuery
                .Select(p => (Guid?)p.StoreId)
                .FirstOrDefaultAsync();
            var discountPercentage = storeId.HasValue
                ? await GetCustomerDiscountPercentageAsync(storeId.Value, userId)
                : 0m;

            return await BuildPagedProductsAsync(productQuery, query, discountPercentage);
        }

        public async Task<ProductDto?> GetProductByIdAsync(
            Guid id,
            Guid? userId = null)
        {
            var product = await IncludeProductReadGraph(
                _context.Products
                    .AsNoTracking())
                .FirstOrDefaultAsync(p => p.Id == id &&
                                          p.Status == ProductStatus.Active);

            if (product == null)
                return null;

            var discountPercentage = await GetCustomerDiscountPercentageAsync(product.StoreId, userId);

            return ToDto(product, discountPercentage);
        }

        // ════════════════════════════════════════════════════
        // Get Product By Slug
        // ════════════════════════════════════════════════════
        public async Task<ProductDto?> GetProductBySlugAsync(
            string slug,
            Guid? userId = null)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return null;

            var normalizedSlug = slug.Trim().ToLowerInvariant();

            var product = await IncludeProductReadGraph(
                _context.Products
                    .AsNoTracking())
                .FirstOrDefaultAsync(p => p.Slug == normalizedSlug &&
                                          p.Status == ProductStatus.Active);

            if (product == null)
                return null;

            var discountPercentage = await GetCustomerDiscountPercentageAsync(product.StoreId, userId);

            return ToDto(product, discountPercentage);
        }

        // ════════════════════════════════════════════════════
        // Create Product
        // ════════════════════════════════════════════════════
        public async Task<ProductDto> CreateProductAsync(CreateProductDto dto)
        {
            await EnsureCanManageStoreAsync(dto.StoreId);

            var canCreateProduct = await _subscriptionService.CanStoreCreateProductAsync(dto.StoreId);
            if (!canCreateProduct)
            {
                throw new Exception("لقد وصلت للحد الأقصى من المنتجات");
            }

            var activeSubscription = await _subscriptionService.GetActiveSubscriptionAsync(dto.StoreId);
            var maxImagesPerProduct = activeSubscription?.MaxImagesPerProduct;
            if (maxImagesPerProduct.HasValue &&
                dto.Images != null &&
                dto.Images.Count > maxImagesPerProduct.Value)
            {
                throw new Exception("وصلت الحد الأقصى للصور");
            }

            var store = await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == dto.StoreId);

            if (store == null)
                throw new Exception("Store not found");

            await EnsureCategoryAndSectionBelongToStoreAsync(
                dto.StoreId,
                dto.CategoryId,
                dto.SectionId);

            var name = dto.Name.Trim();
            var productSlug = NormalizeSlugSegment(dto.Slug);
            var storeSlug = NormalizeSlugSegment(store.Slug);
            var finalSlug = $"{storeSlug}-{productSlug}";

            var slugExists = await _context.Products
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(p => p.Slug == finalSlug);

            if (slugExists)
                throw new Exception("This slug is already used");

            if (!string.IsNullOrWhiteSpace(dto.SKU))
            {
                var skuExists = await _context.Products
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .AnyAsync(p => p.StoreId == dto.StoreId && p.SKU == dto.SKU.Trim());

                if (skuExists)
                    throw new Exception("This SKU is already used in this store");
            }

            var product = new Models.Product
            {
                Name = name,
                Slug = finalSlug,
                SKU = dto.SKU?.Trim(),
                Description = dto.Description?.Trim(),
                ShortDescription = dto.ShortDescription?.Trim(),
                Price = dto.Price,
                CompareAtPrice = dto.CompareAtPrice,
                CostPrice = dto.CostPrice,
                WholesalePrice = dto.WholesalePrice,
                StockQuantity = dto.StockQuantity,
                TrackInventory = dto.TrackInventory,
                ThumbnailUrl = dto.ThumbnailUrl?.Trim(),
                MetaTitle = dto.MetaTitle?.Trim(),
                MetaDescription = dto.MetaDescription?.Trim(),
                Status = ProductStatus.Active,
                StoreId = dto.StoreId,
                CategoryId = dto.CategoryId,
                SectionId = dto.SectionId,
                CreatedAt = DateTime.UtcNow,
                Images = new List<ProductImage>(),
                Variants = new List<ProductVariant>(),
                AttributeValues = new List<ProductAttributeValue>()
            };

            if (dto.Variants != null && dto.Variants.Any())
            {
                var sortOrder = 0;
                foreach (var variantDto in dto.Variants)
                {
                    product.Variants.Add(new ProductVariant
                    {
                        Name = variantDto.Name.Trim(),
                        SKU = null,
                        Description = string.IsNullOrWhiteSpace(variantDto.Description)
                            ? null
                            : variantDto.Description.Trim(),
                        Price = variantDto.Price,
                        CompareAtPrice = variantDto.CompareAtPrice,
                        StockQuantity = variantDto.StockQuantity,
                        ImageUrl = variantDto.ImageUrl?.Trim(),
                        IsDefault = sortOrder == 0,
                        IsActive = true,
                        SortOrder = variantDto.SortOrder ?? sortOrder,
                        ProductId = product.Id,
                        CreatedAt = DateTime.UtcNow
                    });

                    sortOrder++;
                }
            }
            else
            {
                product.Variants.Add(CreateDefaultVariant(product));
            }

            if (dto.AttributeValues != null && dto.AttributeValues.Any())
            {
                foreach (var attributeValueDto in dto.AttributeValues)
                {
                    product.AttributeValues.Add(new ProductAttributeValue
                    {
                        AttributeId = attributeValueDto.AttributeId,
                        Value = attributeValueDto.Value.Trim(),
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            CreateProductFolder(dto.StoreId, product.Id);
            CreateVariantFolders(dto.StoreId, product.Id, product.Variants);

            if (dto.Images != null && dto.Images.Any())
            {
                for (int i = 0; i < dto.Images.Count; i++)
                {
                    var imageUrl = await SaveProductImageAsync(
                        dto.StoreId,
                        product.Id,
                        dto.Images[i],
                        i + 1,
                        variantId: null);

                    var productImage = new ProductImage
                    {
                        ProductId = product.Id,
                        Url = imageUrl,
                        AltText = product.Name,
                        DisplayOrder = i + 1,
                        IsPrimary = i == 0,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.ProductImages.Add(productImage);

                    if (i == 0)
                        product.ThumbnailUrl = imageUrl;
                }

                await _context.SaveChangesAsync();
            }

            var createdProduct = await IncludeProductReadGraph(
                _context.Products
                    .AsNoTracking())
                .FirstAsync(p => p.Id == product.Id);

            _logger.LogInformation("Product created: {ProductName}", product.Name);

            return ToDto(createdProduct, 0m, includeManagementFields: true);
        }

        // ════════════════════════════════════════════════════
        // Update Product
        // ════════════════════════════════════════════════════
        public async Task<ProductDto?> UpdateProductAsync(Guid id, UpdateProductDto dto)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.Category)
                .Include(p => p.Section)
                .Include(p => p.AttributeValues)
                    .ThenInclude(av => av.Attribute)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return null;

            await EnsureCanManageStoreAsync(product.StoreId);

            // Variant edits are handled through the dedicated variant endpoint.

            if (dto.Name != null)
                product.Name = dto.Name.Trim();

            if (dto.Description != null)
                product.Description = dto.Description.Trim();

            if (dto.ShortDescription != null)
                product.ShortDescription = dto.ShortDescription.Trim();

            if (dto.Price != null)
                product.Price = dto.Price.Value;

            if (dto.CompareAtPrice != null)
                product.CompareAtPrice = dto.CompareAtPrice;

            if (dto.CostPrice != null)
                product.CostPrice = dto.CostPrice;

            if (dto.WholesalePrice != null)
                product.WholesalePrice = dto.WholesalePrice.Value;

            if (dto.StockQuantity != null)
                product.StockQuantity = dto.StockQuantity.Value;

            if (dto.TrackInventory != null)
                product.TrackInventory = dto.TrackInventory.Value;

            if (dto.ThumbnailUrl != null)
                product.ThumbnailUrl = dto.ThumbnailUrl.Trim();

            if (dto.Status != null)
                product.Status = dto.Status.Value;

            if (dto.IsFeatured != null)
                product.IsFeatured = dto.IsFeatured.Value;

            if (dto.MetaTitle != null)
                product.MetaTitle = dto.MetaTitle.Trim();

            if (dto.MetaDescription != null)
                product.MetaDescription = dto.MetaDescription.Trim();

            if (dto.CategoryId != null)
            {
                var categoryInStore = await _context.Categories
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == dto.CategoryId.Value && c.StoreId == product.StoreId);

                if (!categoryInStore)
                    throw new InvalidOperationException("Category does not belong to this store.");

                product.CategoryId = dto.CategoryId.Value;
            }

            if (dto.SectionId != null)
            {
                var sectionInStore = await _context.Sections
                    .AsNoTracking()
                    .AnyAsync(s => s.Id == dto.SectionId.Value && s.StoreId == product.StoreId);

                if (!sectionInStore)
                    throw new InvalidOperationException("Section does not belong to this store.");

                product.SectionId = dto.SectionId.Value;
            }

            await _context.SaveChangesAsync();

            if (dto.Images != null && dto.Images.Any())
            {
                var nextDisplayOrder = product.Images.Any()
                    ? product.Images.Max(i => i.DisplayOrder) + 1
                    : 1;

                foreach (var file in dto.Images)
                {
                    var imageUrl = await SaveProductImageAsync(
                        product.StoreId,
                        product.Id,
                        file,
                        nextDisplayOrder);

                    var image = new ProductImage
                    {
                        ProductId = product.Id,
                        Url = imageUrl,
                        AltText = product.Name,
                        DisplayOrder = nextDisplayOrder,
                        IsPrimary = !product.Images.Any() && nextDisplayOrder == 1,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.ProductImages.Add(image);

                    if (string.IsNullOrWhiteSpace(product.ThumbnailUrl))
                        product.ThumbnailUrl = imageUrl;

                    nextDisplayOrder++;
                }

                await _context.SaveChangesAsync();
            }

            var updatedProduct = await IncludeProductReadGraph(
                _context.Products
                    .AsNoTracking())
                .FirstAsync(p => p.Id == id);

            _logger.LogInformation("Product updated: {ProductId}", id);

            return ToDto(updatedProduct, 0m, includeManagementFields: true);
        }

        // ════════════════════════════════════════════════════
        // Delete Product — Soft Delete
        // ════════════════════════════════════════════════════
        public async Task<bool> DeleteProductAsync(Guid id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return false;

            await EnsureCanManageStoreAsync(product.StoreId);

            product.IsDeleted = true;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Product soft deleted: {ProductId}", id);

            return true;
        }

        // ════════════════════════════════════════════════════
        // Add Image
        // ════════════════════════════════════════════════════
        public async Task<ProductImageDto> AddImageAsync(AddProductImageDto dto)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == dto.ProductId);

            if (product == null)
                throw new Exception("Product not found");

            if (product.IsDeleted)
                throw new Exception("You cannot add images to a deleted product");

            await EnsureCanManageStoreAsync(product.StoreId);

            ProductVariant? variant = null;
            if (dto.VariantId.HasValue)
            {
                variant = await _context.ProductVariants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v =>
                        v.Id == dto.VariantId.Value &&
                        v.ProductId == dto.ProductId);

                if (variant == null)
                    throw new InvalidOperationException("Variant does not belong to this product.");
            }

            var canAddImage = await _subscriptionService.CanStoreAddImageAsync(dto.ProductId);
            if (!canAddImage)
            {
                throw new Exception("وصلت الحد الأقصى للصور");
            }

            if (dto.Image == null || dto.Image.Length == 0)
                throw new Exception("Invalid image");

            var scopedImages = product.Images
                .Where(i => i.VariantId == dto.VariantId)
                .ToList();

            if (dto.IsPrimary)
            {
                foreach (var existingImage in scopedImages)
                    existingImage.IsPrimary = false;
            }

            var displayOrder = dto.DisplayOrder > 0
                ? dto.DisplayOrder
                : (scopedImages.Any() ? scopedImages.Max(i => i.DisplayOrder) + 1 : 1);

            var imageUrl = await SaveProductImageAsync(
                product.StoreId,
                product.Id,
                dto.Image,
                displayOrder,
                dto.VariantId);

            var image = new ProductImage
            {
                Url = imageUrl,
                AltText = dto.AltText?.Trim(),
                DisplayOrder = displayOrder,
                IsPrimary = dto.IsPrimary || !scopedImages.Any(),
                ProductId = dto.ProductId,
                VariantId = dto.VariantId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProductImages.Add(image);

            if (image.IsPrimary && !dto.VariantId.HasValue)
                product.ThumbnailUrl = image.Url;

            await _context.SaveChangesAsync();

            return ToImageDto(image);
        }

        // ════════════════════════════════════════════════════
        // Delete Image
        // ════════════════════════════════════════════════════
        public async Task<bool> DeleteImageAsync(Guid imageId)
        {
            var image = await _context.ProductImages
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.Id == imageId);

            if (image == null)
                return false;

            if (image.Product == null)
                throw new InvalidOperationException("Image product does not exist.");

            await EnsureCanManageStoreAsync(image.Product.StoreId);

            var product = image.Product;
            var wasPrimary = image.IsPrimary;
            var wasVariantImage = image.VariantId.HasValue;

            DeletePhysicalImage(image.Url);

            _context.ProductImages.Remove(image);
            await _context.SaveChangesAsync();

            var remainingImages = await _context.ProductImages
                .Where(i => i.ProductId == product.Id &&
                            i.VariantId == image.VariantId)
                .OrderBy(i => i.DisplayOrder)
                .ToListAsync();

            if (wasPrimary)
            {
                foreach (var img in remainingImages)
                    img.IsPrimary = false;

                var newPrimary = remainingImages.FirstOrDefault();

                if (newPrimary != null)
                {
                    newPrimary.IsPrimary = true;
                    if (!wasVariantImage)
                        product.ThumbnailUrl = newPrimary.Url;
                }
                else if (!wasVariantImage)
                {
                    product.ThumbnailUrl = null;
                }

                await _context.SaveChangesAsync();
            }

            return true;
        }

        // ════════════════════════════════════════════════════
        // Add Variant
        // ════════════════════════════════════════════════════
        public async Task<ProductVariantDto> AddVariantAsync(
            Guid productId,
            CreateProductVariantDto dto)
        {
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
                throw new Exception("Product not found");

            await EnsureCanManageStoreAsync(product.StoreId);

            var nextSortOrder = await _context.ProductVariants
                .AsNoTracking()
                .Where(v => v.ProductId == productId && !v.IsDeleted)
                .Select(v => (int?)v.SortOrder)
                .MaxAsync() ?? -1;

            var variant = new ProductVariant
            {
                Name = dto.Name.Trim(),
                SKU = null,
                Description = string.IsNullOrWhiteSpace(dto.Description)
                    ? null
                    : dto.Description.Trim(),
                Price = dto.Price,
                CompareAtPrice = dto.CompareAtPrice,
                StockQuantity = dto.StockQuantity,
                ImageUrl = dto.ImageUrl?.Trim(),
                IsActive = true,
                SortOrder = dto.SortOrder ?? nextSortOrder + 1,
                ProductId = productId,
                CreatedAt = DateTime.UtcNow
            };

            if (dto.AttributeValueIds?.Any() == true)
            {
                var duplicateAttributeValueId = dto.AttributeValueIds
                    .GroupBy(id => id)
                    .FirstOrDefault(group => group.Count() > 1)
                    ?.Key;

                if (duplicateAttributeValueId.HasValue)
                    throw new InvalidOperationException("Duplicate attribute values are not allowed for the same variant.");

                var attributeValues = await _context.ProductAttributeValues
                    .Include(av => av.Attribute)
                    .Where(av => dto.AttributeValueIds.Contains(av.Id))
                    .ToListAsync();

                if (attributeValues.Count != dto.AttributeValueIds.Count)
                    throw new InvalidOperationException("One or more attribute values were not found.");

                if (attributeValues.Any(av => av.ProductId != productId))
                    throw new InvalidOperationException("One or more attribute values do not belong to this product.");

                foreach (var attributeValue in attributeValues)
                {
                    variant.AttributeValues.Add(new ProductVariantAttributeValue
                    {
                        Variant = variant,
                        AttributeValueId = attributeValue.Id,
                        AttributeValue = attributeValue,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            _context.ProductVariants.Add(variant);
            await _context.SaveChangesAsync();
            CreateVariantFolder(product.StoreId, product.Id, variant.Id);

            return ToVariantDto(variant, product);
        }

        // ════════════════════════════════════════════════════
        // Delete Variant
        // ════════════════════════════════════════════════════
        // Update Variant
        public async Task<ProductVariantDto?> UpdateVariantAsync(
            Guid variantId,
            UpdateProductVariantDto dto)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                    .ThenInclude(p => p.Images)
                .Include(v => v.Images)
                .Include(v => v.AttributeValues)
                    .ThenInclude(vav => vav.AttributeValue)
                        .ThenInclude(av => av.Attribute)
                .FirstOrDefaultAsync(v => v.Id == variantId);

            if (variant == null || variant.IsDeleted)
                return null;

            await EnsureCanManageStoreAsync(variant.Product.StoreId);

            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new InvalidOperationException("Variant name is required.");

            if (dto.IsDefault == true && dto.IsActive == false)
                throw new InvalidOperationException("Default variant must remain active.");

            var normalizedSku = dto.SKU?.Trim();
            await EnsureVariantSkuIsUniqueForStoreAsync(
                variant.Product.StoreId,
                normalizedSku,
                variant.Id);

            if (dto.IsActive.HasValue && !dto.IsActive.Value && variant.IsActive)
                await EnsureVariantCanBeDisabledAsync(variant);

            if (dto.IsDefault == true)
            {
                var defaultSiblings = await _context.ProductVariants
                    .Where(v => v.ProductId == variant.ProductId &&
                                v.Id != variant.Id &&
                                !v.IsDeleted &&
                                v.IsDefault)
                    .ToListAsync();

                foreach (var sibling in defaultSiblings)
                {
                    sibling.IsDefault = false;
                    sibling.UpdatedAt = DateTime.UtcNow;
                }

                variant.IsDefault = true;
                variant.IsActive = true;
            }
            else if (dto.IsDefault == false && variant.IsDefault)
            {
                var replacementDefault = await _context.ProductVariants
                    .Where(v => v.ProductId == variant.ProductId &&
                                v.Id != variant.Id &&
                                !v.IsDeleted &&
                                v.IsActive)
                    .OrderBy(v => v.SortOrder)
                    .ThenBy(v => v.CreatedAt)
                    .FirstOrDefaultAsync();

                if (replacementDefault == null)
                    throw new InvalidOperationException("Cannot remove the default variant without another active default replacement.");

                replacementDefault.IsDefault = true;
                replacementDefault.UpdatedAt = DateTime.UtcNow;
                variant.IsDefault = false;
            }

            variant.Name = dto.Name.Trim();
            variant.SKU = string.IsNullOrWhiteSpace(normalizedSku) ? null : normalizedSku;
            variant.Description = string.IsNullOrWhiteSpace(dto.Description)
                ? null
                : dto.Description.Trim();
            variant.Price = dto.Price;
            variant.CompareAtPrice = dto.CompareAtPrice;
            if (dto.StockQuantity.HasValue)
                variant.StockQuantity = dto.StockQuantity.Value;
            variant.ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl)
                ? null
                : dto.ImageUrl.Trim();

            if (dto.SortOrder.HasValue)
                variant.SortOrder = dto.SortOrder.Value;

            if (dto.IsActive.HasValue)
                variant.IsActive = dto.IsActive.Value;

            if (dto.AttributeValueIds != null)
                await SyncVariantAttributeValuesAsync(
                    variant.Id,
                    variant.ProductId,
                    dto.AttributeValueIds);

            variant.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var updatedVariant = await _context.ProductVariants
                .AsNoTracking()
                .Include(v => v.Product)
                    .ThenInclude(p => p.Images)
                .Include(v => v.Images)
                .Include(v => v.AttributeValues)
                    .ThenInclude(vav => vav.AttributeValue)
                        .ThenInclude(av => av.Attribute)
                .FirstAsync(v => v.Id == variant.Id);

            return ToVariantDto(updatedVariant, updatedVariant.Product);
        }

        public async Task<bool> DeleteVariantAsync(Guid variantId)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == variantId);

            if (variant == null)
                return false;

            await EnsureCanManageStoreAsync(variant.Product.StoreId);

            if (variant.IsDeleted)
                return false;

            var activeVariants = await _context.ProductVariants
                .Where(v => v.ProductId == variant.ProductId &&
                            !v.IsDeleted &&
                            v.IsActive)
                .OrderBy(v => v.SortOrder)
                .ThenBy(v => v.CreatedAt)
                .ToListAsync();

            if (variant.IsActive && activeVariants.Count <= 1)
                throw new InvalidOperationException("Cannot delete the last active variant for this product.");

            if (variant.IsDefault)
            {
                var replacementDefault = activeVariants
                    .FirstOrDefault(v => v.Id != variant.Id);

                if (replacementDefault == null)
                    throw new InvalidOperationException("Cannot delete the default variant without another active default replacement.");

                replacementDefault.IsDefault = true;
                replacementDefault.UpdatedAt = DateTime.UtcNow;
            }

            variant.IsDefault = false;
            variant.IsActive = false;
            variant.IsDeleted = true;
            variant.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }

        // ════════════════════════════════════════════════════
        // Product Visits
        // ════════════════════════════════════════════════════
        public async Task<int?> IncrementProductVisitAsync(Guid productId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
                return null;

            product.VisitCount += 1;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Product visit incremented: {ProductId}, Count: {VisitCount}",
                productId, product.VisitCount);

            return product.VisitCount;
        }

        public async Task<int?> GetProductVisitCountAsync(Guid productId)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => new { p.Id, p.StoreId, p.VisitCount })
                .FirstOrDefaultAsync();

            if (product == null)
                return null;

            return product.VisitCount;
        }

        // ════════════════════════════════════════════════════
        // Paths Helpers
        // ════════════════════════════════════════════════════
        private async Task EnsureVariantSkusAreUniqueForStoreAsync(
            Guid storeId,
            IEnumerable<CreateProductVariantDto> variants)
        {
            var normalizedSkus = variants
                .Select(v => v.SKU?.Trim())
                .Where(sku => !string.IsNullOrWhiteSpace(sku))
                .Cast<string>()
                .ToList();

            var duplicateSkuInRequest = normalizedSkus
                .GroupBy(sku => sku, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1)
                ?.Key;

            if (!string.IsNullOrWhiteSpace(duplicateSkuInRequest))
                throw new InvalidOperationException("This variant SKU is already used in this request.");

            foreach (var sku in normalizedSkus)
                await EnsureVariantSkuIsUniqueForStoreAsync(storeId, sku, excludeVariantId: null);
        }

        private async Task EnsureVariantSkuIsUniqueForStoreAsync(
            Guid storeId,
            string? sku,
            Guid? excludeVariantId)
        {
            var normalizedSku = sku?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedSku))
                return;

            var skuExists = await _context.ProductVariants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(v =>
                    v.SKU == normalizedSku &&
                    !v.IsDeleted &&
                    !v.Product.IsDeleted &&
                    v.Product.StoreId == storeId &&
                    (!excludeVariantId.HasValue || v.Id != excludeVariantId.Value));

            if (skuExists)
                throw new InvalidOperationException("This variant SKU is already used in this store.");
        }

        private async Task EnsureVariantCanBeDisabledAsync(ProductVariant variant)
        {
            var activeVariants = await _context.ProductVariants
                .Where(v => v.ProductId == variant.ProductId &&
                            !v.IsDeleted &&
                            v.IsActive)
                .OrderBy(v => v.SortOrder)
                .ThenBy(v => v.CreatedAt)
                .ToListAsync();

            if (activeVariants.Count <= 1)
                throw new InvalidOperationException("Cannot disable the last active variant for this product.");

            if (!variant.IsDefault)
                return;

            var replacementDefault = activeVariants
                .FirstOrDefault(v => v.Id != variant.Id);

            if (replacementDefault == null)
                throw new InvalidOperationException("Cannot disable the default variant without another active default replacement.");

            replacementDefault.IsDefault = true;
            replacementDefault.UpdatedAt = DateTime.UtcNow;
            variant.IsDefault = false;
        }

        private async Task<List<ProductAttributeValue>> LoadValidatedAttributeValuesAsync(
            Guid productId,
            IEnumerable<Guid> attributeValueIds)
        {
            var ids = attributeValueIds.ToList();
            var duplicateAttributeValueId = ids
                .GroupBy(id => id)
                .FirstOrDefault(group => group.Count() > 1)
                ?.Key;

            if (duplicateAttributeValueId.HasValue)
                throw new InvalidOperationException("Duplicate attribute values are not allowed for the same variant.");

            if (!ids.Any())
                return new List<ProductAttributeValue>();

            var attributeValues = await _context.ProductAttributeValues
                .Include(av => av.Attribute)
                .Where(av => ids.Contains(av.Id))
                .ToListAsync();

            if (attributeValues.Count != ids.Count)
                throw new InvalidOperationException("One or more attribute values were not found.");

            if (attributeValues.Any(av => av.ProductId != productId))
                throw new InvalidOperationException("One or more attribute values do not belong to this product.");

            return attributeValues;
        }

        private async Task SyncVariantAttributeValuesAsync(
            Guid variantId,
            Guid productId,
            IEnumerable<Guid> attributeValueIds)
        {
            var attributeValues = await LoadValidatedAttributeValuesAsync(
                productId,
                attributeValueIds);
            var desiredIds = attributeValues
                .Select(av => av.Id)
                .ToHashSet();

            var existingMappings = await _context.ProductVariantAttributeValues
                .IgnoreQueryFilters()
                .Where(vav => vav.VariantId == variantId)
                .ToListAsync();

            foreach (var mapping in existingMappings)
            {
                if (desiredIds.Contains(mapping.AttributeValueId))
                {
                    mapping.IsDeleted = false;
                    mapping.UpdatedAt = DateTime.UtcNow;
                    desiredIds.Remove(mapping.AttributeValueId);
                }
                else if (!mapping.IsDeleted)
                {
                    mapping.IsDeleted = true;
                    mapping.UpdatedAt = DateTime.UtcNow;
                }
            }

            foreach (var attributeValueId in desiredIds)
            {
                _context.ProductVariantAttributeValues.Add(new ProductVariantAttributeValue
                {
                    VariantId = variantId,
                    AttributeValueId = attributeValueId,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        private string GetStoreFolderPath(Guid storeId)
        {
            var rootPath = _environment.ContentRootPath;

            _logger.LogInformation("RORO Root path for uploads: {RootPath}", rootPath);

            return Path.Combine(
                rootPath,
                "uploads",
                "stores",
                storeId.ToString()
            );
        }

        private string GetProductsFolderPath(Guid storeId)
        {
            return Path.Combine(
                GetStoreFolderPath(storeId),
                "products"
            );
        }

        private string GetProductFolderPath(Guid storeId, Guid productId)
        {
            return Path.Combine(
                GetProductsFolderPath(storeId),
                productId.ToString()
            );
        }

        private void CreateProductFolder(Guid storeId, Guid productId)
        {
            var productsFolder = GetProductsFolderPath(storeId);
            var productFolder = GetProductFolderPath(storeId, productId);
            var variantsFolder = GetProductVariantsFolderPath(storeId, productId);

            if (!Directory.Exists(productsFolder))
                Directory.CreateDirectory(productsFolder);

            if (!Directory.Exists(productFolder))
                Directory.CreateDirectory(productFolder);

            if (!Directory.Exists(variantsFolder))
                Directory.CreateDirectory(variantsFolder);
        }

        private string GetProductVariantsFolderPath(Guid storeId, Guid productId)
        {
            return Path.Combine(
                GetProductFolderPath(storeId, productId),
                "variants"
            );
        }

        private string GetVariantFolderPath(Guid storeId, Guid productId, Guid variantId)
        {
            return Path.Combine(
                GetProductVariantsFolderPath(storeId, productId),
                variantId.ToString()
            );
        }

        private void CreateVariantFolder(Guid storeId, Guid productId, Guid variantId)
        {
            var variantFolder = GetVariantFolderPath(storeId, productId, variantId);

            if (!Directory.Exists(variantFolder))
                Directory.CreateDirectory(variantFolder);
        }

        private void CreateVariantFolders(
            Guid storeId,
            Guid productId,
            IEnumerable<ProductVariant> variants)
        {
            foreach (var variant in variants)
                CreateVariantFolder(storeId, productId, variant.Id);
        }

        private static ProductVariant CreateDefaultVariant(Models.Product product)
        {
            return new ProductVariant
            {
                Name = "Default",
                SKU = null,
                Price = null,
                CompareAtPrice = null,
                StockQuantity = product.StockQuantity,
                ImageUrl = product.ThumbnailUrl,
                IsDefault = true,
                IsActive = true,
                SortOrder = 0,
                ProductId = product.Id,
                CreatedAt = DateTime.UtcNow
            };
        }

        private async Task<string> SaveProductImageAsync(
            Guid storeId,
            Guid productId,
            IFormFile file,
            int imageIndex,
            Guid? variantId = null)
        {
            if (file == null || file.Length == 0)
                throw new Exception("الصورة غير صالحة");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
                throw new Exception("Only .jpg, .jpeg, .png, .webp files are allowed");

            const long maxFileSize = 5 * 1024 * 1024;
            if (file.Length > maxFileSize)
                throw new Exception("Image size must not exceed 5 MB");

            var uploadFolder = variantId.HasValue
                ? GetVariantFolderPath(storeId, productId, variantId.Value)
                : GetProductFolderPath(storeId, productId);

            if (!Directory.Exists(uploadFolder))
                Directory.CreateDirectory(uploadFolder);

            var fileName = $"{imageIndex}{extension}";
            var filePath = Path.Combine(uploadFolder, fileName);

            if (File.Exists(filePath))
                File.Delete(filePath);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            if (variantId.HasValue)
                return $"/uploads/stores/{storeId}/products/{productId}/variants/{variantId.Value}/{fileName}";

            return $"/uploads/stores/{storeId}/products/{productId}/{fileName}";
        }

        private void DeletePhysicalImage(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return;

            var rootPath = _environment.ContentRootPath;

            var relativePath = imageUrl
                .TrimStart('/')
                .Replace("/", Path.DirectorySeparatorChar.ToString());

            var fullPath = Path.Combine(rootPath, relativePath);

            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }

        // ════════════════════════════════════════════════════
        // Security Helpers
        // ════════════════════════════════════════════════════
        private async Task EnsureCanManageStoreAsync(Guid storeId, CancellationToken cancellationToken = default)
        {
            if (!_currentUser.UserId.HasValue)
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

                throw new UnauthorizedAccessException("غير مصرح لك بإدارة هذا المورد");
            }
        }

        private async Task<bool> CanCurrentUserManageStoreAsync(Guid storeId, CancellationToken cancellationToken = default)
        {
            if (!_currentUser.UserId.HasValue)
                return false;

            var canManageStore = await _storeAuthorizationService.CanManageStoreAsync(
                _currentUser.UserId.Value,
                storeId,
                cancellationToken);

            return canManageStore;
        }

        private async Task EnsureCategoryAndSectionBelongToStoreAsync(
            Guid storeId,
            Guid categoryId,
            Guid sectionId,
            CancellationToken cancellationToken = default)
        {
            var categoryInStore = await _context.Categories
                .AsNoTracking()
                .AnyAsync(c => c.Id == categoryId && c.StoreId == storeId, cancellationToken);

            if (!categoryInStore)
                throw new InvalidOperationException("Category does not belong to this store.");

            var sectionInStore = await _context.Sections
                .AsNoTracking()
                .AnyAsync(s => s.Id == sectionId && s.StoreId == storeId, cancellationToken);

            if (!sectionInStore)
                throw new InvalidOperationException("Section does not belong to this store.");
        }

        // ════════════════════════════════════════════════════
        // Other Helpers
        // ════════════════════════════════════════════════════
        private static string NormalizeSlugSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("Slug is required.");

            var normalized = value.Trim().ToLowerInvariant();
            normalized = Regex.Replace(normalized, @"\s+", "-");
            normalized = Regex.Replace(normalized, @"-+", "-");

            var cleaned = normalized.Trim('-');

            if (string.IsNullOrWhiteSpace(cleaned))
                throw new InvalidOperationException("Slug is invalid.");

            return cleaned;
        }

        private async Task<decimal> GetCustomerDiscountPercentageAsync(
            Guid storeId,
            Guid? storeCustomerId)
        {
            if (!storeCustomerId.HasValue)
                return 0m;

            var discount = await _context.CustomerStores
                .AsNoTracking()
                .Where(x => x.StoreId == storeId &&
                            x.Id == storeCustomerId.Value &&
                            x.IsActive)
                .Select(x => x.DiscountPercentage)
                .FirstOrDefaultAsync();

            return discount;
        }

        private static decimal ResolvePriceBeforeStoreCustomerDiscount(Models.Product product)
        {
            if (product.CompareAtPrice.HasValue &&
                product.CompareAtPrice.Value > product.Price)
            {
                return product.CompareAtPrice.Value;
            }

            return product.Price;
        }

        private static IQueryable<Models.Product> ApplyPublicProductFilters(
            IQueryable<Models.Product> query,
            ProductQueryDto? filters)
        {
            if (filters == null)
                return query;

            var keyword = filters.Search?.Trim();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var pattern = $"%{keyword}%";
                query = query.Where(p =>
                    EF.Functions.Like(p.Name, pattern) ||
                    (p.Description != null && EF.Functions.Like(p.Description, pattern)) ||
                    (p.ShortDescription != null && EF.Functions.Like(p.ShortDescription, pattern)) ||
                    (p.SKU != null && EF.Functions.Like(p.SKU, pattern)));
            }

            if (filters.OnlyInStock == true)
            {
                query = query.Where(p =>
                    !p.TrackInventory ||
                    p.StockQuantity > 0 ||
                    p.Variants.Any(v => !v.IsDeleted && v.IsActive && v.StockQuantity > 0));
            }

            if (filters.MinPrice.HasValue)
                query = query.Where(p => p.Price >= filters.MinPrice.Value);

            if (filters.MaxPrice.HasValue)
                query = query.Where(p => p.Price <= filters.MaxPrice.Value);

            return query;
        }

        private static IQueryable<Models.Product> ApplyProductSorting(
            IQueryable<Models.Product> query,
            string? sort)
        {
            return (sort ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "price-asc" => query.OrderBy(p => p.Price).ThenByDescending(p => p.CreatedAt),
                "price-desc" => query.OrderByDescending(p => p.Price).ThenByDescending(p => p.CreatedAt),
                "alphabetical" => query.OrderBy(p => p.Name).ThenByDescending(p => p.CreatedAt),
                "popular" => query.OrderByDescending(p => p.VisitCount).ThenByDescending(p => p.CreatedAt),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };
        }

        private async Task<PagedResultDto<ProductDto>> BuildPagedProductsAsync(
            IQueryable<Models.Product> query,
            ProductQueryDto filters,
            decimal discountPercentage)
        {
            var page = filters.NormalizedPage;
            var pageSize = filters.NormalizedPageSize;
            var totalCount = await query.CountAsync();
            var products = await IncludeProductReadGraph(
                    ApplyProductSorting(query, filters.Sort)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize))
                .AsSplitQuery()
                .ToListAsync();
            var items = products
                .Select(p => ToDto(p, discountPercentage))
                .ToList();

            return PagedResultDto<ProductDto>.Create(items, page, pageSize, totalCount);
        }

        private static IQueryable<Models.Product> IncludeProductReadGraph(
            IQueryable<Models.Product> query)
        {
            return query
                .Include(p => p.Category)
                .Include(p => p.Section)
                .Include(p => p.Images)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.Images)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.AttributeValues)
                        .ThenInclude(vav => vav.AttributeValue)
                            .ThenInclude(av => av.Attribute)
                .Include(p => p.AttributeValues)
                    .ThenInclude(av => av.Attribute);
        }

        private static ProductImageDto ToImageDto(ProductImage image)
        {
            return new ProductImageDto
            {
                Id = image.Id,
                Url = image.Url,
                AltText = image.AltText,
                DisplayOrder = image.DisplayOrder,
                IsPrimary = image.IsPrimary,
                VariantId = image.VariantId
            };
        }

        private static ProductVariantDto ToVariantDto(
            ProductVariant variant,
            Models.Product product)
        {
            var variantImageUrl = variant.Images?
                .OrderBy(i => i.DisplayOrder)
                .Select(i => i.Url)
                .FirstOrDefault();
            var productImageUrl = product.Images?
                .Where(i => i.VariantId == null)
                .OrderBy(i => i.DisplayOrder)
                .Select(i => i.Url)
                .FirstOrDefault();

            return new ProductVariantDto
            {
                Id = variant.Id,
                ProductId = variant.ProductId,
                Name = variant.Name,
                SKU = variant.SKU,
                Description = variant.Description,
                Price = variant.Price,
                CompareAtPrice = variant.CompareAtPrice,
                EffectivePrice = variant.Price ?? product.Price,
                EffectiveCompareAtPrice = variant.CompareAtPrice ?? product.CompareAtPrice,
                StockQuantity = variant.StockQuantity,
                ImageUrl = variant.ImageUrl,
                EffectiveImageUrl = variant.ImageUrl
                    ?? variantImageUrl
                    ?? product.ThumbnailUrl
                    ?? productImageUrl,
                IsDefault = variant.IsDefault,
                IsActive = variant.IsActive,
                SortOrder = variant.SortOrder,
                AttributeValues = variant.AttributeValues?
                    .OrderBy(vav => vav.AttributeValue.Attribute != null
                        ? vav.AttributeValue.Attribute.Name
                        : string.Empty)
                    .ThenBy(vav => vav.AttributeValue.Value)
                    .Select(vav => new VariantAttributeValueDto
                    {
                        AttributeValueId = vav.AttributeValueId,
                        AttributeId = vav.AttributeValue.AttributeId,
                        AttributeName = vav.AttributeValue.Attribute?.Name ?? string.Empty,
                        Value = vav.AttributeValue.Value
                    }).ToList() ?? new List<VariantAttributeValueDto>(),
                Images = variant.Images?
                    .OrderBy(i => i.DisplayOrder)
                    .Select(ToImageDto)
                    .ToList() ?? new List<ProductImageDto>()
            };
        }

        private static ProductDto ToDto(
            Models.Product p,
            decimal discountPercentage = 0m,
            bool includeManagementFields = false)
        {
            var isStoreCustomerDiscountApplied = discountPercentage > 0m;
            var priceBeforeStoreCustomerDiscount = isStoreCustomerDiscountApplied
                ? ResolvePriceBeforeStoreCustomerDiscount(p)
                : p.Price;
            var finalPrice = priceBeforeStoreCustomerDiscount;

            if (isStoreCustomerDiscountApplied)
            {
                finalPrice = priceBeforeStoreCustomerDiscount -
                             (priceBeforeStoreCustomerDiscount * discountPercentage / 100m);
            }

            var variants = (p.Variants ?? new List<ProductVariant>())
                .Where(v => !v.IsDeleted && (includeManagementFields || v.IsActive))
                .OrderBy(v => v.SortOrder)
                .ThenByDescending(v => v.IsDefault)
                .ThenBy(v => v.Name)
                .ToList();

            var activeVariants = variants
                .Where(v => v.IsActive)
                .ToList();
            var defaultVariant = activeVariants.FirstOrDefault(v => v.IsDefault)
                ?? activeVariants.FirstOrDefault();
            var hasVariants = activeVariants.Count > 1 ||
                activeVariants.Any(v => !v.IsDefault);
            var effectiveStockQuantity = activeVariants.Any()
                ? activeVariants.Sum(v => v.StockQuantity)
                : p.StockQuantity;

            return new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                SKU = p.SKU,
                Description = p.Description,
                ShortDescription = p.ShortDescription,

                Price = finalPrice,
                OriginalPrice = priceBeforeStoreCustomerDiscount,
                FinalPrice = finalPrice,
                AppliedDiscountPercentage = discountPercentage,
                IsWholesalePriceApplied = isStoreCustomerDiscountApplied,

                CompareAtPrice = isStoreCustomerDiscountApplied ? null : p.CompareAtPrice,
                WholesalePrice = includeManagementFields ? p.WholesalePrice : null,
                StockQuantity = p.StockQuantity,
                TrackInventory = p.TrackInventory,
                ThumbnailUrl = p.ThumbnailUrl,
                Status = p.Status,
                IsFeatured = p.IsFeatured,
                StoreId = p.StoreId,
                CategoryId = p.CategoryId,
                CategoryName = p.Category?.Name,
                SectionId = p.SectionId,
                SectionName = p.Section?.Name,
                VisitCount = p.VisitCount,
                CreatedAt = p.CreatedAt,
                HasVariants = hasVariants,
                DefaultVariantId = defaultVariant?.Id,
                EffectiveStockQuantity = effectiveStockQuantity,

                Images = p.Images?
                    .Where(i => i.VariantId == null)
                    .OrderBy(i => i.DisplayOrder)
                    .Select(ToImageDto)
                    .ToList() ?? new List<ProductImageDto>(),

                Variants = variants
                    .Select(v => ToVariantDto(v, p))
                    .ToList(),

                AttributeValues = p.AttributeValues?
                    .Select(av => new ProductAttributeValueDto
                    {
                        Id = av.Id,
                        AttributeId = av.AttributeId,
                        AttributeName = av.Attribute?.Name ?? "",
                        Value = av.Value
                    }).ToList() ?? new List<ProductAttributeValueDto>()
            };
        }
    }
}

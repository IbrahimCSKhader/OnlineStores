// Services/Product/ProductService.cs
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Product;
using onlineStore.Models;
using onlineStore.Models.Enums;

namespace onlineStore.Services.Product
{
    public class ProductService : IProductService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProductService> _logger;

        public ProductService(
            AppDbContext context,
            ILogger<ProductService> logger)
        {
            _context = context;
            _logger = logger;
        }


        // ════════════════════════════════════════════════════
        // Get Store Products
        // ════════════════════════════════════════════════════
        public async Task<List<ProductDto>> GetStoreProductsAsync(Guid storeId)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.StoreId == storeId
                         && p.Status == ProductStatus.Active)
                .Include(p => p.Category)
                .Include(p => p.Section)
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.AttributeValues)
                    .ThenInclude(av => av.Attribute)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => ToDto(p))
                .ToListAsync();
        }


        // ════════════════════════════════════════════════════
        // Get Featured Products
        // ════════════════════════════════════════════════════
        public async Task<List<ProductDto>> GetFeaturedProductsAsync(Guid storeId)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.StoreId == storeId
                         && p.IsFeatured
                         && p.Status == ProductStatus.Active)
                .Include(p => p.Images)
                .Select(p => ToDto(p))
                .ToListAsync();
        }


        // ════════════════════════════════════════════════════
        // Get Products By Category
        // ════════════════════════════════════════════════════
        public async Task<List<ProductDto>> GetProductsByCategoryAsync(Guid categoryId)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.CategoryId == categoryId
                         && p.Status == ProductStatus.Active)
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Select(p => ToDto(p))
                .ToListAsync();
        }


        // ════════════════════════════════════════════════════
        // Get Products By Section
        // ════════════════════════════════════════════════════
        public async Task<List<ProductDto>> GetProductsBySectionAsync(Guid sectionId)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.SectionId == sectionId
                         && p.Status == ProductStatus.Active)
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Select(p => ToDto(p))
                .ToListAsync();
        }


        // ════════════════════════════════════════════════════
        // Get Product By Id
        // ════════════════════════════════════════════════════
        public async Task<ProductDto?> GetProductByIdAsync(Guid id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Section)
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.AttributeValues)
                    .ThenInclude(av => av.Attribute)
                .FirstOrDefaultAsync(p => p.Id == id);

            return product == null ? null : ToDto(product);
        }


        // ════════════════════════════════════════════════════
        // Get Product By Slug
        // ════════════════════════════════════════════════════
        public async Task<ProductDto?> GetProductBySlugAsync(string slug)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Section)
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.AttributeValues)
                    .ThenInclude(av => av.Attribute)
                .FirstOrDefaultAsync(p => p.Slug == slug.ToLower());

            return product == null ? null : ToDto(product);
        }


        // ════════════════════════════════════════════════════
        // Create Product
        // ════════════════════════════════════════════════════
        public async Task<ProductDto> CreateProductAsync(CreateProductDto dto)
        {
            // 🔐 تحقق إن الـ Slug مش موجود
            var slugExists = await _context.Products
                .AnyAsync(p => p.Slug == dto.Slug.ToLower()
                            && p.StoreId == dto.StoreId);

            if (slugExists)
                throw new Exception("هذا الرابط مستخدم مسبقاً");

            var product = new Models.Product
            {
                Name = dto.Name.Trim(),
                Slug = dto.Slug.Trim().ToLower(),
                SKU = dto.SKU,
                Description = dto.Description,
                ShortDescription = dto.ShortDescription,
                Price = dto.Price,
                CompareAtPrice = dto.CompareAtPrice,
                CostPrice = dto.CostPrice,
                StockQuantity = dto.StockQuantity,
                TrackInventory = dto.TrackInventory,
                ThumbnailUrl = dto.ThumbnailUrl,
                MetaTitle = dto.MetaTitle,
                MetaDescription = dto.MetaDescription,
                Status = ProductStatus.Draft,
                StoreId = dto.StoreId,
                CategoryId = dto.CategoryId,
                SectionId = dto.SectionId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);

            // ── إضافة Variants ──
            if (dto.Variants != null && dto.Variants.Any())
            {
                foreach (var v in dto.Variants)
                {
                    product.Variants.Add(new ProductVariant
                    {
                        Name = v.Name.Trim(),
                        SKU = v.SKU,
                        PriceOverride = v.PriceOverride,
                        StockQuantity = v.StockQuantity,
                        ImageUrl = v.ImageUrl,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            // ── إضافة Attributes ──
            if (dto.AttributeValues != null && dto.AttributeValues.Any())
            {
                foreach (var av in dto.AttributeValues)
                {
                    product.AttributeValues.Add(new ProductAttributeValue
                    {
                        AttributeId = av.AttributeId,
                        Value = av.Value.Trim(),
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            // ── إنشاء مجلد المنتج ──
            CreateProductFolder(dto.StoreId, product.Id);

            _logger.LogInformation(
                "Product created: {ProductName}", product.Name);

            return ToDto(product);
        }


        // ════════════════════════════════════════════════════
        // Update Product
        // ════════════════════════════════════════════════════
        public async Task<ProductDto?> UpdateProductAsync(
            Guid id, UpdateProductDto dto)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return null;

            if (dto.Name != null) product.Name = dto.Name.Trim();
            if (dto.Description != null) product.Description = dto.Description;
            if (dto.ShortDescription != null) product.ShortDescription = dto.ShortDescription;
            if (dto.Price != null) product.Price = dto.Price.Value;
            if (dto.CompareAtPrice != null) product.CompareAtPrice = dto.CompareAtPrice;
            if (dto.CostPrice != null) product.CostPrice = dto.CostPrice;
            if (dto.StockQuantity != null) product.StockQuantity = dto.StockQuantity.Value;
            if (dto.TrackInventory != null) product.TrackInventory = dto.TrackInventory.Value;
            if (dto.ThumbnailUrl != null) product.ThumbnailUrl = dto.ThumbnailUrl;
            if (dto.Status != null) product.Status = dto.Status.Value;
            if (dto.IsFeatured != null) product.IsFeatured = dto.IsFeatured.Value;
            if (dto.MetaTitle != null) product.MetaTitle = dto.MetaTitle;
            if (dto.MetaDescription != null) product.MetaDescription = dto.MetaDescription;
            if (dto.CategoryId != null) product.CategoryId = dto.CategoryId.Value;
            if (dto.SectionId != null) product.SectionId = dto.SectionId.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Product updated: {ProductId}", id);

            return ToDto(product);
        }


        // ════════════════════════════════════════════════════
        // Delete Product — Soft Delete
        // ════════════════════════════════════════════════════
        public async Task<bool> DeleteProductAsync(Guid id)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return false;

            product.IsDeleted = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Product deleted: {ProductId}", id);

            return true;
        }


        // ════════════════════════════════════════════════════
        // Add Image
        // ════════════════════════════════════════════════════
        public async Task<ProductImageDto> AddImageAsync(AddProductImageDto dto)
        {
            // لو IsPrimary = true، نشيل Primary من الصور الثانية
            if (dto.IsPrimary)
            {
                var existingImages = await _context.ProductImages
                    .Where(i => i.ProductId == dto.ProductId)
                    .ToListAsync();

                foreach (var img in existingImages)
                    img.IsPrimary = false;
            }

            var image = new ProductImage
            {
                Url = dto.Url,
                AltText = dto.AltText,
                DisplayOrder = dto.DisplayOrder,
                IsPrimary = dto.IsPrimary,
                ProductId = dto.ProductId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProductImages.Add(image);
            await _context.SaveChangesAsync();

            return new ProductImageDto
            {
                Id = image.Id,
                Url = image.Url,
                AltText = image.AltText,
                DisplayOrder = image.DisplayOrder,
                IsPrimary = image.IsPrimary
            };
        }


        // ════════════════════════════════════════════════════
        // Delete Image
        // ════════════════════════════════════════════════════
        public async Task<bool> DeleteImageAsync(Guid imageId)
        {
            var image = await _context.ProductImages
                .FirstOrDefaultAsync(i => i.Id == imageId);

            if (image == null) return false;

            _context.ProductImages.Remove(image);
            await _context.SaveChangesAsync();

            return true;
        }


        // ════════════════════════════════════════════════════
        // Add Variant
        // ════════════════════════════════════════════════════
        public async Task<ProductVariantDto> AddVariantAsync(
            Guid productId, CreateProductVariantDto dto)
        {
            var variant = new ProductVariant
            {
                Name = dto.Name.Trim(),
                SKU = dto.SKU,
                PriceOverride = dto.PriceOverride,
                StockQuantity = dto.StockQuantity,
                ImageUrl = dto.ImageUrl,
                IsActive = true,
                ProductId = productId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProductVariants.Add(variant);
            await _context.SaveChangesAsync();

            return new ProductVariantDto
            {
                Id = variant.Id,
                Name = variant.Name,
                SKU = variant.SKU,
                PriceOverride = variant.PriceOverride,
                StockQuantity = variant.StockQuantity,
                ImageUrl = variant.ImageUrl,
                IsActive = variant.IsActive
            };
        }


        // ════════════════════════════════════════════════════
        // Delete Variant
        // ════════════════════════════════════════════════════
        public async Task<bool> DeleteVariantAsync(Guid variantId)
        {
            var variant = await _context.ProductVariants
                .FirstOrDefaultAsync(v => v.Id == variantId);

            if (variant == null) return false;

            _context.ProductVariants.Remove(variant);
            await _context.SaveChangesAsync();

            return true;
        }


        // ════════════════════════════════════════════════════
        // Helper — إنشاء مجلد المنتج
        // ════════════════════════════════════════════════════
        private void CreateProductFolder(Guid storeId, Guid productId)
        {
            var path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "uploads", "stores", storeId.ToString(),
                "products", productId.ToString()
            );

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }


        // ════════════════════════════════════════════════════
        // Helper — ToDto
        // ════════════════════════════════════════════════════
        private static ProductDto ToDto(Models.Product p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Slug = p.Slug,
            SKU = p.SKU,
            Description = p.Description,
            ShortDescription = p.ShortDescription,
            Price = p.Price,
            CompareAtPrice = p.CompareAtPrice,
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
            CreatedAt = p.CreatedAt,
            Images = p.Images?.Select(i => new ProductImageDto
            {
                Id = i.Id,
                Url = i.Url,
                AltText = i.AltText,
                DisplayOrder = i.DisplayOrder,
                IsPrimary = i.IsPrimary
            }).ToList(),
            Variants = p.Variants?.Select(v => new ProductVariantDto
            {
                Id = v.Id,
                Name = v.Name,
                SKU = v.SKU,
                PriceOverride = v.PriceOverride,
                StockQuantity = v.StockQuantity,
                ImageUrl = v.ImageUrl,
                IsActive = v.IsActive
            }).ToList(),
            AttributeValues = p.AttributeValues?.Select(av => new ProductAttributeValueDto
            {
                Id = av.Id,
                AttributeName = av.Attribute?.Name ?? "",
                Value = av.Value
            }).ToList()
        };
    }
}
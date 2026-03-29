// Services/Product/ProductService.cs
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Product;
using onlineStore.Models;
using onlineStore.Models.Enums;
using Microsoft.AspNetCore.Http;
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
            if (string.IsNullOrWhiteSpace(slug))
                return null;

            var normalizedSlug = slug.Trim().ToLower();

            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Section)
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.AttributeValues)
                    .ThenInclude(av => av.Attribute)
                .FirstOrDefaultAsync(p => p.Slug == normalizedSlug);

            return product == null ? null : ToDto(product);
        }

        // ════════════════════════════════════════════════════
        // Create Product
        // ════════════════════════════════════════════════════
        public async Task<ProductDto> CreateProductAsync(CreateProductDto dto)
        {
            var normalizedSlug = dto.Slug.Trim().ToLower();

            var slugExists = await _context.Products
                .AnyAsync(p => p.Slug == normalizedSlug
                            && p.StoreId == dto.StoreId);

            if (slugExists)
                throw new Exception("هذا الرابط مستخدم مسبقاً");

            var product = new Models.Product
            {
                Name = dto.Name.Trim(),
                Slug = normalizedSlug,
                SKU = dto.SKU?.Trim(),
                Description = dto.Description?.Trim(),
                ShortDescription = dto.ShortDescription?.Trim(),
                Price = dto.Price,
                CompareAtPrice = dto.CompareAtPrice,
                CostPrice = dto.CostPrice,
                StockQuantity = dto.StockQuantity,
                TrackInventory = dto.TrackInventory,
                ThumbnailUrl = dto.ThumbnailUrl?.Trim(),
                MetaTitle = dto.MetaTitle?.Trim(),
                MetaDescription = dto.MetaDescription?.Trim(),
                Status = ProductStatus.Draft,
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
                foreach (var v in dto.Variants)
                {
                    product.Variants.Add(new ProductVariant
                    {
                        Name = v.Name.Trim(),
                        SKU = v.SKU?.Trim(),
                        PriceOverride = v.PriceOverride,
                        StockQuantity = v.StockQuantity,
                        ImageUrl = v.ImageUrl?.Trim(),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

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

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            CreateProductFolder(dto.StoreId, product.Id);

            if (dto.Images != null && dto.Images.Any())
            {
                for (int i = 0; i < dto.Images.Count; i++)
                {
                    var file = dto.Images[i];
                    var imageUrl = await SaveProductImageAsync(dto.StoreId, product.Id, file);

                    var productImage = new ProductImage
                    {
                        ProductId = product.Id,
                        Url = imageUrl,
                        AltText = product.Name,
                        DisplayOrder = i,
                        IsPrimary = i == 0,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.ProductImages.Add(productImage);

                    if (i == 0)
                        product.ThumbnailUrl = imageUrl;
                }

                await _context.SaveChangesAsync();
            }

            var createdProduct = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Section)
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.AttributeValues)
                    .ThenInclude(av => av.Attribute)
                .FirstAsync(p => p.Id == product.Id);

            _logger.LogInformation(
                "Product created: {ProductName}", product.Name);

            return ToDto(createdProduct);
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
                product.CategoryId = dto.CategoryId.Value;

            if (dto.SectionId != null)
                product.SectionId = dto.SectionId.Value;

            await _context.SaveChangesAsync();

            var updatedProduct = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Section)
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.AttributeValues)
                    .ThenInclude(av => av.Attribute)
                .FirstAsync(p => p.Id == id);

            _logger.LogInformation("Product updated: {ProductId}", id);

            return ToDto(updatedProduct);
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
                throw new Exception("product not exist");

            if (product.IsDeleted)
                throw new Exception("you cant add image for deleted product");

            if (dto.Image == null || dto.Image.Length == 0)
                throw new Exception("invalid image");

            if (dto.IsPrimary)
            {
                foreach (var existingImage in product.Images)
                    existingImage.IsPrimary = false;
            }

            var imageUrl = await SaveProductImageAsync(
                product.StoreId,
                product.Id,
                dto.Image
            );

            var image = new ProductImage
            {
                Url = imageUrl,
                AltText = dto.AltText?.Trim(),
                DisplayOrder = dto.DisplayOrder,
                IsPrimary = dto.IsPrimary || !product.Images.Any(),
                ProductId = dto.ProductId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProductImages.Add(image);

            if (image.IsPrimary)
                product.ThumbnailUrl = image.Url;

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
                .Include(i => i.Product)
                    .ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(i => i.Id == imageId);

            if (image == null)
                return false;

            var product = image.Product;
            var wasPrimary = image.IsPrimary;

            DeletePhysicalImage(image.Url);

            _context.ProductImages.Remove(image);
            await _context.SaveChangesAsync();

            if (product != null)
            {
                var remainingImages = await _context.ProductImages
                    .Where(i => i.ProductId == product.Id)
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
                        product.ThumbnailUrl = newPrimary.Url;
                    }
                    else
                    {
                        product.ThumbnailUrl = null;
                    }

                    await _context.SaveChangesAsync();
                }
            }

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
                "wwwroot",
                "uploads",
                "stores",
                storeId.ToString(),
                "products",
                productId.ToString()
            );

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
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
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => (int?)p.VisitCount)
                .FirstOrDefaultAsync();
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
            VisitCount = p.VisitCount,
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
        private async Task<string> SaveProductImageAsync(
      Guid storeId,
      Guid productId,
      IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new Exception("الصورة غير صالحة");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

            if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
                throw new Exception("صيغة الصورة غير مدعومة");

            const long maxFileSize = 5 * 1024 * 1024;
            if (file.Length > maxFileSize)
                throw new Exception("حجم الصورة يجب ألا يتجاوز 5 MB");

            var productFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "uploads",
                "stores",
                storeId.ToString(),
                "products",
                productId.ToString()
            );

            if (!Directory.Exists(productFolder))
                Directory.CreateDirectory(productFolder);

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(productFolder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/stores/{storeId}/products/{productId}/{fileName}";
        }
        private void DeletePhysicalImage(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return;

            var relativePath = imageUrl
                .TrimStart('/')
                .Replace("/", Path.DirectorySeparatorChar.ToString());

            var fullPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                relativePath
            );

            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
    }
}
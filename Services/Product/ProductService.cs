using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Product;
using onlineStore.Models;
using onlineStore.Models.Enums;
using onlineStore.Security;
using System.Text.RegularExpressions;

namespace onlineStore.Services.Product
{
    public class ProductService : IProductService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProductService> _logger;
        private readonly ICurrentUserService _currentUser;
        private readonly IStoreOwnershipService _storeOwnershipService;
        private readonly IWebHostEnvironment _environment;

        public ProductService(
            AppDbContext context,
            ILogger<ProductService> logger,
            ICurrentUserService currentUser,
            IStoreOwnershipService storeOwnershipService,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _currentUser = currentUser;
            _storeOwnershipService = storeOwnershipService;
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

            IQueryable<Models.Product> query = _context.Products
                .AsNoTracking()
                .Where(p => p.StoreId == storeId)
                .Include(p => p.Category)
                .Include(p => p.Section)
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.AttributeValues)
                    .ThenInclude(av => av.Attribute);

            var canManageStore =true ;

           
            
             
            
     

            if (!canManageStore)
            {
                query = query.Where(p => p.Status == ProductStatus.Active);
            }

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return products.Select(p => ToDto(p, discountPercentage)).ToList();
        }

        // ════════════════════════════════════════════════════
        // Get Featured Products
        // ════════════════════════════════════════════════════
        public async Task<List<ProductDto>> GetFeaturedProductsAsync(
            Guid storeId,
            Guid? userId = null)
        {
            var discountPercentage = await GetCustomerDiscountPercentageAsync(storeId, userId);

            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.StoreId == storeId &&
                            p.IsFeatured &&
                            p.Status == ProductStatus.Active)
                .Include(p => p.Images)
                .ToListAsync();

            return products.Select(p => ToDto(p, discountPercentage)).ToList();
        }

        // ════════════════════════════════════════════════════
        // Get Products By Category
        // ════════════════════════════════════════════════════
        public async Task<List<ProductDto>> GetProductsByCategoryAsync(
            Guid categoryId,
            Guid? userId = null)
        {
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.CategoryId == categoryId && p.Status == ProductStatus.Active)
                .Include(p => p.Category)
                .Include(p => p.Section)
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.AttributeValues)
                    .ThenInclude(av => av.Attribute)
                .ToListAsync();

            if (!products.Any())
                return new List<ProductDto>();

            var storeId = products.First().StoreId;
            var discountPercentage = await GetCustomerDiscountPercentageAsync(storeId, userId);

            return products.Select(p => ToDto(p, discountPercentage)).ToList();
        }

        // ════════════════════════════════════════════════════
        // Get Products By Section
        // ════════════════════════════════════════════════════
        public async Task<List<ProductDto>> GetProductsBySectionAsync(
            Guid sectionId,
            Guid? userId = null)
        {
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.SectionId == sectionId && p.Status == ProductStatus.Active)
                .Include(p => p.Category)
                .Include(p => p.Section)
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.AttributeValues)
                    .ThenInclude(av => av.Attribute)
                .ToListAsync();

            if (!products.Any())
                return new List<ProductDto>();

            var storeId = products.First().StoreId;
            var discountPercentage = await GetCustomerDiscountPercentageAsync(storeId, userId);

            return products.Select(p => ToDto(p, discountPercentage)).ToList();
        }

        // ════════════════════════════════════════════════════
        // Get Product By Id
        // ════════════════════════════════════════════════════
        public async Task<ProductDto?> GetProductByIdAsync(
            Guid id,
            Guid? userId = null)
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

            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Section)
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.AttributeValues)
                    .ThenInclude(av => av.Attribute)
                .FirstOrDefaultAsync(p => p.Slug == normalizedSlug);

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
                foreach (var variantDto in dto.Variants)
                {
                    product.Variants.Add(new ProductVariant
                    {
                        Name = variantDto.Name.Trim(),
                        SKU = variantDto.SKU?.Trim(),
                        PriceOverride = variantDto.PriceOverride,
                        StockQuantity = variantDto.StockQuantity,
                        ImageUrl = variantDto.ImageUrl?.Trim(),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }
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

            if (dto.Images != null && dto.Images.Any())
            {
                for (int i = 0; i < dto.Images.Count; i++)
                {
                    var imageUrl = await SaveProductImageAsync(
                        dto.StoreId,
                        product.Id,
                        dto.Images[i],
                        i + 1);

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

            var createdProduct = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Section)
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.AttributeValues)
                    .ThenInclude(av => av.Attribute)
                .FirstAsync(p => p.Id == product.Id);

            _logger.LogInformation("Product created: {ProductName}", product.Name);

            return ToDto(createdProduct, 0m);
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

            return ToDto(updatedProduct, 0m);
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

            if (dto.Image == null || dto.Image.Length == 0)
                throw new Exception("Invalid image");

            if (dto.IsPrimary)
            {
                foreach (var existingImage in product.Images)
                    existingImage.IsPrimary = false;
            }

            var displayOrder = dto.DisplayOrder > 0
                ? dto.DisplayOrder
                : (product.Images.Any() ? product.Images.Max(i => i.DisplayOrder) + 1 : 1);

            var imageUrl = await SaveProductImageAsync(
                product.StoreId,
                product.Id,
                dto.Image,
                displayOrder);

            var image = new ProductImage
            {
                Url = imageUrl,
                AltText = dto.AltText?.Trim(),
                DisplayOrder = displayOrder,
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
                .FirstOrDefaultAsync(i => i.Id == imageId);

            if (image == null)
                return false;

            if (image.Product == null)
                throw new InvalidOperationException("Image product does not exist.");

            await EnsureCanManageStoreAsync(image.Product.StoreId);

            var product = image.Product;
            var wasPrimary = image.IsPrimary;

            DeletePhysicalImage(image.Url);

            _context.ProductImages.Remove(image);
            await _context.SaveChangesAsync();

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

            var variant = new ProductVariant
            {
                Name = dto.Name.Trim(),
                SKU = dto.SKU?.Trim(),
                PriceOverride = dto.PriceOverride,
                StockQuantity = dto.StockQuantity,
                ImageUrl = dto.ImageUrl?.Trim(),
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
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == variantId);

            if (variant == null)
                return false;

            await EnsureCanManageStoreAsync(variant.Product.StoreId);

            _context.ProductVariants.Remove(variant);
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

            await EnsureCanManageStoreAsync(product.StoreId);

            return product.VisitCount;
        }

        // ════════════════════════════════════════════════════
        // Paths Helpers
        // ════════════════════════════════════════════════════
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

            if (!Directory.Exists(productsFolder))
                Directory.CreateDirectory(productsFolder);

            if (!Directory.Exists(productFolder))
                Directory.CreateDirectory(productFolder);
        }

        private async Task<string> SaveProductImageAsync(
            Guid storeId,
            Guid productId,
            IFormFile file,
            int imageIndex)
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

            var productFolder = GetProductFolderPath(storeId, productId);

            if (!Directory.Exists(productFolder))
                Directory.CreateDirectory(productFolder);

            var fileName = $"{imageIndex}{extension}";
            var filePath = Path.Combine(productFolder, fileName);

            if (File.Exists(filePath))
                File.Delete(filePath);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/stores/{storeId}/products/{productId}/{fileName}";
        }

        private void DeletePhysicalImage(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return;

            var rootPath = _environment.WebRootPath ?? _environment.ContentRootPath;

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
            Guid? userId)
        {
            if (!userId.HasValue)
                return 0m;

            var discount = await _context.CustomerStores
                .AsNoTracking()
                .Where(x => x.StoreId == storeId &&
                            x.CustomerId == userId.Value &&
                            x.IsActive)
                .Select(x => x.DiscountPercentage)
                .FirstOrDefaultAsync();

            return discount;
        }

        private static ProductDto ToDto(Models.Product p, decimal discountPercentage = 0m)
        {
            var finalPrice = p.Price;

            if (discountPercentage > 0)
                finalPrice = p.Price - (p.Price * discountPercentage / 100m);

            return new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                SKU = p.SKU,
                Description = p.Description,
                ShortDescription = p.ShortDescription,

                Price = finalPrice,
                OriginalPrice = p.Price,
                FinalPrice = finalPrice,
                AppliedDiscountPercentage = discountPercentage,
                IsWholesalePriceApplied = discountPercentage > 0,

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

                Images = p.Images?
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new ProductImageDto
                    {
                        Id = i.Id,
                        Url = i.Url,
                        AltText = i.AltText,
                        DisplayOrder = i.DisplayOrder,
                        IsPrimary = i.IsPrimary
                    }).ToList() ?? new List<ProductImageDto>(),

                Variants = p.Variants?
                    .Select(v => new ProductVariantDto
                    {
                        Id = v.Id,
                        Name = v.Name,
                        SKU = v.SKU,
                        PriceOverride = v.PriceOverride,
                        StockQuantity = v.StockQuantity,
                        ImageUrl = v.ImageUrl,
                        IsActive = v.IsActive
                    }).ToList() ?? new List<ProductVariantDto>(),

                AttributeValues = p.AttributeValues?
                    .Select(av => new ProductAttributeValueDto
                    {
                        Id = av.Id,
                        AttributeName = av.Attribute?.Name ?? "",
                        Value = av.Value
                    }).ToList() ?? new List<ProductAttributeValueDto>()
            };
        }
    }
}
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.SystemStorage;

namespace onlineStore.Services.SystemStorage
{
    public class SystemStorageService : ISystemStorageService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<SystemStorageService> _logger;

        public SystemStorageService(
            AppDbContext context,
            IWebHostEnvironment environment,
            ILogger<SystemStorageService> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        public async Task<EnsureVariantFoldersResultDto> EnsureVariantFoldersExistAsync(
            CancellationToken cancellationToken = default)
        {
            var result = new EnsureVariantFoldersResultDto();

            var stores = await _context.Stores
                .AsNoTracking()
                .Select(store => new
                {
                    store.Id,
                    Products = _context.Products
                        .AsNoTracking()
                        .Where(product => product.StoreId == store.Id)
                        .Select(product => new
                        {
                            product.Id,
                            Variants = _context.ProductVariants
                                .AsNoTracking()
                                .Where(variant => variant.ProductId == product.Id)
                                .Select(variant => variant.Id)
                                .ToList()
                        })
                        .ToList()
                })
                .ToListAsync(cancellationToken);

            foreach (var store in stores)
            {
                result.StoresProcessed++;

                foreach (var product in store.Products)
                {
                    result.ProductsProcessed++;

                    try
                    {
                        var productFolder = GetProductFolderPath(store.Id, product.Id);
                        EnsureDirectory(Path.Combine(productFolder, "images"), result);
                        EnsureDirectory(Path.Combine(productFolder, "variants"), result);

                        foreach (var variantId in product.Variants)
                        {
                            result.VariantsProcessed++;

                            try
                            {
                                EnsureDirectory(
                                    Path.Combine(productFolder, "variants", variantId.ToString()),
                                    result);
                            }
                            catch (Exception ex)
                            {
                                var message =
                                    $"Variant folder failed. StoreId={store.Id}, ProductId={product.Id}, VariantId={variantId}, Error={ex.Message}";
                                result.Errors.Add(message);
                                _logger.LogError(
                                    ex,
                                    "Failed to ensure variant folder. StoreId={StoreId}, ProductId={ProductId}, VariantId={VariantId}",
                                    store.Id,
                                    product.Id,
                                    variantId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var message =
                            $"Product folders failed. StoreId={store.Id}, ProductId={product.Id}, Error={ex.Message}";
                        result.Errors.Add(message);
                        _logger.LogError(
                            ex,
                            "Failed to ensure product folders. StoreId={StoreId}, ProductId={ProductId}",
                            store.Id,
                            product.Id);
                    }
                }
            }

            _logger.LogInformation(
                "Ensure variant folders completed. Stores={Stores}, Products={Products}, Variants={Variants}, Created={Created}, Existing={Existing}, Errors={Errors}",
                result.StoresProcessed,
                result.ProductsProcessed,
                result.VariantsProcessed,
                result.FoldersCreated,
                result.FoldersAlreadyExisted,
                result.Errors.Count);

            return result;
        }

        public async Task<RemoveEmptyProductImagesFoldersResultDto> RemoveEmptyProductImagesFoldersAsync(
            bool dryRun = true,
            CancellationToken cancellationToken = default)
        {
            var result = new RemoveEmptyProductImagesFoldersResultDto
            {
                DryRun = dryRun
            };

            var stores = await _context.Stores
                .AsNoTracking()
                .Select(store => store.Id)
                .ToListAsync(cancellationToken);

            result.StoresProcessed = stores.Count;

            var products = await _context.Products
                .AsNoTracking()
                .Where(product => stores.Contains(product.StoreId))
                .Select(product => new ProductImagesFolderCandidate(
                    product.StoreId,
                    product.Id,
                    product.ThumbnailUrl))
                .ToListAsync(cancellationToken);

            result.ProductsProcessed = products.Count;

            if (products.Count == 0)
            {
                return result;
            }

            var productIds = products
                .Select(product => product.ProductId)
                .ToHashSet();

            var productImageUrlsByProduct = await _context.ProductImages
                .AsNoTracking()
                .Where(image => productIds.Contains(image.ProductId))
                .Select(image => new
                {
                    image.ProductId,
                    image.Url
                })
                .ToListAsync(cancellationToken);

            var variantImageUrlsByProduct = await _context.ProductVariants
                .AsNoTracking()
                .Where(variant => productIds.Contains(variant.ProductId) && !variant.IsDeleted)
                .Select(variant => new
                {
                    variant.ProductId,
                    variant.ImageUrl
                })
                .ToListAsync(cancellationToken);

            var productImageLookup = productImageUrlsByProduct
                .GroupBy(image => image.ProductId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(image => image.Url).ToList());

            var variantImageLookup = variantImageUrlsByProduct
                .GroupBy(variant => variant.ProductId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(variant => variant.ImageUrl).ToList());

            var uploadsStoresRoot = Path.GetFullPath(Path.Combine(
                _environment.ContentRootPath,
                "uploads",
                "stores"));

            foreach (var product in products)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = GetProductImagesFolderRelativePath(product.StoreId, product.ProductId);
                var folderPath = Path.GetFullPath(GetProductImagesFolderPath(product.StoreId, product.ProductId));

                try
                {
                    if (!IsUnderRoot(folderPath, uploadsStoresRoot))
                    {
                        SkipBecauseError(
                            result,
                            product,
                            relativePath,
                            "Path is outside the expected uploads/stores root.");
                        continue;
                    }

                    if (!Directory.Exists(folderPath))
                    {
                        result.FoldersMissing++;
                        continue;
                    }

                    result.FoldersFound++;

                    var referenceReasons = GetReferenceReasons(
                        product,
                        productImageLookup,
                        variantImageLookup);

                    if (referenceReasons.Count > 0)
                    {
                        result.FoldersSkippedBecauseReferenced++;
                        var reason = string.Join("; ", referenceReasons);
                        result.Skipped.Add(CreateCleanupItem(product, relativePath, reason));
                        _logger.LogWarning(
                            "Skipped product images folder because it is referenced. Path={Path}, Reason={Reason}",
                            relativePath,
                            reason);
                        continue;
                    }

                    var attributes = File.GetAttributes(folderPath);
                    if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        SkipBecauseError(
                            result,
                            product,
                            relativePath,
                            "Folder is a reparse point.");
                        continue;
                    }

                    if (Directory.EnumerateFileSystemEntries(folderPath).Any())
                    {
                        result.FoldersSkippedBecauseNotEmpty++;
                        result.Skipped.Add(CreateCleanupItem(
                            product,
                            relativePath,
                            "Folder is not empty."));
                        _logger.LogWarning(
                            "Skipped non-empty product images folder. Path={Path}",
                            relativePath);
                        continue;
                    }

                    if (dryRun)
                    {
                        result.FoldersWouldBeDeleted++;
                        result.WouldDelete.Add(CreateCleanupItem(
                            product,
                            relativePath,
                            "Empty and unreferenced. Dry run only."));
                        _logger.LogInformation(
                            "Dry run: product images folder would be deleted. Path={Path}",
                            relativePath);
                        continue;
                    }

                    Directory.Delete(folderPath, recursive: false);
                    result.FoldersDeleted++;
                    result.Deleted.Add(CreateCleanupItem(
                        product,
                        relativePath,
                        "Deleted empty unreferenced folder."));
                    _logger.LogInformation(
                        "Deleted empty product images folder. Path={Path}",
                        relativePath);
                }
                catch (Exception ex)
                {
                    SkipBecauseError(
                        result,
                        product,
                        relativePath,
                        ex.Message);
                    _logger.LogError(
                        ex,
                        "Failed while processing product images folder. Path={Path}",
                        relativePath);
                }
            }

            _logger.LogInformation(
                "Remove empty product images folders completed. DryRun={DryRun}, Stores={Stores}, Products={Products}, Found={Found}, Deleted={Deleted}, WouldDelete={WouldDelete}, Missing={Missing}, Referenced={Referenced}, NotEmpty={NotEmpty}, Errors={Errors}",
                result.DryRun,
                result.StoresProcessed,
                result.ProductsProcessed,
                result.FoldersFound,
                result.FoldersDeleted,
                result.FoldersWouldBeDeleted,
                result.FoldersMissing,
                result.FoldersSkippedBecauseReferenced,
                result.FoldersSkippedBecauseNotEmpty,
                result.FoldersSkippedBecauseError);

            return result;
        }

        private string GetProductFolderPath(Guid storeId, Guid productId)
        {
            return Path.Combine(
                _environment.ContentRootPath,
                "uploads",
                "stores",
                storeId.ToString(),
                "products",
                productId.ToString());
        }

        private string GetProductImagesFolderPath(Guid storeId, Guid productId)
        {
            return Path.Combine(GetProductFolderPath(storeId, productId), "images");
        }

        private static string GetProductImagesFolderRelativePath(Guid storeId, Guid productId)
        {
            return string.Join(
                "/",
                "uploads",
                "stores",
                storeId.ToString(),
                "products",
                productId.ToString(),
                "images");
        }

        private void EnsureDirectory(
            string path,
            EnsureVariantFoldersResultDto result)
        {
            if (Directory.Exists(path))
            {
                result.FoldersAlreadyExisted++;
                _logger.LogDebug("Folder already exists: {Path}", path);
                return;
            }

            Directory.CreateDirectory(path);
            result.FoldersCreated++;
            _logger.LogInformation("Folder created: {Path}", path);
        }

        private static List<string> GetReferenceReasons(
            ProductImagesFolderCandidate product,
            Dictionary<Guid, List<string>> productImageLookup,
            Dictionary<Guid, List<string?>> variantImageLookup)
        {
            var reasons = new List<string>();

            if (ReferencesProductImagesFolder(product.ThumbnailUrl, product.StoreId, product.ProductId))
            {
                reasons.Add("Referenced by Product.ThumbnailUrl");
            }

            if (productImageLookup.TryGetValue(product.ProductId, out var productImageUrls)
                && productImageUrls.Any(url => ReferencesProductImagesFolder(url, product.StoreId, product.ProductId)))
            {
                reasons.Add("Referenced by ProductImages.Url");
            }

            if (variantImageLookup.TryGetValue(product.ProductId, out var variantImageUrls)
                && variantImageUrls.Any(url => ReferencesProductImagesFolder(url, product.StoreId, product.ProductId)))
            {
                reasons.Add("Referenced by ProductVariant.ImageUrl");
            }

            return reasons;
        }

        private static bool ReferencesProductImagesFolder(
            string? value,
            Guid storeId,
            Guid productId)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value
                .Replace('\\', '/')
                .ToLowerInvariant();

            var storeIdValue = storeId.ToString().ToLowerInvariant();
            var productIdValue = productId.ToString().ToLowerInvariant();

            return ContainsPathSegment(
                    normalized,
                    $"uploads/stores/{storeIdValue}/products/{productIdValue}/images")
                || ContainsPathSegment(
                    normalized,
                    $"stores/{storeIdValue}/products/{productIdValue}/images")
                || ContainsPathSegment(
                    normalized,
                    $"products/{productIdValue}/images");
        }

        private static bool ContainsPathSegment(string normalizedValue, string segment)
        {
            var startIndex = 0;

            while (startIndex < normalizedValue.Length)
            {
                var index = normalizedValue.IndexOf(
                    segment,
                    startIndex,
                    StringComparison.Ordinal);

                if (index < 0)
                {
                    return false;
                }

                var end = index + segment.Length;
                var boundaryAfterSegment =
                    end == normalizedValue.Length
                    || normalizedValue[end] == '/'
                    || normalizedValue[end] == '?'
                    || normalizedValue[end] == '#';

                if (boundaryAfterSegment)
                {
                    return true;
                }

                startIndex = index + 1;
            }

            return false;
        }

        private static bool IsUnderRoot(string path, string root)
        {
            var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;

            return path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        private static ProductImagesFolderCleanupItemDto CreateCleanupItem(
            ProductImagesFolderCandidate product,
            string relativePath,
            string reason)
        {
            return new ProductImagesFolderCleanupItemDto
            {
                StoreId = product.StoreId,
                ProductId = product.ProductId,
                Path = relativePath,
                Reason = reason
            };
        }

        private void SkipBecauseError(
            RemoveEmptyProductImagesFoldersResultDto result,
            ProductImagesFolderCandidate product,
            string relativePath,
            string reason)
        {
            result.FoldersSkippedBecauseError++;
            result.Errors.Add(
                $"StoreId={product.StoreId}, ProductId={product.ProductId}, Path={relativePath}, Error={reason}");
            result.Skipped.Add(CreateCleanupItem(product, relativePath, reason));
        }

        private sealed record ProductImagesFolderCandidate(
            Guid StoreId,
            Guid ProductId,
            string? ThumbnailUrl);
    }
}

using onlineStore.DTOs.Product;

namespace onlineStore.Services.Product
{
    public interface IProductService
    {
        // Queries
        Task<List<ProductDto>> GetStoreProductsAsync(Guid storeId, Guid? userId = null);
        Task<List<ProductDto>> GetFeaturedProductsAsync(Guid storeId, Guid? userId = null);
        Task<List<ProductDto>> GetProductsByCategoryAsync(Guid categoryId, Guid? userId = null);
        Task<List<ProductDto>> GetProductsBySectionAsync(Guid sectionId, Guid? userId = null);
        Task<ProductDto?> GetProductByIdAsync(Guid id, Guid? userId = null);
        Task<ProductDto?> GetProductBySlugAsync(string slug, Guid? userId = null);

        // Commands
        Task<ProductDto> CreateProductAsync(CreateProductDto dto);
        Task<ProductDto?> UpdateProductAsync(Guid id, UpdateProductDto dto);
        Task<bool> DeleteProductAsync(Guid id);

        // Images
        Task<ProductImageDto> AddImageAsync(AddProductImageDto dto);
        Task<bool> DeleteImageAsync(Guid imageId);

        // Visits
        Task<int?> IncrementProductVisitAsync(Guid productId);
        Task<int?> GetProductVisitCountAsync(Guid productId);

        // Variants
        Task<ProductVariantDto> AddVariantAsync(Guid productId, CreateProductVariantDto dto);
        Task<bool> DeleteVariantAsync(Guid variantId);
    }
}
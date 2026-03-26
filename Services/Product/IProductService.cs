// Services/Product/IProductService.cs
using onlineStore.DTOs.Product;

namespace onlineStore.Services.Product
{
    public interface IProductService
    {
        // ── Queries ──
        Task<List<ProductDto>> GetStoreProductsAsync(Guid storeId);
        Task<List<ProductDto>> GetFeaturedProductsAsync(Guid storeId);
        Task<List<ProductDto>> GetProductsByCategoryAsync(Guid categoryId);
        Task<List<ProductDto>> GetProductsBySectionAsync(Guid sectionId);
        Task<ProductDto?> GetProductByIdAsync(Guid id);
        Task<ProductDto?> GetProductBySlugAsync(string slug);

        // ── Commands ──
        Task<ProductDto> CreateProductAsync(CreateProductDto dto);
        Task<ProductDto?> UpdateProductAsync(Guid id, UpdateProductDto dto);
        Task<bool> DeleteProductAsync(Guid id);

        // ── Images ──
        Task<ProductImageDto> AddImageAsync(AddProductImageDto dto);
        Task<bool> DeleteImageAsync(Guid imageId);

        // ── Variants ──
        Task<ProductVariantDto> AddVariantAsync(Guid productId, CreateProductVariantDto dto);
        Task<bool> DeleteVariantAsync(Guid variantId);
    }
}
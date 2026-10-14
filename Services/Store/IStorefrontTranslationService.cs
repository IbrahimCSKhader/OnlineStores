using onlineStore.DTOs.Category;
using onlineStore.DTOs.Product;
using onlineStore.DTOs.Section;
using onlineStore.DTOs.Store;

namespace onlineStore.Services.Store;

public interface IStorefrontTranslationService
{
    Task ApplyStoreAsync(StoreDto store, string? language, CancellationToken cancellationToken = default);
    Task ApplyCategoriesAsync(Guid storeId, IReadOnlyCollection<CategoryDto> categories, string? language, CancellationToken cancellationToken = default);
    Task ApplySectionsAsync(Guid storeId, IReadOnlyCollection<SectionDto> sections, string? language, CancellationToken cancellationToken = default);
    Task ApplyProductAsync(ProductDto? product, string? language, CancellationToken cancellationToken = default);
    Task ApplyProductsAsync(Guid storeId, IReadOnlyCollection<ProductDto> products, string? language, CancellationToken cancellationToken = default);
}

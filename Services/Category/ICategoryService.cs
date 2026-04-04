using onlineStore.DTOs.Category;

namespace onlineStore.Services.Category
{
    public interface ICategoryService
    {
  
            Task<List<CategoryDto>> GetStoreCategoriesAsync(Guid storeId, CancellationToken cancellationToken = default);
            Task<CategoryDto?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default);
            Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto dto, CancellationToken cancellationToken = default);
            Task<CategoryDto?> UpdateCategoryAsync(Guid id, UpdateCategoryDto dto, CancellationToken cancellationToken = default);
            Task<bool> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);
        
    }
}
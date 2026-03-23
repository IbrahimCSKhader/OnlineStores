using onlineStore.DTOs.Category;

namespace onlineStore.Services.Category
{
    public interface ICategoryService
    {
        Task<List<CategoryDto>> GetStoreCategoriesAsync(Guid storeId);
        Task<CategoryDto?> GetCategoryByIdAsync(Guid id);
        Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto dto);
        Task<CategoryDto?> UpdateCategoryAsync(Guid id, UpdateCategoryDto dto);
        Task<bool> DeleteCategoryAsync(Guid id);
    }
}

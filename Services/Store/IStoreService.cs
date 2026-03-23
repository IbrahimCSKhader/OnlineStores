using onlineStore.DTOs.Store;

namespace onlineStore.Services.Store
{
    public interface IStoreService
    {
        // SuperAdmin
        Task<List<StoreDto>> GetAllStoresAsync();
        Task<StoreDto?> GetStoreByIdAsync(Guid id);
        Task<StoreDto> CreateStoreAsync(CreateStoreDto dto, string ownerId);
        Task<StoreDto?> UpdateStoreAsync(Guid id, UpdateStoreDto dto);
        Task<bool> DeleteStoreAsync(Guid id);

        // Public
        Task<StoreDto?> GetStoreBySlugAsync(string slug);
    }
}

using onlineStore.DTOs.Store;

namespace onlineStore.Services.Store
{
    public interface IStoreService
    {
        // SuperAdmin
        Task<List<StoreDto>> GetAllStoresAsync();
        Task<StoreDto?> GetStoreByIdAsync(Guid id);
        Task<StoreDto?> GetStoreBySlugAsync(string slug);
        Task<StoreDto> CreateStoreAsync(CreateStoreDto dto);
        Task<StoreDto?> UpdateStoreAsync(Guid id, UpdateStoreDto dto);
        Task<bool> DeleteStoreAsync(Guid id);
        Task<int?> IncrementStoreVisitAsync(Guid storeId);
        Task<int?> GetStoreVisitCountAsync(Guid storeId);
    }
}

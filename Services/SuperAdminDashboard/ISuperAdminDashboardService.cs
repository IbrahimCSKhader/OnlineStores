using onlineStore.DTOs.SuperAdminDashboard;

namespace onlineStore.Services.SuperAdminDashboard
{
    public interface ISuperAdminDashboardService
    {
        Task<SuperAdminDashboardSummaryDto> GetSummaryAsync();
        Task<List<SuperAdminOwnerListItemDto>> GetStoreOwnersAsync();
        Task<SuperAdminOwnerDetailsDto?> GetStoreOwnerDetailsAsync(Guid ownerId);
        Task<bool> SetStoreOwnerStatusAsync(Guid ownerId, bool isActive);
        Task<List<SuperAdminStoreListItemDto>> GetStoresAsync();
        Task<SuperAdminStoreDetailsDto?> GetStoreDetailsAsync(Guid storeId);
        Task<bool> SetStoreStatusAsync(Guid storeId, bool isActive);
        Task<List<SuperAdminStoreCustomerDto>?> GetStoreCustomersAsync(Guid storeId);
    }
}

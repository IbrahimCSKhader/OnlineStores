using onlineStore.DTOs.Subscription;

namespace onlineStore.Services.Subscription
{
    public interface IStoreSubscriptionService
    {
        Task<List<StoreSubscriptionDto>> GetStoreSubscriptionsAsync(Guid storeId);
        Task<StoreSubscriptionDto?> GetActiveSubscriptionForStoreAsync(Guid storeId);
        Task<StoreSubscriptionDto?> GetActiveSubscriptionAsync(Guid storeId);
        Task<StoreSubscriptionDto> AssignStoreSubscriptionAsync(AssignStoreSubscriptionDto dto);
        Task<StoreSubscriptionDto?> ChangeStoreSubscriptionAsync(Guid storeSubscriptionId, ChangeStoreSubscriptionDto dto);
        Task<bool> CancelStoreSubscriptionAsync(Guid storeSubscriptionId, string? notes = null);

        Task<StorePlanLimitsDto> GetStorePlanLimitsAsync(Guid storeId);
        Task<bool> CanStoreCreateProductAsync(Guid storeId);
        Task<bool> CanStoreAddProductImageAsync(Guid productId);
        Task<bool> CanStoreUseOffersAsync(Guid storeId);
    }
}

using onlineStore.DTOs.Subscription;

namespace onlineStore.Services.Subscription
{
    public interface ISubscriptionService
    {
        Task<List<SubscriptionPlanDto>> GetAllPlansAsync();
        Task<SubscriptionPlanDto?> GetPlanByIdAsync(Guid id);
        Task<StoreSubscriptionDto?> GetActiveSubscriptionAsync(Guid storeId);
        Task<StoreSubscriptionDto> AssignPlanToStoreAsync(Guid storeId, Guid planId);
        Task<StoreSubscriptionDto> ChangePlanAsync(Guid storeId, Guid newPlanId);

        Task<bool> CanStoreCreateProductAsync(Guid storeId);
        Task<bool> CanStoreAddImageAsync(Guid productId);
        Task<bool> CanStoreUseOffersAsync(Guid storeId);

        Task EnsureDefaultPlansSeededAsync();
    }
}

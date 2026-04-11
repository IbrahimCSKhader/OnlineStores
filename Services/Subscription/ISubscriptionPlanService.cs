using onlineStore.DTOs.Subscription;

namespace onlineStore.Services.Subscription
{
    public interface ISubscriptionPlanService
    {
        Task<List<SubscriptionPlanDto>> GetAllAsync();
        Task<SubscriptionPlanDto?> GetByIdAsync(Guid id);
        Task<SubscriptionPlanDto> CreateAsync(CreateSubscriptionPlanDto dto);
        Task<SubscriptionPlanDto?> UpdateAsync(Guid id, UpdateSubscriptionPlanDto dto);
        Task<bool> DeleteAsync(Guid id);
        Task EnsureDefaultPlansSeededAsync();
    }
}

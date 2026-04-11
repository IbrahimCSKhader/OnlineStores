using onlineStore.DTOs.Cart;
using onlineStore.Models.CartModels;

namespace onlineStore.Services.Pricing
{
    public interface ICartPricingService
    {
        Task<CartPricingDto> CalculatePricingAsync(
            Guid storeId,
            IReadOnlyCollection<CartItem> items,
            CancellationToken cancellationToken = default);
    }
}

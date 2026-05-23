using onlineStore.DTOs.Cart;
using onlineStore.Models.CartModels;

namespace onlineStore.Services.Pricing
{
    public interface ICartPricingService
    {
        Task<CartPricingDto> CalculatePricingAsync(
            Guid storeId,
            IReadOnlyCollection<CartItem> items,
            decimal customerDiscountPercentage = 0m,
            CancellationToken cancellationToken = default);
    }
}

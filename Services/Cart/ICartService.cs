using onlineStore.DTOs.Cart;

namespace onlineStore.Services.Cart
{
    public interface ICartService
    {
        Task<CartDto> GetCartAsync(Guid storeCustomerId, Guid storeId);
        Task<CartDto> AddToCartAsync(Guid storeCustomerId, AddToCartDto dto);
        Task<CartDto> UpdateCartItemAsync(Guid storeCustomerId, Guid cartItemId, UpdateCartItemDto dto);
        Task<CartDto> RemoveFromCartAsync(Guid storeCustomerId, Guid cartItemId);
        Task<bool> ClearCartAsync(Guid storeCustomerId, Guid storeId);
    }
}

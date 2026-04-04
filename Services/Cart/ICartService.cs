using onlineStore.DTOs.Cart;

namespace onlineStore.Services.Cart
{
    public interface ICartService
    {
        Task<CartDto> GetCartAsync(Guid userId, Guid storeId);
        Task<CartDto> AddToCartAsync(Guid userId, AddToCartDto dto);
        Task<CartDto> UpdateCartItemAsync(Guid userId, Guid cartItemId, UpdateCartItemDto dto);
        Task<CartDto> RemoveFromCartAsync(Guid userId, Guid cartItemId);
        Task<bool> ClearCartAsync(Guid userId, Guid storeId);
    }
}
using onlineStore.DTOs.Order;

namespace onlineStore.Services.Order
{
    public interface IOrderService
    {
        Task<OrderDto> CreateOrderAsync(Guid userId, CreateOrderDto dto);

        Task<List<OrderSummaryDto>> GetUserOrdersAsync(Guid userId);
        Task<OrderDto?> GetUserOrderByIdAsync(Guid userId, Guid orderId);

        Task<List<OrderSummaryDto>> GetStoreOrdersAsync(Guid storeId);
        Task<OrderDto?> GetStoreOrderByIdAsync(Guid storeId, Guid orderId);

        Task<OrderDto?> UpdateOrderStatusAsync(Guid orderId, UpdateOrderStatusDto dto);
    }
}

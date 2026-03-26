using onlineStore.Models.Enums;

namespace onlineStore.DTOs.Order
{
    public class OrderDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;

        public OrderStatus Status { get; set; }

        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }

        public string? CustomerNotes { get; set; }
        public string? StoreNotes { get; set; }

        public string? DeliveryAddress { get; set; }
        public string? DeliveryCity { get; set; }
        public string? DeliveryPhone { get; set; }

        public Guid UserId { get; set; }
        public Guid StoreId { get; set; }

        public Guid? CouponId { get; set; }
        public string? CouponCode { get; set; }

        public DateTime CreatedAt { get; set; }

        public List<OrderItemDto> Items { get; set; } = new();
    }
}
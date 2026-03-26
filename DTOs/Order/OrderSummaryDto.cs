using onlineStore.Models.Enums;

namespace onlineStore.DTOs.Order
{
    public class OrderSummaryDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;

        public OrderStatus Status { get; set; }

        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }

        public int ItemsCount { get; set; }
        public Guid StoreId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
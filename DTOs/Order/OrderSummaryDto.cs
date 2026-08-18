using onlineStore.Models.Enums;

namespace onlineStore.DTOs.Order
{
    public class OrderSummaryDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string? Title { get; set; }

        public OrderStatus Status { get; set; }

        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public int PointsEarned { get; set; }

        public Guid StoreCustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
        public decimal CustomerDiscountPercentage { get; set; }
        public int CustomerPurchasePoints { get; set; }

        public int ItemsCount { get; set; }
        public Guid StoreId { get; set; }
        public Guid? CouponId { get; set; }
        public string? CouponCode { get; set; }
        public DiscountType? CouponDiscountType { get; set; }
        public decimal? CouponDiscountValue { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

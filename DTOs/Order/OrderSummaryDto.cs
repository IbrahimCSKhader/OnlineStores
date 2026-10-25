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
        public Guid? DeliveryLocationId { get; set; }
        public string DeliveryLocationName { get; set; } = "افتراضي";
        public decimal DeliveryCost { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal? ReceivedAmount { get; set; }
        public decimal? RemainingAmount => ReceivedAmount.HasValue ? Math.Max(TotalAmount - ReceivedAmount.Value, 0m) : null;
        public decimal? OverpaidAmount => ReceivedAmount.HasValue ? Math.Max(ReceivedAmount.Value - TotalAmount, 0m) : null;
        public int PointsEarned { get; set; }

        public Guid StoreCustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
        public decimal CustomerDiscountPercentage { get; set; }
        public int CustomerPurchasePoints { get; set; }

        public bool ContainsBundles { get; set; }
        public int ItemsCount { get; set; }
        public Guid StoreId { get; set; }
        public Guid? CouponId { get; set; }
        public string? CouponCode { get; set; }
        public DiscountType? CouponDiscountType { get; set; }
        public decimal? CouponDiscountValue { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

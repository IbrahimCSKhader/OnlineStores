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
        public Guid? AccountCustomerId { get; set; }
        public string? PaymentStatus { get; set; }
        public decimal? RemainingAmount => ReceivedAmount.HasValue ? Math.Max((Status is OrderStatus.Cancelled or OrderStatus.Refunded ? 0m : TotalAmount) - ReceivedAmount.Value, 0m) : null;
        public decimal? OverpaidAmount => ReceivedAmount.HasValue ? Math.Max(ReceivedAmount.Value - (Status is OrderStatus.Cancelled or OrderStatus.Refunded ? 0m : TotalAmount), 0m) : null;
        public int PointsEarned { get; set; }

        public Guid? StoreCustomerId { get; set; }
        public bool IsGuest => !StoreCustomerId.HasValue;
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
        public string? DeliveryPhone { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? DeliveryCity { get; set; }
        public decimal CustomerDiscountPercentage { get; set; }
        public int CustomerPurchasePoints { get; set; }

        public bool ContainsBundles { get; set; }
        public int ItemsCount { get; set; }
        public List<OrderProductPreviewDto> ProductPreviews { get; set; } = new();
        public Guid StoreId { get; set; }
        public Guid? CouponId { get; set; }
        public string? CouponCode { get; set; }
        public DiscountType? CouponDiscountType { get; set; }
        public decimal? CouponDiscountValue { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class OrderProductPreviewDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? VariantName { get; set; }
        public int Quantity { get; set; }
        public string? ImageUrl { get; set; }
    }
}

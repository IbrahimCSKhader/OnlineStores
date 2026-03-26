using onlineStore.Models.Enums;

namespace onlineStore.DTOs.Coupon
{
    public class CouponDto
    {
        public Guid Id { get; set; }

        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }

        public DiscountType DiscountType { get; set; }
        public decimal DiscountValue { get; set; }

        public decimal? MinOrderAmount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }

        public int? UsageLimit { get; set; }
        public int UsageCount { get; set; }
        public int? PerUserLimit { get; set; }

        public DateTime? StartsAt { get; set; }
        public DateTime? ExpiresAt { get; set; }

        public bool IsActive { get; set; }

        public Guid StoreId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
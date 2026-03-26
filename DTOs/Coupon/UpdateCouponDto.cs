using System.ComponentModel.DataAnnotations;
using onlineStore.Models.Enums;

namespace onlineStore.DTOs.Coupon
{
    public class UpdateCouponDto
    {
        [MaxLength(50)]
        public string? Code { get; set; }

        [MaxLength(200)]
        public string? Description { get; set; }

        public DiscountType? DiscountType { get; set; }

        [Range(typeof(decimal), "0.01", "999999999")]
        public decimal? DiscountValue { get; set; }

        [Range(typeof(decimal), "0", "999999999")]
        public decimal? MinOrderAmount { get; set; }

        [Range(typeof(decimal), "0", "999999999")]
        public decimal? MaxDiscountAmount { get; set; }

        [Range(1, int.MaxValue)]
        public int? UsageLimit { get; set; }

        [Range(1, int.MaxValue)]
        public int? PerUserLimit { get; set; }

        public DateTime? StartsAt { get; set; }
        public DateTime? ExpiresAt { get; set; }

        public bool? IsActive { get; set; }
    }
}
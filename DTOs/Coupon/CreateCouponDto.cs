using System.ComponentModel.DataAnnotations;
using onlineStore.Models.Enums;

namespace onlineStore.DTOs.Coupon
{
    public class CreateCouponDto
    {
        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }

        [Required]
        public DiscountType DiscountType { get; set; }

        [Required]
        [Range(typeof(decimal), "0.01", "999999999")]
        public decimal DiscountValue { get; set; }

        [Range(typeof(decimal), "0", "999999999")]
        public decimal? MinOrderAmount { get; set; }

        [Range(typeof(decimal), "0", "999999999")]
        public decimal? MaxDiscountAmount { get; set; }

        [Range(1, int.MaxValue)]
        public int? UsageLimit { get; set; }

        [Range(1, int.MaxValue)]
        public int? PerUserLimit { get; set; } = 1;

        public DateTime? StartsAt { get; set; }
        public DateTime? ExpiresAt { get; set; }

        public bool IsActive { get; set; } = true;

        [Required]
        public Guid StoreId { get; set; }
    }
}
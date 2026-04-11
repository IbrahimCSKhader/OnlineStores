using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Subscription
{
    public class ChangeStoreSubscriptionDto
    {
        [Required]
        public Guid NewSubscriptionPlanId { get; set; }

        public DateTime? EffectiveFrom { get; set; }

        [Range(0, double.MaxValue)]
        public decimal PaidAmount { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "USD";

        public bool IsAutoRenew { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }
}

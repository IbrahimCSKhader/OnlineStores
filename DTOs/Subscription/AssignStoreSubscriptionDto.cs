using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Subscription
{
    public class AssignStoreSubscriptionDto
    {
        [Required]
        public Guid StoreId { get; set; }

        [Required]
        public Guid SubscriptionPlanId { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [Range(0, double.MaxValue)]
        public decimal PaidAmount { get; set; }

        [Required, MaxLength(10)]
        public string Currency { get; set; } = "USD";

        public bool IsAutoRenew { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }
}

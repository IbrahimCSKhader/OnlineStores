using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using onlineStore.Models.Subscriptions.Enums;

namespace onlineStore.Models.Subscriptions
{
    [Index(nameof(StoreId), nameof(Status))]
    [Index(nameof(StoreId), nameof(StartDate), nameof(EndDate))]
    [Index(nameof(SubscriptionPlanId))]
    public class StoreSubscription : BaseEntity
    {
        public Guid StoreId { get; set; }
        public Store Store { get; set; } = null!;

        public Guid SubscriptionPlanId { get; set; }
        public SubscriptionPlan SubscriptionPlan { get; set; } = null!;

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

        public decimal PaidAmount { get; set; }

        [Required, MaxLength(10)]
        public string Currency { get; set; } = "USD";

        public bool IsAutoRenew { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }
}

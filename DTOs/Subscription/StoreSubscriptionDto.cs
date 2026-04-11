using onlineStore.Models.Subscriptions.Enums;

namespace onlineStore.DTOs.Subscription
{
    public class StoreSubscriptionDto
    {
        public Guid Id { get; set; }
        public Guid StoreId { get; set; }
        public Guid SubscriptionPlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string PlanCode { get; set; } = string.Empty;
        public int? MaxProducts { get; set; }
        public int? MaxImagesPerProduct { get; set; }
        public bool CanUseOffers { get; set; }
        public bool CanUseCoupons { get; set; }
        public bool CanUseAnalytics { get; set; }
        public bool CanUseAdvancedOffers { get; set; }
        public bool CanUseCustomDomain { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public SubscriptionStatus Status { get; set; }
        public decimal PaidAmount { get; set; }
        public string Currency { get; set; } = "USD";
        public bool IsAutoRenew { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

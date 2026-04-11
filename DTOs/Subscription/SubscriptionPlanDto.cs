namespace onlineStore.DTOs.Subscription
{
    public class SubscriptionPlanDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "USD";
        public int? MaxProducts { get; set; }
        public int? MaxImagesPerProduct { get; set; }
        public bool CanUseOffers { get; set; }
        public bool CanUseCoupons { get; set; }
        public bool CanUseAnalytics { get; set; }
        public bool CanUseAdvancedOffers { get; set; }
        public bool CanUseCustomDomain { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

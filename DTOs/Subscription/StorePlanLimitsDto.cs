namespace onlineStore.DTOs.Subscription
{
    public class StorePlanLimitsDto
    {
        public Guid StoreId { get; set; }
        public Guid SubscriptionPlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string PlanCode { get; set; } = string.Empty;
        public int? MaxProducts { get; set; }
        public int? MaxImagesPerProduct { get; set; }
        public bool CanUseOffers { get; set; }
    }
}

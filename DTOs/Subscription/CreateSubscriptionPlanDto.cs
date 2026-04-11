using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Subscription
{
    public class CreateSubscriptionPlanDto
    {
        [Required, MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(60)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(600)]
        public string? Description { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [Required, MaxLength(10)]
        public string Currency { get; set; } = "USD";

        [Range(0, int.MaxValue)]
        public int? MaxProducts { get; set; }

        [Range(0, int.MaxValue)]
        public int? MaxImagesPerProduct { get; set; }

        public bool CanUseOffers { get; set; }
        public bool CanUseCoupons { get; set; }
        public bool CanUseAnalytics { get; set; }
        public bool CanUseAdvancedOffers { get; set; }
        public bool CanUseCustomDomain { get; set; }

        public bool IsActive { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int SortOrder { get; set; }
    }
}

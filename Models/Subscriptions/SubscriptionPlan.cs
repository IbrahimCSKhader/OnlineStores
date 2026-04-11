using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace onlineStore.Models.Subscriptions
{
    [Index(nameof(Code), IsUnique = true)]
    [Index(nameof(IsActive))]
    [Index(nameof(SortOrder))]
    public class SubscriptionPlan : BaseEntity
    {
        [Required, MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(60)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(600)]
        public string? Description { get; set; }

        public decimal Price { get; set; }

        [Required, MaxLength(10)]
        public string Currency { get; set; } = "USD";

        public int? MaxProducts { get; set; }

        public int? MaxImagesPerProduct { get; set; }

        public bool CanUseOffers { get; set; }
        public bool CanUseCoupons { get; set; }
        public bool CanUseAnalytics { get; set; }
        public bool CanUseAdvancedOffers { get; set; }
        public bool CanUseCustomDomain { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; }

        public ICollection<StoreSubscription> StoreSubscriptions { get; set; } = new List<StoreSubscription>();
    }
}

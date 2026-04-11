using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using onlineStore.Models.Offers.Enums;

namespace onlineStore.Models.Offers
{
    [Index(nameof(StoreId), nameof(IsActive))]
    [Index(nameof(StoreId), nameof(StartDate), nameof(EndDate))]
    [Index(nameof(Type))]
    public class Offer : BaseEntity
    {
        public Guid StoreId { get; set; }
        public Store Store { get; set; } = null!;

        [Required, MaxLength(180)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public OfferType Type { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public int Priority { get; set; }

        public decimal? BundlePrice { get; set; }

        public decimal? DiscountPercentage { get; set; }

        public decimal? DiscountAmount { get; set; }

        public bool AppliesToAllProducts { get; set; }

        public ICollection<OfferItem> Items { get; set; } = new List<OfferItem>();
    }
}

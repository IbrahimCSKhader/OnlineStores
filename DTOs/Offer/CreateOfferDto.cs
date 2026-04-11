using System.ComponentModel.DataAnnotations;
using onlineStore.Models.Offers.Enums;

namespace onlineStore.DTOs.Offer
{
    public class CreateOfferDto
    {
        [Required]
        public Guid StoreId { get; set; }

        [Required, MaxLength(180)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public OfferType Type { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        public DateTime? EndDate { get; set; }

        public int Priority { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal? BundlePrice { get; set; }

        [Range(0.01, 100)]
        public decimal? DiscountPercentage { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal? DiscountAmount { get; set; }

        public bool AppliesToAllProducts { get; set; }

        [Required]
        public List<CreateOfferItemDto> Items { get; set; } = new();
    }
}

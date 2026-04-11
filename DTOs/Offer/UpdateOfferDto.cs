using System.ComponentModel.DataAnnotations;
using onlineStore.Models.Offers.Enums;

namespace onlineStore.DTOs.Offer
{
    public class UpdateOfferDto
    {
        [MaxLength(180)]
        public string? Name { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public OfferType? Type { get; set; }

        public bool? IsActive { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public int? Priority { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal? BundlePrice { get; set; }

        [Range(0.01, 100)]
        public decimal? DiscountPercentage { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal? DiscountAmount { get; set; }

        public bool? AppliesToAllProducts { get; set; }

        public List<CreateOfferItemDto>? Items { get; set; }
    }
}

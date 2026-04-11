using onlineStore.Models.Offers.Enums;

namespace onlineStore.DTOs.Offer
{
    public class OfferDto
    {
        public Guid Id { get; set; }
        public Guid StoreId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public OfferType Type { get; set; }
        public bool IsActive { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Priority { get; set; }
        public decimal? BundlePrice { get; set; }
        public decimal? DiscountPercentage { get; set; }
        public decimal? DiscountAmount { get; set; }
        public bool AppliesToAllProducts { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<OfferItemDto> Items { get; set; } = new();
    }
}

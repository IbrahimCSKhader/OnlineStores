namespace onlineStore.DTOs.Cart
{
    public class CartPricingDto
    {
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal FinalTotal { get; set; }
        public List<AppliedOfferDto> AppliedOffers { get; set; } = new();
    }

    public class AppliedOfferDto
    {
        public Guid OfferId { get; set; }
        public string OfferName { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
    }
}

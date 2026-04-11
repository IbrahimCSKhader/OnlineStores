namespace onlineStore.DTOs.Offer
{
    public class OfferItemDto
    {
        public Guid Id { get; set; }
        public Guid OfferId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int RequiredQuantity { get; set; }
    }
}

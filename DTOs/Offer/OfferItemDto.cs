namespace onlineStore.DTOs.Offer
{
    public class OfferItemDto
    {
        public Guid Id { get; set; }
        public Guid OfferId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImageUrl { get; set; }
        public decimal UnitPrice { get; set; }
        public int AvailableStock { get; set; }
        public bool IsAvailable { get; set; }
        public int RequiredQuantity { get; set; }
    }
}

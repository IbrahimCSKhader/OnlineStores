namespace onlineStore.DTOs.Cart
{
    public class CartItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string? ProductThumbnail { get; set; }
        public Guid? VariantId { get; set; }
        public string? VariantName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice => UnitPrice * Quantity;
        public int AvailableStock { get; set; }
    }
}
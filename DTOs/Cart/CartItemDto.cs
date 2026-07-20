namespace onlineStore.DTOs.Cart
{
    public class CartItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductThumbnail { get; set; }
        public Guid? VariantId { get; set; }
        public string? VariantName { get; set; }
        public string? VariantSKU { get; set; }
        public string? VariantImageUrl { get; set; }
        public string? EffectiveVariantImageUrl { get; set; }
        public List<CartItemVariantAttributeDto> VariantAttributes { get; set; } = new();
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice => UnitPrice * Quantity;
        public int AvailableStock { get; set; }
    }

    public class CartItemVariantAttributeDto
    {
        public Guid AttributeValueId { get; set; }
        public Guid AttributeId { get; set; }
        public string AttributeName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}

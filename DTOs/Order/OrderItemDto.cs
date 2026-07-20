namespace onlineStore.DTOs.Order
{
    public class OrderItemDto
    {
        public Guid Id { get; set; }

        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;

        public Guid? VariantId { get; set; }
        public string? VariantName { get; set; }
        public string? VariantSKU { get; set; }
        public string? VariantImageUrl { get; set; }
        public string? EffectiveVariantImageUrl { get; set; }
        public List<OrderItemVariantAttributeDto> VariantAttributes { get; set; } = new();

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class OrderItemVariantAttributeDto
    {
        public Guid AttributeValueId { get; set; }
        public Guid AttributeId { get; set; }
        public string AttributeName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}

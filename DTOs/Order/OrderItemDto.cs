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

        // Snapshot identity survives offer edits/deletion; components remain inventory-bearing rows.
        public Guid? BundleOfferId { get; set; }
        public Guid? BundleGroupId { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(180)]
        public string? BundleName { get; set; }
        public int? BundleQuantity { get; set; }
        public int? BundleItemQuantity { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18,2)")]
        public decimal? BundlePrice { get; set; }

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

namespace onlineStore.DTOs.Order
{
    public class OrderItemDto
    {
        public Guid Id { get; set; }

        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;

        public Guid? VariantId { get; set; }
        public string? VariantName { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}
namespace onlineStore.DTOs.Cart
{
    public class CartDto
    {
        public Guid Id { get; set; }
        public Guid StoreCustomerId { get; set; }
        public Guid StoreId { get; set; }
        public List<CartItemDto> Items { get; set; } = new();
        public decimal TotalAmount => Items.Sum(i => i.TotalPrice);
        public int TotalItems => Items.Sum(i => i.Quantity);
        public DateTime CreatedAt { get; set; }
    }
}

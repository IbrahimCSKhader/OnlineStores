namespace onlineStore.DTOs.Cart
{
    public class CreateCartItemDto
    {
        public Guid StoreId { get; set; }
        public Guid ProductId { get; set; }
        public Guid? VariantId { get; set; }
        public int Quantity { get; set; } = 1;
    }
}
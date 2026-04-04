namespace onlineStore.DTOs.CustomerStore
{
    public class CustomerStoreDto
    {
        public Guid Id { get; set; }
        public Guid StoreId { get; set; }
        public Guid CustomerId { get; set; }

        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;

        public decimal DiscountPercentage { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
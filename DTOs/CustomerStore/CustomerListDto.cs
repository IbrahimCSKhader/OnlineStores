namespace onlineStore.DTOs.CustomerStore
{
    public class CustomerListDto
    {
        public Guid Id { get; set; }
        public Guid StoreId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public decimal DiscountPercentage { get; set; }
        public int PurchasePoints { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

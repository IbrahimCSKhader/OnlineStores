namespace onlineStore.DTOs.Review
{
    public class ReviewDto
    {
        public Guid Id { get; set; }

        public int Rating { get; set; }
        public string? Comment { get; set; }
        public bool IsApproved { get; set; }

        public Guid ProductId { get; set; }
        public Guid StoreCustomerId { get; set; }
        public Guid StoreId { get; set; }

        public string? StoreCustomerFullName { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

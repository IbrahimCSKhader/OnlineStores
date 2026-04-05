namespace onlineStore.DTOs.StoreCustomerAuth
{
    public class StoreCustomerAuthResponseDto
    {
        public bool Success { get; set; }
        public bool RequiresEmailVerification { get; set; }
        public bool IsGuest { get; set; }
        public string? Message { get; set; }
        public string? Token { get; set; }
        public Guid? StoreCustomerId { get; set; }
        public Guid? StoreId { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string AccountType { get; set; } = Security.StoreCustomerClaimTypes.StoreCustomerAccountType;
        public DateTime? ExpiresAt { get; set; }
    }
}

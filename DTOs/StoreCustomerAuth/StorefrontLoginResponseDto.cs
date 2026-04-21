namespace onlineStore.DTOs.StoreCustomerAuth
{
    public class StorefrontLoginResponseDto
    {
        public bool Success { get; set; }
        public bool RequiresEmailVerification { get; set; }
        public bool IsForbidden { get; set; }
        public string? Message { get; set; }
        public string? Token { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string AccountType { get; set; } = string.Empty;
        public IList<string>? Roles { get; set; }
        public Guid? StoreId { get; set; }
        public Guid? StoreCustomerId { get; set; }
        public string Dashboard { get; set; } = string.Empty;
    }
}

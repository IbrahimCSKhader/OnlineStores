namespace onlineStore.DTOs.Auth
{
    public class AuthResponseDto
    {
        public bool Success { get; set; }
        public bool RequiresEmailVerification { get; set; }
        public string? Message { get; set; }
        public string? ErrorCode { get; set; }
        public string? Token { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public Guid? StoreCustomerId { get; set; }
        public string? AccountType { get; set; }
        public IList<string>? Roles { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public Guid? StoreId { get; set; }
        public string? StoreSlug { get; set; }
        public string? RedirectTo { get; set; }
        public string? AuthMode { get; set; }
        public string? SessionScope { get; set; }
        public string? Dashboard { get; set; }
    }
}

namespace onlineStore.DTOs.Auth
{
    public class GoogleAuthDto
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public Guid? StoreId { get; set; }
        public string? StoreSlug { get; set; }
    }
}

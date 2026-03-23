namespace onlineStore.DTOs.Auth
{
    public class AuthResponseDto
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Token { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public IList<string>? Roles { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}

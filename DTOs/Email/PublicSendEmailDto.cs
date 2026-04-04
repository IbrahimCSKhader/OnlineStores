using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Email
{
    public class PublicSendEmailDto
    {
        [Required, EmailAddress]
        public string To { get; set; } = null!;

        [MaxLength(200)]
        public string? Subject { get; set; }

        [Required]
        public string Message { get; set; } = null!;
    }
}

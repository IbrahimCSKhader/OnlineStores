using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Auth
{
    public class VerifyEmailDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [StringLength(10, MinimumLength = 4)]
        public string Code { get; set; } = null!;
    }
}

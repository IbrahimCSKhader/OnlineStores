using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Auth
{
    public class ResendVerificationCodeDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;
    }
}

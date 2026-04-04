using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Auth
{
    public class ForgotPasswordDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;
    }
}

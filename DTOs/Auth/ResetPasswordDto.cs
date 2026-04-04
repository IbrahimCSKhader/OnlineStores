using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Auth
{
    public class ResetPasswordDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [StringLength(10, MinimumLength = 4)]
        public string Code { get; set; } = null!;

        [Required]
        [MinLength(8)]
        public string NewPassword { get; set; } = null!;
    }
}

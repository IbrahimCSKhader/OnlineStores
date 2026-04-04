using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Auth
{
    public class AdminChangeUserPasswordDto
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
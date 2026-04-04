using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Email
{
    public class SendCodeEmailDto
    {
        [Required, EmailAddress]
        public string To { get; set; } = null!;

        public string? FirstName { get; set; }

        [Required]
        [StringLength(10, MinimumLength = 4)]
        public string Code { get; set; } = null!;
    }
}

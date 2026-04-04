using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Email
{
    public class SendEmailDto
    {
        [Required, EmailAddress]
        public string To { get; set; } = null!;

        [Required]
        public string Subject { get; set; } = null!;

        [Required]
        public string HtmlBody { get; set; } = null!;
    }
}

using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Email
{
    public class SendWelcomeEmailDto
    {
        [Required, EmailAddress]
        public string To { get; set; } = null!;

        public string? FirstName { get; set; }
    }
}

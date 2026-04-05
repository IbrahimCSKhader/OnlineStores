using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.StoreCustomerAuth
{
    public class StoreCustomerForgotPasswordDto
    {
        [Required]
        public Guid StoreId { get; set; }

        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = string.Empty;
    }
}

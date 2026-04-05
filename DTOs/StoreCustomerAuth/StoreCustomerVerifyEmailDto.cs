using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.StoreCustomerAuth
{
    public class StoreCustomerVerifyEmailDto
    {
        [Required]
        public Guid StoreId { get; set; }

        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(10, MinimumLength = 4)]
        public string Code { get; set; } = string.Empty;
    }
}

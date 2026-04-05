using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.CustomerStore
{
    public class CreateCustomerStoreDto
    {
        [Required]
        public Guid StoreId { get; set; }

        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Phone { get; set; }

        [Required, MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [Range(0, 100)]
        public decimal DiscountPercentage { get; set; }

        public bool IsActive { get; set; } = true;
    }
}

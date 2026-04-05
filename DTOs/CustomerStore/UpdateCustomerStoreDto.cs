using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.CustomerStore
{
    public class UpdateCustomerStoreDto
    {
        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        [EmailAddress, MaxLength(256)]
        public string? Email { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MinLength(8)]
        public string? Password { get; set; }

        [Range(0, 100)]
        public decimal? DiscountPercentage { get; set; }

        public bool? IsActive { get; set; }
    }
}

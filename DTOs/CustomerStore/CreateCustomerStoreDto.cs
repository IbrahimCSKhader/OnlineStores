using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.CustomerStore
{
    public class CreateCustomerStoreDto
    {
        [Required]
        public Guid StoreId { get; set; }

        [Required]
        public Guid CustomerId { get; set; }

        [Range(0, 100)]
        public decimal DiscountPercentage { get; set; }
    }
}
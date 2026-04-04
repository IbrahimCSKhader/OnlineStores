using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.CustomerStore
{
    public class UpdateCustomerStoreDto
    {
        [Range(0, 100)]
        public decimal? DiscountPercentage { get; set; }

        public bool? IsActive { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.StoreCustomerAuth
{
    public class StoreCustomerGuestSessionDto
    {
        [Required]
        public Guid StoreId { get; set; }
    }
}

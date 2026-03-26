using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Order
{
    public class CreateOrderDto
    {
        [Required]
        public Guid StoreId { get; set; }

        [MaxLength(50)]
        public string? CouponCode { get; set; }

        [MaxLength(1000)]
        public string? CustomerNotes { get; set; }

        [MaxLength(300)]
        public string? DeliveryAddress { get; set; }

        [MaxLength(100)]
        public string? DeliveryCity { get; set; }

        [MaxLength(20)]
        public string? DeliveryPhone { get; set; }
    }
}
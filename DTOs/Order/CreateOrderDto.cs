using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Order
{
    public class CreateOrderDto
    {
        [Required(ErrorMessage = "معرّف المتجر مطلوب")]
        public Guid StoreId { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(50)]
        public string? CouponCode { get; set; }

        [MaxLength(1000)]
        public string? CustomerNotes { get; set; }

        [Required(ErrorMessage = "عنوان التوصيل مطلوب")]
        [MaxLength(300)]
        public string? DeliveryAddress { get; set; }

        [Required(ErrorMessage = "المدينة مطلوبة")]
        [MaxLength(100)]
        public string? DeliveryCity { get; set; }

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [MaxLength(20)]
        public string? DeliveryPhone { get; set; }
    }
}

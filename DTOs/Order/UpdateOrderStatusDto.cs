using System.ComponentModel.DataAnnotations;
using onlineStore.Models.Enums;

namespace onlineStore.DTOs.Order
{
    public class UpdateOrderStatusDto
    {
        [Required]
        public OrderStatus Status { get; set; }

        [MaxLength(1000)]
        public string? StoreNotes { get; set; }
    }
}
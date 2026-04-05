using Microsoft.EntityFrameworkCore;
using onlineStore.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using onlineStore.Models.Discounts;
namespace onlineStore.Models.Orders
{
    [Index(nameof(StoreCustomerId))]
    [Index(nameof(StoreId))]
    [Index(nameof(Status))]
    [Index(nameof(OrderNumber), IsUnique = true)]
    public class Order : BaseEntity
    {
        [Required, MaxLength(50)]
        public string OrderNumber { get; set; } // e.g. "ORD-20240321-0001"

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [MaxLength(1000)]
        public string? CustomerNotes { get; set; }

        [MaxLength(1000)]
        public string? StoreNotes { get; set; } // ملاحظات صاحب المتجر

        // Delivery Info (بدون شحن — بس بنحفظ العنوان)
        [MaxLength(300)]
        public string? DeliveryAddress { get; set; }

        [MaxLength(100)]
        public string? DeliveryCity { get; set; }

        [MaxLength(20)]
        public string? DeliveryPhone { get; set; }

        // Coupon
        public Guid? CouponId { get; set; }
        public Coupon? Coupon { get; set; }

        // FKs
        public Guid StoreCustomerId { get; set; }
        public StoreCustomer StoreCustomer { get; set; } = null!;

        public Guid StoreId { get; set; }
        public Store Store { get; set; } = null!;

        // Navigation
        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
}

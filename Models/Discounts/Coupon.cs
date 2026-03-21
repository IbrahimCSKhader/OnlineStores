using Microsoft.EntityFrameworkCore;
using onlineStore.Models.Enums;
using onlineStore.Models.Orders;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace onlineStore.Models.Discounts
{
    // Models/Discounts/Coupon.cs
    [Index(nameof(Code), IsUnique = true)]
    [Index(nameof(StoreId))]
    public class Coupon : BaseEntity
    {
        [Required, MaxLength(50)]
        public string Code { get; set; } // e.g. "SAVE20"

        [MaxLength(200)]
        public string? Description { get; set; }

        public DiscountType DiscountType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; } // 20 (يعني 20% أو 20$)

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinOrderAmount { get; set; } // أقل مبلغ للطلب

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxDiscountAmount { get; set; } // أقصى خصم (للـ percentage)

        public int? UsageLimit { get; set; } // كم مرة يستخدم إجمالاً
        public int UsageCount { get; set; } = 0;
        public int? PerUserLimit { get; set; } = 1; // كم مرة لنفس المستخدم

        public DateTime? StartsAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;

        public Guid StoreId { get; set; }
        public Store Store { get; set; }

        public ICollection<Order> Orders { get; set; }
    }
}

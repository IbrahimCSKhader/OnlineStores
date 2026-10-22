using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace onlineStore.Models.Orders
{
    [Index(nameof(OrderId))]
    [Index(nameof(ProductId))]
    public class OrderItem : BaseEntity
    {
        [Required, MaxLength(200)]
        public string ProductName { get; set; } // نحفظ الاسم لو المنتج اتحذف

        [MaxLength(100)]
        public string? VariantName { get; set; }

        // Snapshot identity survives offer edits/deletion; components remain inventory-bearing rows.
        public Guid? BundleOfferId { get; set; }
        public Guid? BundleGroupId { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(180)]
        public string? BundleName { get; set; }
        public int? BundleQuantity { get; set; }
        public int? BundleItemQuantity { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18,2)")]
        public decimal? BundlePrice { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        public Guid OrderId { get; set; }
        public Order Order { get; set; }

        public Guid ProductId { get; set; }
        public Product Product { get; set; }

        public Guid? VariantId { get; set; }
        public ProductVariant? Variant { get; set; }
    }
}

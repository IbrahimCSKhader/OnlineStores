using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace onlineStore.Models.CartModels
{
    [Index(nameof(CartId))]
    [Index(nameof(ProductId))]
    public class CartItem : BaseEntity
    {
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
        public decimal UnitPrice { get; set; } // السعر وقت الإضافة للكارت

        [Column(TypeName = "decimal(18,2)")]
        public decimal? BundleLineTotal { get; set; }

        public Guid CartId { get; set; }
        public ShoppingCart Cart { get; set; }

        public Guid ProductId { get; set; }
        public Product Product { get; set; }

        public Guid? VariantId { get; set; }
        public ProductVariant? Variant { get; set; }
    }
}

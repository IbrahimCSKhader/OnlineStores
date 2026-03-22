using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace onlineStore.Models.CartModels
{
    [Index(nameof(CartId))]
    [Index(nameof(ProductId))]
    public class CartItem : BaseEntity
    {
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; } // السعر وقت الإضافة للكارت

        public Guid CartId { get; set; }
        public Cart Cart { get; set; }

        public Guid ProductId { get; set; }
        public Product Product { get; set; }

        public Guid? VariantId { get; set; }
        public ProductVariant? Variant { get; set; }
    }
}

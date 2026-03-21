using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace onlineStore.Models
{
    [Index(nameof(ProductId))]
    [Index(nameof(SKU), IsUnique = true)]
    public class ProductVariant : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } // e.g. "XL / Red"

        [MaxLength(100)]
        public string? SKU { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PriceOverride { get; set; } // If null, use Product.Price

        public int StockQuantity { get; set; } = 0;
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; } = true;

        public Guid ProductId { get; set; }
        public Product Product { get; set; }
    }
}

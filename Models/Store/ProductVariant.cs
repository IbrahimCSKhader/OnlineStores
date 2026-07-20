// Models/ProductVariant.cs
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace onlineStore.Models
{
    [Index(nameof(ProductId))]
    [Index(nameof(ProductId), nameof(IsDefault))]
    [Index(nameof(ProductId), nameof(IsActive))]
    public class ProductVariant : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? SKU { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Price { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CompareAtPrice { get; set; }

        public int StockQuantity { get; set; } = 0;
        public string? ImageUrl { get; set; }
        public bool IsDefault { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = default!;

        public ICollection<ProductVariantAttributeValue> AttributeValues { get; set; } =
            new List<ProductVariantAttributeValue>();
        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    }
}

using Microsoft.EntityFrameworkCore;
using onlineStore.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace onlineStore.Models
{
    [Index(nameof(StoreId))]
    [Index(nameof(CategoryId))]
    [Index(nameof(SectionId))]
    [Index(nameof(StoreId), nameof(Slug), IsUnique = true)]
    [Index(nameof(StoreId), nameof(SKU), IsUnique = true)]
    [Index(nameof(Status))]
    [Index(nameof(Price))]
    public class Product : BaseEntity
    {
        [Required, MaxLength(200)]
        public string Name { get; set; }

        [Required, MaxLength(200)]
        public string Slug { get; set; }
        public int VisitCount { get; set; } = 0;

        [MaxLength(100)]
        public string? SKU { get; set; } 

        [MaxLength(2000)]
        public string? Description { get; set; }

        public string? ShortDescription { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CompareAtPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CostPrice { get; set; }

        public int StockQuantity { get; set; } = 0;
        public bool TrackInventory { get; set; } = true;

        public ProductStatus Status { get; set; } = ProductStatus.Draft;

        public bool IsFeatured { get; set; } = false;
        public double? WeightKg { get; set; }

        public string? ThumbnailUrl { get; set; }

        
        [MaxLength(200)]
        public string? MetaTitle { get; set; }
        [MaxLength(500)]
        public string? MetaDescription { get; set; }

        
        public Guid StoreId { get; set; }
        public Store Store { get; set; }

        public Guid CategoryId { get; set; }
        public Category Category { get; set; }

        public Guid SectionId { get; set; }
        public Section Section { get; set; }

        // Navigation
        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
        public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
        public ICollection<ProductAttributeValue> AttributeValues { get; set; } = new List<ProductAttributeValue>();
    }
}

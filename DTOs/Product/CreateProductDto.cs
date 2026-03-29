// DTOs/Product/CreateProductDto.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace onlineStore.DTOs.Product
{
    public class CreateProductDto
    {
        [Required, MaxLength(200)]
        public string Name { get; set; }

        [Required, MaxLength(200)]
        public string Slug { get; set; }

        [MaxLength(100)]
        public string? SKU { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        public string? ShortDescription { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public decimal? CompareAtPrice { get; set; }  // السعر قبل الخصم
        public decimal? CostPrice { get; set; }       // سعر التكلفة

        public int StockQuantity { get; set; } = 0;
        public bool TrackInventory { get; set; } = true;

        public string? ThumbnailUrl { get; set; }
        public List<IFormFile>? Images { get; set; }

        // SEO
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }

        // FKs
        public Guid StoreId { get; set; }
        public Guid CategoryId { get; set; }
        public Guid SectionId { get; set; }

        // Variants & Attributes
        public List<CreateProductVariantDto>? Variants { get; set; }
        public List<CreateProductAttributeValueDto>? AttributeValues { get; set; }
    }
}
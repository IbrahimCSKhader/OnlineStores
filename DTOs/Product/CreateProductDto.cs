// DTOs/Product/CreateProductDto.cs
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace onlineStore.DTOs.Product
{
    public class CreateProductDto
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Slug { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? SKU { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(500)]
        public string? ShortDescription { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public decimal? CompareAtPrice { get; set; }
        public decimal? CostPrice { get; set; }

        public int StockQuantity { get; set; } = 0;
        public bool TrackInventory { get; set; } = true;

        public string? ThumbnailUrl { get; set; }

        // يدعم أكثر من صورة
        public List<IFormFile>? Images { get; set; }

        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }

        [Required]
        public Guid StoreId { get; set; }

        [Required]
        public Guid CategoryId { get; set; }

        [Required]
        public Guid SectionId { get; set; }

        public List<CreateProductVariantDto>? Variants { get; set; }
        public List<CreateProductAttributeValueDto>? AttributeValues { get; set; }
    }
}
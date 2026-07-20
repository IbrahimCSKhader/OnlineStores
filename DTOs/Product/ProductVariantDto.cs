// DTOs/Product/ProductVariantDto.cs
using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Product
{
    public class ProductVariantDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public decimal EffectivePrice { get; set; }
        public decimal? EffectiveCompareAtPrice { get; set; }
        public int StockQuantity { get; set; }
        public string? ImageUrl { get; set; }
        public string? EffectiveImageUrl { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public List<VariantAttributeValueDto> AttributeValues { get; set; } = new();
        public List<ProductImageDto> Images { get; set; } = new();
    }

    public class VariantAttributeValueDto
    {
        public Guid AttributeValueId { get; set; }
        public Guid AttributeId { get; set; }
        public string AttributeName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class CreateProductVariantDto
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? SKU { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public decimal? Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public int StockQuantity { get; set; } = 0;
        public string? ImageUrl { get; set; }
        public int? SortOrder { get; set; }
        public List<Guid> AttributeValueIds { get; set; } = new();
    }

    public class UpdateProductVariantDto
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? SKU { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public decimal? Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public int? StockQuantity { get; set; }
        public string? ImageUrl { get; set; }
        public int? SortOrder { get; set; }
        public bool? IsDefault { get; set; }
        public bool? IsActive { get; set; }
        public List<Guid>? AttributeValueIds { get; set; }
    }
}

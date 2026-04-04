// DTOs/Product/ProductVariantDto.cs
using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Product
{
    public class ProductVariantDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public decimal? PriceOverride { get; set; }
        public int StockQuantity { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateProductVariantDto
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? SKU { get; set; }
        public decimal? PriceOverride { get; set; }
        public int StockQuantity { get; set; } = 0;
        public string? ImageUrl { get; set; }
    }
}
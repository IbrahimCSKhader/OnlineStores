// DTOs/Product/ProductDto.cs
using onlineStore.Models.Enums;

namespace onlineStore.DTOs.Product
{
    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public string? SKU { get; set; }
        public string? Description { get; set; }
        public string? ShortDescription { get; set; }
        public decimal Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public int StockQuantity { get; set; }
        public bool TrackInventory { get; set; }
        public bool IsInStock => !TrackInventory || StockQuantity > 0;
        public bool HasDiscount => CompareAtPrice.HasValue && CompareAtPrice > Price;
        public decimal? DiscountPercentage => HasDiscount
            ? Math.Round((1 - Price / CompareAtPrice!.Value) * 100, 0)
            : null;
        public string? ThumbnailUrl { get; set; }
        public ProductStatus Status { get; set; }
        public bool IsFeatured { get; set; }
        public Guid StoreId { get; set; }
        public Guid CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public Guid SectionId { get; set; }
        public string? SectionName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int VisitCount { get; set; }

        // Relations
        public List<ProductImageDto>? Images { get; set; }
        public List<ProductVariantDto>? Variants { get; set; }
        public List<ProductAttributeValueDto>? AttributeValues { get; set; }
    }
}
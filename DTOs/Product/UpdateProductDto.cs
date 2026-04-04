// DTOs/Product/UpdateProductDto.cs
using Microsoft.AspNetCore.Http;
using onlineStore.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Product
{
    public class UpdateProductDto
    {
        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(500)]
        public string? ShortDescription { get; set; }

        public decimal? Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public decimal? CostPrice { get; set; }

        public int? StockQuantity { get; set; }
        public bool? TrackInventory { get; set; }

        // إضافة صور جديدة للمنتج
        public List<IFormFile>? Images { get; set; }

        public string? ThumbnailUrl { get; set; }
        public ProductStatus? Status { get; set; }
        public bool? IsFeatured { get; set; }

        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }

        public Guid? CategoryId { get; set; }
        public Guid? SectionId { get; set; }
    }
}
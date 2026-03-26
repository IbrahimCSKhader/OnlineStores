// DTOs/Product/ProductImageDto.cs
using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Product
{
    public class ProductImageDto
    {
        public Guid Id { get; set; }
        public string Url { get; set; }
        public string? AltText { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsPrimary { get; set; }
    }

    public class AddProductImageDto
    {
        [Required]
        public string Url { get; set; }
        public string? AltText { get; set; }
        public int DisplayOrder { get; set; } = 0;
        public bool IsPrimary { get; set; } = false;
        public Guid ProductId { get; set; }
    }
}
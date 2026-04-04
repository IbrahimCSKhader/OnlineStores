// Models/ProductImage.cs
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace onlineStore.Models
{
    [Index(nameof(ProductId))]
    public class ProductImage : BaseEntity
    {
        [Required]
        public string Url { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? AltText { get; set; }

        public int DisplayOrder { get; set; } = 0;
        public bool IsPrimary { get; set; } = false;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = default!;
    }
}
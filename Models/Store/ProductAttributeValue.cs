// Models/ProductAttributeValue.cs
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace onlineStore.Models
{
    [Index(nameof(ProductId))]
    [Index(nameof(AttributeId))]
    public class ProductAttributeValue : BaseEntity
    {
        [Required, MaxLength(200)]
        public string Value { get; set; } = string.Empty;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = default!;

        public Guid AttributeId { get; set; }
        public ProductAttribute Attribute { get; set; } = default!;
    }
}
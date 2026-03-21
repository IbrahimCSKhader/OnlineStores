using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace onlineStore.Models
{
    [Index(nameof(ProductId))]
    [Index(nameof(AttributeId))]
    public class ProductAttributeValue : BaseEntity
    {
        [Required, MaxLength(200)]
        public string Value { get; set; } // e.g. "Red", "XL", "Cotton"

        public Guid ProductId { get; set; }
        public Product Product { get; set; }

        public Guid AttributeId { get; set; }
        public ProductAttribute Attribute { get; set; }
    }
}

// Models/ProductAttribute.cs
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace onlineStore.Models
{
    [Index(nameof(StoreId))]
    public class ProductAttribute : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public Guid StoreId { get; set; }
        public Store Store { get; set; } = default!;

        public ICollection<ProductAttributeValue> Values { get; set; } = new List<ProductAttributeValue>();
    }
}
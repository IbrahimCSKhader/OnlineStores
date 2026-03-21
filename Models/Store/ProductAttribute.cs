using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace onlineStore.Models
{
    [Index(nameof(StoreId))]
    public class ProductAttribute : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } // e.g. "Color"

        public Guid StoreId { get; set; }
        public Store Store { get; set; }

        public ICollection<ProductAttributeValue> Values { get; set; }
    }
}

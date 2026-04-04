using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace onlineStore.Models
{
    [Index(nameof(StoreId))]
    [Index(nameof(Slug))]
    public class Section : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; }

        [Required, MaxLength(100)]
        public string Slug { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public int DisplayOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;


        public Guid StoreId { get; set; }
        public Store Store { get; set; }


        public ICollection<Product> Products { get; set; }
    }
}

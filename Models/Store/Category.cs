using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using onlineStore.Models;
namespace onlineStore.Models
{
    [Index(nameof(StoreId))]
    [Index(nameof(Slug))]
    public class Category : BaseEntity
    {
        [Required, MaxLength(100)]
        public string Name { get; set; }

        [Required, MaxLength(100)]
        public string Slug { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public string? ImageUrl { get; set; }

        public int DisplayOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        public Guid? ParentCategoryId { get; set; }
        public Category? ParentCategory { get; set; }
        public ICollection<Category>? SubCategories { get; set; }

        public Guid StoreId { get; set; }
        public Store Store { get; set; }

        public ICollection<Product> Products { get; set; }
    }
}

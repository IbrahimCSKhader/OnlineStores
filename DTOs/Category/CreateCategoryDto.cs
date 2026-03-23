using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Category
{
    public class CreateCategoryDto
    {
        [Required, MaxLength(100)]
        public string Name { get; set; }

        [Required, MaxLength(100)]
        public string Slug { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public string? ImageUrl { get; set; }
        public int DisplayOrder { get; set; } = 0;
        public Guid? ParentCategoryId { get; set; }
        public Guid StoreId { get; set; }
    }
}
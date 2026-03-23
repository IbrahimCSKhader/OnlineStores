using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Category
{
    public class UpdateCategoryDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public string? ImageUrl { get; set; }
        public int? DisplayOrder { get; set; }
        public bool? IsActive { get; set; }
        public Guid? ParentCategoryId { get; set; }
    }
}
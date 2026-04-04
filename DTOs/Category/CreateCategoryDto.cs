using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Category
{
    public class CreateCategoryDto
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Slug { get; set; }

        public string? Description { get; set; }
        public int DisplayOrder { get; set; } = 0;

        [Required]
        public Guid StoreId { get; set; }
    }
}
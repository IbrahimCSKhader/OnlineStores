using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Section
{
    public class CreateSectionDto
    {
        [Required, MaxLength(100)]
        public string Name { get; set; }

        [Required, MaxLength(100)]
        public string Slug { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public int DisplayOrder { get; set; } = 0;

        [Required]
        public Guid StoreId { get; set; }
    }
}
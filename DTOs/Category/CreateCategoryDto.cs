using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Category
{
    public class CreateCategoryDto
    {
        public List<onlineStore.DTOs.Store.CatalogTranslationDto>? Translations { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Slug { get; set; }

        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public IFormFile? ImageFile { get; set; }
        public int DisplayOrder { get; set; } = 0;

        [Required]
        public Guid StoreId { get; set; }
    }
}

namespace onlineStore.DTOs.Category
{
    public class UpdateCategoryDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public IFormFile? ImageFile { get; set; }
        public int? DisplayOrder { get; set; }
        public bool? IsActive { get; set; }

    }
}

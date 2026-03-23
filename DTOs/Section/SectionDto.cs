namespace onlineStore.DTOs.Section
{
    public class SectionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public Guid StoreId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
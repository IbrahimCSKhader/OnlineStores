using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Store
{
    public class CreateStoreDto
    {
        [Required, MaxLength(100)]
        public string Name { get; set; }

        [Required, MaxLength(100)]
        public string Slug { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? BusinessType { get; set; }

        public string? LogoUrl { get; set; }
        public string? CoverImageUrl { get; set; }

        public string? WhatsAppNumber { get; set; }
        public string? ThemeTemplate { get; set; } = "default";
    }
}

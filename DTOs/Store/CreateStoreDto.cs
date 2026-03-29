using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Store
{
    public class CreateStoreDto
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(100)]
        public string Slug { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? BusinessType { get; set; }

        public IFormFile? Logo { get; set; }

        public IFormFile? CoverPage { get; set; }

        [MaxLength(30)]
        public string? WhatsAppNumber { get; set; }

        [MaxLength(50)]
        public string? ThemeTemplate { get; set; } = "default";

        [Required]
        public Guid OwnerId { get; set; }
    }
}
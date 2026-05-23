using Microsoft.AspNetCore.Http;
using onlineStore.Models;
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

        [MaxLength(255)]
        public string? CustomDomain { get; set; }

        [MaxLength(100)]
        public string? BusinessType { get; set; }

        public IFormFile? Logo { get; set; }

        public IFormFile? CoverPage { get; set; }

        [MaxLength(30)]
        public string? WhatsAppNumber { get; set; }

        [MaxLength(100000)]
        public string? StoreStory { get; set; }

        [MaxLength(1)]
        public string? ThemeTemplate { get; set; } = StoreThemeTemplates.Default;

        public List<StoreContactAccountInputDto>? ContactAccounts { get; set; }

        [Required]
        public Guid OwnerId { get; set; }
    }
}

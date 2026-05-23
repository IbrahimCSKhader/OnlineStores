using onlineStore.Models;
using System.ComponentModel.DataAnnotations;

namespace onlineStore.DTOs.Store
{
    public class UpdateStoreDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? CustomDomain { get; set; }

        [MaxLength(100)]
        public string? BusinessType { get; set; }

        public string? LogoUrl { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? WhatsAppNumber { get; set; }

        [MaxLength(100000)]
        public string? StoreStory { get; set; }

        [MaxLength(1)]
        public string? ThemeTemplate { get; set; }

        public bool? IsActive { get; set; }
        public List<StoreContactAccountInputDto>? ContactAccounts { get; set; }
    }
}

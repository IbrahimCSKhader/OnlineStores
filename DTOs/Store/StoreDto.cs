using onlineStore.Models;

namespace onlineStore.DTOs.Store
{
    public class StoreDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Description { get; set; }
        public string? CustomDomain { get; set; }
        public string? BusinessType { get; set; }
        public string? LogoUrl { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? StoreStory { get; set; }
        public string ThemeTemplate { get; set; } = StoreThemeTemplates.Default;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int VisitCount { get; set; }
        public List<StoreContactAccountDto> ContactAccounts { get; set; } = new();
    }
}

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

        public bool IsCatalog { get; set; } = false;
        public string? LogoUrl { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? StoreStory { get; set; }
        public string ThemeTemplate { get; set; } = StoreThemeTemplates.Default;
        public string Language { get; set; } = StoreLanguages.Arabic;
        public IReadOnlyList<string> SupportedLanguages { get; set; } = StoreLanguages.Supported;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int VisitCount { get; set; }
        public List<StoreDeliveryLocationDto> DeliveryLocations { get; set; } = new();
        public List<StoreContactAccountDto> ContactAccounts { get; set; } = new();
    }
}

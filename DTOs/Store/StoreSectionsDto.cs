using System.ComponentModel.DataAnnotations;
namespace onlineStore.DTOs.Store;
public class StoreSectionsDto
{
    public bool ShowCover { get; set; } = true;
    public bool ShowFeaturedProducts { get; set; } = true;
    public bool PremiumEnabled { get; set; } = true;
    public Guid? PremiumOfferId { get; set; }
    public Dictionary<string, string> OffersHeading { get; set; } = new();
    public Dictionary<Guid, OfferPresentationDto> Offers { get; set; } = new();
}
public class OfferPresentationDto
{
    public List<string> Images { get; set; } = new();
    public Dictionary<string, string> Title { get; set; } = new();
    public Dictionary<string, string> Badge { get; set; } = new();
    public string? TitleColor { get; set; }
}

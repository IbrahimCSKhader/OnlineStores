using System.ComponentModel.DataAnnotations;
namespace onlineStore.DTOs.Store;
public class StoreSectionsDto
{
    public bool ShowLocations { get; set; }
    public List<StoreBranchDto> Locations { get; set; } = new();
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

public class StoreBranchDto
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public string WhatsApp { get; set; } = "";
}

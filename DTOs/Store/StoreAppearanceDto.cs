using System.Text.Json.Serialization;

namespace onlineStore.DTOs.Store;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class StoreAppearanceDto
{
    public StoreSectionsDto? Sections { get; set; }
    public Dictionary<string, string> Light { get; set; } = new();
    public Dictionary<string, string> Dark { get; set; } = new();
}

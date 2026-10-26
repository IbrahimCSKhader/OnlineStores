using System.Text.Json.Serialization;

namespace onlineStore.DTOs.Store;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class StoreAppearanceDto
{
    public Dictionary<string, string> Light { get; set; } = new();
    public Dictionary<string, string> Dark { get; set; } = new();
}

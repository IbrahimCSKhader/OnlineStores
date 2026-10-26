namespace onlineStore.Models;

// One optional row per store. Only explicit overrides are serialized.
public class StoreAppearance
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;
    public string OverridesJson { get; set; } = "{}";
}

using onlineStore.Models;

namespace onlineStore.Tests;

public class StoreThemeTemplatesTests
{
    public static TheoryData<string> SupportedTemplates => new()
    {
        StoreThemeTemplates.Default,
        StoreThemeTemplates.L,
        StoreThemeTemplates.F,
        StoreThemeTemplates.P,
        StoreThemeTemplates.B,
        StoreThemeTemplates.Blue,
        StoreThemeTemplates.Brown,
        StoreThemeTemplates.Purple,
        StoreThemeTemplates.Gold,
        StoreThemeTemplates.DeepGreen,
        StoreThemeTemplates.Red,
    };

    [Theory]
    [MemberData(nameof(SupportedTemplates))]
    public void NormalizeForResponse_PreservesSupportedTemplate(string template)
    {
        Assert.Equal(template, StoreThemeTemplates.NormalizeForResponse(template));
    }

    [Fact]
    public void NormalizeForResponse_NormalizesPersistedValue()
    {
        Assert.Equal(StoreThemeTemplates.Red, StoreThemeTemplates.NormalizeForResponse(" r "));
    }

    [Fact]
    public void NormalizeForResponse_UsesDefaultForMissingValue()
    {
        Assert.Equal(StoreThemeTemplates.Default, StoreThemeTemplates.NormalizeForResponse(null));
        Assert.Equal(StoreThemeTemplates.Default, StoreThemeTemplates.NormalizeForResponse("  "));
    }
}

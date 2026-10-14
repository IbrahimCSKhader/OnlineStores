using onlineStore.Models;

namespace onlineStore.Tests;

public class StoreLanguagesTests
{
    [Theory]
    [InlineData(null, StoreLanguages.Arabic)]
    [InlineData("", StoreLanguages.Arabic)]
    [InlineData("A", StoreLanguages.Arabic)]
    [InlineData("ar", StoreLanguages.Arabic)]
    [InlineData("E", StoreLanguages.English)]
    [InlineData("en", StoreLanguages.English)]
    [InlineData("I", StoreLanguages.Hebrew)]
    [InlineData("he", StoreLanguages.Hebrew)]
    [InlineData("unknown", StoreLanguages.Arabic)]
    public void NormalizeOrDefault_ReturnsSupportedLanguage(string? value, string expected)
    {
        Assert.Equal(expected, StoreLanguages.NormalizeOrDefault(value));
    }
}

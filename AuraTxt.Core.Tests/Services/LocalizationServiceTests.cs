using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class LocalizationServiceTests
{
    [Theory]
    [InlineData("zh", "zh-Hans")]
    [InlineData("ja", "ja")]
    [InlineData("ko", "ko")]
    [InlineData("es", "es")]
    [InlineData("fr", "fr")]
    [InlineData("de", "de")]
    public void Resolve_Auto_MapsKnownOsLanguageToSupportedCode(string osIso, string expected)
        => Assert.Equal(expected, LocalizationService.Resolve("auto", osIso));

    [Theory]
    [InlineData("it")]
    [InlineData("pt")]
    [InlineData("ru")]
    [InlineData("")]
    public void Resolve_Auto_FallsBackToEnglish_ForUnmappedOsLanguage(string osIso)
        => Assert.Equal("en", LocalizationService.Resolve("auto", osIso));

    [Theory]
    [InlineData("en")]
    [InlineData("zh-Hans")]
    [InlineData("ja")]
    [InlineData("ko")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("de")]
    public void Resolve_ExplicitSupportedCode_PassesThrough(string code)
        => Assert.Equal(code, LocalizationService.Resolve(code));

    [Theory]
    [InlineData("zh-Hant")]
    [InlineData("fil")]
    [InlineData("bogus")]
    public void Resolve_ExplicitUnsupportedCode_FallsBackToEnglish(string code)
        => Assert.Equal("en", LocalizationService.Resolve(code));

    [Fact]
    public void Resolve_Auto_WithoutOverride_DoesNotThrow()
    {
        // Smoke test only — the real OS locale varies by machine, so we don't
        // assert a specific result, just that reading CultureInfo.InstalledUICulture
        // and mapping it doesn't throw.
        var result = LocalizationService.Resolve("auto");
        Assert.Contains(result, LocalizationService.SupportedLanguages.Select(l => l.Code));
    }

    [Fact]
    public void SupportedLanguages_ContainsAutoPlusSevenLanguages()
    {
        Assert.Equal(8, LocalizationService.SupportedLanguages.Length);
        Assert.Contains(LocalizationService.SupportedLanguages, l => l.Code == "auto");
        Assert.Contains(LocalizationService.SupportedLanguages, l => l.Code == "en");
        Assert.Contains(LocalizationService.SupportedLanguages, l => l.Code == "zh-Hans");
        Assert.Contains(LocalizationService.SupportedLanguages, l => l.Code == "ja");
        Assert.Contains(LocalizationService.SupportedLanguages, l => l.Code == "ko");
        Assert.Contains(LocalizationService.SupportedLanguages, l => l.Code == "es");
        Assert.Contains(LocalizationService.SupportedLanguages, l => l.Code == "fr");
        Assert.Contains(LocalizationService.SupportedLanguages, l => l.Code == "de");
    }
}

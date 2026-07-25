using System.Globalization;

namespace AuraTxt.Core.Services;

/// Resolves AppSettings.UiLanguage ("auto" or an explicit code) to one of the 8
/// shipped languages, and applies it to the process's UI culture. Pure BCL logic
/// (no WPF dependency) so it's usable — and testable — from Core.
public static class LocalizationService
{
    public static readonly (string Code, string Name)[] SupportedLanguages =
    [
        ("auto",    "Auto"),
        ("en",      "English"),
        ("zh-Hans", "简体中文"),
        ("zh-Hant", "繁體中文"),
        ("ja",      "日本語"),
        ("ko",      "한국어"),
        ("es",      "Español"),
        ("fr",      "Français"),
        ("de",      "Deutsch"),
    ];

    private static readonly HashSet<string> SupportedCodes =
        SupportedLanguages.Where(l => l.Code != "auto").Select(l => l.Code).ToHashSet();

    /// Resolves "auto" against the OS UI language (or osCultureName, if given — a
    /// test seam accepting either a full culture name like "zh-TW" or a bare
    /// two-letter code like "ja"); explicit codes pass through if recognized,
    /// else "en". Chinese needs the script/region, not just the "zh" prefix, to
    /// tell Simplified and Traditional apart — the two-letter ISO code alone is
    /// the same for both.
    public static string Resolve(string uiLanguage, string? osCultureName = null)
    {
        if (uiLanguage != "auto")
            return SupportedCodes.Contains(uiLanguage) ? uiLanguage : "en";

        var name = osCultureName ?? CultureInfo.InstalledUICulture.Name;
        if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return IsTraditionalChinese(name) ? "zh-Hant" : "zh-Hans";

        var iso = name.Length >= 2 ? name[..2].ToLowerInvariant() : name.ToLowerInvariant();
        return iso switch
        {
            "ja" => "ja",
            "ko" => "ko",
            "es" => "es",
            "fr" => "fr",
            "de" => "de",
            _    => "en",
        };
    }

    /// Traditional Chinese: the zh-Hant* script subtag, or the classic
    /// region codes TW/HK/MO. Everything else Chinese (zh-Hans*, zh-CN, zh-SG,
    /// or a bare "zh") defaults to Simplified.
    private static bool IsTraditionalChinese(string cultureName)
    {
        var n = cultureName.ToLowerInvariant();
        return n.Contains("hant") || n.EndsWith("-tw") || n.EndsWith("-hk") || n.EndsWith("-mo");
    }

    /// Sets CurrentUICulture (+ DefaultThreadCurrentUICulture for any non-UI
    /// thread that touches Strings) to the resolved language. Call before
    /// constructing any window so its x:Static bindings pick up the right
    /// resource set — re-reads OS locale each call, so a second "auto" call
    /// after CurrentUICulture has already been overridden still resolves
    /// against the real OS setting, not its own prior output.
    public static void Apply(string uiLanguage)
    {
        var culture = new CultureInfo(Resolve(uiLanguage));
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}

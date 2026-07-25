using System.Globalization;

namespace AuraTxt.Core.Services;

/// Resolves AppSettings.UiLanguage ("auto" or an explicit code) to one of the 7
/// shipped languages, and applies it to the process's UI culture. Pure BCL logic
/// (no WPF dependency) so it's usable — and testable — from Core.
public static class LocalizationService
{
    public static readonly (string Code, string Name)[] SupportedLanguages =
    [
        ("auto",    "Auto"),
        ("en",      "English"),
        ("zh-Hans", "简体中文"),
        ("ja",      "日本語"),
        ("ko",      "한국어"),
        ("es",      "Español"),
        ("fr",      "Français"),
        ("de",      "Deutsch"),
    ];

    private static readonly HashSet<string> SupportedCodes =
        SupportedLanguages.Where(l => l.Code != "auto").Select(l => l.Code).ToHashSet();

    /// Resolves "auto" against the OS UI language (or osTwoLetterIso, if given —
    /// a test seam); explicit codes pass through if recognized, else "en".
    public static string Resolve(string uiLanguage, string? osTwoLetterIso = null)
    {
        if (uiLanguage != "auto")
            return SupportedCodes.Contains(uiLanguage) ? uiLanguage : "en";

        var iso = osTwoLetterIso ?? CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
        return iso switch
        {
            "zh" => "zh-Hans",
            "ja" => "ja",
            "ko" => "ko",
            "es" => "es",
            "fr" => "fr",
            "de" => "de",
            _    => "en",
        };
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

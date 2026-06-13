using System.Text.Json;
using AuraTxt.Core.Models;

namespace AuraTxt.Core.Services;

public static class ThemeService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string ThemesDir => Path.Combine(AppContext.BaseDirectory, "themes");

    /// <summary>
    /// Creates the themes/ directory and default light.json / dark.json if they don't exist.
    /// Never overwrites existing files — user edits are safe.
    /// </summary>
    public static void EnsureScaffold()
    {
        Directory.CreateDirectory(ThemesDir);

        WriteIfMissing("light.json", BuiltinLight());
        WriteIfMissing("dark.json",  BuiltinDark());
    }

    /// <summary>Lists all .json theme files in the themes directory.</summary>
    public static List<ThemeMeta> ListThemes()
    {
        if (!Directory.Exists(ThemesDir)) return new();

        return Directory.GetFiles(ThemesDir, "*.json")
            .Select(f =>
            {
                var id = Path.GetFileNameWithoutExtension(f);
                try
                {
                    var tf = JsonSerializer.Deserialize<ThemeFile>(File.ReadAllText(f), JsonOpts);
                    return new ThemeMeta(id, tf?.Name ?? id, tf?.Description ?? "");
                }
                catch { return new ThemeMeta(id, id, ""); }
            })
            .OrderBy(m => m.Id)
            .ToList();
    }

    /// <summary>Loads a theme JSON file. Missing keys are backfilled from built-in defaults.</summary>
    public static ThemeFile LoadTheme(string themeId)
    {
        var path = Path.Combine(ThemesDir, $"{themeId}.json");
        ThemeFile file;
        if (File.Exists(path))
        {
            try { file = JsonSerializer.Deserialize<ThemeFile>(File.ReadAllText(path), JsonOpts)!; }
            catch { file = new ThemeFile(); }
        }
        else
        {
            file = new ThemeFile();
        }

        // Merge with built-in fallback so missing keys don't break the UI.
        // A theme can declare BaseTheme:"dark" to get dark fallbacks (third-party dark themes).
        var baseId = string.IsNullOrEmpty(file.BaseTheme) ? themeId : file.BaseTheme;
        var fallback = baseId.Equals("dark", StringComparison.OrdinalIgnoreCase)
            ? BuiltinDark() : BuiltinLight();
        foreach (var kv in fallback.Colors)
        {
            if (!file.Colors.ContainsKey(kv.Key))
                file.Colors[kv.Key] = kv.Value;
        }
        return file;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Built-in defaults (fallback when theme files are missing / broken)
    // ═══════════════════════════════════════════════════════════════════

    private static ThemeFile BuiltinLight() => new()
    {
        Name = "Light",
        Description = "Windows 11 Fluent Light theme",
        Colors = new Dictionary<string, string>
        {
            ["SurfaceFill"]      = "#F3F6F9",
            ["SurfaceStroke"]    = "#CCE0F5",
            ["SurfaceElevated"]  = "#F3F6F9",
            ["TitleBarFill"]     = "#E6EEF7",
            ["TextPrimary"]      = "#111111",
            ["TextSecondary"]    = "#555555",
            ["TextTertiary"]     = "#808080",
            ["BtnFill"]          = "#F3F6F9",
            ["BtnFillHover"]     = "#E6EEF7",
            ["BtnFillPressed"]   = "#D5D5D5",
            ["BtnStroke"]        = "#CCE0F5",
            ["Accent"]           = "#6366F1",
            ["AccentHover"]      = "#4F46E5",
            ["InputFill"]        = "#F3F6F9",
            ["InputStroke"]      = "#CCE0F5",
            ["Divider"]          = "#CCE0F5",
            ["CloseBtn"]         = "#FF5F57",
            ["CopyBtn"]          = "#6366F1",
            ["SendBtn"]          = "#22C55E",
            ["MenuBtnFill"]      = "#00FFFFFF",
            ["MenuBtnFillHover"] = "#E6EEF7",
            ["IconBtnFill"]      = "#F3F6F9",
            ["IconBtnFillHover"] = "#E6EEF7",
            ["IconBtnStroke"]    = "#CCE0F5",
            ["UserInputFill"]    = "#F3F6F9",
            ["UserInputStroke"]  = "#CCE0F5",
            ["CmbFill"]          = "#FFFFFF",
            ["CmbStroke"]        = "#CCE0F5",
            ["CmbHighlight"]     = "#DBEAFE",
            ["CmbHighlightText"] = "#1E40AF",
            ["MenuSurfaceFill"]  = "#F3F6F9",
            ["PickerBgFill"]     = "#FFFFFF",
            ["PickerFgFill"]     = "#111111",
            ["ShadowOpacity"]    = "0.08",
        }
    };

    private static ThemeFile BuiltinDark() => new()
    {
        Name = "Dark",
        Description = "Windows 11 Fluent Dark theme",
        Colors = new Dictionary<string, string>
        {
            ["SurfaceFill"]      = "#F9F9F9",
            ["SurfaceStroke"]    = "#E0E0E0",
            ["SurfaceElevated"]  = "#1E1E1E",
            ["TitleBarFill"]     = "#2A2A2A",
            ["TextPrimary"]      = "#E2E8F0",
            ["TextSecondary"]    = "#888888",
            ["TextTertiary"]     = "#64748B",
            ["BtnFill"]          = "#2D2D2D",
            ["BtnFillHover"]     = "#3D3D3D",
            ["BtnFillPressed"]   = "#4D4D4D",
            ["BtnStroke"]        = "#3D3D3D",
            ["Accent"]           = "#818CF8",
            ["AccentHover"]      = "#A5B4FC",
            ["InputFill"]        = "#0F172A",
            ["InputStroke"]      = "#334155",
            ["Divider"]          = "#E5E5E5",
            ["CloseBtn"]         = "#FF5F57",
            ["CopyBtn"]          = "#818CF8",
            ["SendBtn"]          = "#22C55E",
            ["MenuBtnFill"]      = "#00FFFFFF",
            ["MenuBtnFillHover"] = "#F3F3F3",
            ["IconBtnFill"]      = "#2D2D2D",
            ["IconBtnFillHover"] = "#3D3D3D",
            ["IconBtnStroke"]    = "#3D3D3D",
            ["UserInputFill"]    = "#181818",
            ["UserInputStroke"]  = "#334155",
            ["CmbFill"]          = "#FFFFFF",
            ["CmbStroke"]        = "#3D3D3D",
            ["CmbHighlight"]     = "#DBEAFE",
            ["CmbHighlightText"] = "#1E40AF",
            ["MenuSurfaceFill"]  = "#F3F3F3",
            ["PickerBgFill"]     = "#FFFFFF",
            ["PickerFgFill"]     = "#1A1A1A",
            ["ShadowOpacity"]    = "0.08",
        }
    };

    private static void WriteIfMissing(string fileName, ThemeFile content)
    {
        var path = Path.Combine(ThemesDir, fileName);
        if (!File.Exists(path))
            File.WriteAllText(path, JsonSerializer.Serialize(content, JsonOpts));
    }
}

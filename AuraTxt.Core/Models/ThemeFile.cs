namespace AuraTxt.Core.Models;

public class ThemeFile
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    /// Which built-in palette ("light" or "dark") backfills missing color keys.
    /// Lets dark third-party themes get dark fallbacks instead of light ones.
    public string BaseTheme { get; set; } = "";

    public Dictionary<string, string> Colors { get; set; } = new();
}

public record ThemeMeta(string Id, string Name, string Description);

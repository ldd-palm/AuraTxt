namespace AuraTxt.Core.Models;

public class ThemeFile
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Dictionary<string, string> Colors { get; set; } = new();
}

public record ThemeMeta(string Id, string Name, string Description);

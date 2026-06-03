using System.IO;
using System.Net.Http;

namespace AuraTxt.Core.Services;

public static class IconDownloadService
{
    public static readonly string CacheDir = Path.Combine(AppContext.BaseDirectory, "icons");

    private static readonly HttpClient Http = new();

    /// Downloads the Lucide icon SVG to the local cache directory.
    /// Returns true if the file is now available (already existed or just downloaded).
    public static async Task<bool> EnsureDownloadedAsync(string lucideName)
    {
        if (string.IsNullOrWhiteSpace(lucideName)) return false;

        var path = Path.Combine(CacheDir, $"{lucideName}.svg");
        if (File.Exists(path)) return true;

        try
        {
            Directory.CreateDirectory(CacheDir);
            var url = $"https://raw.githubusercontent.com/lucide-icons/lucide/main/icons/{lucideName}.svg";
            var svg = await Http.GetStringAsync(url);
            if (svg.TrimStart().StartsWith("404") || svg.Contains("Not Found"))
                return false;
            await File.WriteAllTextAsync(path, svg);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

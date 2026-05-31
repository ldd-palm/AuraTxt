using System.IO;
using System.Net.Http;
using System.Windows.Media;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace AuraTxt.Services;

public static class IconCacheService
{
    private static readonly string CacheDir =
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AuraTxt", "icons");

    private static readonly HttpClient Http = new();

    public static async Task<DrawingImage?> GetIconAsync(string lucideName)
    {
        if (string.IsNullOrWhiteSpace(lucideName)) return null;
        Directory.CreateDirectory(CacheDir);
        var path = System.IO.Path.Combine(CacheDir, $"{lucideName}.svg");

        if (!File.Exists(path))
        {
            try
            {
                var url = $"https://raw.githubusercontent.com/lucide-icons/lucide/main/icons/{lucideName}.svg";
                var svg = await Http.GetStringAsync(url);
                await File.WriteAllTextAsync(path, svg);
            }
            catch { return null; }
        }

        try
        {
            var settings = new WpfDrawingSettings { IncludeRuntime = true };
            using var converter = new FileSvgConverter(settings);
            var success = converter.Convert(path);
            if (!success || converter.Drawing is null) return null;
            return new DrawingImage(converter.Drawing);
        }
        catch { return null; }
    }
}

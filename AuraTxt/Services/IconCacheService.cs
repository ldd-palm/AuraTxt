using System.IO;
using System.Windows;
using System.Windows.Media;
using AuraTxt.Core.Services;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace AuraTxt.Services;

public static class IconCacheService
{
    private static readonly string CacheDir = Path.Combine(AppContext.BaseDirectory, "icons");

    private static readonly Dictionary<string, DrawingImage?> MemCache = new();

    /// Synchronous icon load — never blocks on network. Returns null if icon not yet available.
    public static DrawingImage? GetIconSync(string lucideName)
    {
        if (string.IsNullOrWhiteSpace(lucideName)) return null;
        if (MemCache.TryGetValue(lucideName, out var cached)) return cached;

        // Ensure bundled icons have been extracted to cache dir
        EnsureBundledExtracted(lucideName);

        var path = Path.Combine(CacheDir, $"{lucideName}.svg");
        if (!File.Exists(path))
        {
            MemCache[lucideName] = null;
            return null;
        }

        try
        {
            var settings = new WpfDrawingSettings { IncludeRuntime = true };
            using var converter = new FileSvgConverter(settings);
            var ok = converter.Convert(path);
            var img = ok && converter.Drawing is not null ? new DrawingImage(converter.Drawing) : null;
            MemCache[lucideName] = img;
            return img;
        }
        catch
        {
            MemCache[lucideName] = null;
            return null;
        }
    }

    /// For icons not bundled — download in background so next open shows icon.
    public static void DownloadInBackground(string lucideName)
    {
        if (string.IsNullOrWhiteSpace(lucideName)) return;
        if (File.Exists(Path.Combine(CacheDir, $"{lucideName}.svg"))) return;

        _ = Task.Run(async () =>
        {
            var ok = await IconDownloadService.EnsureDownloadedAsync(lucideName);
            if (ok) lock (MemCache) { MemCache.Remove(lucideName); }
        });
    }

    /// Copy embedded SVG resources to cache dir on first use (so FileSvgConverter can load them).
    private static readonly HashSet<string> _extracted = new();

    private static void EnsureBundledExtracted(string lucideName)
    {
        if (_extracted.Contains(lucideName)) return;
        _extracted.Add(lucideName);

        var destPath = Path.Combine(CacheDir, $"{lucideName}.svg");
        if (File.Exists(destPath)) return;

        var uri = new Uri($"pack://application:,,,/Resources/icons/{lucideName}.svg", UriKind.Absolute);
        try
        {
            var info = Application.GetResourceStream(uri);
            if (info is null) return;

            Directory.CreateDirectory(CacheDir);
            using var src = info.Stream;
            using var dst = File.Create(destPath);
            src.CopyTo(dst);
        }
        catch { }
    }
}

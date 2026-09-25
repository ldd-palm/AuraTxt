using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AuraTxt.Core.Services;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace AuraTxt.Services;

public static class IconCacheService
{
    private static readonly string CacheDir = Path.Combine(AppContext.BaseDirectory, "icons");

    // ConcurrentDictionary: GetIconSync writes on the UI thread while the background
    // download task removes entries — a plain Dictionary would corrupt under that race.
    // Keyed by "{lucideName}|{colorHex}" (see GetIconSync) — the same icon can be cached
    // in more than one color across a theme switch, so the color is part of the identity.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DrawingImage?> MemCache = new();

    /// Synchronous icon load — never blocks on network. Returns null if icon not yet available.
    public static DrawingImage? GetIconSync(string lucideName)
    {
        if (string.IsNullOrWhiteSpace(lucideName)) return null;

        var colorHex = CurrentIconColorHex();
        var cacheKey = $"{lucideName}|{colorHex}";
        if (MemCache.TryGetValue(cacheKey, out var cached)) return cached;

        // Ensure bundled icons have been extracted to cache dir
        EnsureBundledExtracted(lucideName);

        var path = Path.Combine(CacheDir, $"{lucideName}.svg");
        if (!File.Exists(path))
        {
            MemCache[cacheKey] = null;
            return null;
        }

        try
        {
            // Lucide SVGs are monochrome line art: fill="none" stroke="currentColor" — no
            // background, color meant to inherit from context. SharpVectors doesn't resolve
            // CSS currentColor, so left alone every icon rendered at the SVG spec's default
            // (black) regardless of theme — invisible against a dark theme's dark surfaces.
            // Substitute the active theme's text color before parsing so icons stay legible
            // in both themes; done via a throwaway temp file since FileSvgConverter only
            // takes a path, not SVG text directly.
            var svg = File.ReadAllText(path).Replace("currentColor", colorHex, StringComparison.OrdinalIgnoreCase);
            var tempPath = Path.Combine(Path.GetTempPath(), $"auratxt_icon_{Guid.NewGuid():N}.svg");
            File.WriteAllText(tempPath, svg);

            var settings = new WpfDrawingSettings { IncludeRuntime = true };
            using var converter = new FileSvgConverter(settings);
            var ok = converter.Convert(tempPath);
            var img = ok && converter.Drawing is not null ? new DrawingImage(converter.Drawing) : null;
            MemCache[cacheKey] = img;
            try { File.Delete(tempPath); } catch { }
            return img;
        }
        catch
        {
            MemCache[cacheKey] = null;
            return null;
        }
    }

    /// TextPrimary (rather than a dedicated icon color) so icons read at the same visual
    /// weight as text and automatically follow whatever the active theme resolves it to —
    /// no separate icon-color key to keep in sync across theme files.
    private static string CurrentIconColorHex()
    {
        if (Application.Current?.Resources["TextPrimary"] is SolidColorBrush brush)
            return $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
        return "#111111"; // matches Light theme's TextPrimary
    }

    /// For icons not bundled — download in background so next open shows icon.
    public static void DownloadInBackground(string lucideName)
    {
        if (string.IsNullOrWhiteSpace(lucideName)) return;
        if (File.Exists(Path.Combine(CacheDir, $"{lucideName}.svg"))) return;

        _ = Task.Run(async () =>
        {
            var ok = await IconDownloadService.EnsureDownloadedAsync(lucideName);
            if (!ok) return;
            // Invalidate every color variant cached under the old "file doesn't exist" miss.
            foreach (var key in MemCache.Keys.Where(k => k.StartsWith(lucideName + "|", StringComparison.Ordinal)).ToList())
                MemCache.TryRemove(key, out _);
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

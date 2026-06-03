using System.IO;

namespace AuraTxt.Core.Services;

/// Simple file logger for diagnostics. Off by default; enable with --log flag.
public static class LogService
{
    public static bool Enabled { get; set; }
    public static string? LogPath { get; set; }

    private static readonly object _lock = new();

    public static void Info(string message)
    {
        if (!Enabled || LogPath is null) return;
        Write($"[INFO] {Timestamp}  {message}");
    }

    public static void Error(string message, Exception? ex = null)
    {
        if (!Enabled || LogPath is null) return;
        var text = $"[ERROR] {Timestamp}  {message}";
        if (ex is not null)
            text += $"\n  → {ex.GetType().Name}: {ex.Message}";
        if (ex?.InnerException is not null)
            text += $"\n     inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
        Write(text);
    }

    public static void Raw(string content)
    {
        if (!Enabled || LogPath is null) return;
        Write(content);
    }

    private static void Write(string entry)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (dir is not null) Directory.CreateDirectory(dir);
            lock (_lock)
                File.AppendAllText(LogPath!, entry + "\n");
        }
        catch { /* must never break the app */ }
    }

    private static string Timestamp => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
}

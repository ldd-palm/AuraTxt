using System.Runtime.InteropServices;
using AuraTxt.Core.Models;

namespace AuraTxt.Services;

/// Decides whether the foreground window should suppress AuraTxt's selection-capture —
/// primarily so a game isn't sent a synthetic Ctrl+C (see ClipboardService) that collides
/// with its own keybinds. Two independent checks: an explicit process-name list, and an
/// exclusive-fullscreen heuristic (whole-monitor window with no title bar).
public static class GameDetectionService
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    private const int GWL_STYLE               = -16;
    private const int WS_CAPTION               = 0x00C00000;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    public static bool ShouldSkip(AppSettings settings)
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        if (IsIgnoredProcess(hwnd, settings.IgnoredProcesses)) return true;
        if (settings.PauseOnFullscreenApp && IsExclusiveFullscreen(hwnd)) return true;
        return false;
    }

    private static bool IsIgnoredProcess(IntPtr hwnd, string ignoredProcesses)
    {
        if (string.IsNullOrWhiteSpace(ignoredProcesses)) return false;
        try
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
            var name = proc.ProcessName; // already without .exe

            return ignoredProcesses
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(p => string.Equals(TrimExeSuffix(p), name, StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    private static string TrimExeSuffix(string s) =>
        s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? s[..^4] : s;

    private static bool IsExclusiveFullscreen(IntPtr hwnd)
    {
        try
        {
            if (!GetWindowRect(hwnd, out var rect)) return false;

            // A visible title bar means this is an ordinary (even if maximized) window.
            if ((GetWindowLong(hwnd, GWL_STYLE) & WS_CAPTION) != 0) return false;

            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return false;
            var mi = new MonitorInfo { cbSize = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref mi)) return false;

            return rect.Left <= mi.rcMonitor.Left && rect.Top <= mi.rcMonitor.Top &&
                   rect.Right >= mi.rcMonitor.Right && rect.Bottom >= mi.rcMonitor.Bottom;
        }
        catch { return false; }
    }
}

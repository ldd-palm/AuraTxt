using Microsoft.Win32;

namespace AuraTxt.Core.Services;

/// Manages the HKCU Run registry key that makes AuraTxt launch at Windows logon.
/// Registry-based rather than a Startup-folder shortcut — no admin rights
/// needed, one value add/remove per toggle, no COM interop for .lnk creation.
/// This is the one place AuraTxt writes outside its own portable folder; if the
/// folder is moved, the stale path left behind only gets corrected the next
/// time Apply() runs (startup, or Reload Settings) with the new exePath.
public static class StartupService
{
    internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string ValueName  = "AuraTxt";

    /// Syncs the actual registry state to match `enabled`, pointing at exePath
    /// (quoted, since portable install paths may contain spaces). Safe to call
    /// on every startup/reload — a no-op if the value already matches.
    public static void Apply(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null) return;   // shouldn't happen for HKCU, but don't crash startup over it

        if (enabled)
            key.SetValue(ValueName, $"\"{exePath}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

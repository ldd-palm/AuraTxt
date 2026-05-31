using AuraTxt.Core.Constants;
using AuraTxt.Core.Models;

namespace AuraTxt.Core.Services;

public enum HotkeyValidationResult { Valid, InvalidFormat, SystemReserved, Conflict }

public class HotkeyValidator
{
    private static readonly HashSet<string> Modifiers =
        new(StringComparer.OrdinalIgnoreCase) { "Ctrl", "Alt", "Shift", "Win" };

    private static readonly HashSet<string> Keys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "A","B","C","D","E","F","G","H","I","J","K","L","M",
            "N","O","P","Q","R","S","T","U","V","W","X","Y","Z",
            "F1","F2","F3","F4","F5","F6","F7","F8","F9","F10","F11","F12",
            "0","1","2","3","4","5","6","7","8","9",
            "Space","Tab","Enter","Escape","Delete","Insert",
            "Home","End","PageUp","PageDown","Left","Right","Up","Down"
        };

    public bool IsValidFormat(string hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey)) return false;
        var parts = hotkey.Split('+');
        if (parts.Length < 2) return false;
        var key  = parts[^1].Trim();
        var mods = parts[..^1].Select(p => p.Trim()).ToArray();
        return mods.Length > 0
            && mods.All(m => Modifiers.Contains(m))
            && Keys.Contains(key);
    }

    public (HotkeyValidationResult result, string? conflictName) Validate(
        string hotkey, IEnumerable<ActionItem> existing, string? excludeId = null)
    {
        if (!IsValidFormat(hotkey))
            return (HotkeyValidationResult.InvalidFormat, null);

        if (SystemKeys.Reserved.Contains(hotkey))
            return (HotkeyValidationResult.SystemReserved, null);

        var conflict = existing
            .Where(a => a.Id != excludeId)
            .FirstOrDefault(a =>
                string.Equals(a.Hotkey, hotkey, StringComparison.OrdinalIgnoreCase));

        return conflict is null
            ? (HotkeyValidationResult.Valid, null)
            : (HotkeyValidationResult.Conflict, conflict.Name);
    }
}

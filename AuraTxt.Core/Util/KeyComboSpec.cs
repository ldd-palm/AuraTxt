namespace AuraTxt.Core.Util;

/// Parsed representation of a modifier+key combo string like "F1" or "Ctrl+F1"
/// (AppSettings.ForcePopupDoubleTapKey). The base key is kept as a raw token string —
/// this project doesn't reference System.Windows.Forms, so callers that own a Keys enum
/// (AuraTxt, AuraTxt.Cli) resolve/validate that token against it themselves.
public readonly record struct KeyComboSpec(bool Ctrl, bool Alt, bool Shift, string Key)
{
    public bool IsEmpty => string.IsNullOrEmpty(Key);

    public override string ToString()
    {
        if (IsEmpty) return "None";
        return (Ctrl ? "Ctrl+" : "") + (Alt ? "Alt+" : "") + (Shift ? "Shift+" : "") + Key;
    }

    /// Parses "F1", "Ctrl+F1", "Ctrl+Alt+F2", etc., case-insensitively. Blank/whitespace
    /// input, or the literal word "none", parses to the empty (disabled) combo. Returns
    /// null only for malformed input — no base key, or more than one non-modifier token —
    /// the base key token itself is not checked against a real key name here.
    public static KeyComboSpec? TryParse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
            return new KeyComboSpec(false, false, false, "");

        bool ctrl = false, alt = false, shift = false;
        string? key = null;
        foreach (var raw in input.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || raw.Equals("Control", StringComparison.OrdinalIgnoreCase)) ctrl = true;
            else if (raw.Equals("Alt", StringComparison.OrdinalIgnoreCase)) alt = true;
            else if (raw.Equals("Shift", StringComparison.OrdinalIgnoreCase)) shift = true;
            else if (key is null) key = raw;
            else return null; // a second non-modifier token — invalid
        }
        return key is null ? null : new KeyComboSpec(ctrl, alt, shift, key);
    }
}

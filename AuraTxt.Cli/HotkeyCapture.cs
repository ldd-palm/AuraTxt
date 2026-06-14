using System.Text;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli;

/// Reads a hotkey as a typed string (e.g. "Alt+T"). CLI best practice — key capture
/// via Console.ReadKey is fragile for combos and breaks over SSH/odd terminals.
public static class HotkeyCapture
{
    /// Returns a normalized hotkey string like "Alt+T", "" if blank, or null if Esc was pressed.
    public static string? Capture(IEnumerable<ActionItem> actions, string? excludeId = null)
    {
        var validator  = new HotkeyValidator();
        var actionList = actions.ToList();

        while (true)
        {
            Console.Write("  Hotkey (e.g. Alt+T, Ctrl+Shift+R — blank to skip, Esc to cancel): ");
            var sb      = new StringBuilder();
            bool escaped = false;
            while (true)
            {
                var ki = Console.ReadKey(intercept: true);
                if (ki.Key == ConsoleKey.Escape)   { Console.WriteLine(); escaped = true; break; }
                if (ki.Key == ConsoleKey.Enter)     { Console.WriteLine(); break; }
                if (ki.Key == ConsoleKey.Backspace && sb.Length > 0) { sb.Remove(sb.Length - 1, 1); Console.Write("\b \b"); }
                else if (!char.IsControl(ki.KeyChar) && ki.KeyChar != '\0') { sb.Append(ki.KeyChar); Console.Write(ki.KeyChar); }
            }
            if (escaped) return null;

            var input = sb.ToString().Trim();
            if (input.Length == 0)
            {
                WriteGray("  (no hotkey assigned)");
                return "";
            }

            var hotkey = Normalize(input);
            var (res, conflict) = validator.Validate(hotkey, actionList, excludeId);

            switch (res)
            {
                case HotkeyValidationResult.InvalidFormat:
                    WriteError("  Invalid format. Use Modifier+Key, e.g. Alt+T, Ctrl+Shift+R.");
                    continue;
                case HotkeyValidationResult.SystemReserved:
                    WriteError($"  \"{hotkey}\" is a system-reserved key. Try another.");
                    continue;
                case HotkeyValidationResult.Conflict:
                    WriteError($"  \"{hotkey}\" conflicts with action \"{conflict}\". Try another.");
                    continue;
                default:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  ✓ {hotkey}");
                    Console.ResetColor();
                    return hotkey;
            }
        }
    }

    /// Title-cases each segment so "alt+t" → "Alt+T", "ctrl+shift+f12" → "Ctrl+Shift+F12".
    private static string Normalize(string hotkey) =>
        string.Join("+", hotkey.Split('+')
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Select(p => char.ToUpper(p[0]) + p[1..].ToLower()));

    private static void WriteError(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(msg);
        Console.ResetColor();
    }

    private static void WriteGray(string msg)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(msg);
        Console.ResetColor();
    }
}

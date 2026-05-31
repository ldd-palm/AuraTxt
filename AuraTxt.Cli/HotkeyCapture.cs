using AuraTxt.Core.Constants;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli;

public static class HotkeyCapture
{
    /// Returns a hotkey string like "Alt+T", or "" if user pressed ESC to skip.
    public static string Capture(IEnumerable<ActionItem> actions, string? excludeId = null)
    {
        var validator  = new HotkeyValidator();
        var actionList = actions.ToList();

        while (true)
        {
            Console.Write("  Press shortcut key (ESC to skip — optional): ");
            var info = Console.ReadKey(intercept: true);
            Console.WriteLine();

            if (info.Key == ConsoleKey.Escape)
            {
                WriteGray("  (Skipped — no hotkey assigned)");
                return "";
            }

            var mods = new List<string>();
            if ((info.Modifiers & ConsoleModifiers.Control) != 0) mods.Add("Ctrl");
            if ((info.Modifiers & ConsoleModifiers.Alt)     != 0) mods.Add("Alt");
            if ((info.Modifiers & ConsoleModifiers.Shift)   != 0) mods.Add("Shift");

            if (mods.Count == 0)
            {
                WriteError("  Modifier required (Ctrl/Alt/Shift). Try again or ESC to skip.");
                continue;
            }

            var keyName = MapKey(info.Key);
            if (keyName is null)
            {
                WriteError("  Unsupported key. Use A-Z, 0-9, or F1-F12. Try again or ESC to skip.");
                continue;
            }

            var hotkey = string.Join("+", mods) + "+" + keyName;

            if (SystemKeys.Reserved.Contains(hotkey))
            {
                WriteError($"  \"{hotkey}\" is a system-reserved key. Try again or ESC to skip.");
                continue;
            }

            var (res, conflictName) = validator.Validate(hotkey, actionList, excludeId);
            if (res == HotkeyValidationResult.Conflict)
            {
                WriteError($"  \"{hotkey}\" conflicts with action \"{conflictName}\". Try again or ESC to skip.");
                continue;
            }

            Console.Write("  Detected: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(hotkey);
            Console.ResetColor();
            Console.Write(" — confirm? (Y/n/ESC): ");

            var confirm = Console.ReadKey(intercept: true);
            Console.WriteLine();

            if (confirm.Key == ConsoleKey.Escape)
            {
                WriteGray("  (Skipped)");
                return "";
            }
            if (confirm.Key == ConsoleKey.Enter || char.ToLower(confirm.KeyChar) == 'y')
                return hotkey;
        }
    }

    private static string? MapKey(ConsoleKey key) => key switch
    {
        >= ConsoleKey.A and <= ConsoleKey.Z    => key.ToString(),
        >= ConsoleKey.D0 and <= ConsoleKey.D9  => ((int)(key - ConsoleKey.D0)).ToString(),
        >= ConsoleKey.F1 and <= ConsoleKey.F12 => key.ToString(),
        ConsoleKey.Spacebar  => "Space",
        ConsoleKey.Tab       => "Tab",
        ConsoleKey.Delete    => "Delete",
        ConsoleKey.Insert    => "Insert",
        ConsoleKey.Home      => "Home",
        ConsoleKey.End       => "End",
        ConsoleKey.PageUp    => "PageUp",
        ConsoleKey.PageDown  => "PageDown",
        ConsoleKey.LeftArrow  => "Left",
        ConsoleKey.RightArrow => "Right",
        ConsoleKey.UpArrow    => "Up",
        ConsoleKey.DownArrow  => "Down",
        _ => null
    };

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

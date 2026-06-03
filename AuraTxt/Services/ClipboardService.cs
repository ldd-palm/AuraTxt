using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace AuraTxt.Services;

public static class ClipboardService
{
    // ── Strategy 1: UI Automation ─────────────────────────────────────────────
    // Reads the selected text directly from the focused control — no clipboard
    // manipulation, no fake key events, works in browsers, Notepad, Office, etc.
    private static string? TryUiAutomation()
    {
        try
        {
            var el = AutomationElement.FocusedElement;
            if (el is null) return null;
            if (!el.TryGetCurrentPattern(TextPattern.Pattern, out var raw)) return null;
            var tp  = (TextPattern)raw;
            var sel = tp.GetSelection();
            if (sel.Length == 0) return null;
            var text = sel[0].GetText(-1);
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch { return null; }
    }

    // ── Strategy 2: keybd_event P/Invoke ─────────────────────────────────────
    // Bypasses System.Windows.Forms.SendKeys entirely (which needs WinForms message
    // loop and behaves unpredictably in WPF). keybd_event is a thin Win32 wrapper
    // around SendInput and reliably targets the foreground window's thread.
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr extra);

    private const byte VK_CONTROL     = 0x11;
    private const byte VK_C           = 0x43;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private static void PressCtrlC()
    {
        keybd_event(VK_CONTROL, 0, 0,               UIntPtr.Zero);
        keybd_event(VK_C,       0, 0,               UIntPtr.Zero);
        keybd_event(VK_C,       0, KEYEVENTF_KEYUP,  UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP,  UIntPtr.Zero);
    }

    private static async Task<string?> TryClipboardAsync()
    {
        string prev = "";
        try
        {
            if (System.Windows.Clipboard.ContainsText())
                prev = System.Windows.Clipboard.GetText();

            System.Windows.Clipboard.Clear();
            PressCtrlC();
            await Task.Delay(150);

            var text = System.Windows.Clipboard.ContainsText()
                ? System.Windows.Clipboard.GetText() : "";
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch { return null; }
        finally
        {
            try
            {
                if (!string.IsNullOrEmpty(prev))
                    System.Windows.Clipboard.SetText(prev);
                else
                    System.Windows.Clipboard.Clear();
            }
            catch { }
        }
    }

    // ── Public entry point ────────────────────────────────────────────────────
    public static async Task<string> GetSelectedTextAsync(int delayMs = 100)
    {
        await Task.Delay(delayMs);

        // Fast path: UI Automation (no clipboard involved).
        var text = TryUiAutomation();
        if (!string.IsNullOrWhiteSpace(text)) return text;

        // Slow path: Ctrl+C simulation.
        return await TryClipboardAsync() ?? "";
    }
}

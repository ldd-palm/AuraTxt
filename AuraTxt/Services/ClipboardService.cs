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

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

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
        uint seqAfterRead = 0;
        try
        {
            if (System.Windows.Clipboard.ContainsText())
                prev = System.Windows.Clipboard.GetText();

            System.Windows.Clipboard.Clear();
            var seqBefore = GetClipboardSequenceNumber();
            PressCtrlC();

            // Poll the clipboard sequence number instead of a fixed wait: most apps
            // land the copy in <50 ms (saves ~100 ms latency), slow apps get up to 300 ms.
            for (int waited = 0; waited < 300; waited += 25)
            {
                await Task.Delay(25);
                if (GetClipboardSequenceNumber() != seqBefore) break;
            }

            var text = System.Windows.Clipboard.ContainsText()
                ? System.Windows.Clipboard.GetText() : "";
            seqAfterRead = GetClipboardSequenceNumber();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch { return null; }
        finally
        {
            try
            {
                // seqAfterRead == 0  → exception before read; restore prev (safe fallback)
                // seq unchanged      → nobody wrote to clipboard after our read → restore prev
                // seq changed        → user or another app wrote to clipboard → leave it alone
                if (seqAfterRead == 0 || GetClipboardSequenceNumber() == seqAfterRead)
                {
                    if (!string.IsNullOrEmpty(prev))
                        System.Windows.Clipboard.SetText(prev);
                    else
                        System.Windows.Clipboard.Clear();
                }
            }
            catch { }
        }
    }

    // ── Replace in source window ──────────────────────────────────────────────
    public static IntPtr CaptureSourceWindow() => GetForegroundWindow();

    public static async Task ReplaceInSourceWindowAsync(IntPtr hwnd, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || hwnd == IntPtr.Zero) return;
        try
        {
            System.Windows.Clipboard.SetText(text);
            SetForegroundWindow(hwnd);
            await Task.Delay(80);
            keybd_event(VK_CONTROL, 0, 0,              UIntPtr.Zero);
            keybd_event(0x56,       0, 0,              UIntPtr.Zero);  // V
            keybd_event(0x56,       0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        catch { }
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

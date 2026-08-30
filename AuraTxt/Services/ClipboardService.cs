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

    // Set by GlobalHookService.OnKeyDown when it observes a real, physical Ctrl+C.
    // Guards against racing our own synthetic Ctrl+C below with a genuine one: if both
    // land at once, the target app's modifier-key state can desync and the real 'C'
    // keyup gets delivered as an unmodified 'c' character, overwriting the selection.
    private static DateTime _lastRealCtrlC = DateTime.MinValue;
    public static void NotifyRealCtrlC() => _lastRealCtrlC = DateTime.UtcNow;

    // Set just before PressCtrlC() below, for the short window a synthetic Ctrl+C is in
    // flight. Lets GlobalHookService.OnKeyDown tell our own injected Ctrl+C apart from a
    // real one — both otherwise look identical on the global keyboard hook.
    private static DateTime _syntheticCtrlCUntil = DateTime.MinValue;
    public static bool IsSyntheticCtrlCInFlight() => DateTime.UtcNow < _syntheticCtrlCUntil;

    private static async Task<string?> TryClipboardAsync()
    {
        // A real Ctrl+C landed moments ago (or is still in flight) — it already is/will be
        // putting the selection on the clipboard. Don't inject our own synthetic Ctrl+C on
        // top of it (see _lastRealCtrlC), and don't Clear()/restore — that clipboard content
        // is the user's real copy now, not our scratch space.
        if (DateTime.UtcNow - _lastRealCtrlC < TimeSpan.FromMilliseconds(600))
        {
            var seqStart = GetClipboardSequenceNumber();
            for (int waited = 0; waited < 300; waited += 25)
            {
                if (GetClipboardSequenceNumber() != seqStart) break;
                await Task.Delay(25);
            }
            var t = System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : "";
            return string.IsNullOrWhiteSpace(t) ? null : t;
        }

        // Snapshot whatever's on the clipboard now — any format (text, files, images,
        // custom) — so a speculative Ctrl+C that doesn't pan out can be restored exactly,
        // not just wiped. (Previously this only remembered plain text via ContainsText()/
        // GetText(), so a non-text clipboard — e.g. files copied in Explorer — read as
        // "nothing", and got Clear()'d instead of restored below.)
        System.Windows.IDataObject? prevData = null;
        try { prevData = System.Windows.Clipboard.GetDataObject(); } catch { }

        uint seqAfterRead = 0;
        bool gotText = false;
        try
        {
            var seqBefore = GetClipboardSequenceNumber();
            _syntheticCtrlCUntil = DateTime.UtcNow.AddMilliseconds(200);
            PressCtrlC();

            // Poll the clipboard sequence number instead of a fixed wait: most apps
            // land the copy in <50 ms (saves ~100 ms latency), slow apps get up to 300 ms.
            for (int waited = 0; waited < 300; waited += 25)
            {
                await Task.Delay(25);
                if (GetClipboardSequenceNumber() != seqBefore) break;
            }

            if (GetClipboardSequenceNumber() == seqBefore)
                return null; // nothing responded to the Ctrl+C — clipboard untouched, nothing to restore

            var text = System.Windows.Clipboard.ContainsText()
                ? System.Windows.Clipboard.GetText() : "";
            seqAfterRead = GetClipboardSequenceNumber();

            if (!string.IsNullOrWhiteSpace(text))
            {
                gotText = true; // restore prev below unless something else wrote since our read
                return text;
            }

            // The Ctrl+C produced something, just not text — e.g. Explorer copying the
            // files the user dragged over. That's a legitimate result of the keypress we
            // injected; leave it on the clipboard instead of clobbering it in `finally`.
            return null;
        }
        catch { return null; }
        finally
        {
            try
            {
                // Only restore if we actually returned text AND nobody else has written to
                // the clipboard since (another app, or the user) — otherwise leave it alone.
                if (gotText && GetClipboardSequenceNumber() == seqAfterRead)
                {
                    if (prevData is not null)
                        System.Windows.Clipboard.SetDataObject(prevData, true);
                    else
                        System.Windows.Clipboard.Clear();
                }
            }
            catch { }
        }
    }

    // ── Writing to the clipboard, with retry ───────────────────────────────────
    // Clipboard.SetText can throw (usually COMException / CLIPBRD_E_CANT_OPEN) when
    // another process — Windows' own Clipboard History (Win+V), a third-party
    // clipboard manager, antivirus hooking clipboard events — has the clipboard
    // open at that exact instant. The lock is virtually always released within a
    // few ms, so a short retry loop turns an occasional silent failure (previously:
    // the Copy button doing nothing, requiring 2-3 clicks) into a reliable one.
    public static async Task<bool> TrySetTextAsync(string text, int maxAttempts = 8, int delayMs = 30)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try { System.Windows.Clipboard.SetText(text); return true; }
            catch { await Task.Delay(delayMs); }
        }
        return false;
    }

    // ── Replace in source window ──────────────────────────────────────────────
    public static IntPtr CaptureSourceWindow() => GetForegroundWindow();

    public static async Task ReplaceInSourceWindowAsync(IntPtr hwnd, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || hwnd == IntPtr.Zero) return;
        try
        {
            if (!await TrySetTextAsync(text)) return; // couldn't set clipboard — don't paste stale content
            SetForegroundWindow(hwnd);
            await Task.Delay(150);
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
        // Fast path: UI Automation has no side effects, so try it immediately — most apps
        // (browsers, Office, VSCode) commit the selection to the accessibility tree
        // synchronously on mouse-up, so this skips delayMs entirely in the common case.
        var text = TryUiAutomation();
        if (!string.IsNullOrWhiteSpace(text)) return text;

        // Slow path: give the app more time to settle, retry, then fall back to Ctrl+C simulation.
        await Task.Delay(delayMs);
        text = TryUiAutomation();
        if (!string.IsNullOrWhiteSpace(text)) return text;

        return await TryClipboardAsync() ?? "";
    }
}

using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using AuraTxt.Core.Services;

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

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const byte VK_CONTROL     = 0x11;
    private const byte VK_SHIFT       = 0x10;
    private const byte VK_MENU        = 0x12;
    private const byte VK_C           = 0x43;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    // Tags every key AuraTxt injects via keybd_event with a fixed marker in dwExtraInfo.
    // MouseKeyHook's KeyEventArgs doesn't surface this field, so GlobalHookService.OnKeyDown
    // still tells our own synthetic Ctrl+C apart from a real one via the _syntheticCtrlCUntil
    // time window, not this marker — this is prep for a future low-level keyboard hook
    // (stage 3) that reads KBDLLHOOKSTRUCT.dwExtraInfo directly and can match precisely.
    private static readonly UIntPtr AuraExtraInfo = (UIntPtr)0x41555241; // 'AURA'

    /// True if vKey is physically held down right now (high bit of GetAsyncKeyState).
    private static bool IsPhysicallyDown(byte vKey) => (GetAsyncKeyState(vKey) & 0x8000) != 0;

    private static void PressCtrlC()
    {
        keybd_event(VK_CONTROL, 0, 0,               AuraExtraInfo);
        keybd_event(VK_C,       0, 0,               AuraExtraInfo);
        keybd_event(VK_C,       0, KEYEVENTF_KEYUP,  AuraExtraInfo);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP,  AuraExtraInfo);
    }

    // Set by GlobalHookService.OnKeyDown when it observes a real, physical Ctrl+C.
    // Guards against racing our own synthetic Ctrl+C below with a genuine one: if both
    // land at once, the target app's modifier-key state can desync and the real 'C'
    // keyup gets delivered as an unmodified 'c' character, overwriting the selection.
    private static DateTime _lastRealCtrlC = DateTime.MinValue;
    public static void NotifyRealCtrlC() => _lastRealCtrlC = DateTime.UtcNow;

    // Set just before PressCtrlC() below, for the short window a synthetic Ctrl+C is in
    // flight. Lets GlobalHookService.OnKeyDown tell our own injected Ctrl+C apart from a
    // real one — both otherwise look identical on the global keyboard hook. Only needs to
    // bridge keybd_event → the low-level hook observing it (same-machine OS dispatch, a
    // few ms in practice) — kept short so a genuine Ctrl+C landing shortly after ours
    // isn't misclassified as our own and silently dropped.
    private static DateTime _syntheticCtrlCUntil = DateTime.MinValue;
    public static bool IsSyntheticCtrlCInFlight() => DateTime.UtcNow < _syntheticCtrlCUntil;

    // Serializes TryClipboardAsync so two overlapping calls (e.g. two selections captured
    // in quick succession) can never have their PressCtrlC() keybd_event sequences
    // interleave. An interleaved Ctrl-down/Ctrl-up pair from two overlapping calls can
    // land out of order and leave Windows' key-state table thinking Ctrl is still held —
    // which then makes the physically-down check below veto every future capture forever,
    // since nothing else ever clears that stuck state.
    private static readonly SemaphoreSlim _captureLock = new(1, 1);

    private static async Task<string?> TryClipboardAsync()
    {
        await _captureLock.WaitAsync();
        try
        {
            return await TryClipboardCoreAsync();
        }
        finally
        {
            _captureLock.Release();
        }
    }

    private static async Task<string?> TryClipboardCoreAsync()
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

        // A modifier key is physically held down right now — the user is very likely mid
        // keyboard-shortcut (most importantly a real Ctrl+V). Injecting our own Ctrl+C on
        // top of that can transiently lock the clipboard exactly when their shortcut needs
        // it, so back off instead of racing it — same selection will still be there to
        // retry on the next trigger.
        if (IsPhysicallyDown(VK_CONTROL) || IsPhysicallyDown(VK_SHIFT) || IsPhysicallyDown(VK_MENU))
            return null;

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
            _syntheticCtrlCUntil = DateTime.UtcNow.AddMilliseconds(100);
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
            // Only restore if we actually returned text AND nobody else has written to
            // the clipboard since (another app, or the user) — otherwise leave it alone.
            if (gotText && GetClipboardSequenceNumber() == seqAfterRead)
            {
                if (prevData is not null)
                    await RetryClipboardOpAsync(() => System.Windows.Clipboard.SetDataObject(prevData, true));
                else
                    await RetryClipboardOpAsync(() => System.Windows.Clipboard.Clear());
            }
        }
    }

    // ── Writing to the clipboard, with retry ───────────────────────────────────
    // Clipboard.SetText/SetDataObject/Clear can throw (usually COMException /
    // CLIPBRD_E_CANT_OPEN) when another process — Windows' own Clipboard History (Win+V),
    // a third-party clipboard manager, antivirus hooking clipboard events — has the
    // clipboard open at that exact instant. The lock is virtually always released within
    // a few ms, so a short retry loop turns an occasional silent failure (previously: the
    // Copy button doing nothing, or a restored clipboard silently staying clobbered) into
    // a reliable one.
    private static async Task<bool> RetryClipboardOpAsync(Action action, int maxAttempts = 8, int delayMs = 30)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try { action(); return true; }
            catch { await Task.Delay(delayMs); }
        }
        return false;
    }

    // Dispatched to ClipboardWorkerThread like GetSelectedTextAsync below — Clipboard.SetText
    // is a blocking OLE call, not just a throw-and-retry; if a third-party clipboard manager
    // (Ditto etc.) is slow to release its hold, this could otherwise freeze whichever UI
    // thread called it (e.g. ResultWindow/InteractiveWindow's Copy button) for that long.
    public static Task<bool> TrySetTextAsync(string text, int maxAttempts = 8, int delayMs = 30) =>
        ClipboardWorkerThread.InvokeAsync(() =>
            RetryClipboardOpAsync(() => System.Windows.Clipboard.SetText(text), maxAttempts, delayMs));

    // ── Replace in source window ──────────────────────────────────────────────
    public static IntPtr CaptureSourceWindow() => GetForegroundWindow();

    public static async Task ReplaceInSourceWindowAsync(IntPtr hwnd, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || hwnd == IntPtr.Zero) return;
        try
        {
            if (!await TrySetTextAsync(text)) return; // couldn't set clipboard — don't paste stale content

            if (!SetForegroundWindow(hwnd))
            {
                LogService.Error($"Replace: SetForegroundWindow denied for {hwnd}");
                return; // foreground-lock denied it — firing Ctrl+V now would hit whatever window has focus instead
            }
            await Task.Delay(150);
            if (GetForegroundWindow() != hwnd)
            {
                LogService.Error($"Replace: foreground changed away from {hwnd} before paste");
                return;
            }
            keybd_event(VK_CONTROL, 0, 0,              AuraExtraInfo);
            keybd_event(0x56,       0, 0,              AuraExtraInfo);  // V
            keybd_event(0x56,       0, KEYEVENTF_KEYUP, AuraExtraInfo);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, AuraExtraInfo);
        }
        catch { }
    }

    // ── Public entry point ────────────────────────────────────────────────────
    // Runs on ClipboardWorkerThread's dedicated STA thread, not the caller's — UI
    // Automation and clipboard access here can block for a while (slow/unresponsive
    // target app), and the caller is GlobalHookService's mouse hook thread, which must
    // stay free for WH_MOUSE_LL or cursor/input lags system-wide.
    public static Task<string> GetSelectedTextAsync(int delayMs = 100) =>
        ClipboardWorkerThread.InvokeAsync(() => CaptureCoreAsync(delayMs));

    private static async Task<string> CaptureCoreAsync(int delayMs)
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

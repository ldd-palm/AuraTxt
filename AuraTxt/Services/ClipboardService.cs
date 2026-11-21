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
    // One-shot: the first Ctrl+C observed inside the window is ours and consumes it — a
    // second one landing inside the same window can only be the user's real keypress, and
    // must still reach NotifyRealCtrlC (otherwise the finally-restore below would overwrite
    // the user's fresh copy with the pre-capture clipboard).
    private static DateTime _syntheticCtrlCUntil = DateTime.MinValue;
    public static bool ConsumeSyntheticCtrlC()
    {
        if (DateTime.UtcNow >= _syntheticCtrlCUntil) return false;
        _syntheticCtrlCUntil = DateTime.MinValue;
        return true;
    }

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

        // Any real Ctrl+C observed after this point means the user copied mid-capture —
        // the clipboard is theirs now, so the finally-restore below must not touch it.
        var captureStart = DateTime.UtcNow;

        // Snapshot whatever's on the clipboard now — any format (text, files, images,
        // custom) — so a speculative Ctrl+C that doesn't pan out can be restored exactly,
        // not just wiped. (Previously this only remembered plain text via ContainsText()/
        // GetText(), so a non-text clipboard — e.g. files copied in Explorer — read as
        // "nothing", and got Clear()'d instead of restored below.)
        // null = snapshot failed/timed out → don't restore at all (see SnapshotClipboard).
        var prevData = await RunOnThrowawayStaThreadAsync(SnapshotClipboard, StaCallTimeoutMs);

        uint seqAfterRead = 0;
        bool gotText = false;
        // Everything the clipboard goes through from here on — the target app's copy of the
        // selection, our restore — is AuraTxt's doing, not the user's: keep it out of the
        // clipboard history (see IsOwnClipboardChange).
        BeginOwnWrite();
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
            // the clipboard since (another app, or the user) AND no real Ctrl+C was seen
            // during this capture — otherwise leave it alone. Re-checked inside each retry
            // attempt, right before writing: retries can span seconds, and a check done
            // only once up front would let a later attempt clobber a copy made meanwhile.
            bool StillOurs() => GetClipboardSequenceNumber() == seqAfterRead && _lastRealCtrlC < captureStart;
            try
            {
                if (gotText && prevData is not null && StillOurs())
                {
                    if (prevData.GetFormats(false).Length > 0)
                        await RetryClipboardOpAsync(() => { if (StillOurs()) System.Windows.Clipboard.SetDataObject(prevData, true); });
                    else
                        await RetryClipboardOpAsync(() => { if (StillOurs()) System.Windows.Clipboard.Clear(); });
                }
            }
            finally { EndOwnWrite(); }
        }
    }

    // ── Own-write tracking (for ClipboardHistoryService) ────────────────────────
    // Clipboard changes AuraTxt itself causes — selection capture (the target app's copy
    // plus our restore), Replace, pasting from the history window — must not land in the
    // clipboard history. WM_CLIPBOARDUPDATE is posted asynchronously, so a change can be
    // handled after the write that caused it has finished: besides "a write is in flight
    // right now", remember the sequence-number range our last writes covered. A genuine
    // user copy that happens to land inside that window is skipped too — rare, acceptable.
    private static readonly object _ownWriteLock = new();
    private static int  _ownWriteDepth;
    private static uint _ownSeqFrom = 1, _ownSeqTo; // empty range until the first own write

    private static void BeginOwnWrite()
    {
        lock (_ownWriteLock)
            if (_ownWriteDepth++ == 0) _ownSeqFrom = GetClipboardSequenceNumber() + 1;
    }

    private static void EndOwnWrite()
    {
        lock (_ownWriteLock)
            if (--_ownWriteDepth == 0) _ownSeqTo = GetClipboardSequenceNumber();
    }

    /// True if the clipboard change with sequence number `seq` was caused by AuraTxt.
    public static bool IsOwnClipboardChange(uint seq)
    {
        lock (_ownWriteLock)
            return _ownWriteDepth > 0 || (seq >= _ownSeqFrom && seq <= _ownSeqTo);
    }

    public static uint ClipboardSequenceNumber() => GetClipboardSequenceNumber();

    // ── Reading for the clipboard history ───────────────────────────────────────
    private sealed record HistoryRead(ClipboardHistoryEntry? Entry);

    private const string ExcludeFromMonitorFormat = "ExcludeClipboardContentFromMonitorProcessing";
    private const string ViewerIgnoreFormat       = "Clipboard Viewer Ignore";
    private const string CanIncludeInHistoryFormat = "CanIncludeInClipboardHistory";
    private const int    MaxRichFormatChars       = 1_000_000;

    /// Reads the current clipboard as a history entry: null if it holds no text, or the
    /// owner asked clipboard monitors to skip it (password managers set these formats).
    /// Bounded and retried like every other clipboard access here (another listener —
    /// Windows' own clipboard history, Ditto — often opens the clipboard at this very
    /// moment, right after the change notification).
    public static async Task<ClipboardHistoryEntry?> ReadForHistoryAsync()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var read = await RunOnThrowawayStaThreadAsync(ReadHistoryEntry, StaCallTimeoutMs);
            if (read is not null) return read.Entry; // null = threw or timed out → retry
            await Task.Delay(50);
        }
        return null;
    }

    private static HistoryRead ReadHistoryEntry()
    {
        var data = System.Windows.Clipboard.GetDataObject();
        if (data is null) return new(null);
        var formats = data.GetFormats(false);
        if (formats.Contains(ExcludeFromMonitorFormat) || formats.Contains(ViewerIgnoreFormat)) return new(null);
        if (formats.Contains(CanIncludeInHistoryFormat) &&
            data.GetData(CanIncludeInHistoryFormat, false) is System.IO.MemoryStream ms &&
            ms.Length >= 4 && BitConverter.ToInt32(ms.ToArray(), 0) == 0)
            return new(null);

        if (data.GetData(System.Windows.DataFormats.UnicodeText, true) is not string text ||
            string.IsNullOrWhiteSpace(text))
            return new(null);

        var rich = new Dictionary<string, string>();
        foreach (var f in new[] { System.Windows.DataFormats.Html, System.Windows.DataFormats.Rtf })
        {
            if (formats.Contains(f) && data.GetData(f, false) is string s &&
                s.Length > 0 && s.Length <= MaxRichFormatChars)
                rich[f] = s;
        }
        return new(new ClipboardHistoryEntry(text, rich));
    }

    // [关键] Eager copy, NOT Clipboard.GetDataObject() kept as-is. GetDataObject() returns a
    // live OLE proxy onto *whatever is on the clipboard when it's read*, not a snapshot —
    // after our synthetic Ctrl+C it reads back the captured selection, so restoring it
    // could never bring the old content back. Worse, SetDataObject(proxy, copy:true) makes
    // OleFlushClipboard render the proxy from the clipboard it is itself replacing: WPF's
    // internal retry loop then blocks ~1s and fails with CLIPBRD_E_CANT_OPEN, leaving the
    // clipboard unusable for other apps meanwhile (their copies silently fail — observed as
    // manual Ctrl+C not copying, and Lister's next capture getting no clipboard change).
    // Copying each native format's data into an independent DataObject fixes both. Runs
    // via RunOnThrowawayStaThreadAsync: GetData calls into the owning app (delayed
    // rendering) and can hang. An empty DataObject means "clipboard was empty".
    private static System.Windows.DataObject SnapshotClipboard()
    {
        var snap = new System.Windows.DataObject();
        var live = System.Windows.Clipboard.GetDataObject();
        if (live is null) return snap;
        foreach (var format in live.GetFormats(false))
        {
            try
            {
                var value = live.GetData(format, false);
                if (value is not null) snap.SetData(format, value, false);
            }
            catch { } // a format that can't be rendered is just not restored
        }
        return snap;
    }

    // ── Writing to the clipboard, with retry ───────────────────────────────────
    // Clipboard.SetText/SetDataObject/Clear can throw (usually COMException /
    // CLIPBRD_E_CANT_OPEN) when another process — Windows' own Clipboard History (Win+V),
    // a third-party clipboard manager, antivirus hooking clipboard events — has the
    // clipboard open at that exact instant. The lock is virtually always released within
    // a few ms, so a short retry loop turns an occasional silent failure (previously: the
    // Copy button doing nothing, or a restored clipboard silently staying clobbered) into
    // a reliable one.
    //
    // [关键] Each attempt runs through RunOnThrowawayStaThreadAsync, not directly on the
    // caller's thread — this method (via TrySetTextAsync and the capture-restore `finally`
    // above) runs on ClipboardWorkerThread's single shared dispatcher, the same one
    // GetSelectedTextAsync uses. Clipboard.SetText/SetDataObject/Clear are blocking OLE
    // calls that can hang instead of throwing (observed: clicking the ActionMenu's Copy
    // button after selecting text in an Electron app could leave the action bar unable to
    // pop up anywhere, for anyone, until the hang cleared on its own) — the exact same bug
    // class as the UI Automation hang below, just on the write side. Bounding each attempt
    // on its own throwaway thread means a hang only leaks that one thread instead of
    // starving the shared worker every other capture/copy depends on.
    private static async Task<bool> RetryClipboardOpAsync(Action action, int maxAttempts = 8, int delayMs = 30)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var ok = await RunOnThrowawayStaThreadAsync(() =>
            {
                action();
                return true;
            }, StaCallTimeoutMs);
            if (ok == true) return true; // null = timed out (treat like a failed attempt), false is unreachable here
            await Task.Delay(delayMs);
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

    public static Task ReplaceInSourceWindowAsync(IntPtr hwnd, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;
        return PasteIntoWindowAsync(hwnd, () => TrySetTextAsync(text), "Replace");
    }

    /// Clipboard-history paste: the entry's text plus its rich formats, or (plainText) the
    /// text alone.
    public static Task PasteHistoryEntryAsync(IntPtr hwnd, ClipboardHistoryEntry entry, bool plainText) =>
        PasteIntoWindowAsync(hwnd, () => ClipboardWorkerThread.InvokeAsync(() => RetryClipboardOpAsync(() =>
        {
            var obj = new System.Windows.DataObject();
            obj.SetData(System.Windows.DataFormats.UnicodeText, entry.Text, false);
            if (!plainText)
                foreach (var (format, value) in entry.Formats) obj.SetData(format, value, false);
            System.Windows.Clipboard.SetDataObject(obj, true);
        })), "History paste");

    /// Puts content on the clipboard (`setClipboard`), brings `hwnd` back to the foreground
    /// and sends it Ctrl+V. The write is AuraTxt's own, so it's kept out of the history.
    private static async Task PasteIntoWindowAsync(IntPtr hwnd, Func<Task<bool>> setClipboard, string what)
    {
        if (hwnd == IntPtr.Zero) return;
        BeginOwnWrite();
        try
        {
            if (!await setClipboard()) return; // couldn't set clipboard — don't paste stale content

            if (!SetForegroundWindow(hwnd))
            {
                LogService.Error($"{what}: SetForegroundWindow denied for {hwnd}");
                return; // foreground-lock denied it — firing Ctrl+V now would hit whatever window has focus instead
            }
            await Task.Delay(150);
            if (GetForegroundWindow() != hwnd)
            {
                LogService.Error($"{what}: foreground changed away from {hwnd} before paste");
                return;
            }
            keybd_event(VK_CONTROL, 0, 0,              AuraExtraInfo);
            keybd_event(0x56,       0, 0,              AuraExtraInfo);  // V
            keybd_event(0x56,       0, KEYEVENTF_KEYUP, AuraExtraInfo);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, AuraExtraInfo);
        }
        catch { }
        finally { EndOwnWrite(); }
    }

    // ── Public entry point ────────────────────────────────────────────────────
    // Runs on ClipboardWorkerThread's dedicated STA thread, not the caller's — UI
    // Automation and clipboard access here can block for a while (slow/unresponsive
    // target app), and the caller is GlobalHookService's mouse hook thread, which must
    // stay free for WH_MOUSE_LL or cursor/input lags system-wide.
    public static Task<string> GetSelectedTextAsync(int delayMs = 100) =>
        ClipboardWorkerThread.InvokeAsync(() => CaptureCoreAsync(delayMs));

    // Shared by TryUiAutomationWithTimeoutAsync and RetryClipboardOpAsync above: both call
    // blocking COM/OLE APIs (UI Automation, Clipboard.Set*/Clear) that can hang instead of
    // throwing when the other side of the call is stuck, and both are invoked from
    // ClipboardWorkerThread's single shared dispatcher (see that class) — if run directly
    // there, a hang would block every future selection capture AND every future copy, in
    // every app, until the hang resolved on its own (unpredictable, can be a long time).
    // Running `work` on its own one-shot STA thread (OLE/UIA calls expect an STA caller,
    // same reason ClipboardWorkerThread itself is one) and racing it against a timeout
    // means a hang only leaks that one throwaway thread instead of starving the shared
    // worker. The leaked thread isn't actually killed — .NET has no safe way to abort a
    // thread — it just sits blocked until the underlying call eventually returns.
    private const int StaCallTimeoutMs = 400;

    private static async Task<T?> RunOnThrowawayStaThreadAsync<T>(Func<T?> work, int timeoutMs)
    {
        var tcs = new TaskCompletionSource<T?>();
        var thread = new Thread(() =>
        {
            T? result;
            try { result = work(); } catch { result = default; }
            tcs.TrySetResult(result);
        })
        {
            IsBackground = true,
            Name = "AuraTxt-ClipboardStaOp"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
        return completed == tcs.Task ? await tcs.Task : default;
    }

    private static Task<string?> TryUiAutomationWithTimeoutAsync(int timeoutMs) =>
        RunOnThrowawayStaThreadAsync(TryUiAutomation, timeoutMs);

    private static async Task<string> CaptureCoreAsync(int delayMs)
    {
        // Fast path: UI Automation has no side effects, so try it immediately — most apps
        // (browsers, Office, VSCode) commit the selection to the accessibility tree
        // synchronously on mouse-up, so this skips delayMs entirely in the common case.
        var text = await TryUiAutomationWithTimeoutAsync(StaCallTimeoutMs);
        if (!string.IsNullOrWhiteSpace(text)) return text;

        // Slow path: give the app more time to settle, retry, then fall back to Ctrl+C simulation.
        await Task.Delay(delayMs);
        text = await TryUiAutomationWithTimeoutAsync(StaCallTimeoutMs);
        if (!string.IsNullOrWhiteSpace(text)) return text;

        return await TryClipboardAsync() ?? "";
    }
}

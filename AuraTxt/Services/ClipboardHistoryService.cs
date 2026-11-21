using System.Runtime.InteropServices;
using System.Windows.Interop;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Services;

/// Records clipboard text into an in-memory ClipboardHistory, for ClipboardHistoryWindow.
/// Listens via AddClipboardFormatListener on a hidden message-only window owned by the UI
/// thread (WM_CLIPBOARDUPDATE arrives there; the read itself goes through
/// ClipboardService.ReadForHistoryAsync, bounded on a throwaway STA thread). Only runs while
/// Settings.IsClipboardHistoryActive (switch on + hotkey set) — otherwise nothing is recorded.
public sealed class ClipboardHistoryService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    private const int  WM_CLIPBOARDUPDATE = 0x031D;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private HwndSource? _source;
    private uint _lastSeq;

    public ClipboardHistory History { get; } = new(25);

    /// Fixed location next to the exe (portable, like config.json). Excluded from
    /// release zips — see SPEC.md §12.3.
    public static string FilePath => System.IO.Path.Combine(AppContext.BaseDirectory, "clipboard_history.dat");

    /// Listening/recording right now (switch on + hotkey set). While inactive the history
    /// file is neither read nor written — it's left on disk untouched.
    public bool IsActive => _source is not null;

    /// Applies ClipboardHistorySize/ClipboardHistoryHotkey — at startup and on "Reload
    /// Settings". Becoming active (startup, or the switch turned back on) loads the saved
    /// file and merges it under whatever is already in memory.
    public void Apply(AppSettings settings)
    {
        History.Capacity = Math.Min(settings.ClipboardHistorySize, AppSettings.ClipboardHistoryMaxSize);
        var enabled = settings.IsClipboardHistoryActive;
        if (enabled && _source is null)
        {
            Start();
            var saved = ClipboardHistoryStore.Load(FilePath);
            History.AppendOlder(saved);
            LogService.Info($"[history] loaded {saved.Count} saved entries");
        }
        else if (!enabled && _source is not null) Stop();
    }

    /// Writes the in-memory history to FilePath. Called on normal exit, Windows
    /// shutdown/logoff, service pause/resume, and the tray's "Save Clipboard History".
    /// Returns the number of entries saved, or null if inactive (nothing written) or the
    /// write failed. A crash loses only what was copied since the last save.
    public int? Save()
    {
        if (!IsActive) return null;
        try
        {
            ClipboardHistoryStore.Save(FilePath, History.Items);
            LogService.Info($"[history] saved {History.Items.Count} entries");
            return History.Items.Count;
        }
        catch (Exception ex)
        {
            LogService.Error("Clipboard history save failed", ex);
            return null;
        }
    }

    private void Start()
    {
        _source = new HwndSource(new HwndSourceParameters("AuraTxt-ClipboardListener")
        {
            ParentWindow = HWND_MESSAGE,
            WindowStyle  = 0
        });
        _source.AddHook(WndProc);
        if (!AddClipboardFormatListener(_source.Handle))
            LogService.Error($"AddClipboardFormatListener failed: {Marshal.GetLastWin32Error()}");
    }

    private void Stop()
    {
        if (_source is null) return;
        RemoveClipboardFormatListener(_source.Handle);
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE) _ = OnClipboardUpdateAsync();
        return IntPtr.Zero;
    }

    private async Task OnClipboardUpdateAsync()
    {
        try
        {
            var seq = ClipboardService.ClipboardSequenceNumber();
            if (seq == _lastSeq) return; // several notifications for one change
            _lastSeq = seq;
            if (ClipboardService.IsOwnClipboardChange(seq))
            {
                LogService.Info($"[history] skip own clipboard change seq={seq}");
                return;
            }

            var entry = await ClipboardService.ReadForHistoryAsync();
            if (entry is null) return;
            History.Add(entry);
            LogService.Info($"[history] added seq={seq} len={entry.Text.Length} formats={entry.Formats.Count}");
        }
        catch (Exception ex) { LogService.Error("Clipboard history update failed", ex); }
    }

    public void Dispose() => Stop();
}

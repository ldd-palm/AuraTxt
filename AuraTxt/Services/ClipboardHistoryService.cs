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

    /// Applies ClipboardHistorySize/ClipboardHistoryHotkey — at startup and on "Reload Settings".
    public void Apply(AppSettings settings)
    {
        History.Capacity = Math.Min(settings.ClipboardHistorySize, AppSettings.ClipboardHistoryMaxSize);
        var enabled = settings.IsClipboardHistoryActive;
        if (enabled && _source is null) Start();
        else if (!enabled && _source is not null) Stop();
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

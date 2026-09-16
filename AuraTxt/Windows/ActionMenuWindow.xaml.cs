using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using AuraTxt.Services;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using Separator = System.Windows.Controls.Separator;
using TextBlock = System.Windows.Controls.TextBlock;
using ToolTipService = System.Windows.Controls.ToolTipService;

namespace AuraTxt.Windows;

public partial class ActionMenuWindow : Window
{
    private readonly ConfigRoot _cfg;
    private string _selectedText;
    private System.Drawing.Point _physicalCursor;
    private bool _closing;
    private bool _ready;
    private CancellationTokenSource? _dismissCts;

    public ActionMenuWindow(ConfigRoot cfg, string selectedText, System.Drawing.Point physicalCursor)
    {
        InitializeComponent();
        _cfg            = cfg;
        _selectedText   = selectedText;
        _physicalCursor = physicalCursor;

        // Keep window off-screen until OnLoaded computes the correct DIP position.
        // (Setting physical pixels directly as DIPs here is wrong on HiDPI displays.)
        Left = -9999;
        Top  = -9999;

        AppState.ActiveMenu = this;

        Loaded      += OnLoaded;
        Deactivated += (_, _) => DeferredClose();
        Closed      += (_, _) => { if (AppState.ActiveMenu == this) AppState.ActiveMenu = null; };
    }

    /// XAML's ShowActivated="False" only skips WPF's own Activate() call on Show() — it
    /// does not stop Windows from later giving this HWND keyboard focus (e.g. via mouse
    /// interaction), which is what OnPreviewKeyDown's Ctrl+C special-case was working
    /// around. WS_EX_NOACTIVATE set here, before the window is shown, is the actual
    /// Win32-level "this window can never be activated/focused" contract — buttons still
    /// receive mouse clicks normally, but keystrokes (and text being typed elsewhere) can
    /// no longer be silently swallowed by this window stealing focus.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        MoveHwndToCursorMonitor();
        PositionNearCursor();
        BuildMenu();
        UpdateLayout();
        ClampToWorkArea();   // re-clamp with the real size (PositionNearCursor used estimates)
        _ready = true;
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const uint SWP_NOSIZE      = 0x0001;
    private const uint SWP_NOZORDER    = 0x0004;
    private const uint SWP_NOACTIVATE  = 0x0010;
    private const int  GWL_EXSTYLE     = -20;
    private const int  WS_EX_NOACTIVATE = 0x08000000;

    /// Per-monitor-V2 WPF interprets Window.Left/Top using the DPI context the HWND
    /// currently has — established when the window was created at the (-9999,-9999)
    /// placeholder, which lands on whatever monitor is nearest that point (usually the
    /// primary one), not wherever the cursor actually is. On a multi-monitor setup with
    /// different per-monitor scaling, doing the DIP math in PositionNearCursor/
    /// GetWorkAreaDip against that stale DPI context produces a position offset roughly
    /// proportional to the scale mismatch. Move the raw HWND to the cursor's physical
    /// position first so Windows fires WM_DPICHANGED synchronously and the window's DPI
    /// context (and TransformFromDevice used below) matches the cursor's real monitor
    /// before any DIP conversion happens.
    private void MoveHwndToCursorMonitor()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, IntPtr.Zero, _physicalCursor.X, _physicalCursor.Y, 0, 0,
            SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// PositionNearCursor clamps with estimated bounds (actual size unknown before layout);
    /// with many actions the menu can exceed that estimate and stick out of the screen.
    private void ClampToWorkArea()
    {
        var wa = GetWorkAreaDip();
        if (ActualWidth > 0)
            Left = Math.Max(wa.Left, Math.Min(Left, wa.Right - ActualWidth));
        if (ActualHeight > 0)
            Top = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - ActualHeight));
    }

    /// SystemParameters.WorkArea always reports the *primary* monitor's work area in WPF,
    /// so on a multi-monitor setup it clamps the menu back onto the main screen instead of
    /// the one the cursor is actually on. Screen.FromPoint gives the correct monitor; its
    /// WorkingArea is physical pixels, so run it through the same device→DIP transform used
    /// for the cursor position to stay correct across monitors with different DPI scaling.
    private Rect GetWorkAreaDip()
    {
        var wa = System.Windows.Forms.Screen.FromPoint(_physicalCursor).WorkingArea;

        var src = PresentationSource.FromVisual(this);
        if (src is not null)
        {
            var transform   = src.CompositionTarget.TransformFromDevice;
            var topLeft     = transform.Transform(new System.Windows.Point(wa.Left, wa.Top));
            var bottomRight = transform.Transform(new System.Windows.Point(wa.Right, wa.Bottom));
            return new Rect(topLeft, bottomRight);
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var sx = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1;
        var sy = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1;
        return new Rect(wa.Left / sx, wa.Top / sy, wa.Width / sx, wa.Height / sy);
    }

    private void PositionNearCursor()
    {
        // Use PresentationSource (most reliable) to convert physical px → WPF DIPs.
        var src = PresentationSource.FromVisual(this);
        double dipX, dipY;
        if (src is not null)
        {
            var pt = src.CompositionTarget.TransformFromDevice
                        .Transform(new System.Windows.Point(_physicalCursor.X, _physicalCursor.Y));
            dipX = pt.X;
            dipY = pt.Y;
        }
        else
        {
            // Fallback: use VisualTreeHelper DPI
            var dpi = VisualTreeHelper.GetDpi(this);
            dipX = _physicalCursor.X / (dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1);
            dipY = _physicalCursor.Y / (dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1);
        }

        // Place menu above-right of cursor; clamp to work area so it's never off-screen.
        var wa      = GetWorkAreaDip();
        const double menuW = 220;   // generous upper bound for clamping (actual size unknown yet)
        const double menuH = 44;

        Left = Math.Max(wa.Left, Math.Min(dipX + 4, wa.Right  - menuW));
        Top  = dipY > wa.Top + menuH ? dipY - menuH : dipY + 4;
        Top  = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - menuH));
    }

    /// <summary>
    /// Starts a deferred close: waits 500 ms so a MouseDoubleClick arriving in
    /// that window can cancel the dismiss and update the menu in-place instead.
    /// Button clicks still call <see cref="SafeClose"/> directly for instant close.
    /// </summary>
    public void DeferredClose()
    {
        if (!_ready || _closing) return;
        CancelDeferredClose();
        _dismissCts = new CancellationTokenSource();
        var token = _dismissCts.Token;
        _ = DelayedCloseAsync(token);
    }

    private async Task DelayedCloseAsync(CancellationToken ct)
    {
        try { await Task.Delay(500, ct); }
        catch (OperationCanceledException) { return; }
        SafeClose(applySuppress: false);  // light-dismiss: don't block immediate re-trigger
    }

    /// <summary>Cancels any pending deferred close.</summary>
    public void CancelDeferredClose()
    {
        _dismissCts?.Cancel();
        _dismissCts?.Dispose();
        _dismissCts = null;
    }

    /// <summary>
    /// Updates the menu in-place with new selected text and cursor position.
    /// Repositions the window and rebuilds action buttons without closing/reopening.
    /// </summary>
    public void UpdateMenu(string newText, System.Drawing.Point newPhysicalPosition)
    {
        if (_closing) return;
        _selectedText = newText;
        _physicalCursor = newPhysicalPosition;
        AppState.IsMenuUpdating = true;
        try
        {
            MoveHwndToCursorMonitor();
            PositionNearCursor();
            IconPanel.Children.Clear();
            BuildMenu();
            UpdateLayout();
            ClampToWorkArea();
        }
        finally { AppState.IsMenuUpdating = false; }
    }

    private void SafeClose(bool applySuppress = true)
    {
        if (!_ready || _closing) return;
        if (AppState.IsMenuUpdating) return;
        _closing = true;
        if (applySuppress)
            AppState.MenuSuppressUntil = DateTime.UtcNow.AddSeconds(2);
        Close();
    }

    /// Called from global keyboard hook (UI thread via Dispatcher.BeginInvoke).
    public void CloseNow() => SafeClose();

    private void BuildMenu()
    {
        // App logo at the far left — branding so users know which app owns the popup
        IconPanel.Children.Add(MakeLogo());
        IconPanel.Children.Add(MakeSeparator());

        // Order by display position: Order asc, then name alphabetical
        var ordered = _cfg.Actions
            .Where(a => a.Enabled)
            .OrderBy(a => a.Order)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var action in ordered)
        {
            var a       = action;
            var img     = IconCacheService.GetIconSync(a.Icon);
            var fallback = string.IsNullOrEmpty(a.Name) ? "?" : a.Name[..1];

            if (img is null) IconCacheService.DownloadInBackground(a.Icon);

            var tip = $"{a.Name}{(string.IsNullOrEmpty(a.Hotkey) ? "" : $" ({a.Hotkey})")}";

            var style = (Style)FindResource("MenuActionBtnStyle");

            Button btn;
            if (string.IsNullOrEmpty(a.ModelId))
            {
                btn = img is not null
                    ? MakeImageButton(img, tip, () => ExecuteSystemAction(a.Id))
                    : MakeEmojiButton(fallback, tip, () => ExecuteSystemAction(a.Id));
            }
            else
            {
                btn = img is not null
                    ? MakeImageButton(img, tip, () => { SafeClose(); HotkeyService.ShowResultFor(a, _selectedText, _cfg); })
                    : MakeEmojiButton(fallback, tip, () => { SafeClose(); HotkeyService.ShowResultFor(a, _selectedText, _cfg); });
            }
            btn.Style = style;
            IconPanel.Children.Add(btn);
        }
    }

    private void ExecuteSystemAction(string id)
    {
        switch (id)
        {
            case "copy":
                // Clipboard can be locked by another process (CLIPBRD_E_CANT_OPEN).
                try { Clipboard.SetText(_selectedText); }
                catch (Exception ex) { LogService.Error("Copy action failed", ex); }
                break;
            case "speech":
                SpeechService.Speak(_selectedText, _cfg.Settings.SpeechVoice);
                break;
            case "google":
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                        "https://www.google.com/search?q=" + Uri.EscapeDataString(_selectedText))
                        { UseShellExecute = true });
                }
                catch (Exception ex) { LogService.Error("Google search failed", ex); }
                break;
        }
        SafeClose();
    }

    private static Button MakeEmojiButton(string emoji, string tooltip, Action onClick)
    {
        var btn = new Button
        {
            Content     = new TextBlock { Text = emoji, FontSize = 17, VerticalAlignment = VerticalAlignment.Center },
            Width       = 34,
            Height      = 34,
            Background  = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Cursor      = Cursors.Hand,
            ToolTip     = tooltip
        };
        ToolTipService.SetInitialShowDelay(btn, 300);
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button MakeImageButton(DrawingImage img, string tooltip, Action onClick)
    {
        var btn = new Button
        {
            Content     = new System.Windows.Controls.Image { Source = img, Width = 17, Height = 17 },
            Width       = 34,
            Height      = 34,
            Background  = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Cursor      = Cursors.Hand,
            ToolTip     = tooltip
        };
        ToolTipService.SetInitialShowDelay(btn, 300);
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private System.Windows.UIElement MakeLogo()
    {
        var img = new System.Windows.Controls.Image
        {
            Width             = 26,
            Height            = 26,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible  = false
        };
        try
        {
            img.Source = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Resources/aruatxt_logo.png"));
        }
        catch { /* logo is optional — border remains, drag still works */ }

        var border = new System.Windows.Controls.Border
        {
            Width      = 34,
            Height     = 34,
            Child      = img,
            Background = Brushes.Transparent,   // required for hit-testing
            Cursor     = Cursors.SizeAll,
            ToolTip    = "AuraTxt — Drag to move"
        };
        ToolTipService.SetInitialShowDelay(border, 600);

        // PreviewMouseLeftButtonDown (tunneling) fires before child elements can swallow it
        border.PreviewMouseLeftButtonDown += (_, _) =>
        {
            DragMove();
            // DragMove drives an OS-level move loop that can leave this window activated
            // even with WS_EX_NOACTIVATE set; hand focus back so the next keystroke lands
            // in the source app instead of nowhere.
            if (AppState.SourceWindowHandle != IntPtr.Zero)
                SetForegroundWindow(AppState.SourceWindowHandle);
        };

        return border;
    }

    private Separator MakeSeparator() => new()
    {
        Width      = 1,
        Height     = 22,
        Background = (System.Windows.Media.Brush)FindResource("Divider"),
        Margin     = new Thickness(3, 0, 3, 0)
    };
}

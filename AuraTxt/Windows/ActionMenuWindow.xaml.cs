using System.Threading;
using System.Windows;
using System.Windows.Media;
using AuraTxt.Core.Models;
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionNearCursor();
        BuildMenu();
        _ready = true;
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
        var wa      = SystemParameters.WorkArea;
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
        SafeClose();
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
            PositionNearCursor();
            IconPanel.Children.Clear();
            BuildMenu();
            UpdateLayout();
        }
        finally { AppState.IsMenuUpdating = false; }
    }

    private void SafeClose()
    {
        if (!_ready || _closing) return;
        if (AppState.IsMenuUpdating) return;
        _closing = true;
        // Suppress menu re-trigger for 2s (the mouse-up that clicked us fires globally)
        AppState.MenuSuppressUntil = DateTime.UtcNow.AddSeconds(2);
        Close();
    }

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
                Clipboard.SetText(_selectedText);
                break;
            case "speech":
                var synthesizer = new System.Speech.Synthesis.SpeechSynthesizer();
                synthesizer.SpeakAsync(_selectedText);
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
        border.PreviewMouseLeftButtonDown += (_, _) => DragMove();

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

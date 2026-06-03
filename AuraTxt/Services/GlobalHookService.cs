using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Gma.System.MouseKeyHook;
using AuraTxt.Core.Services;
using AuraTxt.Windows;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace AuraTxt.Services;

public class GlobalHookService
{
    private readonly ConfigService _config;
    private readonly HotkeyService _hotkeys;
    private IKeyboardMouseEvents? _hook;

    /// Physical pixel position where the left button was pressed. Used to tell a
    /// plain click (no movement) apart from a real drag-selection on mouse-up.
    private System.Drawing.Point _mouseDownPoint;

    /// Min pixel movement between down and up to count as a "drag" (text selection).
    /// A plain click stays under this and is ignored — no Ctrl+C, no menu.
    private const int DragThreshold = 5;

    /// Set true by OnMouseDoubleClick so the trailing MouseUp of a double-click
    /// sequence is silently skipped rather than treated as a new drag-selection.
    private bool _skipNextMouseUp;

    public GlobalHookService(ConfigService config, HotkeyService hotkeys)
    {
        _config  = config;
        _hotkeys = hotkeys;
    }

    public void Start()
    {
        _hook = Hook.GlobalEvents();
        _hook.MouseDownExt     += OnMouseDown;
        _hook.MouseUpExt       += OnMouseUp;
        _hook.MouseDoubleClick += OnMouseDoubleClick;
        _hotkeys.RegisterAll(_config.Load());
    }

    public void Stop()
    {
        if (_hook is null) return;
        _hook.MouseDownExt     -= OnMouseDown;
        _hook.MouseUpExt       -= OnMouseUp;
        _hook.MouseDoubleClick -= OnMouseDoubleClick;
        _hook.Dispose();
        _hook = null;
    }

    // ── Light-dismiss: close the action menu when clicking outside its bounds ─
    private void OnMouseDown(object? sender, MouseEventExtArgs e)
    {
        try
        {
            if (e.Button != System.Windows.Forms.MouseButtons.Left) return;

            // Record press position for the click-vs-drag test on mouse-up.
            _mouseDownPoint = new System.Drawing.Point(e.X, e.Y);

            if (AppState.ActiveMenu is null) return;

            var clickX = e.X;
            var clickY = e.Y;

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    var menu = AppState.ActiveMenu;
                    if (menu is null || !menu.IsVisible) return;

                    // Convert menu WPF DIPs → physical pixels for bounds comparison
                    var src = PresentationSource.FromVisual(menu);
                    if (src is null) return;

                    var toDevice = src.CompositionTarget.TransformToDevice;
                    var tl = toDevice.Transform(new System.Windows.Point(menu.Left, menu.Top));
                    var br = toDevice.Transform(new System.Windows.Point(
                        menu.Left + menu.ActualWidth,
                        menu.Top  + menu.ActualHeight));

                    // Click inside menu bounds → do nothing (let normal click handling run)
                    if (clickX >= tl.X && clickX <= br.X &&
                        clickY >= tl.Y && clickY <= br.Y)
                        return;

                    // Click outside → deferred light-dismiss.
                    // The 500 ms delay gives a MouseDoubleClick time to arrive and
                    // "claim" the event, updating the menu in-place instead of closing it.
                    // Do NOT reset LastProcessedText here — the same text is still
                    // highlighted in the source app, and clearing it would let the
                    // very next click re-pop the menu. The dedup cache is only
                    // re-armed when the user actually deselects (empty selection).
                    if (menu is ActionMenuWindow actionMenu)
                        actionMenu.DeferredClose();
                }
                catch { }
            });
        }
        catch { }
    }

    // ── Double-click: capture selected word and show/update the action menu ──
    private void OnMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        try
        {
            // Cancel any pending deferred light-dismiss — this double-click
            // "claims" the event and the menu will be updated in-place instead.
            if (AppState.ActiveMenu is ActionMenuWindow actionMenu)
                actionMenu.CancelDeferredClose();

            // Suppress the trailing MouseUp to prevent duplicate text capture
            // or an unwanted timer restart.
            _skipNextMouseUp = true;

            if (AppState.IsMonitoringPaused || AppState.IsMenuHidden) return;
            if (DateTime.UtcNow < AppState.MenuSuppressUntil) return;
            if (AppState.IsResultWindowOpen) return;

            var pos = new System.Drawing.Point(e.X, e.Y);

            Application.Current?.Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    if (AppState.IsResultWindowOpen) return;

                    var cfg  = _config.Load();
                    var text = await ClipboardService.GetSelectedTextAsync(
                        cfg.Settings.MenuTriggerDelayMs);

                    // Empty selection → re-arm dedup cache.
                    if (string.IsNullOrWhiteSpace(text)) { AppState.LastProcessedText = ""; return; }

                    // Same text + menu already visible → don't rebuild unnecessarily.
                    if (text == AppState.LastProcessedText
                        && AppState.ActiveMenu is not null
                        && AppState.ActiveMenu.IsVisible)
                        return;

                    AppState.LastProcessedText = text;

                    if (AppState.ActiveMenu is ActionMenuWindow existingMenu
                        && existingMenu.IsVisible)
                    {
                        // In-place update: reposition and rebuild content.
                        existingMenu.UpdateMenu(text, pos);
                    }
                    else
                    {
                        // No menu visible → create new one.
                        var menu = new ActionMenuWindow(cfg, text, pos);
                        menu.Show();
                    }
                }
                catch { }
            });
        }
        catch { }
    }

    // ── Mouse-up: capture selected text and show the action menu ─────────────
    private void OnMouseUp(object? sender, MouseEventExtArgs e)
    {
        try
        {
            // If the preceding MouseDoubleClick already handled this, skip the
            // trailing MouseUp to avoid a duplicate text capture or menu show.
            if (_skipNextMouseUp) { _skipNextMouseUp = false; return; }

            if (AppState.IsMonitoringPaused || AppState.IsMenuHidden) return;
            if (e.Button != System.Windows.Forms.MouseButtons.Left) return;

            // Physical click-vs-drag test (first line of defense): a plain click has
            // near-zero movement between down and up. Without a drag there is no text
            // selection, so bail out before touching the clipboard or the state locks.
            // This alone kills the "click empty space → menu re-pops" race.
            var dx = Math.Abs(e.X - _mouseDownPoint.X);
            var dy = Math.Abs(e.Y - _mouseDownPoint.Y);
            if (dx < DragThreshold && dy < DragThreshold) return;

            if (DateTime.UtcNow < AppState.MenuSuppressUntil) return;
            if (AppState.IsResultWindowOpen) return;

            var pos = new System.Drawing.Point(e.X, e.Y);

            Application.Current?.Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    if (AppState.IsResultWindowOpen) return;

                    var cfg  = _config.Load();
                    var text = await ClipboardService.GetSelectedTextAsync(
                        cfg.Settings.MenuTriggerDelayMs);

                    // Empty selection = user deselected → re-arm so the same text
                    // can trigger the menu again on a fresh selection.
                    if (string.IsNullOrWhiteSpace(text)) { AppState.LastProcessedText = ""; return; }

                    // Same text still highlighted → don't re-pop the menu.
                    if (text == AppState.LastProcessedText) return;

                    AppState.LastProcessedText = text;
                    var menu = new ActionMenuWindow(cfg, text, pos);
                    menu.Show();
                }
                catch { }
            });
        }
        catch { }
    }
}

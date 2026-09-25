using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Gma.System.MouseKeyHook;
using AuraTxt.Core.Services;
using AuraTxt.Windows;

namespace AuraTxt.Services;

public class GlobalHookService
{
    private readonly ConfigService _config;
    private readonly HotkeyService _hotkeys;
    private IKeyboardMouseEvents? _hook;

    /// Physical pixel position where the left button was pressed. Used to tell a
    /// plain click (no movement) apart from a real drag-selection on mouse-up.
    private System.Drawing.Point _mouseDownPoint;

    /// True when the left button went down inside the active action menu's bounds
    /// (e.g. on its drag-handle logo). Lets OnMouseUp recognize a menu drag/click
    /// — which DragMove reports to us as ordinary large mouse movement — instead of
    /// mistaking it for a text drag-selection elsewhere. See OnMouseUp.
    private bool _mouseDownInsideMenu;

    /// Min pixel movement between down and up to count as a "drag" (text selection).
    /// A plain click stays under this and is ignored — no Ctrl+C, no menu.
    private const int DragThreshold = 5;

    /// Set from OnMouseDown when the down is the second half of a double-click
    /// (MouseKeyHook reports Clicks==2 on that down event only — the paired MouseUp
    /// always reports Clicks==1, so OnMouseUp cannot detect this on its own). Consumed
    /// and reset at the top of the next OnMouseUp to route it to the double-click path
    /// instead of the drag/plain-click logic.
    private bool _isDoubleClick;

    /// Guards against a slow selection-capture (UIA + Ctrl+C fallback can take up to
    /// ~450ms) completing after the user has already moved on — e.g. started typing, or
    /// begun a new gesture — and popping a menu they no longer want. All access happens
    /// on the UI thread (Dispatcher), so no locking is needed.
    private CancellationTokenSource? _triggerCts;

    /// Cancels and clears any in-flight selection-capture, without starting a new one.
    /// Called whenever the user does something that makes a pending/about-to-appear menu
    /// stale: any mouse-down, or a key that dismisses the menu.
    private void CancelTrigger()
    {
        if (_triggerCts is null) return;
        _triggerCts.Cancel();
        _triggerCts.Dispose();
        _triggerCts = null;
    }

    /// Cancels any in-flight capture and returns a token for a new one.
    private CancellationToken NewTriggerToken()
    {
        CancelTrigger();
        _triggerCts = new CancellationTokenSource();
        return _triggerCts.Token;
    }

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
        _hook.KeyPress         += OnKeyPress;
        _hook.KeyDown          += OnKeyDown;
        _hotkeys.RegisterAll(_config.Load());
    }

    public void Stop()
    {
        if (_hook is null) return;
        _hook.MouseDownExt     -= OnMouseDown;
        _hook.MouseUpExt       -= OnMouseUp;
        _hook.KeyPress         -= OnKeyPress;
        _hook.KeyDown          -= OnKeyDown;
        _hook.Dispose();
        _hook = null;
    }

    // ── Light-dismiss: close the action menu when clicking outside its bounds ─
    private void OnMouseDown(object? sender, MouseEventExtArgs e)
    {
        try
        {
            if (e.Button != System.Windows.Forms.MouseButtons.Left) return;

            // A new gesture is starting anywhere on screen — any selection-capture still
            // in flight from a previous one is now stale and must not pop a menu once it
            // finishes. Unconditional (not gated on an existing menu) since the capture
            // this cancels may not have produced a menu yet either.
            Application.Current?.Dispatcher.BeginInvoke(CancelTrigger);

            // MouseKeyHook reports Clicks==2 only on the second down of a double-click
            // (the library's own timing/position-based detection, applied before this
            // event fires) — the paired MouseUp always reports Clicks==1, so this is the
            // only place double-clicks can be detected. See OnMouseUp.
            _isDoubleClick = e.Clicks == 2;

            // Record press position for the click-vs-drag test on mouse-up.
            _mouseDownPoint = new System.Drawing.Point(e.X, e.Y);

            if (AppState.ActiveMenu is null) { _mouseDownInsideMenu = false; return; }

            var clickX = e.X;
            var clickY = e.Y;
            var isDoubleClick = _isDoubleClick;

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    var menu = AppState.ActiveMenu;
                    if (menu is null || !menu.IsVisible) { _mouseDownInsideMenu = false; return; }

                    _mouseDownInsideMenu = IsPointInsideMenu(menu, clickX, clickY);
                    if (menu is not ActionMenuWindow actionMenu) return;

                    if (isDoubleClick)
                    {
                        // The second down of a double-click "claims" the menu — the
                        // trailing MouseUp will update it in place instead of closing it,
                        // so cancel any pending light-dismiss instead of (re)starting one.
                        actionMenu.CancelDeferredClose();
                    }
                    else if (!_mouseDownInsideMenu)
                    {
                        // Click outside → deferred light-dismiss.
                        // The 500 ms delay gives a double-click's second down time to
                        // arrive and cancel it above, updating the menu in-place instead
                        // of closing it. Do NOT reset LastProcessedText here — the same
                        // text is still highlighted in the source app, and clearing it
                        // would let the very next click re-pop the menu. The dedup cache
                        // is only re-armed when the user actually deselects (empty
                        // selection).
                        actionMenu.DeferredClose();
                    }
                }
                catch { }
            });
        }
        catch { }
    }

    /// True if (x,y) in physical screen pixels falls within menu's current on-screen
    /// bounds. Shared by OnMouseDown (gates the deferred light-dismiss) and OnMouseUp
    /// (recognizes an internal action-menu drag/click) — the menu may have moved
    /// between the down and up events via DragMove, so this is always re-evaluated
    /// against the menu's current position, not the one at mouse-down time.
    private static bool IsPointInsideMenu(Window menu, int x, int y)
    {
        if (!menu.IsVisible) return false;
        var src = PresentationSource.FromVisual(menu);
        if (src is null) return false;

        var toDevice = src.CompositionTarget.TransformToDevice;
        var tl = toDevice.Transform(new System.Windows.Point(menu.Left, menu.Top));
        var br = toDevice.Transform(new System.Windows.Point(
            menu.Left + menu.ActualWidth,
            menu.Top  + menu.ActualHeight));

        return x >= tl.X && x <= br.X && y >= tl.Y && y <= br.Y;
    }

    /// Captures the current selection and shows or updates the action menu.
    /// Shared by the drag-selection (OnMouseUp) and double-click paths. `allowInPlaceUpdate`
    /// no longer gates whether an existing menu may be reused (a visible menu is always
    /// reused now, for both paths — see below) — it only relaxes the text-match dedup
    /// guard for double-click, letting the user re-double-click the same already-processed
    /// word to reopen a menu that was dismissed, which a drag-reselection shouldn't do.
    private async Task CaptureAndShowMenuAsync(System.Drawing.Point pos, bool allowInPlaceUpdate, CancellationToken token)
    {
        if (AppState.IsResultWindowOpen) return;

        var cfg = _config.Load();
        // A game may be in the foreground — don't inject a synthetic Ctrl+C into it
        // (see ClipboardService, GameDetectionService).
        if (GameDetectionService.ShouldSkip(cfg.Settings)) return;

        var text = await ClipboardService.GetSelectedTextAsync(cfg.Settings.MenuTriggerDelayMs);

        // The user has since started a new gesture or dismissed the menu (see
        // CancelTrigger) — this result is stale. Discard silently, without touching the
        // selection state machine (it's already been updated, or will be, by whatever
        // superseded this attempt).
        if (token.IsCancellationRequested) return;

        // Empty selection → re-arm dedup cache.
        if (string.IsNullOrWhiteSpace(text)) { AppState.MarkDeselected(); return; }

        // Same text still relevant → don't rebuild unnecessarily. Drag-selection always
        // skips on a text match; double-click only skips if the menu is still visible
        // (otherwise it should reopen for the same word).
        var menuVisible = AppState.ActiveMenu is ActionMenuWindow { IsVisible: true };
        if (text == AppState.LastProcessedText && (!allowInPlaceUpdate || menuVisible)) return;

        AppState.MarkNewSelection(text);

        if (AppState.ActiveMenu is ActionMenuWindow existing && existing.IsVisible)
        {
            // Reuse the visible menu instead of creating a second one, regardless of which
            // path got here — a drag-selection landing while the previous menu is still
            // alive (e.g. mid its own DeferredClose countdown) used to always create a
            // brand-new window here, so both were on screen at once for a moment (flicker).
            existing.CancelDeferredClose();
            existing.UpdateMenu(text, pos);
        }
        else
        {
            // No menu visible → create new one.
            var menu = new ActionMenuWindow(cfg, text, pos);
            menu.Show();
        }
    }

    // ── Mouse-up: capture selected text and show the action menu ─────────────
    private void OnMouseUp(object? sender, MouseEventExtArgs e)
    {
        try
        {
            if (AppState.IsMonitoringPaused || AppState.IsMenuHidden) return;
            if (e.Button != System.Windows.Forms.MouseButtons.Left) return;

            // Consume the flag OnMouseDown set for this button's second down — the
            // paired MouseUp always reports Clicks==1 regardless (see OnMouseDown), so
            // this is the only way this event learns it's the tail of a double-click.
            var wasDoubleClick = _isDoubleClick;
            _isDoubleClick = false;
            if (wasDoubleClick)
            {
                if (DateTime.UtcNow < AppState.MenuSuppressUntil) return;
                if (AppState.IsResultWindowOpen) return;

                var doubleClickPos = new System.Drawing.Point(e.X, e.Y);
                AppState.SourceWindowHandle = ClipboardService.CaptureSourceWindow();

                Application.Current?.Dispatcher.BeginInvoke(async () =>
                {
                    try
                    {
                        var token = NewTriggerToken();
                        await CaptureAndShowMenuAsync(doubleClickPos, allowInPlaceUpdate: true, token);
                    }
                    catch { }
                });
                return;
            }

            // Physical click-vs-drag test (first line of defense): a plain click has
            // near-zero movement between down and up. Without a drag there is no text
            // selection, so bail out before touching the clipboard or the state locks.
            var dx = Math.Abs(e.X - _mouseDownPoint.X);
            var dy = Math.Abs(e.Y - _mouseDownPoint.Y);
            if (dx < DragThreshold && dy < DragThreshold)
            {
                _mouseDownInsideMenu = false;
                if (!AppState.SelectionActioned)
                {
                    // No action was taken — user dismissed the menu without acting.
                    // Plain click likely cleared the selection → re-arm immediately (Scenario B).
                    AppState.MarkDeselected();
                }
                else
                {
                    // Action was taken — selection might still be highlighted (silence shield).
                    // config.Load()/ShouldSkip() must not run on the hook callback itself — this
                    // branch fires on every plain click, and WH_MOUSE_LL expects to return almost
                    // immediately or Windows can lag/drop input system-wide. Defer the whole check.
                    Application.Current?.Dispatcher.BeginInvoke(async () =>
                    {
                        if (GameDetectionService.ShouldSkip(_config.Load().Settings)) return;
                        var t = await ClipboardService.GetSelectedTextAsync(50);
                        if (string.IsNullOrWhiteSpace(t))
                            AppState.MarkDeselected();
                    });
                }
                return;
            }

            if (DateTime.UtcNow < AppState.MenuSuppressUntil) return;
            if (AppState.IsResultWindowOpen) return;

            var pos = new System.Drawing.Point(e.X, e.Y);
            var wasInsideMenuOnDown = _mouseDownInsideMenu;
            _mouseDownInsideMenu = false;
            AppState.SourceWindowHandle = ClipboardService.CaptureSourceWindow();

            Application.Current?.Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    // The action menu's drag-handle logo moves the window via DragMove —
                    // to the global hook that looks exactly like a large mouse-down/up
                    // movement, which would otherwise be mistaken for a text
                    // drag-selection. If the gesture started and ends inside the
                    // (possibly now-moved) menu bounds, it's an internal interaction,
                    // not a selection — leave the menu alone.
                    if (wasInsideMenuOnDown && AppState.ActiveMenu is { } menu &&
                        IsPointInsideMenu(menu, pos.X, pos.Y))
                        return;

                    var token = NewTriggerToken();
                    await CaptureAndShowMenuAsync(pos, allowInPlaceUpdate: false, token);
                }
                catch { }
            });
        }
        catch { }
    }

    // ── Keyboard dismiss: close the menu when the user types or deletes ────────

    /// Cancels any in-flight capture and closes the menu, synchronously — called directly
    /// from the hook callback (which already runs on the UI thread, see App.xaml.cs
    /// _hook.Start()), not via Dispatcher.BeginInvoke. The deferred version used to let the
    /// real keystroke that triggered the dismiss (e.g. Delete) reach the target app *before*
    /// the menu actually closed — the menu was still on-screen, Topmost, at the moment some
    /// apps (observed with browser-hosted editors; native controls like Notepad were fine)
    /// processed that keystroke, and it silently didn't take effect, needing a second press.
    /// Closing synchronously means the menu is gone before the low-level hook returns and
    /// Windows delivers the key onward.
    private void DismissMenuNow()
    {
        try
        {
            CancelTrigger();
            if (AppState.ActiveMenu is ActionMenuWindow menu) menu.CloseNow();
        }
        catch { }
    }

    /// Fires for every printable character — close the menu so the user can type freely.
    private void OnKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar)) return;
        DismissMenuNow();
    }

    private static readonly HashSet<Keys> ModifierKeyCodes = new()
    {
        Keys.ControlKey, Keys.LControlKey, Keys.RControlKey,
        Keys.ShiftKey,   Keys.LShiftKey,   Keys.RShiftKey,
        Keys.Menu,       Keys.LMenu,       Keys.RMenu,
        Keys.LWin,       Keys.RWin
    };

    /// Catch Backspace, Delete, app-switch keys (Win, Alt+Tab), and editing shortcuts
    /// (Ctrl+anything, Shift+Insert) which KeyPress does not fire for.
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Track real Ctrl+C so ClipboardService can avoid injecting its own synthetic
        // Ctrl+C on top of a genuine one (see ClipboardService.NotifyRealCtrlC).
        var isCtrlC = e.Control && e.KeyCode == Keys.C;
        var isOwnSyntheticCtrlC = isCtrlC && ClipboardService.IsSyntheticCtrlCInFlight();

        if (isCtrlC && !isOwnSyntheticCtrlC)
            ClipboardService.NotifyRealCtrlC();

        // Any real Ctrl+<key> combo (copy/paste/cut/undo/bold/... — standard editing
        // shortcuts) or a legacy Shift+Insert paste means the user wants to do something
        // else entirely, not act on the selection via the menu — dismiss it immediately.
        // Our own synthetic capture Ctrl+C is excluded so it doesn't prematurely close a
        // menu that's mid in-place-update (see ClipboardService.IsSyntheticCtrlCInFlight).
        var isShortcut = (e.Control && !ModifierKeyCodes.Contains(e.KeyCode) && !isOwnSyntheticCtrlC)
                       || (e.Shift && e.KeyCode == Keys.Insert);

        var dismiss = isShortcut
                   || e.KeyCode is Keys.Back or Keys.Delete or Keys.LWin or Keys.RWin
                   || (e.Alt && e.KeyCode is Keys.Tab or Keys.F4);
        if (!dismiss) return;
        DismissMenuNow();
    }
}

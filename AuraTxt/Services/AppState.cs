namespace AuraTxt.Services;

public static class AppState
{
    public static bool IsMonitoringPaused { get; set; }
    public static bool IsMenuHidden       { get; set; }

    /// Suppress the action menu until this UTC time (prevents re-trigger after action selected).
    public static DateTime MenuSuppressUntil { get; set; } = DateTime.MinValue;

    /// True while a ResultWindow or InteractiveWindow is open — hook ignores all mouse-up events.
    public static bool IsResultWindowOpen { get; set; }

    /// Text that was most recently shown in the action menu. Prevents re-popping the menu when
    /// the same selection is still highlighted and the user clicks elsewhere (e.g. in the result window).
    /// Cleared when the result window closes so the user can deliberately re-process the same text.
    public static string LastProcessedText { get; set; } = "";

    /// The currently visible ActionMenuWindow. Set by the window itself; used by the mouse-down
    /// hook to implement light-dismiss (close on click outside bounds).
    public static System.Windows.Window? ActiveMenu { get; set; }

    /// True while the action menu is being updated in-place (double-click reposition).
    /// Prevents the Deactivated handler from closing the window during content rebuild.
    public static bool IsMenuUpdating { get; set; }

    public static double? SessionResultWindowWidth      { get; set; }
    public static double? SessionInteractiveWindowWidth { get; set; }

    public static IntPtr SourceWindowHandle { get; set; }

    /// True after an action was triggered for LastProcessedText (silence shield).
    /// On plain click: if false → clear LastProcessedText immediately (scenario B re-arm);
    ///                 if true → check async whether selection is gone before clearing.
    public static bool SelectionActioned { get; set; }

    // ── Selection state machine (SPEC.md §5.4) ──────────────────────────────
    // Idle / MenuShowing / ActionProcessed, encoded via LastProcessedText +
    // SelectionActioned. These are the only methods that should write either
    // field, so the three states stay consistent across call sites.

    /// Idle: no selection tracked, dedup cache cleared.
    public static void MarkDeselected()
    {
        LastProcessedText = "";
        SelectionActioned = false;
    }

    /// MenuShowing: a fresh selection was captured.
    public static void MarkNewSelection(string text)
    {
        LastProcessedText = text;
        SelectionActioned = false;
    }

    /// ActionProcessed: an action was triggered for the current selection.
    public static void MarkActionTaken()
    {
        SelectionActioned = true;
    }
}

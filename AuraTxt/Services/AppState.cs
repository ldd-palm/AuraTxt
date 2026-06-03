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
}

using System.Runtime.InteropServices;

namespace AuraTxt.Services;

// Triggers a paste into the source window that was focused before an AuraTxt action
// fired — either Windows' own clipboard-history flyout (Win+V) or a direct Ctrl+V of
// whatever's already on the clipboard. Never writes the clipboard itself (contrast with
// ClipboardService.ReplaceInSourceWindowAsync, which injects AI-generated text) — Paste
// only triggers "paste what's already there".
public static class ClipboardPasteService
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr extra);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const byte VK_CONTROL      = 0x11;
    private const byte VK_LWIN         = 0x5B;
    private const byte VK_V            = 0x56;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static async Task RunAsync(IntPtr sourceHwnd, bool useHistory)
    {
        if (sourceHwnd == IntPtr.Zero) return;
        try
        {
            SetForegroundWindow(sourceHwnd);
            await Task.Delay(150); // same figure ClipboardService.ReplaceInSourceWindowAsync uses

            if (useHistory)
            {
                // Windows shows its own clipboard-history flyout at the now-focused
                // window's caret; the user picks and Windows pastes directly. No
                // callback exists — our involvement ends the instant this fires.
                keybd_event(VK_LWIN, 0, 0,               UIntPtr.Zero);
                keybd_event(VK_V,    0, 0,               UIntPtr.Zero);
                keybd_event(VK_V,    0, KEYEVENTF_KEYUP,  UIntPtr.Zero);
                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP,  UIntPtr.Zero);
            }
            else
            {
                keybd_event(VK_CONTROL, 0, 0,               UIntPtr.Zero);
                keybd_event(VK_V,       0, 0,               UIntPtr.Zero);
                keybd_event(VK_V,       0, KEYEVENTF_KEYUP,  UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP,  UIntPtr.Zero);
            }
        }
        catch { }
    }
}

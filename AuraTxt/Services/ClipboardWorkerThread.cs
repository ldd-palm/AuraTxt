using System.Threading;
using System.Windows.Threading;

namespace AuraTxt.Services;

/// Dedicated STA thread with its own Dispatcher for UI Automation + clipboard work.
/// GlobalHookService's low-level mouse hook shares the main WPF thread — UI Automation
/// (cross-process COM, can take hundreds of ms in a slow/unresponsive app) or clipboard
/// contention blocking that thread stalls WH_MOUSE_LL's message pump, which is visible
/// system-wide as cursor/input lag. Running Dispatcher.Run() here gives this thread a
/// DispatcherSynchronizationContext, so ClipboardService's existing `await Task.Delay(...)`
/// polling loops keep resuming on this same STA thread (required for Clipboard/UIA) without
/// any changes to that code.
internal static class ClipboardWorkerThread
{
    private static readonly Dispatcher _dispatcher = Start();

    private static Dispatcher Start()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "AuraTxt-ClipboardWorker"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return dispatcher!;
    }

    public static Task<string> InvokeAsync(Func<Task<string>> work) =>
        _dispatcher.InvokeAsync(work).Task.Unwrap();
}

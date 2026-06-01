using System.Windows;
using Gma.System.MouseKeyHook;
using AuraTxt.Core.Services;
using AuraTxt.Windows;

namespace AuraTxt.Services;

public class GlobalHookService
{
    private readonly ConfigService _config;
    private readonly HotkeyService _hotkeys;
    private IKeyboardMouseEvents? _hook;

    public GlobalHookService(ConfigService config, HotkeyService hotkeys)
    {
        _config  = config;
        _hotkeys = hotkeys;
    }

    public void Start()
    {
        _hook = Hook.GlobalEvents();
        _hook.MouseUpExt += OnMouseUp;
        _hotkeys.RegisterAll(_config.Load());
    }

    public void Stop()
    {
        if (_hook is null) return;
        _hook.MouseUpExt -= OnMouseUp;
        _hook.Dispose();
        _hook = null;
    }

    private void OnMouseUp(object? sender, MouseEventExtArgs e)
    {
        if (AppState.IsMonitoringPaused || AppState.IsMenuHidden || e.Button != System.Windows.Forms.MouseButtons.Left) return;
        if (DateTime.UtcNow < AppState.MenuSuppressUntil) return;

        var pos = new System.Drawing.Point(e.X, e.Y);
        var cfg = _config.Load();

        // Dispatch entire clipboard + menu flow to UI thread
        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            var text = await ClipboardService.GetSelectedTextAsync(cfg.Settings.MenuTriggerDelayMs);
            if (string.IsNullOrWhiteSpace(text)) return;

            var menu = new ActionMenuWindow(cfg, text, pos);
            menu.Show();
        });
    }
}

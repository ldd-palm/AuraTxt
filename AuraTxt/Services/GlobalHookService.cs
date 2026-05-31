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
        if (AppState.IsMonitoringPaused || e.Button != System.Windows.Forms.MouseButtons.Left) return;
        var pos = new System.Drawing.Point(e.X, e.Y);
        _ = TryShowMenuAsync(pos);
    }

    private async Task TryShowMenuAsync(System.Drawing.Point cursorPos)
    {
        var cfg  = _config.Load();
        var text = await ClipboardService.GetSelectedTextAsync(cfg.Settings.MenuTriggerDelayMs);
        if (string.IsNullOrWhiteSpace(text)) return;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var menu = new ActionMenuWindow(cfg, text, cursorPos);
            menu.Show();
        });
    }
}

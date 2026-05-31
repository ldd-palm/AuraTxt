using System.Windows;
using AuraTxt.Core.Services;
using AuraTxt.Services;

namespace AuraTxt;

public partial class App : Application
{
    private TrayIconManager? _tray;
    private GlobalHookService? _hook;
    private HotkeyService? _hotkeys;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var config = new ConfigService();
        _hotkeys = new HotkeyService(config);
        _tray    = new TrayIconManager(config, () => Shutdown());
        _hook    = new GlobalHookService(config, _hotkeys);
        _hook.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Stop();
        _hotkeys?.UnregisterAll();
        _tray?.Dispose();
        base.OnExit(e);
    }
}

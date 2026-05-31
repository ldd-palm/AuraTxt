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

        DispatcherUnhandledException += (_, ex) =>
        {
            System.Windows.MessageBox.Show($"UI 线程异常:\n{ex.Exception}", "AuraTxt Error");
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            System.Windows.MessageBox.Show($"未处理异常:\n{ex.ExceptionObject}", "AuraTxt Error");

        try
        {
            var config = new ConfigService();
            _hotkeys = new HotkeyService(config);
            _tray    = new TrayIconManager(config, () => Shutdown());
            _hook    = new GlobalHookService(config, _hotkeys);
            _hook.Start();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"启动失败:\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}", "AuraTxt Error");
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Stop();
        _hotkeys?.UnregisterAll();
        _tray?.Dispose();
        base.OnExit(e);
    }
}

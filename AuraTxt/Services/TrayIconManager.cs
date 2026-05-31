using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using AuraTxt.Core.Services;

namespace AuraTxt.Services;

public class TrayIconManager : IDisposable
{
    private readonly TaskbarIcon _icon;

    public TrayIconManager(ConfigService config, Action onExit)
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = "AuraTxt"
        };

        // Load icon from embedded resource
        try
        {
            _icon.IconSource = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Resources/tray.ico"));
        }
        catch { /* icon optional for now */ }

        var menu = new ContextMenu();

        var toggle = new MenuItem { Header = "暂停监听" };
        toggle.Click += (_, _) =>
        {
            AppState.IsMonitoringPaused = !AppState.IsMonitoringPaused;
            toggle.Header = AppState.IsMonitoringPaused ? "恢复监听" : "暂停监听";
        };

        var settingsItem = new MenuItem { Header = "配置 (auracfg)" };
        settingsItem.Click += (_, _) =>
        {
            var auracfg = System.IO.Path.Combine(AppContext.BaseDirectory, "auracfg.exe");
            if (System.IO.File.Exists(auracfg))
                System.Diagnostics.Process.Start(auracfg);
            else
                System.Windows.MessageBox.Show("auracfg.exe not found in app directory.", "AuraTxt");
        };

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (_, _) => onExit();

        menu.Items.Add(toggle);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);

        _icon.ContextMenu = menu;
        _icon.ForceCreate();
    }

    public void Dispose() => _icon.Dispose();
}

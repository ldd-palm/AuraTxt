using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using AuraTxt.Core.Services;

namespace AuraTxt.Services;

public class TrayIconManager : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly MenuItem _toggleMonitorItem = null!;
    private readonly MenuItem _toggleMenuItem = null!;
    private readonly MenuItem _settingsItem = null!;

    public TrayIconManager(ConfigService config, Action onReload, Action onExit, Action? onToggleMonitor = null)
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = "AuraTxt"
        };

        SetTrayIcon();

        var menu = new ContextMenu();

        _toggleMonitorItem = new MenuItem { Header = "Service: Pause" };
        _toggleMonitorItem.Click += (_, _) =>
        {
            AppState.IsMonitoringPaused = !AppState.IsMonitoringPaused;
            _toggleMonitorItem.Header = AppState.IsMonitoringPaused
                ? "Service: Resume" : "Service: Pause";
            SetTrayIcon();
            onToggleMonitor?.Invoke();
        };

        _toggleMenuItem = new MenuItem { Header = "Hide Menu" };
        _toggleMenuItem.Click += (_, _) =>
        {
            AppState.IsMenuHidden = !AppState.IsMenuHidden;
            _toggleMenuItem.Header = AppState.IsMenuHidden ? "Show Menu" : "Hide Menu";
        };

        var reloadItem = new MenuItem { Header = "Reload Settings" };
        reloadItem.Click += (_, _) => onReload();

        _settingsItem = new MenuItem { Header = "Settings (auracfg)" };
        _settingsItem.Click += (_, _) =>
        {
            var configEditor = config.Load().Settings.ConfigEditor;
            if (string.IsNullOrEmpty(configEditor))
            {
                var auracfg = System.IO.Path.Combine(AppContext.BaseDirectory, "auracfg.exe");
                if (System.IO.File.Exists(auracfg))
                    System.Diagnostics.Process.Start(auracfg);
                else
                    System.Windows.MessageBox.Show("auracfg.exe not found in app directory.", "AuraTxt");
            }
            else
            {
                var cfgPath = System.IO.Path.Combine(AppContext.BaseDirectory, "config.json");
                System.Diagnostics.Process.Start(configEditor, cfgPath);
            }
        };

        menu.Opened += (_, _) =>
        {
            var editor = config.Load().Settings.ConfigEditor;
            var name = string.IsNullOrEmpty(editor)
                ? "auracfg"
                : System.IO.Path.GetFileNameWithoutExtension(editor);
            _settingsItem.Header = $"Settings ({name})";
        };

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => onExit();

        menu.Items.Add(_toggleMonitorItem);
        menu.Items.Add(_toggleMenuItem);
        menu.Items.Add(reloadItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(exitItem);

        _icon.ContextMenu = menu;
        _icon.ForceCreate();
    }

    private void SetTrayIcon()
    {
        var iconName = AppState.IsMonitoringPaused ? "aruatxt_paused.ico" : "aruatxt_active.ico";
        try
        {
            _icon.IconSource = new System.Windows.Media.Imaging.BitmapImage(
                new Uri($"pack://application:,,,/Resources/{iconName}"));
        }
        catch { /* icon optional */ }
    }

    public void RefreshIcon() => SetTrayIcon();

    public void Dispose() => _icon.Dispose();
}

using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using AuraTxt.Core.Services;

namespace AuraTxt.Services;

public class TrayIconManager : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly ConfigService _config;
    private readonly MenuItem _toggleMonitorItem = null!;
    private readonly MenuItem _toggleMenuItem = null!;
    private readonly MenuItem _settingsItem = null!;
    private UpdateInfo? _pendingUpdate;

    public TrayIconManager(ConfigService config, Action onReload, Action onExit, Action? onToggleMonitor = null)
    {
        _config = config;
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

        var aboutItem = new MenuItem { Header = "About" };
        aboutItem.Click += (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://github.com/ldd-palm/AuraTxt") { UseShellExecute = true });

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => onExit();

        menu.Items.Add(_toggleMonitorItem);
        menu.Items.Add(_toggleMenuItem);
        menu.Items.Add(reloadItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(aboutItem);
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

    /// Silent startup check only — does nothing if the user has turned it off, and
    /// never surfaces a failure or "no update" result (both are non-events for a
    /// background check nobody asked for). Manual checks live in AboutWindow, which
    /// calls UpdateService directly for its own inline feedback instead of going
    /// through this method. See docs/superpowers/specs/2026-07-24-tray-menu-about-redesign.md §5.
    public async Task CheckForUpdatesAsync()
    {
        if (!_config.Load().Settings.AutoUpdateCheckEnabled) return;

        var current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
            ?? new Version(0, 0);

        UpdateInfo? info;
        try
        {
            info = await UpdateService.CheckAsync(current);
        }
        catch
        {
            return;
        }

        if (info is null) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            _pendingUpdate = info;

            var cfg = _config.Load();
            if (info.Version != cfg.Settings.LastNotifiedUpdateVersion)
            {
                _icon.ShowNotification("AuraTxt", $"Version {info.Version} is available.", NotificationIcon.Info);
                cfg.Settings.LastNotifiedUpdateVersion = info.Version;
                _config.Save(cfg);
            }
        });
    }

    /// Lets AboutWindow's own independent update check update the tray's shared
    /// pending-update state without going through the balloon-showing path above —
    /// About shows its result inline, a balloon on top would be redundant.
    public void NotePendingUpdate(UpdateInfo? info) => _pendingUpdate = info;

    public void Dispose() => _icon.Dispose();
}

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
    private readonly MenuItem _updateItem = null!;
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

        _updateItem = new MenuItem { Header = "Check for Updates" };
        _updateItem.Click += (_, _) =>
        {
            if (_pendingUpdate is { } info)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    info.Url) { UseShellExecute = true });
            else
                _ = CheckForUpdatesAsync(manual: true);
        };

        menu.Opened += (_, _) =>
        {
            var editor = config.Load().Settings.ConfigEditor;
            var name = string.IsNullOrEmpty(editor)
                ? "auracfg"
                : System.IO.Path.GetFileNameWithoutExtension(editor);
            _settingsItem.Header = $"Settings ({name})";

            _updateItem.Header = _pendingUpdate is { } pending
                ? $"⬆ Update available (v{pending.Version})"
                : "Check for Updates";
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
        menu.Items.Add(_updateItem);
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

    /// Checks GitHub for a newer release. `manual` controls whether "no update"
    /// and "check failed" outcomes also get a balloon — they don't for the silent
    /// startup check. See docs/superpowers/specs/2026-07-24-auto-update-design.md §3/§4.
    public async Task CheckForUpdatesAsync(bool manual)
    {
        var current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
            ?? new Version(0, 0);

        UpdateInfo? info;
        try
        {
            info = await UpdateService.CheckAsync(current);
        }
        catch
        {
            if (manual)
                Application.Current.Dispatcher.Invoke(() =>
                    _icon.ShowNotification("AuraTxt", "Could not check for updates.", NotificationIcon.Error));
            return;
        }

        if (info is null)
        {
            if (manual)
                Application.Current.Dispatcher.Invoke(() =>
                    _icon.ShowNotification("AuraTxt", $"You're up to date (v{current.ToString(2)}).", NotificationIcon.Info));
            return;
        }

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

    public void Dispose() => _icon.Dispose();
}

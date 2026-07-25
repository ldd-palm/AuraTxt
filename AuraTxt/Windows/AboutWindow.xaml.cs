using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using AuraTxt.Core.Services;
using AuraTxt.Services;

namespace AuraTxt.Windows;

public partial class AboutWindow : Window
{
    private readonly ConfigService _config;
    private readonly TrayIconManager _tray;
    private UpdateInfo? _foundUpdate;

    public AboutWindow(ConfigService config, TrayIconManager tray)
    {
        InitializeComponent();
        _config = config;
        _tray = tray;

        var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
        VersionText.Text = $"v{version.ToString(2)}";
        RuntimeText.Text = $"Running on {RuntimeInformation.FrameworkDescription}";

        AutoUpdateCheckBox.IsChecked = config.Load().Settings.AutoUpdateCheckEnabled;

        UpdateStatusText.Text = "Checking for updates…";
        _ = CheckForUpdateAsync(version);
    }

    private async Task CheckForUpdateAsync(Version current)
    {
        try
        {
            var info = await UpdateService.CheckAsync(current);
            _tray.NotePendingUpdate(info);
            _foundUpdate = info;
            if (info is null)
            {
                UpdateStatusText.Text = "✓ You're up to date";
            }
            else
            {
                UpdateStatusText.Text = $"⬆ Update available: v{info.Version}";
                UpdateStatusText.Cursor = System.Windows.Input.Cursors.Hand;
            }
        }
        catch
        {
            UpdateStatusText.Text = "Could not check for updates";
        }
    }

    private void UpdateStatusText_Click(object sender, MouseButtonEventArgs e)
    {
        if (_foundUpdate is { } info)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                info.Url) { UseShellExecute = true });
    }

    private void AutoUpdateCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var cfg = _config.Load();
        cfg.Settings.AutoUpdateCheckEnabled = AutoUpdateCheckBox.IsChecked == true;
        _config.Save(cfg);
    }

    private void GitHubIcon_Click(object sender, MouseButtonEventArgs e) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "https://github.com/ldd-palm/AuraTxt") { UseShellExecute = true });

    private void ReleasesLink_Click(object sender, MouseButtonEventArgs e) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "https://github.com/ldd-palm/AuraTxt/releases") { UseShellExecute = true });

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

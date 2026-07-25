using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AuraTxt.Core.Services;
using AuraTxt.Resources;
using AuraTxt.Services;

namespace AuraTxt.Windows;

public partial class AboutWindow : Window
{
    private static readonly SolidColorBrush UpToDateBrush = new(System.Windows.Media.Color.FromRgb(0x25, 0x63, 0xEB));
    private static readonly SolidColorBrush UpdateAvailableBrush = new(System.Windows.Media.Color.FromRgb(0xDC, 0x26, 0x26));

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
        RuntimeText.Text = string.Format(Strings.About_RuntimeFormat, RuntimeInformation.FrameworkDescription);

        AutoUpdateToggle.IsChecked = config.Load().Settings.AutoUpdateCheckEnabled;

        UpdateStatusText.Text = Strings.About_CheckingForUpdates;
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
                UpdateStatusText.Text = Strings.About_UpToDate;
                UpdateStatusText.Foreground = UpToDateBrush;
            }
            else
            {
                UpdateStatusText.Text = string.Format(Strings.About_UpdateAvailableFormat, info.Version);
                UpdateStatusText.Foreground = UpdateAvailableBrush;
                UpdateStatusText.Cursor = System.Windows.Input.Cursors.Hand;
            }
        }
        catch
        {
            UpdateStatusText.Text = Strings.About_CheckFailed;
        }
    }

    private void UpdateStatusText_Click(object sender, MouseButtonEventArgs e)
    {
        if (_foundUpdate is { } info)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                info.Url) { UseShellExecute = true });
    }

    private void AutoUpdateToggle_Changed(object sender, RoutedEventArgs e)
    {
        var cfg = _config.Load();
        cfg.Settings.AutoUpdateCheckEnabled = AutoUpdateToggle.IsChecked == true;
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

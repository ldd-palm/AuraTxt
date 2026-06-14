using System.Globalization;
using System.Windows;
using System.Windows.Media;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using AuraTxt.Services;

namespace AuraTxt;

public partial class App : Application
{
    private TrayIconManager? _tray;
    private GlobalHookService? _hook;
    private HotkeyService? _hotkeys;
    private ConfigService? _config;
    private System.Threading.Mutex? _instanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single-instance guard
        _instanceMutex = new System.Threading.Mutex(
            initiallyOwned: true,
            name: "Global\\AuraTxt-SingleInstance-{A3F2C8D1-6E4B-4A9F-B7E2-1C5D8F3A2B6E}",
            out bool createdNew);
        if (!createdNew)
        {
            _instanceMutex.Dispose();
            System.Windows.MessageBox.Show(
                "AuraTxt is already running.\nCheck the system tray.",
                "AuraTxt", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Parse --log / -log flag
        if (e.Args.Any(a => a == "--log" || a == "-log"))
        {
            LogService.Enabled = true;
            LogService.LogPath = System.IO.Path.Combine(AppContext.BaseDirectory, "auratxt.log");
            LogService.Info("=== AuraTxt session start ===");
        }

        DispatcherUnhandledException += (_, ex) =>
        {
            System.Windows.MessageBox.Show($"UI thread exception:\n{ex.Exception}", "AuraTxt Error");
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            System.Windows.MessageBox.Show($"Unhandled exception:\n{ex.ExceptionObject}", "AuraTxt Error");

        try
        {
            ThemeService.EnsureScaffold();   // create themes/ dir + default light.json/dark.json
            PromptService.EnsureScaffold();   // ensure Prompts dir + default system.md/template.md
            ProfileService.EnsureScaffold();  // seed profiles/ dir + embedded JSONs
            _config  = new ConfigService();
            ApplyTheme(_config.Load().Settings.Theme);
            _hotkeys = new HotkeyService(_config);
            _tray    = new TrayIconManager(_config, ReloadConfig, () => Shutdown(), () =>
            {
                if (AppState.IsMonitoringPaused)
                    _hotkeys!.UnregisterAll();
                else
                    _hotkeys!.RegisterAll(_config!.Load());
            });
            _hook    = new GlobalHookService(_config, _hotkeys);
            _hook.Start();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Startup failed:\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}", "AuraTxt Error");
            Shutdown();
        }
    }

    /// <summary>Loads a theme JSON file and inserts it into the app's merged dictionaries.</summary>
    internal static void ApplyTheme(string themeId)
    {
        var tf = ThemeService.LoadTheme(themeId);
        var dict = new ResourceDictionary();

        foreach (var (key, value) in tf.Colors)
        {
            if (value.StartsWith("#"))
            {
                try
                {
                    var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);
                    dict[key] = new SolidColorBrush(color);
                }
                catch { /* skip malformed color */ }
            }
            else
            {
                // Numeric value (e.g. ShadowOpacity)
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    dict[key] = d;
            }
        }

        // Replace any existing theme dictionary (first position); Styles.xaml stays put.
        var merged = Current.Resources.MergedDictionaries;
        if (merged.Count > 0 && merged[0].Source is null)
            merged.RemoveAt(0);
        merged.Insert(0, dict);
    }

    private void ReloadConfig()
    {
        try
        {
            ProfileService.Reload();
            var cfg = _config!.Load();
            ApplyTheme(cfg.Settings.Theme);
            _hotkeys!.RegisterAll(cfg);
            _tray!.RefreshIcon();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Reload failed:\n{ex.Message}", "AuraTxt Error");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Stop();
        _hotkeys?.UnregisterAll();
        _tray?.Dispose();
        try { _instanceMutex?.ReleaseMutex(); } catch { }
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}

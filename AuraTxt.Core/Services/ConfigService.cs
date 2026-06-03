using System.Text.Json;
using AuraTxt.Core.Models;

namespace AuraTxt.Core.Services;

public class ConfigService
{
    private static readonly string DefaultConfigDir = AppContext.BaseDirectory;

    public static string DefaultConfigPath =>
        Path.Combine(DefaultConfigDir, "config.json");

    private readonly string _path;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ConfigService() : this(DefaultConfigPath) { }
    public ConfigService(string path) => _path = path;

    public ConfigRoot Load()
    {
        if (!File.Exists(_path))
            return CreateDefault();

        // Retry on transient file locks (e.g. another process is mid-save).
        for (int retry = 0; ; retry++)
        {
            try
            {
                var json = File.ReadAllText(_path);
                var cfg = JsonSerializer.Deserialize<ConfigRoot>(json, JsonOpts) ?? CreateDefault();

                // Backfill: old configs had no Enabled field → default(bool)=false → fix to true.
                foreach (var (_, provider) in cfg.Models)
                    foreach (var model in provider.Models)
                        if (!model.Enabled && !string.IsNullOrEmpty(model.TargetModel))
                            model.Enabled = true;

                return cfg;
            }
            catch (IOException) when (retry < 3)
            {
                Thread.Sleep(100);
            }
        }
    }

    public void Save(ConfigRoot config)
    {
        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, JsonOpts));
        File.Move(tmp, _path, overwrite: true);
    }

    /// Copies current config.json → config.json.bak, then saves new content.
    public void SaveWithBackup(ConfigRoot config)
    {
        if (File.Exists(_path))
            File.Copy(_path, _path + ".bak", overwrite: true);
        Save(config);
    }

    /// Restores config.json from config.json.bak.
    public void Restore()
    {
        var bak = _path + ".bak";
        if (!File.Exists(bak))
            throw new FileNotFoundException("No backup found at " + bak, bak);
        File.Copy(bak, _path, overwrite: true);
    }

    private ConfigRoot CreateDefault()
    {
        var cfg = new ConfigRoot();
        cfg.Models["default"] = new ProviderConfig
        {
            DisplayName = "Built-in",
            BaseUrl     = "",
            ApiKey      = "",
            Models      = new()
            {
                new ModelEntry { TargetModel = "Google_Translate", Alias = "GTrans", DisableThinking = false, Enabled = true },
                new ModelEntry { TargetModel = "Youdao_Dict",      Alias = "Youdao",  DisableThinking = false, Enabled = true }
            }
        };

        // System actions — undeletable, model-less, routed by WPF
        cfg.Actions.Add(new ActionItem
        {
            Id       = "copy",
            Name     = "Copy",
            Icon     = "clipboard-copy",
            Hotkey   = "",
            Enabled  = true,
            IsSystem = true
        });
        cfg.Actions.Add(new ActionItem
        {
            Id       = "speech",
            Name     = "Speech",
            Icon     = "speech",
            Hotkey   = "Ctrl+E",
            Enabled  = true,
            IsSystem = true
        });

        Save(cfg);
        return cfg;
    }
}

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

    // mtime-keyed JSON text cache: Load() sits on the hot path (every text selection),
    // so skip the disk read when the file hasn't changed. Deserialization still runs
    // per call — every caller gets a fresh, independently mutable ConfigRoot.
    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, (DateTime Mtime, string Json)> JsonCache = new();

    /// Last loaded settings — used by AiClient for TargetLanguage and SystemPrompt.
    public static AppSettings? DefaultSettings { get; private set; }

    public ConfigRoot Load()
    {
        if (!File.Exists(_path))
            return CreateDefault();

        // Retry on transient file locks (e.g. another process is mid-save).
        for (int retry = 0; ; retry++)
        {
            try
            {
                var mtime = File.GetLastWriteTimeUtc(_path);
                string json;
                lock (CacheLock)
                {
                    if (JsonCache.TryGetValue(_path, out var cached) && cached.Mtime == mtime)
                        json = cached.Json;
                    else
                    {
                        json = File.ReadAllText(_path);
                        JsonCache[_path] = (mtime, json);
                    }
                }
                var cfg = JsonSerializer.Deserialize<ConfigRoot>(json, JsonOpts) ?? CreateDefault();

                NormaliseThinkingModes(cfg);
                EnsureSystemAction(cfg, "copy",   "Copy",   "clipboard-copy", "");
                EnsureSystemAction(cfg, "speech", "Speech", "speech",         "Ctrl+E");
                EnsureSystemAction(cfg, "google", "Google", "search",         "");
                DefaultSettings = cfg.Settings;
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

    private static void EnsureSystemAction(ConfigRoot cfg, string id, string name, string icon, string hotkey)
    {
        if (cfg.Actions.Any(a => a.Id == id)) return;
        cfg.Actions.Add(new ActionItem
        {
            Id = id, Name = name, Icon = icon, Hotkey = hotkey,
            Enabled = true, IsSystem = true, ThinkingMode = "disable"
        });
    }

    private static void NormaliseThinkingModes(ConfigRoot cfg)
    {
        foreach (var action in cfg.Actions)
        {
            if (action.ThinkingMode is not ("disable" or "enable_high"))
            {
                LogService.Error($"Invalid ThinkingMode '{action.ThinkingMode}' on action '{action.Id}'; defaulting to 'disable'");
                action.ThinkingMode = "disable";
            }
        }
    }

    private ConfigRoot CreateDefault()
    {
        var cfg = new ConfigRoot();
        cfg.Models["default"] = new ProviderConfig
        {
            DisplayName = "Built-in",
            BaseUrl     = "",
            ApiKey      = "",
            AdapterType = "openai_compatible",
            Models      = new()
            {
                new ModelEntry { TargetModel = "Google_Translate", Alias = "GTrans", Enabled = true },
                new ModelEntry { TargetModel = "Youdao_Dict",      Alias = "Youdao",  Enabled = true }
            }
        };

        cfg.Actions.Add(new ActionItem
        {
            Id           = "copy",
            Name         = "Copy",
            Icon         = "clipboard-copy",
            Hotkey       = "",
            Enabled      = true,
            IsSystem     = true,
            ThinkingMode = "disable"
        });
        cfg.Actions.Add(new ActionItem
        {
            Id           = "speech",
            Name         = "Speech",
            Icon         = "speech",
            Hotkey       = "Ctrl+E",
            Enabled      = true,
            IsSystem     = true,
            ThinkingMode = "disable"
        });
        cfg.Actions.Add(new ActionItem
        {
            Id           = "google",
            Name         = "Google",
            Icon         = "search",
            Hotkey       = "",
            Enabled      = true,
            IsSystem     = true,
            ThinkingMode = "disable"
        });

        DefaultSettings = cfg.Settings;
        Save(cfg);
        return cfg;
    }
}

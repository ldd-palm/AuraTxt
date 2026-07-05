using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class ConfigServiceTests : IDisposable
{
    private readonly string _tmpPath =
        Path.Combine(Path.GetTempPath(), $"auratxt_test_{Guid.NewGuid()}.json");
    private readonly ConfigService _svc;

    public ConfigServiceTests() => _svc = new ConfigService(_tmpPath);

    [Fact]
    public void Load_CreatesDefaultWithBuiltinProvider_WhenFileAbsent()
    {
        var cfg = _svc.Load();
        Assert.True(cfg.Models.ContainsKey("default"));
        var def = cfg.Models["default"];
        Assert.Equal(3, def.Models.Count);
        Assert.Equal("Google_Translate", def.Models[0].TargetModel);
        Assert.Equal("Youdao_Dict",      def.Models[1].TargetModel);
        Assert.Equal("Terminal",         def.Models[2].TargetModel);
    }

    [Fact]
    public void Load_InjectsTerminalBuiltin_ForExistingConfigMissingIt()
    {
        // Simulate an old config.json saved before the Terminal built-in existed.
        var cfg = _svc.Load();
        cfg.Models["default"].Models.RemoveAll(m => m.TargetModel == "Terminal");
        _svc.Save(cfg);
        var onDiskBefore = File.ReadAllText(_tmpPath);

        var reloaded = _svc.Load();

        Assert.Contains(reloaded.Models["default"].Models, m => m.TargetModel == "Terminal");
        Assert.Equal(onDiskBefore, File.ReadAllText(_tmpPath)); // in-memory only, not persisted
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var cfg = _svc.Load();
        cfg.Models["openai"] = new ProviderConfig
        {
            DisplayName = "OpenAI",
            Models      = new() { new ModelEntry { TargetModel = "gpt-4o", Alias = "4o" } }
        };
        _svc.Save(cfg);
        var loaded = _svc.Load();
        Assert.True(loaded.Models.ContainsKey("openai"));
        Assert.Equal("OpenAI", loaded.Models["openai"].DisplayName);
        Assert.Equal("gpt-4o", loaded.Models["openai"].Models[0].TargetModel);
    }

    [Fact]
    public void Save_NoTempFileRemains()
    {
        _svc.Save(_svc.Load());
        Assert.False(File.Exists(_tmpPath + ".tmp"));
    }

    [Fact]
    public void SaveWithBackup_CreatesBakFile()
    {
        var cfg = _svc.Load();
        _svc.SaveWithBackup(cfg);
        Assert.True(File.Exists(_tmpPath + ".bak"));
    }

    [Fact]
    public void Restore_RestoresFromBak()
    {
        var original = _svc.Load();
        original.Settings.FontSize = 14;
        _svc.SaveWithBackup(original);

        var changed = _svc.Load();
        changed.Settings.FontSize = 99;
        _svc.Save(changed);
        Assert.Equal(99, _svc.Load().Settings.FontSize);

        _svc.Restore();
        Assert.Equal(14, _svc.Load().Settings.FontSize);
    }

    [Fact]
    public void Restore_ThrowsWhenNoBakExists()
    {
        Assert.Throws<FileNotFoundException>(() => _svc.Restore());
    }

    public void Dispose()
    {
        foreach (var f in new[] { _tmpPath, _tmpPath + ".bak", _tmpPath + ".tmp" })
            if (File.Exists(f)) File.Delete(f);
    }
}

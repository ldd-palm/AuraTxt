using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class ConfigServiceTests : IDisposable
{
    private readonly string _tmpPath = Path.Combine(Path.GetTempPath(), $"auratxt_test_{Guid.NewGuid()}.json");
    private readonly ConfigService _svc;

    public ConfigServiceTests()
    {
        _svc = new ConfigService(_tmpPath);
    }

    [Fact]
    public void Load_ReturnDefault_WhenFileAbsent()
    {
        var cfg = _svc.Load();
        Assert.NotNull(cfg);
        Assert.Equal("google-translate", cfg.System.GoogleTranslate.Provider);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var cfg = _svc.Load();
        cfg.Models["test"] = new ModelPlatform { DisplayName = "Test", TargetModel = "gpt-4o" };
        _svc.Save(cfg);

        var loaded = _svc.Load();
        Assert.True(loaded.Models.ContainsKey("test"));
        Assert.Equal("Test", loaded.Models["test"].DisplayName);
    }

    [Fact]
    public void Save_NoTempFileRemains()
    {
        _svc.Save(_svc.Load());
        Assert.False(File.Exists(_tmpPath + ".tmp"));
    }

    public void Dispose()
    {
        if (File.Exists(_tmpPath)) File.Delete(_tmpPath);
    }
}

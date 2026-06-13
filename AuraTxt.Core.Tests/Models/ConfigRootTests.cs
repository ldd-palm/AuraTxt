using AuraTxt.Core.Models;
using Xunit;

namespace AuraTxt.Core.Tests.Models;

public class ConfigRootTests
{
    private static ConfigRoot CfgWithDisabledModel()
    {
        var cfg = new ConfigRoot();
        cfg.Models["default"] = new ProviderConfig
        {
            DisplayName = "Built-in",
            Models =
            [
                new ModelEntry { TargetModel = "Google_Translate", Alias = "GTrans" },
                new ModelEntry { TargetModel = "Youdao_Dict",      Alias = "Youdao" }
            ]
        };
        cfg.Models["acme"] = new ProviderConfig
        {
            DisplayName = "Acme",
            Models =
            [
                new ModelEntry { TargetModel = "fast-1",  Alias = "fast",  Enabled = true  },
                new ModelEntry { TargetModel = "slow-1",  Alias = "slow",  Enabled = false }
            ]
        };
        return cfg;
    }

    [Fact]
    public void AllEnabledModelAliases_FiltersDisabledUserModels()
    {
        var refs = CfgWithDisabledModel().AllEnabledModelAliases().Select(r => r.Ref).ToList();
        Assert.Contains("acme/fast-1", refs);
        Assert.DoesNotContain("acme/slow-1", refs);
    }

    [Fact]
    public void AllEnabledModelAliases_AlwaysIncludesBuiltins()
    {
        var refs = CfgWithDisabledModel().AllEnabledModelAliases().Select(r => r.Ref).ToList();
        Assert.Contains("default/Google_Translate", refs);
        Assert.Contains("default/Youdao_Dict", refs);
    }

    [Fact]
    public void AllEnabledModelRefs_FiltersDisabledUserModels()
    {
        var refs = CfgWithDisabledModel().AllEnabledModelRefs().Select(r => r.Ref).ToList();
        Assert.Contains("acme/fast-1", refs);
        Assert.DoesNotContain("acme/slow-1", refs);
    }

    [Fact]
    public void AllModelAliases_IncludesDisabledModels()
    {
        var refs = CfgWithDisabledModel().AllModelAliases().Select(r => r.Ref).ToList();
        Assert.Contains("acme/slow-1", refs);
    }

    [Fact]
    public void ResolveModel_SplitsProviderAndTarget()
    {
        var resolved = CfgWithDisabledModel().ResolveModel("acme/fast-1");
        Assert.NotNull(resolved);
        Assert.Equal("Acme", resolved!.Value.provider.DisplayName);
        Assert.Equal("fast-1", resolved.Value.model.TargetModel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-slash")]
    [InlineData("unknown/model")]
    [InlineData("acme/unknown")]
    public void ResolveModel_ReturnsNullForInvalidRefs(string modelRef)
        => Assert.Null(CfgWithDisabledModel().ResolveModel(modelRef));
}

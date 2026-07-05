using AuraTxt.Core.Models;
using Xunit;

namespace AuraTxt.Core.Tests.Models;

public class ModelTests
{
    [Fact]
    public void ModelEntry_ProfileId_DefaultsToEmpty()
    {
        var m = new ModelEntry { TargetModel = "gpt-4o", Alias = "gpt-4o" };
        Assert.Equal("", m.ProfileId);
    }

    [Fact]
    public void ActionItem_ThinkingMode_DefaultsToDisable()
    {
        var a = new ActionItem();
        Assert.Equal("disable", a.ThinkingMode);
    }

    [Fact]
    public void ActionItem_IsSystemModel_TrueForDefaultPrefix()
    {
        var a = new ActionItem { ModelId = "default/Google_Translate" };
        Assert.True(a.IsSystemModel);
    }

    [Fact]
    public void ActionItem_IsSystemModel_FalseForUserProvider()
    {
        var a = new ActionItem { ModelId = "openai/gpt-4o" };
        Assert.False(a.IsSystemModel);
    }

    [Fact]
    public void ConfigRoot_ResolveModel_FindsCorrectEntry()
    {
        var cfg = new ConfigRoot();
        cfg.Models["openai"] = new ProviderConfig
        {
            DisplayName = "OpenAI",
            Models      = new() { new ModelEntry { TargetModel = "gpt-4o", Alias = "gpt-4o" } }
        };
        var result = cfg.ResolveModel("openai/gpt-4o");
        Assert.NotNull(result);
        Assert.Equal("OpenAI",  result.Value.provider.DisplayName);
        Assert.Equal("gpt-4o", result.Value.model.TargetModel);
    }

    [Fact]
    public void ConfigRoot_ResolveModel_ReturnsNullForMissing()
    {
        var cfg = new ConfigRoot();
        Assert.Null(cfg.ResolveModel("nonexistent/model"));
        Assert.Null(cfg.ResolveModel("noslash"));
        Assert.Null(cfg.ResolveModel(""));
    }

    [Fact]
    public void ConfigRoot_AllModelRefs_DefaultModelsLast()
    {
        var cfg = new ConfigRoot();
        cfg.Models["openai"] = new ProviderConfig
        {
            DisplayName = "OpenAI",
            Models      = new() { new ModelEntry { TargetModel = "gpt-4o", Alias = "4o" } }
        };
        cfg.Models["default"] = new ProviderConfig
        {
            DisplayName = "Built-in",
            Models = new()
            {
                new ModelEntry { TargetModel = "Google_Translate", Alias = "GTrans" },
                new ModelEntry { TargetModel = "Youdao_Dict",      Alias = "Youdao" }
            }
        };
        var refs = cfg.AllModelRefs().ToList();
        Assert.Equal(3, refs.Count);
        Assert.Equal("openai/gpt-4o",            refs[0].Ref);
        Assert.Equal("default/Google_Translate", refs[1].Ref);
        Assert.Equal("default/Youdao_Dict",      refs[2].Ref);
    }

    [Fact]
    public void ConfigRoot_AllModelRefs_IncludesAnyBuiltinModelGenerically()
    {
        // Regression: AllModelRefs/AllEnabledModelRefs used to hardcode lookups for exactly
        // Google_Translate/Youdao_Dict, so a third built-in (e.g. Terminal) wouldn't appear.
        var cfg = new ConfigRoot();
        cfg.Models["default"] = new ProviderConfig
        {
            DisplayName = "Built-in",
            Models = new()
            {
                new ModelEntry { TargetModel = "Google_Translate", Alias = "GTrans",   Enabled = true },
                new ModelEntry { TargetModel = "Youdao_Dict",      Alias = "Youdao",   Enabled = true },
                new ModelEntry { TargetModel = "Terminal",         Alias = "Terminal", Enabled = true }
            }
        };

        var refs        = cfg.AllModelRefs().ToList();
        var enabledRefs = cfg.AllEnabledModelRefs().ToList();

        Assert.Contains(refs,        r => r.Ref == "default/Terminal");
        Assert.Contains(enabledRefs, r => r.Ref == "default/Terminal");
        Assert.Equal(3, refs.Count);
        Assert.Equal(3, enabledRefs.Count);
    }
}

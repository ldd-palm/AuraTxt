using AuraTxt.Core.Models;
using Xunit;

namespace AuraTxt.Core.Tests.Models;

public class ModelTests
{
    [Fact]
    public void ModelPlatform_Alias_CombinesProviderAndTargetModel()
    {
        var m = new ModelPlatform { Provider = "openai-compatible", TargetModel = "deepseek-chat" };
        Assert.Equal("openai-compatible/deepseek-chat", m.Alias);
    }

    [Fact]
    public void ActionItem_IsSystemModel_TrueWhenDollarPrefix()
    {
        var a = new ActionItem { ModelId = "$google-translate" };
        Assert.True(a.IsSystemModel);
    }

    [Fact]
    public void ActionItem_IsSystemModel_FalseForNormalId()
    {
        var a = new ActionItem { ModelId = "deepseek" };
        Assert.False(a.IsSystemModel);
    }

    [Fact]
    public void ConfigRoot_HasSystemDefaults()
    {
        var c = new ConfigRoot();
        Assert.Equal("google-translate", c.System.GoogleTranslate.Provider);
        Assert.Equal("youdao-dict",      c.System.YoudaoDict.Provider);
    }
}

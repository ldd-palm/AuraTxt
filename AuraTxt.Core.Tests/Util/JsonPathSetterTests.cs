using System.Text.Json.Nodes;
using AuraTxt.Core.Models;
using AuraTxt.Core.Util;
using Xunit;

namespace AuraTxt.Core.Tests.Util;

public class JsonPathSetterTests
{
    [Fact]
    public void SingleSegment_SetsTopLevel()
    {
        var root = new JsonObject();
        JsonPathSetter.SetPath(root, "chat_template_kwargs",
            new JsonObject { ["thinking"] = false });
        Assert.Equal(false, root["chat_template_kwargs"]!["thinking"]!.GetValue<bool>());
    }

    [Fact]
    public void TwoSegments_CreatesIntermediate()
    {
        var root = new JsonObject();
        JsonPathSetter.SetPath(root, "generationConfig.thinkingConfig",
            new JsonObject { ["thinkingBudget"] = 0 });
        var nested = (JsonObject)root["generationConfig"]!;
        Assert.Equal(0, nested["thinkingConfig"]!["thinkingBudget"]!.GetValue<int>());
    }

    [Fact]
    public void LeafAlreadyObject_ShallowMerges()
    {
        var root = new JsonObject
        {
            ["generationConfig"] = new JsonObject { ["temperature"] = 0.3 }
        };
        JsonPathSetter.SetPath(root, "generationConfig",
            new JsonObject { ["topP"] = 0.9 });
        var gc = (JsonObject)root["generationConfig"]!;
        Assert.Equal(0.3, gc["temperature"]!.GetValue<double>());
        Assert.Equal(0.9, gc["topP"]!.GetValue<double>());
    }

    [Fact]
    public void IntermediateIsScalar_Throws()
    {
        var root = new JsonObject { ["a"] = 42 };
        Assert.Throws<ProfileApplicationException>(() =>
            JsonPathSetter.SetPath(root, "a.b", new JsonObject { ["x"] = 1 }));
    }

    [Fact]
    public void ThreeSegments_CreatesAllIntermediates()
    {
        var root = new JsonObject();
        JsonPathSetter.SetPath(root, "a.b.c", new JsonObject { ["val"] = 99 });
        Assert.Equal(99, root["a"]!["b"]!["c"]!["val"]!.GetValue<int>());
    }
}

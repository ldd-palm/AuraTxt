using AuraTxt.Core.Util;
using Xunit;

namespace AuraTxt.Core.Tests.Util;

public class TagStripFilterTests
{
    private static TagStripFilter Think() => new(["<think>...</think>"]);

    [Fact]
    public void NoTag_PassesThrough()
    {
        var f = Think();
        Assert.Equal("hello", f.Feed("hello"));
        Assert.Equal(" world", f.Feed(" world"));
        Assert.Equal("", f.Flush());
    }

    [Fact]
    public void SingleChunk_Strips()
    {
        var f = Think();
        Assert.Equal("result", f.Feed("<think>reasoning</think>result"));
        Assert.Equal("", f.Flush());
    }

    [Fact]
    public void TagSpansChunks()
    {
        var f = Think();
        Assert.Equal("", f.Feed("<thi"));
        Assert.Equal("", f.Feed("nk>inside"));
        Assert.Equal("", f.Feed("</thi"));
        Assert.Equal("after", f.Feed("nk>after"));
        Assert.Equal("", f.Flush());
    }

    [Fact]
    public void Unclosed_FlushDiscards()
    {
        var f = Think();
        f.Feed("<think>no close");
        Assert.Equal("", f.Flush());
    }

    [Fact]
    public void MultipleStrips_InOneChunk()
    {
        var f = Think();
        var result = f.Feed("a<think>x</think>b<think>y</think>c") + f.Flush();
        Assert.Equal("abc", result);
    }

    [Fact]
    public void MultiPattern_BothStripped()
    {
        var f = new TagStripFilter(["<think>...</think>", "<reasoning>...</reasoning>"]);
        var result = f.Feed("a<think>T</think>b<reasoning>R</reasoning>c") + f.Flush();
        Assert.Equal("abc", result);
    }

    [Fact]
    public void ContentBeforeTag_Emitted()
    {
        var f = Think();
        var result = f.Feed("prefix<think>hidden</think>suffix") + f.Flush();
        Assert.Equal("prefixsuffix", result);
    }

    [Fact]
    public void EmptyPatterns_PassesEverything()
    {
        var f = new TagStripFilter([]);
        Assert.Equal("hello<think>world</think>", f.Feed("hello<think>world</think>") + f.Flush());
    }
}

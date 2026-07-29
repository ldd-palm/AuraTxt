using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class PromptServiceTests
{
    [Theory]
    [InlineData("system.md")]
    [InlineData("Prompts\\translate.md")]
    [InlineData("Prompts/translate.md")]
    [InlineData("C:\\apps\\AuraTxt\\Prompts\\reply.md")]
    public void IsFileRef_TrueForSingleLinePaths(string value)
        => Assert.True(PromptService.IsFileRef(value));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Translate the following text")]
    public void IsFileRef_FalseForInlineText(string? value)
        => Assert.False(PromptService.IsFileRef(value));

    [Fact]
    public void IsFileRef_FalseForMultilineInlineContainingSlash()
    {
        // Regression: inline prompts containing </source_text> have a '/' and used
        // to be mistaken for a file path. The multi-line guard prevents that.
        var inline = "### TASK\n<source_text>{SelectedText}</source_text>\nOutput only the result.";
        Assert.False(PromptService.IsFileRef(inline));
    }

    [Fact]
    public void Resolve_ReturnsInlineTextVerbatim_WhenNotAFile()
    {
        const string inline = "Translate this: {SelectedText}";
        Assert.Equal(inline, PromptService.Resolve(inline));
    }

    [Fact]
    public void Resolve_ReadsFileContent_WhenPathExists()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"auratxt_prompt_{Guid.NewGuid()}.md");
        try
        {
            File.WriteAllText(tmp, "file content");
            Assert.Equal("file content", PromptService.Resolve(tmp));
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void Resolve_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Equal("", PromptService.Resolve(null));
        Assert.Equal("", PromptService.Resolve(""));
    }

    [Fact]
    public void ResolveFullPath_RelativePath_AnchorsAtBaseDirectory()
    {
        var full = PromptService.ResolveFullPath(Path.Combine("prompts", "system.md"));
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "prompts", "system.md"), full);
    }

    [Fact]
    public void ResolveFullPath_AbsolutePath_PassesThroughUnchanged()
    {
        var abs = Path.Combine(Path.GetTempPath(), "x.md");
        Assert.Equal(abs, PromptService.ResolveFullPath(abs));
    }

    [Fact]
    public void Resolve_ReadsFileContent_ViaRelativePath_AnchoredAtBaseDirectory()
    {
        // Regression: relative prompt refs used to resolve File.Exists against the
        // process's current working directory (not guaranteed to equal BaseDirectory),
        // silently falling back to treating the path string itself as inline text.
        var fileName = $"auratxt_prompt_{Guid.NewGuid()}.md";
        var full = Path.Combine(AppContext.BaseDirectory, fileName);
        try
        {
            File.WriteAllText(full, "relative file content");
            Assert.Equal("relative file content", PromptService.Resolve(fileName));
        }
        finally { File.Delete(full); }
    }

    [Fact]
    public void ToRelativeIfInsideBase_AbsolutePathInsideBaseDirectory_BecomesRelative()
    {
        var fileName = $"auratxt_prompt_{Guid.NewGuid()}.md";
        var full = Path.Combine(AppContext.BaseDirectory, fileName);
        try
        {
            File.WriteAllText(full, "x");
            Assert.Equal(fileName, PromptService.ToRelativeIfInsideBase(full));
        }
        finally { File.Delete(full); }
    }

    [Fact]
    public void ToRelativeIfInsideBase_AbsolutePathOutsideBaseDirectory_Unchanged()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"auratxt_prompt_{Guid.NewGuid()}.md");
        try
        {
            File.WriteAllText(tmp, "x");
            Assert.Equal(tmp, PromptService.ToRelativeIfInsideBase(tmp));
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void ToRelativeIfInsideBase_NonExistentPath_Unchanged()
    {
        var missing = Path.Combine(AppContext.BaseDirectory, "does_not_exist_xyz.md");
        Assert.Equal(missing, PromptService.ToRelativeIfInsideBase(missing));
    }

    [Fact]
    public void ToRelativeIfInsideBase_InlineText_Unchanged()
    {
        const string inline = "Translate this: {SelectedText}";
        Assert.Equal(inline, PromptService.ToRelativeIfInsideBase(inline));
    }

    [Fact]
    public void ToRelativeIfInsideBase_AlreadyRelative_Unchanged()
    {
        Assert.Equal("prompts/system.md", PromptService.ToRelativeIfInsideBase("prompts/system.md"));
    }
}

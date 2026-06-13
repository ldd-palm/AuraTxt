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
}

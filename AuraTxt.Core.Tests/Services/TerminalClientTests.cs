using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class TerminalClientTests
{
    [Fact]
    public void BuildResolvedCommand_SubstitutesSelectedText()
    {
        var result = TerminalClient.BuildResolvedCommand("ping {SelectedText}", "8.8.8.8", "");
        Assert.Equal("ping 8.8.8.8", result);
    }

    [Fact]
    public void BuildResolvedCommand_SubstitutesMultipleOccurrences()
    {
        var result = TerminalClient.BuildResolvedCommand(
            "echo {SelectedText} && echo {SelectedText}", "hi", "");
        Assert.Equal("echo hi && echo hi", result);
    }

    [Fact]
    public void BuildResolvedCommand_SubstitutesUserInput()
    {
        var result = TerminalClient.BuildResolvedCommand(
            "nslookup {SelectedText} {UserInput}", "example.com", "8.8.8.8");
        Assert.Equal("nslookup example.com 8.8.8.8", result);
    }

    [Fact]
    public void BuildResolvedCommand_NoPlaceholders_ReturnsTemplateUnchanged()
    {
        var result = TerminalClient.BuildResolvedCommand("dir", "ignored", "ignored");
        Assert.Equal("dir", result);
    }

    [Fact]
    public void BuildResolvedCommand_ResolvesFileBackedTemplate()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"auratxt_cmd_{Guid.NewGuid()}.md");
        try
        {
            File.WriteAllText(tmp, "cat \"{SelectedText}\" >> file.txt");
            var result = TerminalClient.BuildResolvedCommand(tmp, "notes.txt", "");
            Assert.Equal("cat \"notes.txt\" >> file.txt", result);
        }
        finally { File.Delete(tmp); }
    }
}

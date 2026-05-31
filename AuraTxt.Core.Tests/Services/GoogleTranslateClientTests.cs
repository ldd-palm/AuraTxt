using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class GoogleTranslateClientTests
{
    [Theory]
    [InlineData("hello")]
    [InlineData("你好")]
    [InlineData("test 123")]
    public void GenerateTk_ProducesFormatNNdotNN(string text)
    {
        var tk = GoogleTranslateClient.GenerateTk(text);
        Assert.Matches(@"^\d+\.\d+$", tk);
    }
}

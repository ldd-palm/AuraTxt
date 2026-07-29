using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class WordReferenceClientTests
{
    [Theory]
    [InlineData("zh-CN", "zh")]
    [InlineData("ja",    "ja")]
    [InlineData("en",    "en")]
    [InlineData("pt-BR", "pt")]
    public void ToWordReferenceCode_StripsRegionAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, WordReferenceClient.ToWordReferenceCode(input));
    }
}

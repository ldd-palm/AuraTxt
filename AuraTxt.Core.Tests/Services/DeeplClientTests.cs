using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class DeeplClientTests
{
    [Theory]
    [InlineData("zh-CN", "ZH")]
    [InlineData("ja",    "JA")]
    [InlineData("en",    "EN")]
    [InlineData("pt-BR", "PT")]
    public void ToDeeplCode_StripsRegionAndUppercases(string input, string expected)
    {
        Assert.Equal(expected, DeeplClient.ToDeeplCode(input));
    }

    [Theory]
    [InlineData(24, true)]   // (24+5) % 29 == 0
    [InlineData(10, true)]   // (10+3) % 13 == 0
    [InlineData(2,  false)]
    [InlineData(1,  false)]
    public void NeedsMethodSpace_MatchesKnownAntiBotFormula(int id, bool expected)
    {
        Assert.Equal(expected, DeeplClient.NeedsMethodSpace(id));
    }
}

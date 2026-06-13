using AuraTxt.Core.Util;
using Xunit;

namespace AuraTxt.Core.Tests.Util;

public class GlobMatcherTests
{
    [Theory]
    [InlineData("*qwen3-next*instruct*", "qwen/qwen3-next-80b-a3b-instruct",      true)]
    [InlineData("*qwen3-next*instruct*", "qwen/qwen3-next-80b-a3b-INSTRUCT",      true)]   // case-insensitive
    [InlineData("gemini-2.5-flash*",     "gemini-2.5-flash-preview",               true)]
    [InlineData("gemini-2.5-flash*",     "gemini-3-flash",                         false)]
    [InlineData("meta/*",                "meta/llama-3.3-70b",                     true)]
    [InlineData("meta/*",                "nvidia/llama-3.3-70b",                   false)]
    [InlineData("*",                     "anything-at-all",                        true)]
    [InlineData("*deepseek-v4*",         "deepseek-ai/deepseek-v4-flash",          true)]
    [InlineData("*deepseek-v4*",         "deepseek-ai/deepseek-v3-flash",          false)]
    [InlineData("*minimax-m2*",          "minimaxai/minimax-m2.7",                 true)]
    [InlineData("*llama-*",              "meta/llama-3.3-70b-instruct",            true)]
    [InlineData("gemini-2.5-pro*",       "gemini-2.5-pro-exp-03-25",              true)]
    public void Matches_VariousPatterns(string pattern, string input, bool expected)
        => Assert.Equal(expected, GlobMatcher.Matches(pattern, input));

    [Fact]
    public void MatchesAny_ReturnsTrueIfAnyMatches()
    {
        var patterns = new[] { "gemini-2.5-pro*", "gemini-3-pro*" };
        Assert.True(GlobMatcher.MatchesAny(patterns, "gemini-2.5-pro-exp"));
        Assert.False(GlobMatcher.MatchesAny(patterns, "gemini-2.5-flash"));
    }

    [Fact]
    public void MatchesAny_EmptyPatterns_ReturnsFalse()
        => Assert.False(GlobMatcher.MatchesAny([], "anything"));
}

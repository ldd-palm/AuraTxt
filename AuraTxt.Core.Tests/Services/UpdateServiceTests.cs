using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class UpdateServiceTests
{
    [Theory]
    [InlineData("v1.4", "1.3")]
    [InlineData("1.4", "1.3")]
    [InlineData("V1.4", "1.3")]
    public void IsNewer_ReturnsTrue_WhenTagIsNewer(string tag, string current)
    {
        Assert.True(UpdateService.IsNewer(tag, Version.Parse(current)));
    }

    [Theory]
    [InlineData("v1.3", "1.3")]
    [InlineData("v1.2", "1.3")]
    [InlineData("v1.3", "1.3.0.0")]
    public void IsNewer_ReturnsFalse_WhenTagIsSameOrOlder(string tag, string current)
    {
        Assert.False(UpdateService.IsNewer(tag, Version.Parse(current)));
    }

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("")]
    [InlineData("vX.Y")]
    public void IsNewer_ReturnsFalse_ForUnparsableTag(string tag)
    {
        Assert.False(UpdateService.IsNewer(tag, Version.Parse("1.3")));
    }
}

using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class HotkeyValidatorTests
{
    private readonly HotkeyValidator _v = new();
    private readonly List<ActionItem> _none = new();

    [Theory]
    [InlineData("Alt+T",        true)]
    [InlineData("Ctrl+Shift+R", true)]
    [InlineData("Win+F1",       true)]
    [InlineData("T",            false)]
    [InlineData("Alt",          false)]
    [InlineData("",             false)]
    [InlineData("Ctrl+",        false)]
    public void IsValidFormat(string hotkey, bool expected) =>
        Assert.Equal(expected, _v.IsValidFormat(hotkey));

    [Fact]
    public void Validate_ReturnsSystemReserved_ForWinL()
    {
        var (result, _) = _v.Validate("Win+L", _none);
        Assert.Equal(HotkeyValidationResult.SystemReserved, result);
    }

    [Fact]
    public void Validate_ReturnsConflict_WhenHotkeyUsed()
    {
        var actions = new List<ActionItem>
        {
            new() { Id = "translate", Name = "翻译", Hotkey = "Alt+T" }
        };
        var (result, name) = _v.Validate("Alt+T", actions);
        Assert.Equal(HotkeyValidationResult.Conflict, result);
        Assert.Equal("翻译", name);
    }

    [Fact]
    public void Validate_ExcludesOwnId_OnUpdate()
    {
        var actions = new List<ActionItem>
        {
            new() { Id = "translate", Name = "翻译", Hotkey = "Alt+T" }
        };
        var (result, _) = _v.Validate("Alt+T", actions, excludeId: "translate");
        Assert.Equal(HotkeyValidationResult.Valid, result);
    }

    [Fact]
    public void Validate_ReturnsValid_ForUnusedHotkey()
    {
        var (result, _) = _v.Validate("Alt+Q", _none);
        Assert.Equal(HotkeyValidationResult.Valid, result);
    }
}

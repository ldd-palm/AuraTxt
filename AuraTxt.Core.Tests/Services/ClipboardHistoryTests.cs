using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class ClipboardHistoryTests
{
    private static ClipboardHistoryEntry E(string text, string? html = null) =>
        new(text, html is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["HTML Format"] = html });

    private static string[] Texts(ClipboardHistory h) => h.Items.Select(i => i.Text).ToArray();

    [Fact]
    public void Add_NewestFirst()
    {
        var h = new ClipboardHistory(10);
        h.Add(E("A")); h.Add(E("B")); h.Add(E("C"));
        Assert.Equal(["C", "B", "A"], Texts(h));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public void Add_IgnoresBlankText(string text)
    {
        var h = new ClipboardHistory(10);
        h.Add(E(text));
        Assert.Empty(h.Items);
    }

    [Fact]
    public void Add_Duplicate_MovesToTopWithoutGrowing_AndKeepsNewerFormats()
    {
        var h = new ClipboardHistory(10);
        h.Add(E("A", "<b>old</b>")); h.Add(E("B"));
        h.Add(E("A", "<b>new</b>"));
        Assert.Equal(["A", "B"], Texts(h));
        Assert.Equal("<b>new</b>", h.Items[0].Formats["HTML Format"]);
    }

    [Fact]
    public void Add_DuplicateIsCaseSensitive()
    {
        var h = new ClipboardHistory(10);
        h.Add(E("abc")); h.Add(E("ABC"));
        Assert.Equal(["ABC", "abc"], Texts(h));
    }

    [Fact]
    public void Add_BeyondCapacity_DropsOldest()
    {
        var h = new ClipboardHistory(3);
        foreach (var t in new[] { "A", "B", "C", "D" }) h.Add(E(t));
        Assert.Equal(["D", "C", "B"], Texts(h));
    }

    [Fact]
    public void Capacity_Shrink_TrimsOldest()
    {
        var h = new ClipboardHistory(5);
        foreach (var t in new[] { "A", "B", "C", "D" }) h.Add(E(t));
        h.Capacity = 2;
        Assert.Equal(["D", "C"], Texts(h));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Capacity_ClampedToAtLeastOne(int capacity)
    {
        var h = new ClipboardHistory(capacity);
        h.Add(E("A")); h.Add(E("B"));
        Assert.Equal(1, h.Capacity);
        Assert.Equal(["B"], Texts(h));
    }

    [Fact]
    public void MoveToTop_ExistingEntry()
    {
        var h = new ClipboardHistory(10);
        h.Add(E("A")); h.Add(E("B")); h.Add(E("C"));
        h.MoveToTop(h.Items[2]);
        Assert.Equal(["A", "C", "B"], Texts(h));
    }

    [Fact]
    public void MoveToTop_EntryNoLongerPresent_IsNoOp()
    {
        var h = new ClipboardHistory(2);
        h.Add(E("A"));
        var a = h.Items[0];
        h.Add(E("B")); h.Add(E("C")); // A trimmed out
        h.MoveToTop(a);
        Assert.Equal(["C", "B"], Texts(h));
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsAll()
    {
        var h = new ClipboardHistory(10);
        h.Add(E("A")); h.Add(E("B"));
        Assert.Equal(["B", "A"], h.Search("  ").Select(i => i.Text));
    }

    [Fact]
    public void Search_CaseInsensitiveSubstring_KeepsOrder()
    {
        var h = new ClipboardHistory(10);
        h.Add(E("Hello world")); h.Add(E("nothing")); h.Add(E("WORLD peace"));
        Assert.Equal(["WORLD peace", "Hello world"], h.Search("World").Select(i => i.Text));
    }

    [Theory]
    [InlineData(true,  "Ctrl+`", true)]
    [InlineData(false, "Ctrl+`", false)]
    [InlineData(true,  "",       false)]
    [InlineData(true,  "  ",     false)]
    public void Settings_IsClipboardHistoryActive(bool enabled, string hotkey, bool expected)
    {
        var s = new AuraTxt.Core.Models.AppSettings { ClipboardHistoryEnabled = enabled, ClipboardHistoryHotkey = hotkey };
        Assert.Equal(expected, s.IsClipboardHistoryActive);
    }

    [Fact]
    public void Settings_IsClipboardHistoryActive_NotSerialized()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new AuraTxt.Core.Models.AppSettings());
        Assert.DoesNotContain("IsClipboardHistoryActive", json);
        Assert.Contains("\"ClipboardHistoryEnabled\":true", json);
    }

    [Fact]
    public void Search_DoesNotMatchFormatsOnly()
    {
        var h = new ClipboardHistory(10);
        h.Add(E("plain", "<span>secret</span>"));
        Assert.Empty(h.Search("secret"));
    }
}

using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class ClipboardHistoryStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "auratxt-cliptest-" + Guid.NewGuid().ToString("N"));
    private string PathOf(string name) => Path.Combine(_dir, name);

    public ClipboardHistoryStoreTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    private static ClipboardHistoryEntry E(string text, string? html = null) =>
        new(text, html is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["HTML Format"] = html });

    [Fact]
    public void SaveThenLoad_RoundTripsTextFormatsAndOrder()
    {
        var path = PathOf("h.dat");
        ClipboardHistoryStore.Save(path, [E("新的 newest", "<b>x</b>"), E("older")]);

        var loaded = ClipboardHistoryStore.Load(path);

        Assert.Equal(["新的 newest", "older"], loaded.Select(e => e.Text));
        Assert.Equal("<b>x</b>", loaded[0].Formats["HTML Format"]);
        Assert.Empty(loaded[1].Formats);
    }

    [Fact]
    public void Save_FileIsEncrypted_NoPlaintextOnDisk()
    {
        var path = PathOf("h.dat");
        ClipboardHistoryStore.Save(path, [E("secret-token-12345")]);

        var raw = File.ReadAllBytes(path);
        Assert.DoesNotContain("secret-token-12345", System.Text.Encoding.UTF8.GetString(raw));
        Assert.DoesNotContain("secret-token-12345", System.Text.Encoding.Unicode.GetString(raw));
    }

    [Fact]
    public void Save_LeavesNoTempFile_AndOverwrites()
    {
        var path = PathOf("h.dat");
        ClipboardHistoryStore.Save(path, [E("first")]);
        ClipboardHistoryStore.Save(path, [E("second")]);

        Assert.Equal(["second"], ClipboardHistoryStore.Load(path).Select(e => e.Text));
        Assert.Equal(["h.dat"], Directory.GetFiles(_dir).Select(Path.GetFileName));
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmpty() =>
        Assert.Empty(ClipboardHistoryStore.Load(PathOf("nope.dat")));

    [Fact]
    public void Load_CorruptFile_ReturnsEmpty()
    {
        var path = PathOf("h.dat");
        File.WriteAllBytes(path, [1, 2, 3, 4, 5]);
        Assert.Empty(ClipboardHistoryStore.Load(path));
    }

    [Fact]
    public void Load_EmptyFile_ReturnsEmpty()
    {
        var path = PathOf("h.dat");
        File.WriteAllBytes(path, []);
        Assert.Empty(ClipboardHistoryStore.Load(path));
    }

    [Fact]
    public void AppendOlder_AddsAfterExisting_SkipsDuplicates_AndTrims()
    {
        var h = new ClipboardHistory(3);
        h.Add(E("A")); h.Add(E("B"));          // memory: B, A
        h.AppendOlder([E("A"), E("C"), E("D")]); // A is a dup; C fits; D exceeds capacity

        Assert.Equal(["B", "A", "C"], h.Items.Select(i => i.Text));
    }

    [Fact]
    public void AppendOlder_IntoEmpty_KeepsLoadedOrder_AndSkipsBlank()
    {
        var h = new ClipboardHistory(10);
        h.AppendOlder([E("X"), E("  "), E("Y")]);
        Assert.Equal(["X", "Y"], h.Items.Select(i => i.Text));
    }
}

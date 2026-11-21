using System.Security.Cryptography;
using System.Text.Json;

namespace AuraTxt.Core.Services;

/// Persists the clipboard history to a single file, encrypted with DPAPI (CurrentUser
/// scope) — clipboard text routinely contains passwords/tokens, so it must never sit on
/// disk in plaintext. A file copied to another machine or Windows account can't be
/// decrypted and simply loads as empty history.
public static class ClipboardHistoryStore
{
    private const int Version = 1;
    private static readonly byte[] Entropy = "AuraTxt.ClipboardHistory.v1"u8.ToArray();

    private sealed record FileDto(int Version, List<EntryDto> Items);
    private sealed record EntryDto(string Text, Dictionary<string, string>? Formats);

    /// Writes `items` (newest first) to `path`, atomically (temp file + move) so a crash
    /// mid-write can never leave a truncated file behind.
    public static void Save(string path, IEnumerable<ClipboardHistoryEntry> items)
    {
        var dto  = new FileDto(Version, items.Select(e => new EntryDto(e.Text, e.Formats.ToDictionary())).ToList());
        var json = JsonSerializer.SerializeToUtf8Bytes(dto);
        var data = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, data);
        File.Move(tmp, path, overwrite: true);
    }

    /// Entries newest first; empty if the file is missing, unreadable, corrupt, or
    /// encrypted for a different Windows user. Never throws.
    public static IReadOnlyList<ClipboardHistoryEntry> Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return [];
            var data = File.ReadAllBytes(path);
            if (data.Length == 0) return [];
            var json = ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
            var dto  = JsonSerializer.Deserialize<FileDto>(json);
            if (dto?.Items is null) return [];
            return dto.Items
                .Where(i => i.Text is not null)
                .Select(i => new ClipboardHistoryEntry(i.Text, i.Formats ?? new Dictionary<string, string>()))
                .ToList();
        }
        catch (Exception ex)
        {
            LogService.Error($"Clipboard history load failed: {path}", ex);
            return [];
        }
    }
}

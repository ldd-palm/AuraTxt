// AuraTxt.Core/Services/ProfileService.cs
using System.Reflection;
using System.Text.Json;
using AuraTxt.Core.Models;
using AuraTxt.Core.Util;

namespace AuraTxt.Core.Services;

public static class ProfileService
{
    private static readonly string ProfilesDir =
        Path.Combine(AppContext.BaseDirectory, "profiles");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static List<ProfileFile> _cache = new();
    private static readonly object _lock = new();

    // ── Embedded resources ─────────────────────────────────────────────────

    private static readonly Assembly Asm = typeof(ProfileService).Assembly;

    private static IReadOnlyDictionary<string, string> LoadEmbeddedJsons()
    {
        const string prefix = "AuraTxt.Core.Profiles.";
        var result = new Dictionary<string, string>();
        foreach (var name in Asm.GetManifestResourceNames())
        {
            if (!name.StartsWith(prefix) || !name.EndsWith(".json")) continue;
            var id = name[prefix.Length..^".json".Length];
            using var stream = Asm.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            result[id] = reader.ReadToEnd();
        }
        return result;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public static void EnsureScaffold()
    {
        Directory.CreateDirectory(ProfilesDir);

        // Seed JSON profiles (never overwrite user files)
        foreach (var (id, json) in LoadEmbeddedJsons())
        {
            var dest = Path.Combine(ProfilesDir, $"{id}.json");
            if (!File.Exists(dest))
                File.WriteAllText(dest, json);
        }

        // Seed README
        var readmeDest = Path.Combine(ProfilesDir, "README.md");
        if (!File.Exists(readmeDest))
        {
            const string readmeName = "AuraTxt.Core.Profiles.README.md.template";
            using var stream = Asm.GetManifestResourceStream(readmeName);
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                File.WriteAllText(readmeDest, reader.ReadToEnd());
            }
        }

        Reload();
    }

    public static void Reload()
    {
        var embedded = LoadEmbeddedJsons();
        var loaded = new List<ProfileFile>();

        // Start from embedded defaults
        foreach (var (id, json) in embedded)
        {
            var p = ParseProfile(json, id);
            if (p is not null) loaded.Add(p);
        }

        // Disk files override embedded (same id = complete replacement)
        if (Directory.Exists(ProfilesDir))
        {
            foreach (var file in Directory.GetFiles(ProfilesDir, "*.json")
                                          .OrderBy(f => f))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var p = ParseProfile(json, null);
                    if (p is null) continue;
                    p.IsUserFile = true;
                    // Remove embedded entry with same id
                    loaded.RemoveAll(x => x.Id == p.Id && !x.IsUserFile);
                    // Replace or add
                    var existingIdx = loaded.FindIndex(x => x.Id == p.Id);
                    if (existingIdx >= 0) loaded[existingIdx] = p;
                    else loaded.Add(p);
                }
                catch (Exception ex)
                {
                    LogService.Error($"ProfileService: failed to load {file}: {ex.Message}");
                }
            }
        }

        // Sort by priority descending, then id ascending
        loaded.Sort((a, b) =>
        {
            int c = b.Priority.CompareTo(a.Priority);
            return c != 0 ? c : string.Compare(a.Id, b.Id, StringComparison.Ordinal);
        });

        lock (_lock) { _cache = loaded; }
    }

    public static ProfileFile Resolve(ModelEntry entry, string adapterType)
    {
        List<ProfileFile> profiles;
        lock (_lock) { profiles = _cache; }

        if (!string.IsNullOrEmpty(entry.ProfileId))
        {
            var p = profiles.FirstOrDefault(x => x.Id == entry.ProfileId)
                ?? throw new ProfileNotFoundException(entry.ProfileId);
            if (!p.AdapterCompatibility.Contains(adapterType, StringComparer.OrdinalIgnoreCase))
                throw new ProfileAdapterMismatchException(p.Id, adapterType);
            return p;
        }

        foreach (var p in profiles)
        {
            if (!p.AdapterCompatibility.Contains(adapterType, StringComparer.OrdinalIgnoreCase))
                continue;
            if (GlobMatcher.MatchesAny(p.Match.NamePatterns, entry.TargetModel))
                return p;
        }

        throw new InvalidOperationException(
            "ProfileService: no fallback profile found. Embedded default-* profiles missing.");
    }

    public static ProfileFile? GetById(string id)
    {
        lock (_lock) { return _cache.FirstOrDefault(p => p.Id == id); }
    }

    public static IReadOnlyList<ProfileFile> All()
    {
        lock (_lock) { return _cache.AsReadOnly(); }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static ProfileFile? ParseProfile(string json, string? fallbackId)
    {
        try
        {
            var p = JsonSerializer.Deserialize<ProfileFile>(json, JsonOpts);
            if (p is null) return null;
            if (string.IsNullOrEmpty(p.Id) && fallbackId is not null) p.Id = fallbackId;
            return p;
        }
        catch (JsonException ex)
        {
            LogService.Error($"ProfileService: JSON parse error (id={fallbackId}): {ex.Message}");
            return null;
        }
    }
}

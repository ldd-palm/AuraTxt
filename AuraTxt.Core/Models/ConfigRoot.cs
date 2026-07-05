namespace AuraTxt.Core.Models;

public class ConfigRoot
{
    public Dictionary<string, ProviderConfig> Models   { get; set; } = new();
    public List<ActionItem>                   Actions  { get; set; } = new();
    public AppSettings                        Settings { get; set; } = new();

    /// Resolves "openai/gpt-4o" → (ProviderConfig, ModelEntry). Returns null if not found.
    public (ProviderConfig provider, ModelEntry model)? ResolveModel(string modelRef)
    {
        if (string.IsNullOrEmpty(modelRef)) return null;
        var slash = modelRef.IndexOf('/');
        if (slash < 0) return null;
        var providerId  = modelRef[..slash];
        var targetModel = modelRef[(slash + 1)..];
        if (!Models.TryGetValue(providerId, out var p)) return null;
        var m = p.Models.FirstOrDefault(x => x.TargetModel == targetModel);
        return m is null ? null : (p, m);
    }

    /// Returns all model refs with alias-only labels for WPF ComboBox.
    /// Order: user providers alphabetically, then built-in models.
    public IEnumerable<(string Ref, string Label)> AllModelAliases()
    {
        foreach (var (pid, p) in Models.Where(kv => kv.Key != "default").OrderBy(kv => kv.Key))
            foreach (var m in p.Models)
                yield return ($"{pid}/{m.TargetModel}", m.Alias);

        if (!Models.TryGetValue("default", out var def)) yield break;
        foreach (var m in def.Models)
            yield return ($"default/{m.TargetModel}", m.Alias);
    }

    /// Returns all model refs for WPF ComboBox.
    /// Order: user providers alphabetically, then all built-in models.
    public IEnumerable<(string Ref, string Label)> AllModelRefs()
    {
        foreach (var (pid, p) in Models.Where(kv => kv.Key != "default").OrderBy(kv => kv.Key))
            foreach (var m in p.Models)
                yield return ($"{pid}/{m.TargetModel}", $"{p.DisplayName} / {m.Alias}");

        if (!Models.TryGetValue("default", out var def)) yield break;
        foreach (var m in def.Models)
            yield return ($"default/{m.TargetModel}", $"Built-in / {m.Alias}");
    }

    /// Like <see cref="AllModelAliases"/> but only enabled user models + all built-in models.
    public IEnumerable<(string Ref, string Label)> AllEnabledModelAliases()
    {
        foreach (var (pid, p) in Models.Where(kv => kv.Key != "default").OrderBy(kv => kv.Key))
            foreach (var m in p.Models.Where(m => m.Enabled))
                yield return ($"{pid}/{m.TargetModel}", m.Alias);

        if (!Models.TryGetValue("default", out var def)) yield break;
        foreach (var m in def.Models)
            yield return ($"default/{m.TargetModel}", m.Alias);
    }

    /// Like <see cref="AllModelRefs"/> but only enabled user models + all built-in models.
    public IEnumerable<(string Ref, string Label)> AllEnabledModelRefs()
    {
        foreach (var (pid, p) in Models.Where(kv => kv.Key != "default").OrderBy(kv => kv.Key))
            foreach (var m in p.Models.Where(m => m.Enabled))
                yield return ($"{pid}/{m.TargetModel}", $"{p.DisplayName} / {m.Alias}");

        if (!Models.TryGetValue("default", out var def)) yield break;
        foreach (var m in def.Models)
            yield return ($"default/{m.TargetModel}", $"Built-in / {m.Alias}");
    }
}

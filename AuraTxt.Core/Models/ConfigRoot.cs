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

    /// Returns all model refs for WPF ComboBox.
    /// Order: user providers alphabetically, then default/Google_Translate, then default/Youdao_Dict.
    public IEnumerable<(string Ref, string Label)> AllModelRefs()
    {
        foreach (var (pid, p) in Models.Where(kv => kv.Key != "default").OrderBy(kv => kv.Key))
            foreach (var m in p.Models)
                yield return ($"{pid}/{m.TargetModel}", $"{p.DisplayName} / {m.Alias}");

        if (!Models.TryGetValue("default", out var def)) yield break;
        var gtrans = def.Models.FirstOrDefault(m => m.TargetModel == "Google_Translate");
        var youdao = def.Models.FirstOrDefault(m => m.TargetModel == "Youdao_Dict");
        if (gtrans is not null) yield return ("default/Google_Translate", $"Built-in / {gtrans.Alias}");
        if (youdao is not null) yield return ("default/Youdao_Dict",      $"Built-in / {youdao.Alias}");
    }
}

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AuraTxt.Core.Models;

public class ProfileFile
{
    [JsonPropertyName("id")]          public string Id { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("match")]       public ProfileMatch Match { get; set; } = new();
    [JsonPropertyName("priority")]    public int Priority { get; set; } = 0;
    [JsonPropertyName("adapter_compatibility")] public List<string> AdapterCompatibility { get; set; } = new();
    [JsonPropertyName("thinking")]    public ThinkingControl? Thinking { get; set; }
    [JsonPropertyName("strip_patterns")] public List<string> StripPatterns { get; set; } = new();
    [JsonPropertyName("capabilities")] public ProfileCapabilities Capabilities { get; set; } = new();
    [JsonPropertyName("recommended_params")] public JsonObject RecommendedParams { get; set; } = new();

    /// True when loaded from disk (user file); false when from embedded resource only.
    [JsonIgnore] public bool IsUserFile { get; set; }
}

public class ProfileMatch
{
    [JsonPropertyName("name_patterns")] public List<string> NamePatterns { get; set; } = new();
}

public class ThinkingControl
{
    [JsonPropertyName("location")] public string Location { get; set; } = "";
    [JsonPropertyName("modes")]    public ThinkingModes Modes { get; set; } = new();
}

public class ThinkingModes
{
    [JsonPropertyName("disable")]     public JsonObject Disable { get; set; } = new();
    [JsonPropertyName("enable_high")] public JsonObject EnableHigh { get; set; } = new();
}

public class ProfileCapabilities
{
    [JsonPropertyName("streaming")]     public bool Streaming { get; set; } = true;
    [JsonPropertyName("system_prompt")] public bool SystemPrompt { get; set; } = true;
    [JsonPropertyName("multi_turn")]    public bool MultiTurn { get; set; } = true;
    [JsonPropertyName("max_context")]   public int? MaxContext { get; set; }
}

public class ProfileApplicationException(string msg) : Exception(msg);
public class ProfileNotFoundException(string id) : Exception($"Profile '{id}' not found");
public class ProfileAdapterMismatchException(string id, string adapter)
    : Exception($"Profile '{id}' is not compatible with adapter '{adapter}'");

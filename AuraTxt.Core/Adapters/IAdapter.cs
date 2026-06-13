using System.Text.Json.Nodes;

namespace AuraTxt.Core.Adapters;

public interface IAdapter
{
    string Name { get; }
    Task<string> CompleteAsync(AdapterRequest request, CancellationToken ct);
    IAsyncEnumerable<string> StreamAsync(AdapterRequest request, CancellationToken ct);
}

public class AdapterRequest
{
    public string BaseUrl      { get; init; } = "";
    public string ApiKey       { get; init; } = "";
    public string TargetModel  { get; init; } = "";
    public string SystemPrompt { get; init; } = "";
    public string UserPrompt   { get; init; } = "";
    /// Pre-assembled from profile thinking injection. Adapter merges into HTTP body.
    public JsonObject ExtraBody { get; init; } = new();
    /// Profile recommended_params + any runtime overrides.
    public Dictionary<string, JsonNode?> Params { get; init; } = new();
}

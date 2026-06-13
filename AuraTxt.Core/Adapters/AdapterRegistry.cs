namespace AuraTxt.Core.Adapters;

public static class AdapterRegistry
{
    private static readonly OpenAICompatibleAdapter _oai = new();
    private static readonly GeminiNativeAdapter     _gem = new();

    /// Returns the adapter for the given type string.
    /// Accepts both old names (generic, nim → openai_compatible; gemini → gemini_native)
    /// so existing config.json files keep working.
    public static IAdapter Get(string adapterType) => adapterType.ToLowerInvariant() switch
    {
        "gemini_native" or "gemini"               => _gem,
        "openai_compatible" or "generic" or "nim" => _oai,
        _ => throw new ArgumentException($"Unknown adapter type: '{adapterType}'")
    };
}

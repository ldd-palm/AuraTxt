using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using AuraTxt.Core.Adapters;
using AuraTxt.Core.Models;
using AuraTxt.Core.Util;

namespace AuraTxt.Core.Services;

public class AiClient
{
    private readonly Func<string, IAdapter> _getAdapter;

    public AiClient() : this(AdapterRegistry.Get) { }
    public AiClient(Func<string, IAdapter> getAdapter) => _getAdapter = getAdapter;

    public async Task<string> CompleteAsync(
        string providerId, ProviderConfig provider, ModelEntry model,
        ActionItem action, string selectedText, string userInput = "",
        CancellationToken ct = default)
    {
        if (providerId == "default")
            return await BuiltinDispatch(model, action, selectedText, userInput, ct);

        var (req, stripPatterns) = BuildRequest(provider, model, action, selectedText, userInput);
        var result = await _getAdapter(provider.AdapterType).CompleteAsync(req, ct);
        return ApplyStrip(result, stripPatterns);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string providerId, ProviderConfig provider, ModelEntry model,
        ActionItem action, string selectedText, string userInput = "",
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (providerId == "default")
        {
            yield return await BuiltinDispatch(model, action, selectedText, userInput, ct);
            yield break;
        }

        var (req, stripPatterns) = BuildRequest(provider, model, action, selectedText, userInput);
        var rawStream = _getAdapter(provider.AdapterType).StreamAsync(req, ct);

        if (stripPatterns.Count == 0)
        {
            await foreach (var chunk in rawStream) yield return chunk;
            yield break;
        }

        var filter = new TagStripFilter(stripPatterns);
        await foreach (var chunk in rawStream)
        {
            var out_ = filter.Feed(chunk);
            if (!string.IsNullOrEmpty(out_)) yield return out_;
        }
        var tail = filter.Flush();
        if (!string.IsNullOrEmpty(tail)) yield return tail;
    }

    // ── Internal (testable) ────────────────────────────────────────────────

    internal (AdapterRequest Req, List<string> StripPatterns) BuildRequest(
        ProviderConfig provider, ModelEntry model, ActionItem action,
        string selectedText, string userInput)
    {
        var adapterType = NormaliseAdapterType(provider.AdapterType);
        var profile = ProfileService.Resolve(model, adapterType);

        var systemPrompt = PromptService.Resolve(ConfigService.DefaultSettings?.SystemPrompt ?? "");
        var userPrompt   = PromptService.Resolve(action.Prompt)
            .Replace("{SelectedText}", selectedText)
            .Replace("{UserInput}",   userInput);

        var @params = new Dictionary<string, JsonNode?>();
        foreach (var (k, v) in profile.RecommendedParams)
            @params[k] = v?.DeepClone();

        var extraBody = new JsonObject();
        if (profile.Thinking is not null)
        {
            var payload = action.ThinkingMode == "enable_high"
                ? profile.Thinking.Modes.EnableHigh
                : profile.Thinking.Modes.Disable;
            // Empty payload means "send nothing" — model doesn't accept the thinking field
            if (payload.Count > 0)
                JsonPathSetter.SetPath(extraBody, profile.Thinking.Location, payload.DeepClone()!.AsObject());
        }

        var req = new AdapterRequest
        {
            BaseUrl      = provider.BaseUrl,
            ApiKey       = provider.ApiKey,
            TargetModel  = model.TargetModel,
            SystemPrompt = systemPrompt,
            UserPrompt   = userPrompt,
            ExtraBody    = extraBody,
            Params       = @params
        };
        return (req, profile.StripPatterns);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private static string NormaliseAdapterType(string t) => t.ToLowerInvariant() switch
    {
        "gemini_native" or "gemini" => "gemini_native",
        _                           => "openai_compatible"
    };

    private static string ApplyStrip(string text, List<string> patterns)
    {
        if (patterns.Count == 0) return text;
        var f = new TagStripFilter(patterns);
        return f.Feed(text) + f.Flush();
    }

    private static async Task<string> BuiltinDispatch(
        ModelEntry model, ActionItem action, string selectedText, string userInput, CancellationToken ct)
    {
        var lang = ConfigService.DefaultSettings?.TargetLanguage ?? "zh-CN";
        return model.TargetModel switch
        {
            "Google_Translate" => await new GoogleTranslateClient().TranslateAsync(selectedText, "auto", lang, ct),
            "Youdao_Dict"      => await new YoudaoClient().DictionaryAsync(selectedText, ct),
            "Terminal"         => await TerminalClient.RunAsync(action.Prompt, selectedText, userInput, ct),
            _                  => $"[Error] Unknown built-in model: {model.TargetModel}"
        };
    }
}

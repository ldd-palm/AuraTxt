using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using AuraTxt.Core.Services;

namespace AuraTxt.Core.Adapters;

public sealed class GeminiNativeAdapter : IAdapter
{
    public string Name => "gemini_native";

    private static readonly HttpClient _http   = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly HttpClient _stream = new() { Timeout = Timeout.InfiniteTimeSpan };

    public async Task<string> CompleteAsync(AdapterRequest req, CancellationToken ct)
    {
        var url = GeminiUrl(req, stream: false);
        using var msg = BuildMessage(req, url, stream: false);
        LogService.Raw($"──── GEMINI REQUEST {url}\n{await msg.Content!.ReadAsStringAsync(ct)}");
        var resp = await _http.SendAsync(msg, ct);
        var raw  = await resp.Content.ReadAsStringAsync(ct);
        LogService.Raw($"──── GEMINI RESPONSE HTTP {(int)resp.StatusCode}\n{raw}");
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)resp.StatusCode} — {OpenAICompatibleAdapter.ExtractApiError(raw)}");
        using var doc = JsonDocument.Parse(raw);
        return ExtractGeminiText(doc.RootElement);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        AdapterRequest req,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var url = GeminiUrl(req, stream: true);
        using var msg = BuildMessage(req, url, stream: true);
        LogService.Raw($"──── GEMINI STREAM REQUEST {url}\n{await msg.Content!.ReadAsStringAsync(ct)}");
        using var resp = await _stream.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"HTTP {(int)resp.StatusCode} — {OpenAICompatibleAdapter.ExtractApiError(err)}");
        }
        using var body   = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(body);
        var log = new System.Text.StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (!line.StartsWith("data: ")) continue;

            var pending = new List<string>();
            try
            {
                using var doc      = JsonDocument.Parse(line[6..]);
                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() == 0) continue;
                foreach (var part in candidates[0].GetProperty("content")
                                                  .GetProperty("parts").EnumerateArray())
                {
                    if (part.TryGetProperty("thought", out var t) && t.GetBoolean()) continue;
                    if (!part.TryGetProperty("text", out var tp)) continue;
                    var text = tp.GetString();
                    if (!string.IsNullOrEmpty(text)) pending.Add(text);
                }
            }
            catch (JsonException) { continue; }

            foreach (var chunk in pending) { log.Append(chunk); yield return chunk; }
        }
        LogService.Raw($"──── GEMINI STREAM RESPONSE\n{log}");
    }

    private static string GeminiUrl(AdapterRequest req, bool stream)
    {
        var origin = new Uri(req.BaseUrl).GetLeftPart(UriPartial.Authority);
        var method = stream ? "streamGenerateContent" : "generateContent";
        var url = $"{origin}/v1beta/models/{Uri.EscapeDataString(req.TargetModel)}:{method}";
        return stream ? url + "?alt=sse" : url;
    }

    private static HttpRequestMessage BuildMessage(AdapterRequest req, string url, bool stream)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, url);
        msg.Headers.Add("x-goog-api-key", req.ApiKey);

        var body = new JsonObject
        {
            ["contents"] = JsonSerializer.SerializeToNode(new[]
            {
                new { role = "user", parts = new[] { new { text = req.UserPrompt } } }
            })
        };
        if (!string.IsNullOrWhiteSpace(req.SystemPrompt))
            body["systemInstruction"] = JsonSerializer.SerializeToNode(
                new { parts = new[] { new { text = req.SystemPrompt } } });

        // Params → generationConfig (with Gemini field name translation)
        var gc = new JsonObject();
        foreach (var (k, v) in req.Params)
        {
            var geminiKey = k switch
            {
                "top_p"      => "topP",
                "max_tokens" => "maxOutputTokens",
                _            => k
            };
            gc[geminiKey] = v?.DeepClone();
        }
        if (gc.Count > 0) body["generationConfig"] = gc;

        // ExtraBody → deep-merge into body (handles nested generationConfig.thinkingConfig)
        DeepMerge(body, req.ExtraBody);

        msg.Content = new StringContent(
            body.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            System.Text.Encoding.UTF8, "application/json");
        return msg;
    }

    private static void DeepMerge(JsonObject target, JsonObject source)
    {
        foreach (var (k, v) in source)
        {
            if (target[k] is JsonObject tChild && v is JsonObject sChild)
                DeepMerge(tChild, sChild);
            else
                target[k] = v?.DeepClone();
        }
    }

    private static string ExtractGeminiText(JsonElement root)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var part in root.GetProperty("candidates")[0]
                                 .GetProperty("content").GetProperty("parts").EnumerateArray())
        {
            if (part.TryGetProperty("thought", out var t) && t.GetBoolean()) continue;
            if (part.TryGetProperty("text", out var tp)) sb.Append(tp.GetString());
        }
        return sb.ToString();
    }
}

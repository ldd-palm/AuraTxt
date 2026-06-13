using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using AuraTxt.Core.Services;

namespace AuraTxt.Core.Adapters;

public sealed class OpenAICompatibleAdapter : IAdapter
{
    public string Name => "openai_compatible";

    private static readonly HttpClient _http   = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly HttpClient _stream = new() { Timeout = Timeout.InfiniteTimeSpan };

    public async Task<string> CompleteAsync(AdapterRequest req, CancellationToken ct)
    {
        using var msg = BuildMessage(req, stream: false);
        var bodyJson = await msg.Content!.ReadAsStringAsync(ct);
        LogService.Raw($"──── OAI REQUEST {req.BaseUrl.TrimEnd('/')}/chat/completions\n{bodyJson}");
        var resp = await _http.SendAsync(msg, ct);
        var raw  = await resp.Content.ReadAsStringAsync(ct);
        LogService.Raw($"──── OAI RESPONSE HTTP {(int)resp.StatusCode}\n{raw}");
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)resp.StatusCode} — {ExtractApiError(raw)}");
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.GetProperty("choices")[0]
                              .GetProperty("message")
                              .GetProperty("content")
                              .GetString() ?? "";
    }

    public async IAsyncEnumerable<string> StreamAsync(
        AdapterRequest req,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var msg = BuildMessage(req, stream: true);
        var bodyJson = await msg.Content!.ReadAsStringAsync(ct);
        LogService.Raw($"──── OAI STREAM REQUEST {req.BaseUrl.TrimEnd('/')}/chat/completions\n{bodyJson}");
        using var resp = await _stream.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"HTTP {(int)resp.StatusCode} — {ExtractApiError(err)}");
        }
        using var body   = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(body);
        var log = new System.Text.StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (!line.StartsWith("data: ")) continue;
            var json = line[6..];
            if (json == "[DONE]") break;

            string? text = null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() == 0) continue;
                var delta = choices[0].GetProperty("delta");
                if (!delta.TryGetProperty("content", out var ce)) continue;
                if (ce.ValueKind == JsonValueKind.Null) continue;
                text = ce.GetString();
            }
            catch (JsonException) { continue; }

            if (!string.IsNullOrEmpty(text)) { log.Append(text); yield return text; }
        }
        LogService.Raw($"──── OAI STREAM RESPONSE\n{log}");
    }

    private static HttpRequestMessage BuildMessage(AdapterRequest req, bool stream)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post,
            $"{req.BaseUrl.TrimEnd('/')}/chat/completions");
        if (!string.IsNullOrEmpty(req.ApiKey))
            msg.Headers.Add("Authorization", $"Bearer {req.ApiKey}");

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(req.SystemPrompt))
            messages.Add(new { role = "system", content = req.SystemPrompt });
        messages.Add(new { role = "user", content = req.UserPrompt });

        var body = new JsonObject
        {
            ["model"]    = req.TargetModel,
            ["messages"] = JsonSerializer.SerializeToNode(messages),
            ["stream"]   = stream
        };

        // Params (temperature, top_p, max_tokens, …) at top level
        foreach (var (k, v) in req.Params)
            body[k] = v?.DeepClone();

        // ExtraBody (thinking injection) at top level — shallow merge
        foreach (var (k, v) in req.ExtraBody)
            body[k] = v?.DeepClone();

        msg.Content = new StringContent(
            body.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            System.Text.Encoding.UTF8, "application/json");
        return msg;
    }

    internal static string ExtractApiError(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("message", out var msg))
                return msg.GetString() ?? "";
        }
        catch { }
        return raw[..Math.Min(200, raw.Length)];
    }
}

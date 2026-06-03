using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AuraTxt.Core.Models;

namespace AuraTxt.Core.Services;

public class AiClient
{
    private readonly HttpClient _http;

    public AiClient(HttpClient? http = null)
        => _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

    public async Task<string> CompleteAsync(
        ProviderConfig provider, ModelEntry model, string userPrompt,
        string? systemPrompt = null, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"{provider.BaseUrl.TrimEnd('/')}/chat/completions");

        req.Headers.Add("Authorization", $"Bearer {provider.ApiKey}");

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            messages.Add(new { role = "system", content = systemPrompt });
        messages.Add(new { role = "user", content = userPrompt });

        var body = new Dictionary<string, object>
        {
            ["model"]    = model.TargetModel,
            ["messages"] = messages,
            ["stream"]   = (object)false
        };

        if (model.DisableThinking)
            body["thinking"] = new { type = "disabled" };

        var bodyJson = JsonSerializer.Serialize(body,
            new JsonSerializerOptions { WriteIndented = true });
        LogService.Raw($"──── REQUEST  {DateTime.Now:HH:mm:ss}  " +
                       $"{provider.BaseUrl.TrimEnd('/')}/chat/completions\n{bodyJson}");

        req.Content = new StringContent(bodyJson, System.Text.Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(req, ct);
        var raw  = await resp.Content.ReadAsStringAsync(ct);
        LogService.Raw($"──── RESPONSE HTTP {(int)resp.StatusCode}  {DateTime.Now:HH:mm:ss}\n{raw}");

        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }
}

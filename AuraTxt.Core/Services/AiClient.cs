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
        ModelPlatform model, string prompt, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"{model.BaseUrl.TrimEnd('/')}/chat/completions");

        req.Headers.Add("Authorization", $"Bearer {model.ApiKey}");
        req.Content = JsonContent.Create(new
        {
            model    = model.TargetModel,
            messages = new[] { new { role = "user", content = prompt } },
            stream   = false
        });

        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }
}

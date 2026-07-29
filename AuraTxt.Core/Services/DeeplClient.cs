using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AuraTxt.Core.Services;

public class DeeplClient
{
    private readonly HttpClient _http;

    // Shared instance — the client is constructed per call site (ResultWindow), so a
    // per-instance HttpClient would leak sockets under frequent use.
    private static readonly HttpClient _sharedHttp = CreateShared();

    private static HttpClient CreateShared()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        AddHeaders(http);
        return http;
    }

    private static void AddHeaders(HttpClient http)
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        http.DefaultRequestHeaders.Referrer = new Uri("https://www.deepl.com/");
        http.DefaultRequestHeaders.Add("Origin", "https://www.deepl.com");
    }

    public DeeplClient(HttpClient? http = null)
    {
        if (http is not null) { _http = http; AddHeaders(_http); }
        else _http = _sharedHttp;
    }

    public async Task<string> TranslateAsync(
        string text, string from = "auto", string to = "zh-CN", CancellationToken ct = default)
    {
        var sourceLang       = from == "auto" ? "auto" : ToDeeplCode(from);
        var preferredSource  = from == "auto" ? "EN"   : sourceLang;
        var targetLang       = ToDeeplCode(to);

        var iCount    = 0;
        foreach (var ch in text) if (ch == 'i') iCount++;
        var c         = 1 + iCount;
        var now       = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var timestamp = now + (c - now % c);
        var id        = Random.Shared.Next(1, 1_000_000);

        var payload = new TranslateRequest(
            "2.0", "LMT_handle_jobs",
            new TranslateParams(
                new List<TranslateJob> { new("default", text) },
                new TranslateLang(new List<string> { preferredSource, targetLang }, sourceLang, targetLang),
                1, timestamp),
            id);

        var body = JsonSerializer.Serialize(payload);
        // DeepL's server rejects the request (misleadingly, as "429 Too many requests")
        // unless the serialized "method" key has the exact spacing its own web client
        // would produce for this id — a signature check, not a real rate limit. See the
        // (reference-only, unported) DeepL.js in the repo root for the original request
        // shape this reverse-engineers a fix on top of.
        var spaced = NeedsMethodSpace(id);
        body = body.Replace("\"method\":\"LMT_handle_jobs\"",
            spaced ? "\"method\" : \"LMT_handle_jobs\"" : "\"method\": \"LMT_handle_jobs\"");

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync("https://www2.deepl.com/jsonrpc", content, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseTranslation(json);
    }

    private static string ParseTranslation(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out var err))
            throw new InvalidOperationException($"DeepL error: {err.GetProperty("message").GetString()}");

        var beam = root.GetProperty("result").GetProperty("translations")[0].GetProperty("beams")[0];
        return beam.GetProperty("postprocessed_sentence").GetString() ?? "";
    }

    // Google/Youdao-style codes ("zh-CN", "ja") -> DeepL's bare uppercase codes ("ZH", "JA").
    public static string ToDeeplCode(string code) => code.Split('-')[0].ToUpperInvariant();

    public static bool NeedsMethodSpace(int id) => (id + 5) % 29 == 0 || (id + 3) % 13 == 0;

    private record TranslateJob(string kind, string raw_en_sentence);
    private record TranslateLang(List<string> user_preferred_langs, string source_lang_user_selected, string target_lang);
    private record TranslateParams(List<TranslateJob> jobs, TranslateLang lang, int priority, long timestamp);
    private record TranslateRequest(string jsonrpc, string method, TranslateParams @params, int id);
}

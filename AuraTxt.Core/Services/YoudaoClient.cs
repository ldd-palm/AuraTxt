using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AuraTxt.Core.Services;

public class YoudaoClient
{
    private readonly HttpClient _http;
    private const string AppKey    = "fanyideskweb";
    private const string SignSuffix = "Y2FYu%TNSbMCxc3t2u^XT";

    public YoudaoClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _http.DefaultRequestHeaders.Add("Referer", "https://fanyi.youdao.com/");
        _http.DefaultRequestHeaders.Add("Cookie", "OUTFOX_SEARCH_USER_ID=1@100.1.1.1;");
    }

    public async Task<string> TranslateAsync(string text, CancellationToken ct = default)
    {
        var salt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var sign = Md5($"{AppKey}{text}{salt}{SignSuffix}");
        var body = $"i={Uri.EscapeDataString(text)}&from=AUTO&to=zh-CHS" +
                   $"&smartresult=dict&client={AppKey}&salt={salt}&sign={sign}" +
                   $"&doctype=json&version=2.1&keyfrom=fanyi.web";

        var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        var resp = await _http.PostAsync(
            "https://fanyi.youdao.com/translate_o?smartresult=dict&smartresult=rule",
            content, ct);
        resp.EnsureSuccessStatusCode();
        return ParseTranslation(await resp.Content.ReadAsStringAsync(ct));
    }

    public async Task<string> DictionaryAsync(string word, CancellationToken ct = default)
    {
        var url  = $"https://dict.youdao.com/w/{Uri.EscapeDataString(word)}/";
        var html = await _http.GetStringAsync(url, ct);
        return ExtractDefinitions(html);
    }

    private static string ParseTranslation(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("translateResult", out var results))
            return "";

        var sb = new StringBuilder();
        foreach (var row in results.EnumerateArray())
        foreach (var seg in row.EnumerateArray())
            if (seg.TryGetProperty("tgt", out var tgt))
                sb.Append(tgt.GetString());

        if (doc.RootElement.TryGetProperty("smartResult", out var smart)
            && smart.TryGetProperty("entries", out var entries))
        {
            sb.AppendLine();
            foreach (var e in entries.EnumerateArray())
                sb.AppendLine(e.GetString());
        }
        return sb.ToString().Trim();
    }

    private static string ExtractDefinitions(string html)
    {
        const string marker = "trans-container";
        var idx = html.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return "";
        var open  = html.IndexOf('>', idx) + 1;
        var close = html.IndexOf("</div>", open, StringComparison.Ordinal);
        if (open < 1 || close < 0) return "";
        var inner = html[open..close];
        return System.Text.RegularExpressions.Regex.Replace(inner, "<[^>]+>", "").Trim();
    }

    private static string Md5(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }
}

using System.Net.Http;
using System.Text.RegularExpressions;

namespace AuraTxt.Core.Services;

public class WordReferenceClient
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

    private static void AddHeaders(HttpClient http) =>
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

    public WordReferenceClient(HttpClient? http = null)
    {
        if (http is not null) { _http = http; AddHeaders(_http); }
        else _http = _sharedHttp;
    }

    public async Task<string> DictionaryAsync(string word, string to = "zh-CN", CancellationToken ct = default)
    {
        var pair = "en" + ToWordReferenceCode(to);
        var url  = $"https://www.wordreference.com/{pair}/{Uri.EscapeDataString(word)}";
        var html = await _http.GetStringAsync(url, ct);
        return ExtractDefinitions(html);
    }

    // App's Google-style target-language codes ("zh-CN", "ja") -> WordReference's bare
    // lowercase codes ("zh", "ja") used in its /{srcLang}{tgtLang}/{word} URL scheme.
    // Dictionary pairs are always anchored on English ("en" + code); when the target
    // language itself is "en" this collapses to "enen", which WordReference redirects
    // to its English-only definition page — still a usable result, not an error case.
    public static string ToWordReferenceCode(string code) => code.Split('-')[0].ToLowerInvariant();

    private static string ExtractDefinitions(string html)
    {
        var m = Regex.Match(html, "<div id=\"(articleWRD|article)\">");
        if (!m.Success) return "";
        var start = m.Index + m.Length;
        var end   = html.IndexOf("<div id=\"postArticle\">", start, StringComparison.Ordinal);
        if (end < 0) end = html.Length;
        var inner = html[start..end];

        inner = Regex.Replace(inner, @"<style[^>]*>.*?</style>", "",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        inner = Regex.Replace(inner, @"</(div|p|li|h[1-6]|tr|ul|ol|table)>", "\n", RegexOptions.IgnoreCase);
        inner = Regex.Replace(inner, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        inner = Regex.Replace(inner, "<[^>]+>", "");
        inner = System.Net.WebUtility.HtmlDecode(inner);
        inner = Regex.Replace(inner, @"[^\S\n]+", " ");
        inner = Regex.Replace(inner, @" *\n *", "\n");
        inner = Regex.Replace(inner, @"\n{3,}", "\n\n");
        return inner.Trim();
    }
}

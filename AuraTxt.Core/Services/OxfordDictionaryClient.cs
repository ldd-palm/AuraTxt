using System.Net.Http;
using System.Text.RegularExpressions;

namespace AuraTxt.Core.Services;

public class OxfordDictionaryClient
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

    public OxfordDictionaryClient(HttpClient? http = null)
    {
        if (http is not null) { _http = http; AddHeaders(_http); }
        else _http = _sharedHttp;
    }

    // Monolingual English dictionary — no target-language parameter (unlike GTrans/DeepL/
    // WordReference). Search endpoint auto-redirects to the definition page (e.g. hello_1).
    public async Task<string> DictionaryAsync(string word, CancellationToken ct = default)
    {
        var url  = $"https://www.oxfordlearnersdictionaries.com/search/english/?q={Uri.EscapeDataString(word)}";
        var html = await _http.GetStringAsync(url, ct);
        return ExtractDefinition(html);
    }

    private static string ExtractDefinition(string html)
    {
        const string start = "<div id=\"ox-wrapper\"";
        const string end   = "<div id=\"rightcolumn\"";
        var s = html.IndexOf(start, StringComparison.Ordinal);
        if (s < 0) return "";
        var e = html.IndexOf(end, s, StringComparison.Ordinal);
        if (e < 0) e = html.Length;
        var inner = html[s..e];

        inner = Regex.Replace(inner, @"<script[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
        inner = Regex.Replace(inner, @"<div class=""sound(.|\n)*?</div>", "", RegexOptions.IgnoreCase);
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

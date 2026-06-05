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

    public async Task<string> TranslateAsync(string text, string to = "zh-CHS", CancellationToken ct = default)
    {
        var salt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var sign = Md5($"{AppKey}{text}{salt}{SignSuffix}");
        var body = $"i={Uri.EscapeDataString(text)}&from=AUTO&to={to}" +
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
        const string start = "results-content\">";
        const string end   = "<div id=\"ads\"";
        var s = html.IndexOf(start, StringComparison.Ordinal);
        if (s < 0) return "";
        s += start.Length;
        var e = html.IndexOf(end, s, StringComparison.Ordinal);
        if (e < 0) e = html.Length;
        var inner = html[s..e];

        // Drop the noisy 网络释义/专业释义/英英释义 block (webTrans → wordArticle sibling)
        var wtStart = inner.IndexOf("<div id=\"webTrans\"", StringComparison.Ordinal);
        var wtEnd   = wtStart >= 0
            ? inner.IndexOf("<div id=\"wordArticle\"", wtStart, StringComparison.Ordinal)
            : -1;
        if (wtStart >= 0)
            inner = wtEnd > wtStart ? inner[..wtStart] + inner[wtEnd..] : inner[..wtStart];

        var rxStyle = new System.Text.RegularExpressions.Regex(
            @"<style[^>]*>.*?</style>",
            System.Text.RegularExpressions.RegexOptions.Singleline |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var rxBlock = new System.Text.RegularExpressions.Regex(
            @"</(div|p|li|h[1-6]|tr|ul|ol|table)>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var rxBr = new System.Text.RegularExpressions.Regex(
            @"<br\s*/?>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        inner = rxStyle.Replace(inner, "");
        inner = System.Text.RegularExpressions.Regex.Replace(inner, @"\s+", " ");
        // Separate word keyword from phonetics (baav div)
        inner = System.Text.RegularExpressions.Regex.Replace(
            inner, @"<div\s+class=""baav"">",
            "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        inner = rxBlock.Replace(inner, "\n");
        inner = rxBr.Replace(inner, "\n");
        inner = System.Text.RegularExpressions.Regex.Replace(inner, "<[^>]+>", "");
        inner = System.Net.WebUtility.HtmlDecode(inner);
        inner = System.Text.RegularExpressions.Regex.Replace(inner, @"[^\S\n]+", " ");
        inner = System.Text.RegularExpressions.Regex.Replace(inner, @" *\n *", "\n");
        inner = System.Text.RegularExpressions.Regex.Replace(inner, @"\n{3,}", "\n\n");

        // Remove noise lines
        inner = System.Text.RegularExpressions.Regex.Replace(inner, @"^\s*(相关文章|更多权威例句)\s*$", "",
                    System.Text.RegularExpressions.RegexOptions.Multiline);
        inner = System.Text.RegularExpressions.Regex.Replace(inner, @"\n{3,}", "\n\n");

        return inner.Trim();
    }

    private static string Md5(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }
}

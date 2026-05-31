using System.Net.Http;
using System.Text.Json;

namespace AuraTxt.Core.Services;

public class GoogleTranslateClient
{
    private readonly HttpClient _http;

    public GoogleTranslateClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<string> TranslateAsync(
        string text, string from = "auto", string to = "zh-CN", CancellationToken ct = default)
    {
        var tk  = GenerateTk(text);
        var url = $"https://translate.google.com/translate_a/single" +
                  $"?client=gtx&sl={from}&tl={to}&hl=zh-CN" +
                  $"&dt=bd&dt=t&ie=UTF-8&oe=UTF-8&tk={tk}&q={Uri.EscapeDataString(text)}";

        var json = await _http.GetStringAsync(url, ct);
        return ParseTranslation(json);
    }

    private static string ParseTranslation(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var sb = new System.Text.StringBuilder();
        foreach (var seg in doc.RootElement[0].EnumerateArray())
        {
            if (seg.GetArrayLength() > 0 && seg[0].ValueKind == JsonValueKind.String)
                sb.Append(seg[0].GetString());
        }
        return sb.ToString();
    }

    // Port of tk token algorithm from google_translate.js
    public static string GenerateTk(string text, string tkk = "0.0")
    {
        var parts = tkk.Split('.');
        long h = long.Parse(parts[0]);
        var g = new List<int>();

        for (int i = 0; i < text.Length; i++)
        {
            int c = text[i];
            if (c < 128)
                g.Add(c);
            else if (c < 2048)
            {
                g.Add(c >> 6 | 192);
                g.Add(c & 63  | 128);
            }
            else if ((c & 0xFC00) == 0xD800
                && i + 1 < text.Length
                && (text[i + 1] & 0xFC00) == 0xDC00)
            {
                c = 0x10000 + ((c & 0x3FF) << 10) + (text[++i] & 0x3FF);
                g.Add(c >> 18 | 240);
                g.Add(c >> 12 & 63 | 128);
                g.Add(c >> 6  & 63 | 128);
                g.Add(c       & 63 | 128);
            }
            else
            {
                g.Add(c >> 12 | 224);
                g.Add(c >> 6  & 63 | 128);
                g.Add(c       & 63 | 128);
            }
        }

        long a = h;
        foreach (int x in g)
        {
            a += x;
            a = Xform(a, "+-a^+6");
        }
        a = Xform(a, "+-3^+b+-f");
        a ^= long.Parse(parts[1]);
        if (a < 0) a = (a & 0x7FFFFFFF) + 0x80000000L;
        a %= 1_000_000;
        return $"{a}.{a ^ h}";
    }

    private static long Xform(long a, string b)
    {
        for (int d = 0; d < b.Length - 2; d += 3)
        {
            long shift = b[d + 2] >= 'a' ? b[d + 2] - 87 : b[d + 2] - '0';
            long val   = b[d + 1] == '+' ? (long)((ulong)a >> (int)shift) : a << (int)shift;
            a = b[d] == '+' ? (a + val) & 0xFFFFFFFFL : a ^ val;
        }
        return a;
    }
}

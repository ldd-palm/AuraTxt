namespace AuraTxt.Core.Util;

internal sealed class TagStripFilter
{
    private readonly (string Open, string Close)[] _patterns;
    private string _buf = "";
    private int _activePattern = -1;  // -1 = outside all patterns

    public TagStripFilter(IEnumerable<string> patterns)
    {
        _patterns = patterns.Select(ParsePattern).ToArray();
    }

    private static (string, string) ParsePattern(string p)
    {
        var idx = p.IndexOf("...", StringComparison.Ordinal);
        if (idx < 0) throw new ArgumentException($"Strip pattern must contain '...': {p}");
        return (p[..idx], p[(idx + 3)..]);
    }

    public string Feed(string chunk)
    {
        _buf += chunk;
        var sb = new System.Text.StringBuilder();

        while (true)
        {
            if (_activePattern >= 0)
            {
                var close = _patterns[_activePattern].Close;
                int end = _buf.IndexOf(close, StringComparison.Ordinal);
                if (end < 0)
                {
                    // Keep enough chars to detect partial close spanning the next chunk
                    if (_buf.Length > close.Length - 1)
                        _buf = _buf[^(close.Length - 1)..];
                    break;
                }
                _buf = _buf[(end + close.Length)..];
                _activePattern = -1;
            }
            else
            {
                int earliest = -1, earliestPat = -1;
                for (int i = 0; i < _patterns.Length; i++)
                {
                    int idx = _buf.IndexOf(_patterns[i].Open, StringComparison.Ordinal);
                    if (idx >= 0 && (earliest < 0 || idx < earliest))
                    { earliest = idx; earliestPat = i; }
                }

                if (earliest < 0)
                {
                    // Emit everything up to the last position that could be a partial open tag
                    int safeEnd = FindPartialOpenStart();
                    if (safeEnd > 0)
                    {
                        sb.Append(_buf[..safeEnd]);
                        _buf = _buf[safeEnd..];
                    }
                    break;
                }
                sb.Append(_buf[..earliest]);
                _buf = _buf[(earliest + _patterns[earliestPat].Open.Length)..];
                _activePattern = earliestPat;
            }
        }
        return sb.ToString();
    }

    public string Flush()
    {
        if (_activePattern >= 0) return "";
        var tail = _buf; _buf = ""; return tail;
    }

    // Returns the leftmost index in _buf where a suffix of _buf could be a partial prefix
    // of any open tag. Everything before that index is safe to emit.
    private int FindPartialOpenStart()
    {
        for (int j = 0; j < _buf.Length; j++)
        {
            int suffixLen = _buf.Length - j;
            foreach (var (open, _) in _patterns)
            {
                if (suffixLen < open.Length &&
                    open.StartsWith(_buf[j..], StringComparison.Ordinal))
                    return j;
            }
        }
        return _buf.Length; // nothing matched; safe to emit everything
    }
}

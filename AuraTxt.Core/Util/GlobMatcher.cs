using System.Text.RegularExpressions;

namespace AuraTxt.Core.Util;

public static class GlobMatcher
{
    public static bool Matches(string pattern, string input)
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace(@"\*", ".*") + "$";
        return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
    }

    public static bool MatchesAny(IEnumerable<string> patterns, string input)
        => patterns.Any(p => Matches(p, input));
}

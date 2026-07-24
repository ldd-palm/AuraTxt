using System.Net.Http;
using System.Text.Json;

namespace AuraTxt.Core.Services;

public record UpdateInfo(string Version, string Url);

/// Checks GitHub Releases for a newer AuraTxt version. Notify-only — never
/// downloads or installs anything.
/// See docs/superpowers/specs/2026-07-24-auto-update-design.md.
public static class UpdateService
{
    private const string ReleasesUrl =
        "https://api.github.com/repos/ldd-palm/AuraTxt/releases/latest";

    // Shared instance — GitHub API + JSON GET, no reason to ever construct a
    // second one (SPEC.md §6.9: no per-call `new HttpClient`).
    private static readonly HttpClient _http = CreateShared();

    private static HttpClient CreateShared()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub's API rejects requests with no User-Agent header.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AuraTxt-UpdateCheck");
        return http;
    }

    /// Returns null when the check succeeded but no newer version exists.
    /// Throws (HttpRequestException, JSON errors) when the check itself failed —
    /// callers that need to tell the two apart (manual "Check for Updates") catch
    /// separately from callers that treat both the same (silent startup check).
    public static async Task<UpdateInfo?> CheckAsync(Version currentVersion, CancellationToken ct = default)
    {
        var json = await _http.GetStringAsync(ReleasesUrl, ct);
        using var doc = JsonDocument.Parse(json);

        var tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
        var htmlUrl = doc.RootElement.GetProperty("html_url").GetString() ?? "";

        return IsNewer(tagName, currentVersion)
            ? new UpdateInfo(tagName.TrimStart('v', 'V'), htmlUrl)
            : null;
    }

    /// Pure — no network. Malformed tagName yields false, never throws.
    internal static bool IsNewer(string tagName, Version current)
    {
        var trimmed = tagName.TrimStart('v', 'V');
        return Version.TryParse(trimmed, out var tagVersion) && tagVersion > current;
    }
}

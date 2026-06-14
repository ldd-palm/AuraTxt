using System.Text.Json;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Commands;

public static class ProfileCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        ProfileService.EnsureScaffold();
        var opts = ParseArgs(args);

        if (opts.ContainsKey("list"))
        {
            foreach (var p in ProfileService.All())
                Console.WriteLine($"{p.Id,-30} priority={p.Priority,-5} adapter={string.Join(",", p.AdapterCompatibility),-20} thinking={(p.Thinking is null ? "null" : p.Thinking.Location),-40} source={(p.IsUserFile ? "on-disk" : "embedded")}");
            return 0;
        }

        if (opts.ContainsKey("show") && opts.TryGetValue("id", out var showId))
        {
            var p = ProfileService.GetById(showId);
            if (p is null) { Console.Error.WriteLine($"Profile '{showId}' not found."); return 2; }
            Console.WriteLine(JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (opts.ContainsKey("reload"))
        {
            ProfileService.Reload();
            Console.WriteLine($"Reloaded {ProfileService.All().Count} profiles.");
            return 0;
        }

        if (opts.ContainsKey("validate") && opts.TryGetValue("validate", out var valFile))
            return ValidateFile(valFile);

        if (opts.ContainsKey("import") && opts.TryGetValue("import", out var importFile))
        {
            var result = ValidateFile(importFile);
            if (result != 0) return result;
            var destDir = Path.Combine(AppContext.BaseDirectory, "profiles");
            Directory.CreateDirectory(destDir);
            var fileName = Path.GetFileName(importFile);
            File.Copy(importFile, Path.Combine(destDir, fileName), overwrite: true);
            ProfileService.Reload();
            Console.WriteLine($"Imported {fileName}.");
            return 0;
        }

        if (opts.ContainsKey("new"))
            return await CreateNew(opts);

        if (opts.ContainsKey("probe"))
            return await Probe(opts);

        Console.Error.WriteLine(
            "Usage: auracfg profile --list | --show --id ID | --reload | --validate FILE | " +
            "--import FILE | --new --id ID --base BASE [--pattern P ...] [--priority N] [--adapter TYPE] | " +
            "--probe --provider PROV --model MODEL");
        return 1;
    }

    private static int ValidateFile(string path)
    {
        if (!File.Exists(path)) { Console.Error.WriteLine($"File not found: {path}"); return 2; }
        try
        {
            var json = File.ReadAllText(path);
            var p    = JsonSerializer.Deserialize<ProfileFile>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (p is null || string.IsNullOrEmpty(p.Id))
            { Console.Error.WriteLine("Invalid profile: missing 'id' field."); return 2; }
            foreach (var pat in p.StripPatterns)
                if (!pat.Contains("..."))
                { Console.Error.WriteLine($"Invalid strip pattern (missing '...'): {pat}"); return 2; }
            Console.WriteLine($"✓ Profile '{p.Id}' is valid.");
            return 0;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"JSON parse error: {ex.Message}");
            return 2;
        }
    }

    private static async Task<int> CreateNew(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id",   out var id))     { Console.Error.WriteLine("--id required"); return 1; }
        if (!opts.TryGetValue("base", out var baseId)) { Console.Error.WriteLine("--base required"); return 1; }
        if (!opts.TryGetValue("adapter", out var adapterArg)) adapterArg = "openai_compatible";

        var baseProfile = ProfileService.GetById(baseId);
        var patterns    = opts.TryGetValue("patterns", out var pats) ? pats.Split('|').ToList() : new List<string> { "*" };
        var priority    = opts.TryGetValue("priority", out var pri)  ? int.Parse(pri) : 90;

        var newProfile = new ProfileFile
        {
            Id                   = id,
            Match                = new() { NamePatterns = patterns },
            Priority             = priority,
            AdapterCompatibility = adapterArg == "both"
                ? new() { "openai_compatible", "gemini_native" }
                : new() { adapterArg },
            Thinking             = baseProfile?.Thinking is null ? null : CloneThinking(baseProfile.Thinking),
            StripPatterns        = baseProfile?.StripPatterns.ToList() ?? new(),
            Capabilities         = baseProfile?.Capabilities ?? new(),
            RecommendedParams    = baseProfile?.RecommendedParams.DeepClone()?.AsObject() ?? new()
        };

        var destDir = Path.Combine(AppContext.BaseDirectory, "profiles");
        Directory.CreateDirectory(destDir);
        var dest = Path.Combine(destDir, $"{id}.json");
        File.WriteAllText(dest, JsonSerializer.Serialize(newProfile,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
        Console.WriteLine($"Created {dest}");
        await Task.CompletedTask;
        return 0;
    }

    private static async Task<int> Probe(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("provider", out var providerId)) { Console.Error.WriteLine("--provider required"); return 1; }
        if (!opts.TryGetValue("model",    out var modelTarget)) { Console.Error.WriteLine("--model required"); return 1; }

        var svc = new ConfigService();
        var cfg = svc.Load();
        if (!cfg.Models.TryGetValue(providerId, out var provider))
        { Console.Error.WriteLine($"Provider '{providerId}' not found in config."); return 1; }
        var model = provider.Models.FirstOrDefault(m => m.TargetModel == modelTarget);
        if (model is null) { Console.Error.WriteLine($"Model '{modelTarget}' not found in provider '{providerId}'."); return 1; }

        var profile = ProfileService.Resolve(model, provider.AdapterType);
        Console.WriteLine($"Resolved profile:  {profile.Id} ({(profile.IsUserFile ? "on-disk" : "embedded")}, priority={profile.Priority})");
        Console.WriteLine($"Thinking location: {profile.Thinking?.Location ?? "(null)"}");
        Console.WriteLine($"Disable payload:   {profile.Thinking?.Modes.Disable.ToJsonString() ?? "(none)"}");
        Console.WriteLine();

        // Enable request/response logging to a temp file so we can show the raw exchange on error
        var logPath = Path.Combine(Path.GetTempPath(), $"auracfg-probe-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        AuraTxt.Core.Services.LogService.Enabled = true;
        AuraTxt.Core.Services.LogService.LogPath  = logPath;

        var client     = new AuraTxt.Core.Services.AiClient();
        // Use {SelectedText} so userPrompt is non-empty ("Say only: OK")
        var testAction = new AuraTxt.Core.Models.ActionItem { ThinkingMode = "disable", Prompt = "{SelectedText}" };
        var sw         = System.Diagnostics.Stopwatch.StartNew();
        string? firstChunk = null;
        var sb = new System.Text.StringBuilder();
        try
        {
            await foreach (var chunk in client.StreamAsync(providerId, provider, model, testAction, "Say only: OK", ""))
            {
                if (firstChunk is null) { firstChunk = chunk; sw.Stop(); }
                sb.Append(chunk);
            }
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"[Error] {ex.Message}");
            if (File.Exists(logPath))
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("── Raw HTTP exchange ──────────────────────────────────────────");
                Console.Error.WriteLine(File.ReadAllText(logPath));
                Console.Error.WriteLine("───────────────────────────────────────────────────────────────");
            }
            return 2;
        }
        catch (Exception ex) { Console.Error.WriteLine($"[Error] {ex.Message}"); return 2; }
        finally { AuraTxt.Core.Services.LogService.Enabled = false; }

        var response = sb.ToString();
        Console.WriteLine($"Request included thinking control: {(profile.Thinking != null ? "YES" : "NO")}");
        Console.WriteLine($"Response contained <think> tags:   {(response.Contains("<think>") ? "YES" : "NO")}");
        Console.WriteLine($"First-token latency:               {sw.ElapsedMilliseconds}ms");
        Console.WriteLine();

        bool ok = !response.Contains("<think>");
        Console.WriteLine(ok
            ? "✓ Profile appears to work correctly for this model."
            : "⚠ Response may contain reasoning tags. Review the profile's thinking.modes.disable payload.");
        return ok ? 0 : 1;
    }

    private static ThinkingControl CloneThinking(ThinkingControl src) =>
        new() { Location = src.Location, Modes = new ThinkingModes
        {
            Disable    = src.Modes.Disable.DeepClone()!.AsObject(),
            EnableHigh = src.Modes.EnableHigh.DeepClone()!.AsObject()
        }};

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var result   = new Dictionary<string, string>();
        var patterns = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            var key = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                result[key] = args[++i];
            else
                result[key] = "";

            if (key == "pattern" && result.TryGetValue("pattern", out var pv))
            {
                patterns.Add(pv);
                result.Remove("pattern");
            }
        }
        if (patterns.Any()) result["patterns"] = string.Join("|", patterns);
        return result;
    }
}

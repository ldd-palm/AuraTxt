using System.Text.Json;
using System.Text.Json.Nodes;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using Spectre.Console;

namespace AuraTxt.Cli.Tui.Flows;

public static class ProfileNewFlow
{
    public static async Task RunAsync(TuiApp app)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]New Profile Wizard[/]\n");

        // Step 1: Adapter compatibility
        var adapter = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Step 1/6  Adapter compatibility")
                .AddChoices("openai_compatible", "gemini_native", "both"));
        var adapterList = adapter == "both"
            ? new List<string> { "openai_compatible", "gemini_native" }
            : new List<string> { adapter };

        // Step 2: Profile id
        string id;
        while (true)
        {
            id = AnsiConsole.Ask<string>("Step 2/6  Profile id (kebab-case, globally unique):");
            if (!string.IsNullOrWhiteSpace(id) && !id.Contains(' ')) break;
            AnsiConsole.MarkupLine("[red]Id must be non-empty and contain no spaces.[/]");
        }

        // Step 3: Base profile
        var bases = ProfileService.All()
            .Select(p => p.Id)
            .Prepend("(blank — thinking: null)")
            .ToList();
        var baseChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>().Title("Step 3/6  Base on which profile?").AddChoices(bases));
        var baseProfile = baseChoice.StartsWith("(") ? null : ProfileService.GetById(baseChoice);

        // Step 4: Match patterns
        var patterns = new List<string>();
        AnsiConsole.MarkupLine("Step 4/6  Match patterns (blank to finish, [grey]*[/] = wildcard):");
        while (true)
        {
            var pat = AnsiConsole.Ask<string>("  Pattern (or blank to finish):", "");
            if (string.IsNullOrWhiteSpace(pat)) break;
            patterns.Add(pat);
        }
        if (!patterns.Any()) patterns.Add("*");

        // Step 5: Priority
        var priority = AnsiConsole.Ask("Step 5/6  Priority (90=normal, 95=high):", 90);

        // Step 6: Review + save
        var newProfile = new ProfileFile
        {
            Id                   = id,
            Match                = new() { NamePatterns = patterns },
            Priority             = priority,
            AdapterCompatibility = adapterList,
            Thinking             = baseProfile?.Thinking is null ? null : CloneThinking(baseProfile.Thinking),
            StripPatterns        = baseProfile?.StripPatterns.ToList() ?? new(),
            Capabilities         = baseProfile?.Capabilities ?? new(),
            RecommendedParams    = CloneParams(baseProfile?.RecommendedParams)
        };

        AnsiConsole.MarkupLine("\n[bold]Step 6/6  Review[/]");
        AnsiConsole.MarkupLine($"  id:                    {newProfile.Id}");
        AnsiConsole.MarkupLine($"  adapter_compatibility: [{string.Join(", ", newProfile.AdapterCompatibility)}]");
        AnsiConsole.MarkupLine($"  match.name_patterns:   [{string.Join(", ", newProfile.Match.NamePatterns)}]");
        AnsiConsole.MarkupLine($"  priority:              {newProfile.Priority}");
        AnsiConsole.MarkupLine($"  thinking:              {(newProfile.Thinking is null ? "null" : newProfile.Thinking.Location)}");
        AnsiConsole.MarkupLine($"  strip_patterns:        [{string.Join(", ", newProfile.StripPatterns)}]");

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Save?")
                .AddChoices("[Enter] Save and open in editor", "[S] Save without opening", "[Esc] Cancel"));

        if (action.StartsWith("[Esc]")) return;

        var profilesDir = Path.Combine(AppContext.BaseDirectory, "profiles");
        Directory.CreateDirectory(profilesDir);
        var dest = Path.Combine(profilesDir, $"{id}.json");
        var json = JsonSerializer.Serialize(newProfile,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        File.WriteAllText(dest, json);
        AnsiConsole.MarkupLine($"[green]Saved to {Markup.Escape(dest)}[/]");

        if (action.StartsWith("[Enter]"))
        {
            var editor = app.Cfg.Settings.ConfigEditor;
            var exe    = string.IsNullOrEmpty(editor) ? "notepad.exe" : editor;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, $"\"{dest}\"")
                { UseShellExecute = true });
        }

        await Task.CompletedTask;
    }

    private static ThinkingControl CloneThinking(ThinkingControl src) =>
        new() { Location = src.Location, Modes = new ThinkingModes
        {
            Disable    = src.Modes.Disable.DeepClone()!.AsObject(),
            EnableHigh = src.Modes.EnableHigh.DeepClone()!.AsObject()
        }};

    private static JsonObject CloneParams(JsonObject? src)
    {
        if (src is null)
            return new JsonObject { ["temperature"] = 0.3, ["top_p"] = 0.95, ["max_tokens"] = 4096 };
        return src.DeepClone()!.AsObject();
    }
}

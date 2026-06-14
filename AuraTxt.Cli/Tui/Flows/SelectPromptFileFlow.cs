using AuraTxt.Core.Services;
using Spectre.Console;

namespace AuraTxt.Cli.Tui.Flows;

public static class SelectPromptFileFlow
{
    /// Shows library + external paths + accepts typed path. Returns absolute path or null if cancelled.
    public static string? Run(TuiApp app)
    {
        var inDir  = PromptService.ListPrompts();
        var dirSet = inDir.Select(p => Path.GetFullPath(p).ToLowerInvariant()).ToHashSet();

        // Collect external paths already wired to actions or system prompt
        var external = app.Cfg.Actions
            .Select(a => a.Prompt)
            .Append(app.Cfg.Settings?.SystemPrompt)
            .Where(p => PromptService.IsFileRef(p))
            .Select(p => Path.GetFullPath(p!))
            .Where(p => !dirSet.Contains(p.ToLowerInvariant()) && File.Exists(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Short filename for dir files, full path for external files
        var entries = inDir.Select(p => (Label: Path.GetFileName(p)!, Path: p))
                   .Concat(external.Select(p => (Label: p, Path: p)))
                   .ToList();

        var options = entries.Select(e => e.Label)
                             .Append("Type an absolute path...")
                             .Append("Cancel")
                             .ToList();

        Console.WriteLine();
        AnsiConsole.MarkupLine("  [bold]Select a prompt file[/]");
        var choice = app.Renderer.SelectFromList("Prompt file", options);
        if (choice == "Cancel" || string.IsNullOrEmpty(choice)) return null;

        string path;
        if (choice == "Type an absolute path...")
        {
            var typed = app.Renderer.AskOrCancel("Absolute path to prompt file");
            if (typed is null) return null;
            path = typed.Trim('"');
        }
        else
        {
            path = entries.First(e => e.Label == choice).Path;
        }

        if (!File.Exists(path))
        { app.Renderer.SetNotice($"File not found: {path}", NoticeKind.Error); return null; }

        Console.WriteLine();
        SystemPromptFlow.ShowPanel(path);

        return Path.GetFullPath(path);
    }
}

using AuraTxt.Core.Services;
using Spectre.Console;

namespace AuraTxt.Cli.Tui.Flows;

public static class SelectPromptFileFlow
{
    /// Shows library + accepts absolute path. Returns absolute path or null if cancelled.
    public static string? Run(TuiApp app)
    {
        var prompts = PromptService.ListPrompts();
        Console.WriteLine();
        AnsiConsole.MarkupLine("  [bold]Select a prompt file[/]");

        var options = prompts.Select(Path.GetFileName).Append("Type an absolute path...").Append("Cancel").ToList();
        var choice  = app.Renderer.SelectFromList("Prompt file", options!);

        if (choice == "Cancel" || string.IsNullOrEmpty(choice)) return null;

        string path;
        if (choice == "Type an absolute path...")
        {
            path = app.Renderer.Ask("Absolute path to prompt file").Trim('"');
        }
        else
        {
            path = prompts.First(p => Path.GetFileName(p) == choice);
        }

        if (!File.Exists(path))
        { app.Renderer.SetNotice($"File not found: {path}", NoticeKind.Error); return null; }

        // Preview
        Console.WriteLine();
        AnsiConsole.MarkupLine($"  [grey]Preview ({Path.GetFileName(path)}):[/]");
        try
        {
            foreach (var line in File.ReadAllText(path).Split('\n').Take(8))
                AnsiConsole.MarkupLine($"  [grey]│[/] {Markup.Escape(line.TrimEnd('\r'))}");
        }
        catch { }

        return Path.GetFullPath(path);
    }
}

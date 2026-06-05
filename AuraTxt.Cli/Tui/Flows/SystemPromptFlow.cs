using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using Spectre.Console;

namespace AuraTxt.Cli.Tui.Flows;

public static class SystemPromptFlow
{
    public static void Run(AppSettings s, TuiApp app)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold cyan]System Prompt[/]");
        Console.WriteLine();

        var label = TuiRenderer.PromptLabel(s.SystemPrompt);
        AnsiConsole.MarkupLine($"  Current: [bold cyan]{Markup.Escape(label)}[/]");
        AnsiConsole.MarkupLine("  [grey]Sent as the system message before every action.[/]");
        Console.WriteLine();

        var content = PromptService.Resolve(s.SystemPrompt);
        if (string.IsNullOrWhiteSpace(content))
        {
            AnsiConsole.MarkupLine("  [grey](empty)[/]");
        }
        else
        {
            var allLines = content.Split('\n');
            var preview  = allLines.Take(20).Select(l => Markup.Escape(l.TrimEnd('\r')));
            var body     = string.Join("\n", preview);
            if (allLines.Length > 20) body += "\n[grey]…(truncated)[/]";

            var panel = new Spectre.Console.Panel(new Markup(body))
            {
                Border      = Spectre.Console.BoxBorder.Rounded,
                BorderStyle = new Style(foreground: Spectre.Console.Color.Cyan1),
                Header      = new PanelHeader($"[bold cyan] {Markup.Escape(label)} [/]"),
                Padding     = new Spectre.Console.Padding(1, 0, 1, 0),
            };
            AnsiConsole.Write(panel);
        }

        Console.WriteLine();
        AnsiConsole.MarkupLine(
            "  [bold white][[E]][/] Edit current file   " +
            "[bold white][[P]][/] Point to different file   " +
            "[bold white][[Esc]][/] Back");
        Console.Write("  Select: ");

        var ki = Console.ReadKey(true);
        Console.WriteLine();

        if (ki.Key == ConsoleKey.Escape || char.ToUpper(ki.KeyChar) == 'B') return;

        if (char.ToUpper(ki.KeyChar) == 'E')
        {
            var target = File.Exists(s.SystemPrompt) ? s.SystemPrompt : PromptService.SystemFile;
            app.OpenInEditor(target);
        }
        else if (char.ToUpper(ki.KeyChar) == 'P')
        {
            var path = SelectPromptFileFlow.Run(app);
            if (path != null) { s.SystemPrompt = path; app.MarkDirty(); app.Renderer.SetNotice("System prompt file updated."); }
        }
    }
}

using AuraTxt.Cli.Tui.Flows;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using Spectre.Console;

namespace AuraTxt.Cli.Tui.Pages;

public class ProfilesPage : PageBase
{
    public override string Title => "Profiles";

    public override async Task<PageResult> RunAsync(TuiApp app, CancellationToken ct)
    {
        while (true)
        {
            var profiles = ProfileService.All();
            AnsiConsole.Clear();

            var crumb = string.Join(" › ", app.GetBreadcrumb().Select(t => t));
            AnsiConsole.MarkupLine($"[bold]{Markup.Escape(crumb)}[/]\n");

            var table = new Table()
                .AddColumn("Priority")
                .AddColumn("Id")
                .AddColumn("Adapter")
                .AddColumn("Thinking")
                .AddColumn("Strip")
                .AddColumn("Source");

            foreach (var p in profiles)
            {
                table.AddRow(
                    p.Priority.ToString(),
                    Markup.Escape(p.Id),
                    Markup.Escape(string.Join(", ", p.AdapterCompatibility)),
                    p.Thinking is null ? "[grey]null[/]" : "[green]yes[/]",
                    p.StripPatterns.Count > 0 ? "[yellow]yes[/]" : "",
                    p.IsUserFile ? "[green]on-disk[/]" : "[grey]embedded[/]");
            }
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey][[O]] Open in editor  [[R]] Reload  [[N]] New  [[Esc]] Back[/]");

            var key = Console.ReadKey(true);
            switch (char.ToUpperInvariant(key.KeyChar))
            {
                case 'O':
                    var toOpen = SelectProfile(profiles, "Open in editor");
                    if (toOpen is not null) OpenInEditor(app, toOpen);
                    break;

                case 'R':
                    ProfileService.Reload();
                    AnsiConsole.MarkupLine("[green]Profiles reloaded.[/]");
                    await Task.Delay(800, ct);
                    break;

                case 'N':
                    await ProfileNewFlow.RunAsync(app);
                    ProfileService.Reload();
                    break;

                case '\x1b':
                    return PageResult.Back();
            }
        }
    }

    private static ProfileFile? SelectProfile(IReadOnlyList<ProfileFile> profiles, string prompt)
    {
        if (!profiles.Any()) return null;
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(prompt)
                .AddChoices(profiles.Select(p => p.Id).Prepend("(cancel)")));
        return choice == "(cancel)" ? null : profiles.FirstOrDefault(p => p.Id == choice);
    }

    private static void OpenInEditor(TuiApp app, ProfileFile profile)
    {
        var dir  = Path.Combine(AppContext.BaseDirectory, "profiles");
        var path = Path.Combine(dir, $"{profile.Id}.json");

        if (!File.Exists(path))
        {
            Directory.CreateDirectory(dir);
            using var stream = typeof(ProfileService).Assembly
                .GetManifestResourceStream($"AuraTxt.Core.Profiles.{profile.Id}.json");
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                File.WriteAllText(path, reader.ReadToEnd());
                AnsiConsole.MarkupLine($"[yellow]Extracted embedded profile to {Markup.Escape(path)}[/]");
            }
        }

        var editor = app.Cfg.Settings.ConfigEditor;
        var exe    = string.IsNullOrEmpty(editor) ? "notepad.exe" : editor;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, $"\"{path}\"")
                { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Cannot open editor: {Markup.Escape(ex.Message)}[/]");
            Console.ReadKey(true);
        }
    }
}

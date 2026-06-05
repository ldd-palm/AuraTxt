using AuraTxt.Core.Services;
using Spectre.Console;

namespace AuraTxt.Cli.Tui;

public class TuiRenderer
{
    private string     _notice     = "";
    private NoticeKind _noticeKind = NoticeKind.Info;

    // ── Symbols (ASCII fallback when color not supported) ──────────────────
    private static bool HasColor => AnsiConsole.Profile.Capabilities.ColorSystem != ColorSystem.NoColors;
    public static string SymActive    => HasColor ? "(●)" : "(*)";
    public static string SymInactive  => "( )";
    public static string SymChecked   => HasColor ? "[■]" : "[X]";
    public static string SymUnchecked => "[ ]";
    public static string SymSep       => HasColor ? "─────────────────────" : "---------------------";

    // ── Notice (shown once on next DrawFrame, then cleared) ────────────────
    public void SetNotice(string message, NoticeKind kind = NoticeKind.Success)
    {
        _notice     = message;
        _noticeKind = kind;
    }

    // ── Main frame rendering ───────────────────────────────────────────────
    public void DrawFrame(string[] breadcrumb, IReadOnlyList<MenuItem> items, int cursorIndex, string footerHints)
    {
        AnsiConsole.Clear();
        var width = Math.Max(40, Console.WindowWidth - 2);

        // 1. Header (breadcrumb)
        var crumb = string.Join(" [grey]›[/] ", breadcrumb.Select(t => $"[bold]{Markup.Escape(t)}[/]"));
        AnsiConsole.Write(new Spectre.Console.Panel(crumb).Expand().Border(BoxBorder.Rounded).BorderColor(Spectre.Console.Color.Grey35));

        // 2. Items
        Console.WriteLine();
        int valueCol = Math.Min(36, width / 2);
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.IsSeparator)
            {
                AnsiConsole.MarkupLine($"[grey35]  {SymSep}[/]");
                continue;
            }
            RenderItem(item, isSelected: i == cursorIndex, valueCol);
        }

        // 3. Notice
        if (!string.IsNullOrEmpty(_notice))
        {
            Console.WriteLine();
            var (color, sym) = _noticeKind switch
            {
                NoticeKind.Success => ("green",  HasColor ? "✓" : "OK"),
                NoticeKind.Warning => ("yellow", "!"),
                NoticeKind.Error   => ("red",    HasColor ? "✗" : "X"),
                _                  => ("grey",   "i"),
            };
            AnsiConsole.MarkupLine($"  [{color}]{sym} {Markup.Escape(_notice)}[/]");
            _notice = "";
        }

        // 4. Footer
        Console.WriteLine();
        AnsiConsole.MarkupLine($"[grey35]  {Markup.Escape(footerHints)}[/]");
    }

    private static void RenderItem(MenuItem item, bool isSelected, int valueCol)
    {
        var labelField = item.Label.PadRight(valueCol);
        var valText    = item.Value is null ? "" : Truncate(item.Value, Math.Max(10, Console.WindowWidth - valueCol - 10));
        var valColor   = item.ValueStyle switch
        {
            ItemValueStyle.Success => "green",
            ItemValueStyle.Danger  => "red",
            ItemValueStyle.Warning => "yellow",
            _                      => "grey",
        };
        var valMarkup = valText.Length > 0 ? $"  [{valColor}]{Markup.Escape(valText)}[/]" : "";

        if (isSelected)
            AnsiConsole.MarkupLine($"[bold cyan]  › [[{Markup.Escape(item.Key)}]] {Markup.Escape(labelField)}[/]{valMarkup}");
        else
            AnsiConsole.MarkupLine($"[grey]    [[{Markup.Escape(item.Key)}]][/] {Markup.Escape(labelField)}{valMarkup}");
    }

    // ── Key reading ────────────────────────────────────────────────────────
    public MenuKey ReadMenuKey()
    {
        var ki = Console.ReadKey(intercept: true);
        return ki.Key switch
        {
            ConsoleKey.UpArrow    => new MenuKey.Arrow(Up: true),
            ConsoleKey.DownArrow  => new MenuKey.Arrow(Up: false),
            ConsoleKey.Enter      => new MenuKey.Confirm(),
            ConsoleKey.Escape     => new MenuKey.Escape(),
            ConsoleKey.Backspace  => new MenuKey.Escape(),
            _ when ki.KeyChar is 'q' or 'Q' => new MenuKey.Quit(),
            _ when char.IsDigit(ki.KeyChar) && ki.KeyChar != '0'
                                  => new MenuKey.Number(ki.KeyChar - '0'),
            _ when char.IsLetter(ki.KeyChar)
                                  => new MenuKey.Letter(char.ToUpper(ki.KeyChar)),
            _                     => new MenuKey.Unknown(),
        };
    }

    // ── Text input (delegates to Spectre prompts) ──────────────────────────
    public string Ask(string prompt, string? defaultValue = null)
    {
        Console.WriteLine();
        var tp = new TextPrompt<string>($"[yellow]  {Markup.Escape(prompt)}:[/]").AllowEmpty();
        if (defaultValue is not null)
            tp.DefaultValue(defaultValue).DefaultValueStyle(Style.Parse("grey"));
        return AnsiConsole.Prompt(tp);
    }

    public string AskSecret(string prompt)
    {
        Console.WriteLine();
        return AnsiConsole.Prompt(
            new TextPrompt<string>($"[yellow]  {Markup.Escape(prompt)}:[/]")
                .Secret('•')
                .AllowEmpty());
    }

    public bool Confirm(string prompt, bool defaultYes = true)
    {
        Console.WriteLine();
        return AnsiConsole.Confirm($"  {Markup.Escape(prompt)}", defaultYes);
    }

    public string SelectFromList(string prompt, IReadOnlyList<string> choices, string? current = null)
    {
        Console.WriteLine();
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"  [yellow]{Markup.Escape(prompt)}[/]")
                .HighlightStyle(Style.Parse("bold cyan"))
                .AddChoices(choices));
    }

    public void PauseForKey(string msg = "Press any key to continue...")
    {
        Console.WriteLine();
        AnsiConsole.MarkupLine($"[grey]  {Markup.Escape(msg)}[/]");
        Console.ReadKey(true);
    }

    // ── Static helpers ─────────────────────────────────────────────────────
    public static string StatusBadge(bool enabled) =>
        enabled ? $"{SymActive} active" : $"{SymInactive} inactive";

    public static ItemValueStyle StatusStyle(bool enabled) =>
        enabled ? ItemValueStyle.Success : ItemValueStyle.Danger;

    public static string Truncate(string s, int max)
    {
        if (max <= 3) return s.Length <= max ? s : s[..max];
        return s.Length <= max ? s : s[..(max - 1)] + "…";
    }

    public static string MaskKey(string key) =>
        string.IsNullOrEmpty(key)    ? "(not set)"
        : key.Length <= 8            ? new string('•', key.Length)
        :                              key[..4] + new string('•', Math.Min(8, key.Length - 4));

    public static string PromptLabel(string? p) =>
        string.IsNullOrEmpty(p)       ? "(none)"
        : PromptService.IsFileRef(p)  ? $"[ {Path.GetFileName(p)} ]"
        :                               Truncate(p, 50);
}

using AuraTxt.Cli.Tui.Flows;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Tui.Pages;

public class GeneralSettingsPage : PageBase
{
    public override string Title => "General Settings";

    public override Task<PageResult> RunAsync(TuiApp app, CancellationToken ct)
    {
        while (true)
        {
            var s     = app.Cfg.Settings;
            var items = BuildItems(s);
            var (cursor, sel) = BuildCursorState(items);
            app.Renderer.DrawFrame(app.GetBreadcrumb(), items, cursor, FooterWith());

            var key = app.Renderer.ReadMenuKey();
            switch (key)
            {
                case MenuKey.Arrow a:
                    if (a.Up) MoveUp(sel.Count); else MoveDown(sel.Count);
                    break;

                case MenuKey.Confirm:
                    var done = HandleKey(items[cursor].Key, s, app);
                    if (done) return Task.FromResult(PageResult.Back());
                    break;

                case MenuKey.Number n:
                    JumpTo(sel, items, n.N.ToString());
                    HandleKey(n.N.ToString(), s, app);
                    break;

                case MenuKey.Escape:
                    return Task.FromResult(PageResult.Back());
                case MenuKey.Quit:
                    return Task.FromResult(PageResult.Exit());
            }
        }
    }

    private static IReadOnlyList<MenuItem> BuildItems(AppSettings s) =>
    [
        new MenuItem("1", "Font Size",       $"{s.FontSize}"),
        new MenuItem("2", "Window Opacity",  $"{s.ResultWindowOpacity:F2}"),
        new MenuItem("3", "Trigger Delay",   $"{s.MenuTriggerDelayMs} ms"),
        new MenuItem("4", "System Prompt",   TuiRenderer.PromptLabel(s.SystemPrompt)),
        new MenuItem("5", "Target Language", TuiApp.LangLabel(s.TargetLanguage)),
        new MenuItem("6", "Theme",           s.Theme),
        new MenuItem("7", "Speech Voice",    s.SpeechVoice),
        new MenuItem("8", "Prompt Editor",   string.IsNullOrEmpty(s.PromptEditor) ? "notepad.exe (default)" : s.PromptEditor),
        new MenuItem("9", "Config Editor",   string.IsNullOrEmpty(s.ConfigEditor) ? "auracfg (default)"    : s.ConfigEditor),
    ];

    private bool HandleKey(string key, AppSettings s, TuiApp app)
    {
        switch (key)
        {
            case "1":
                var fs = app.Renderer.Ask("Font size", s.FontSize.ToString());
                if (int.TryParse(fs, out var fv) && fv > 0) { s.FontSize = fv; app.MarkDirty(); app.Renderer.SetNotice($"Font size → {fv}"); }
                break;
            case "2":
                var op = app.Renderer.Ask("Opacity (0.1–1.0)", s.ResultWindowOpacity.ToString("F2"));
                if (double.TryParse(op, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ov))
                { s.ResultWindowOpacity = Math.Clamp(ov, 0.1, 1.0); app.MarkDirty(); app.Renderer.SetNotice($"Opacity → {s.ResultWindowOpacity:F2}"); }
                break;
            case "3":
                var dm = app.Renderer.Ask("Trigger delay ms", s.MenuTriggerDelayMs.ToString());
                if (int.TryParse(dm, out var dv) && dv >= 0) { s.MenuTriggerDelayMs = dv; app.MarkDirty(); app.Renderer.SetNotice($"Delay → {dv} ms"); }
                break;
            case "4":
                SystemPromptFlow.Run(s, app);
                break;
            case "5":
                SelectLanguage(s, app);
                break;
            case "6":
                SelectTheme(s, app);
                break;
            case "7":
                SelectVoice(s, app);
                break;
            case "8":
                var pe = app.Renderer.Ask("Prompt editor (blank = notepad.exe)", s.PromptEditor);
                s.PromptEditor = pe; app.MarkDirty();
                app.Renderer.SetNotice($"Prompt editor → {(string.IsNullOrEmpty(pe) ? "notepad.exe" : pe)}");
                break;
            case "9":
                var ce = app.Renderer.Ask("Config editor (blank = auracfg)", s.ConfigEditor);
                s.ConfigEditor = ce; app.MarkDirty();
                app.Renderer.SetNotice($"Config editor → {(string.IsNullOrEmpty(ce) ? "auracfg" : ce)}");
                break;
        }
        return false;
    }

    private static void SelectLanguage(AppSettings s, TuiApp app)
    {
        var langs  = TuiApp.TranslateLanguages;
        var labels = langs.Select(l => $"{l.Name} ({l.Code})").Append("Custom code...").ToList();
        var choice = app.Renderer.SelectFromList("Target Language", labels, TuiApp.LangLabel(s.TargetLanguage));

        string code;
        if (choice.StartsWith("Custom"))
            code = app.Renderer.Ask("Language code (e.g. th, vi)");
        else
            code = langs.First(l => $"{l.Name} ({l.Code})" == choice).Code;

        if (!string.IsNullOrWhiteSpace(code))
        { s.TargetLanguage = code; app.MarkDirty(); app.Renderer.SetNotice($"Language → {TuiApp.LangLabel(code)}"); }
    }

    private static void SelectTheme(AppSettings s, TuiApp app)
    {
        ThemeService.EnsureScaffold();
        var themes = ThemeService.ListThemes();
        if (themes.Count == 0) { app.Renderer.SetNotice("No themes found.", NoticeKind.Warning); return; }
        var labels = themes.Select(t => $"{t.Name} — {t.Description}").ToList();
        var choice = app.Renderer.SelectFromList("Theme", labels, $"{themes.FirstOrDefault(t => t.Id == s.Theme)?.Name} — {themes.FirstOrDefault(t => t.Id == s.Theme)?.Description}");
        var theme  = themes.FirstOrDefault(t => $"{t.Name} — {t.Description}" == choice);
        if (theme != null) { s.Theme = theme.Id; app.MarkDirty(); app.Renderer.SetNotice($"Theme → {theme.Name}"); }
    }

    private static void SelectVoice(AppSettings s, TuiApp app)
    {
        var voices = SpeechService.GetInstalledVoices();
        if (voices.Count == 0) { app.Renderer.SetNotice("No TTS voices found.", NoticeKind.Warning); return; }
        var choice = app.Renderer.SelectFromList("Speech Voice", voices, s.SpeechVoice);
        s.SpeechVoice = choice; app.MarkDirty();
        app.Renderer.SetNotice($"Voice → {choice}");
    }
}

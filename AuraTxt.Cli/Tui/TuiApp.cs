using AuraTxt.Cli.Commands;
using AuraTxt.Cli.Tui.Pages;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using Spectre.Console;

namespace AuraTxt.Cli.Tui;

public class TuiApp(ConfigService configService)
{
    internal ConfigRoot  Cfg      { get; private set; } = null!;
    internal bool        Dirty    { get; set; }
    internal TuiRenderer Renderer { get; } = new();
    private  NavStack    _nav     = new();

    // ── Entry point ─────────────────────────────────────────────────────────
    public async Task RunAsync()
    {
        PromptService.EnsureScaffold();
        Cfg   = configService.Load();
        Dirty = false;

        _nav = new NavStack();
        _nav.Push(new MainMenuPage());

        while (!_nav.IsEmpty)
        {
            var page = _nav.Peek()!;
            PageResult result;
            try   { result = await page.RunAsync(this, CancellationToken.None); }
            catch (OperationCanceledException) { break; }

            switch (result.Kind)
            {
                case PageResultKind.Back: _nav.Pop();            break;
                case PageResultKind.Push: _nav.Push(result.Next!); break;
                case PageResultKind.Exit: await HandleExitAsync(); return;
            }
        }
    }

    // ── Nav helpers ─────────────────────────────────────────────────────────
    internal string[] GetBreadcrumb() => _nav.Breadcrumb;

    // ── Config persistence ──────────────────────────────────────────────────
    internal void MarkDirty() => Dirty = true;

    internal void SaveNow()
    {
        configService.SaveWithBackup(Cfg);
        Dirty = false;
        Renderer.SetNotice("Config saved (backup written to config.json.bak).");
    }

    internal async Task HandleExitAsync()
    {
        if (Dirty && Renderer.Confirm("Changes detected. Save before exit?", defaultYes: true))
            configService.SaveWithBackup(Cfg);
        await Task.CompletedTask;
    }

    // ── Doctor ──────────────────────────────────────────────────────────────
    internal void RunDoctor()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]Doctor — Config Validation[/]");
        Console.WriteLine();
        var tmp = Path.GetTempFileName();
        try
        {
            new ConfigService(tmp).Save(Cfg);
            new DoctorCommand(new ConfigService(tmp)).Execute();
        }
        finally { File.Delete(tmp); }
        Renderer.PauseForKey();
    }

    // ── Shared business logic (moved from InteractiveMenu) ──────────────────

    internal static string LangLabel(string code) =>
        TranslateLanguages.FirstOrDefault(x => x.Code == code) is var (_, name) && name is not null
            ? $"{name} ({code})"
            : code;

    internal string ModelLabel(string modelRef)
    {
        var r = Cfg.ResolveModel(modelRef);
        return r is null ? modelRef : $"{r.Value.provider.DisplayName} / {r.Value.model.Alias}";
    }

    internal static bool SamePath(string? a, string b)
    {
        if (string.IsNullOrEmpty(a) || !PromptService.IsFileRef(a)) return false;
        try
        {
            var fullA = Path.IsPathRooted(a) ? a : Path.Combine(AppContext.BaseDirectory, a);
            return string.Equals(Path.GetFullPath(fullA), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    internal List<string> PromptUsers(string path)
    {
        var users = Cfg.Actions.Where(a => SamePath(a.Prompt, path)).Select(a => a.Name).ToList();
        if (SamePath(Cfg.Settings.SystemPrompt, path)) users.Add("(General Settings)");
        return users;
    }

    internal void OpenInEditor(string path)
    {
        var exe = string.IsNullOrEmpty(Cfg.Settings.PromptEditor) ? "notepad.exe" : Cfg.Settings.PromptEditor;
        try
        {
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exe, Arguments = $"\"{path}\"", UseShellExecute = true
            });
            p?.WaitForExit();
        }
        catch (Exception ex)
        {
            Renderer.SetNotice($"Cannot open editor: {ex.Message}", NoticeKind.Error);
        }
    }

    internal static readonly IReadOnlyList<(string Code, string Name)> TranslateLanguages =
    [
        ("zh-CN", "简体中文"),
        ("en",    "English"),
        ("ja",    "日本語"),
        ("ko",    "한국어"),
        ("fr",    "Français"),
        ("de",    "Deutsch"),
        ("es",    "Español"),
        ("pt",    "Português"),
        ("ru",    "Русский"),
        ("ar",    "العربية"),
    ];
}

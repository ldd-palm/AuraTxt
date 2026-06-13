using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Tui.Pages;

public class PromptLibraryPage : PageBase
{
    public override string Title => "Prompt Library";

    public override Task<PageResult> RunAsync(TuiApp app, CancellationToken ct)
    {
        PromptService.EnsureScaffold();
        while (true)
        {
            var prompts = PromptService.ListPrompts();
            var items   = BuildItems(prompts, app);
            var (cursor, sel) = BuildCursorState(items);
            app.Renderer.DrawFrame(app.GetBreadcrumb(), items, cursor,
                "↑↓ Navigate  │  [Enter] Open in editor  │  [A] Add  │  [D] Delete  │  [Esc] Back");

            var key = app.Renderer.ReadMenuKey();
            switch (key)
            {
                case MenuKey.Arrow a:
                    if (a.Up) MoveUp(sel.Count); else MoveDown(sel.Count);
                    break;
                case MenuKey.Confirm:
                    var r = Activate(items[cursor].Key, prompts, app);
                    if (r != null) return Task.FromResult(r);
                    break;
                case MenuKey.Number n:
                    JumpTo(sel, items, n.N.ToString());
                    var r2 = Activate(n.N.ToString(), prompts, app);
                    if (r2 != null) return Task.FromResult(r2);
                    break;
                case MenuKey.Letter l when l.C == 'D':
                    if (int.TryParse(items[cursor].Key, out var di) && di >= 1 && di <= prompts.Count)
                        DeletePrompt(prompts[di - 1], app);
                    else
                        app.Renderer.SetNotice("No prompt selected.", NoticeKind.Warning);
                    break;
                case MenuKey.Letter l:
                    JumpTo(sel, items, l.C.ToString());
                    var r3 = Activate(l.C.ToString(), prompts, app);
                    if (r3 != null) return Task.FromResult(r3);
                    break;
                case MenuKey.Escape:
                    return Task.FromResult(PageResult.Back());
                case MenuKey.Quit:
                    return Task.FromResult(PageResult.Exit());
            }
        }
    }

    private IReadOnlyList<MenuItem> BuildItems(List<string> prompts, TuiApp app)
    {
        var list = new List<MenuItem>();
        for (int i = 0; i < prompts.Count; i++)
        {
            var usedBy = app.PromptUsers(prompts[i]);
            var tag    = usedBy.Count > 0 ? string.Join(", ", usedBy) : "(unused)";
            list.Add(new MenuItem((i + 1).ToString(), Path.GetFileName(prompts[i]), tag,
                usedBy.Count > 0 ? ItemValueStyle.Success : ItemValueStyle.Muted));
        }
        return list;
    }

    private PageResult? Activate(string key, List<string> prompts, TuiApp app)
    {
        if (int.TryParse(key, out var idx) && idx >= 1 && idx <= prompts.Count)
        { app.OpenInEditor(prompts[idx - 1]); return null; }

        switch (key)
        {
            case "A": AddPrompt(app); break;
        }
        return null;
    }

    private static void AddPrompt(TuiApp app)
    {
        var name = app.Renderer.AskOrCancel("Prompt name (no spaces, e.g. summarize)");
        if (name is null) return;
        if (string.IsNullOrWhiteSpace(name)) return;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains(' '))
        { app.Renderer.SetNotice("Name contains invalid characters or spaces.", NoticeKind.Error); return; }
        if (PromptService.Exists(name))
        { app.Renderer.SetNotice($"Prompt '{name}' already exists.", NoticeKind.Error); return; }

        if (!app.Renderer.Confirm($"Create prompts/{name}.md from template?")) return;
        try
        {
            var path = PromptService.CreateFromTemplate(name);
            app.Renderer.SetNotice($"Created {name}.md — opening editor...");
            app.OpenInEditor(path);
        }
        catch (Exception ex) { app.Renderer.SetNotice(ex.Message, NoticeKind.Error); }
    }

    private static void DeletePrompt(string promptPath, TuiApp app)
    {
        var choice = Path.GetFileName(promptPath);
        if (!app.Renderer.Confirm($"Delete '{choice}'?", defaultYes: false)) return;
        var usedBy = app.PromptUsers(promptPath);
        if (usedBy.Count > 0)
        { app.Renderer.SetNotice($"'{choice}' is in use by: {string.Join(", ", usedBy)}", NoticeKind.Error); return; }
        try { File.Delete(promptPath); app.Renderer.SetNotice($"Deleted {choice}."); }
        catch (Exception ex) { app.Renderer.SetNotice(ex.Message, NoticeKind.Error); }
    }
}

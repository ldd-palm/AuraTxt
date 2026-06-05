namespace AuraTxt.Cli.Tui.Pages;

public class ProviderDetailPage(string providerId) : PageBase
{
    public override string Title => $"Provider: {providerId}";

    public override Task<PageResult> RunAsync(TuiApp app, CancellationToken ct)
    {
        while (true)
        {
            if (!app.Cfg.Models.TryGetValue(providerId, out var p))
                return Task.FromResult(PageResult.Back());

            var items = BuildItems(p);
            var (cursor, sel) = BuildCursorState(items);
            app.Renderer.DrawFrame(app.GetBreadcrumb(), items, cursor,
                "↑↓ Navigate  │  [Enter] Select  │  [A] Add Model  │  [D] Delete Model  │  [Esc] Back");

            var key = app.Renderer.ReadMenuKey();
            switch (key)
            {
                case MenuKey.Arrow a:
                    if (a.Up) MoveUp(sel.Count); else MoveDown(sel.Count);
                    break;

                case MenuKey.Confirm:
                    var r = Activate(items[cursor].Key, p, app);
                    if (r != null) return Task.FromResult(r);
                    break;

                case MenuKey.Number n:
                    JumpTo(sel, items, n.N.ToString());
                    var r2 = Activate(n.N.ToString(), p, app);
                    if (r2 != null) return Task.FromResult(r2);
                    break;

                case MenuKey.Letter l:
                    JumpTo(sel, items, l.C.ToString());
                    var r3 = Activate(l.C.ToString(), p, app);
                    if (r3 != null) return Task.FromResult(r3);
                    break;

                case MenuKey.Escape:
                    return Task.FromResult(PageResult.Back());
                case MenuKey.Quit:
                    return Task.FromResult(PageResult.Exit());
            }
        }
    }

    private static IReadOnlyList<MenuItem> BuildItems(AuraTxt.Core.Models.ProviderConfig p)
    {
        var list = new List<MenuItem>
        {
            new MenuItem("1", "Base URL", p.BaseUrl),
            new MenuItem("2", "API Key",  TuiRenderer.MaskKey(p.ApiKey)),
        };
        for (int i = 0; i < p.Models.Count; i++)
        {
            var m     = p.Models[i];
            var value = $"{m.Alias}  thinking:{(m.DisableThinking ? "off" : "on")}  {TuiRenderer.StatusBadge(m.Enabled)}";
            list.Add(new MenuItem((i + 3).ToString(), m.TargetModel, value,
                TuiRenderer.StatusStyle(m.Enabled)));
        }
        return list;
    }

    private PageResult? Activate(string key, AuraTxt.Core.Models.ProviderConfig p, TuiApp app)
    {
        if (int.TryParse(key, out var n))
        {
            if (n == 1)
            {
                var v = app.Renderer.Ask("New Base URL", p.BaseUrl);
                if (!string.IsNullOrWhiteSpace(v)) { p.BaseUrl = v; app.MarkDirty(); app.Renderer.SetNotice("Base URL updated."); }
                return null;
            }
            if (n == 2)
            {
                var v = app.Renderer.AskSecret("New API Key");
                if (!string.IsNullOrWhiteSpace(v)) { p.ApiKey = v; app.MarkDirty(); app.Renderer.SetNotice("API Key updated."); }
                return null;
            }
            var mi = n - 3;
            if (mi >= 0 && mi < p.Models.Count)
                return PageResult.Push(new ModelDetailPage(providerId, mi));
            return null;
        }
        switch (key)
        {
            case "A":
                var tm = app.Renderer.Ask("Model full name (e.g. gpt-4o)");
                if (string.IsNullOrWhiteSpace(tm)) break;
                var al = app.Renderer.Ask("Alias", tm);
                if (string.IsNullOrWhiteSpace(al)) al = tm;
                p.Models.Add(new AuraTxt.Core.Models.ModelEntry { TargetModel = tm, Alias = al, Enabled = true });
                app.MarkDirty();
                app.Renderer.SetNotice($"Model '{tm}' added.");
                break;
            case "D":
                DeleteModel(p, app);
                break;
        }
        return null;
    }

    private void DeleteModel(AuraTxt.Core.Models.ProviderConfig p, TuiApp app)
    {
        if (p.Models.Count == 0) { app.Renderer.SetNotice("No models to delete.", NoticeKind.Warning); return; }
        var labels = p.Models.Select(m => m.TargetModel).Append("Cancel").ToList();
        var choice = app.Renderer.SelectFromList("Delete which model?", labels);
        if (choice == "Cancel") return;

        var modelRef = $"{providerId}/{choice}";
        var bound    = app.Cfg.Actions.Where(a => a.ModelId == modelRef).ToList();
        if (bound.Any())
        { app.Renderer.SetNotice($"'{choice}' is used by {bound.Count} action(s). Update those first.", NoticeKind.Error); return; }

        p.Models.RemoveAll(m => m.TargetModel == choice);
        app.MarkDirty();
        app.Renderer.SetNotice($"Model '{choice}' removed.");
    }
}

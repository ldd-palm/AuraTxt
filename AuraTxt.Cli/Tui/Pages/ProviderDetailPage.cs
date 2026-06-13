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

                case MenuKey.Letter l when l.C == 'D':
                {
                    var ck = items[cursor].Key;
                    if (int.TryParse(ck, out var dn) && dn >= 3 && dn - 3 < p.Models.Count)
                        DeleteModel(p, p.Models[dn - 3], app);
                    else
                        app.Renderer.SetNotice("Navigate to a model entry to delete it.", NoticeKind.Warning);
                    break;
                }
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
            new MenuItem("1", "Base URL", $"{p.BaseUrl}  [{p.AdapterType}]"),
            new MenuItem("2", "API Key",  TuiRenderer.MaskKey(p.ApiKey)),
        };
        for (int i = 0; i < p.Models.Count; i++)
        {
            var m     = p.Models[i];
            var profile = string.IsNullOrEmpty(m.ProfileId) ? "(auto)" : m.ProfileId;
            var value = $"{m.Alias}  profile:{profile}  {TuiRenderer.StatusBadge(m.Enabled)}";
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
                if (!string.IsNullOrWhiteSpace(v))
                {
                    p.BaseUrl = v;
                    app.MarkDirty();
                    app.Renderer.SetNotice($"Base URL updated.");
                }
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
                var tm = app.Renderer.AskOrCancel("Model full name (e.g. gpt-4o)");
                if (string.IsNullOrWhiteSpace(tm)) break;
                var al = app.Renderer.AskOrCancel("Alias", tm);
                if (al is null) break;
                if (string.IsNullOrWhiteSpace(al)) al = tm;
                p.Models.Add(new AuraTxt.Core.Models.ModelEntry { TargetModel = tm, Alias = al, Enabled = true });
                app.MarkDirty();
                app.Renderer.SetNotice($"Model '{tm}' added.");
                break;
        }
        return null;
    }

    private void DeleteModel(AuraTxt.Core.Models.ProviderConfig p, AuraTxt.Core.Models.ModelEntry model, TuiApp app)
    {
        if (!app.Renderer.Confirm($"Delete model '{model.TargetModel}'?", defaultYes: false)) return;
        var modelRef = $"{providerId}/{model.TargetModel}";
        var bound    = app.Cfg.Actions.Where(a => a.ModelId == modelRef).ToList();
        if (bound.Any())
        { app.Renderer.SetNotice($"'{model.TargetModel}' is used by {bound.Count} action(s). Update those first.", NoticeKind.Error); return; }
        p.Models.Remove(model);
        app.MarkDirty();
        app.Renderer.SetNotice($"Model '{model.TargetModel}' removed.");
    }
}

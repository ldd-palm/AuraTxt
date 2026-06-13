namespace AuraTxt.Cli.Tui.Pages;

public class ModelDetailPage(string providerId, int modelIndex) : PageBase
{
    public override string Title => "Model Detail";

    public override Task<PageResult> RunAsync(TuiApp app, CancellationToken ct)
    {
        while (true)
        {
            if (!app.Cfg.Models.TryGetValue(providerId, out var p) || modelIndex >= p.Models.Count)
                return Task.FromResult(PageResult.Back());

            var m     = p.Models[modelIndex];
            var items = BuildItems(m);
            var (cursor, sel) = BuildCursorState(items);
            app.Renderer.DrawFrame(app.GetBreadcrumb(), items, cursor, FooterWith());

            var key = app.Renderer.ReadMenuKey();
            switch (key)
            {
                case MenuKey.Arrow a:
                    if (a.Up) MoveUp(sel.Count); else MoveDown(sel.Count);
                    break;
                case MenuKey.Confirm:
                    if (HandleKey(items[cursor].Key, m, app)) return Task.FromResult(PageResult.Back());
                    break;
                case MenuKey.Number n:
                    JumpTo(sel, items, n.N.ToString());
                    if (HandleKey(n.N.ToString(), m, app)) return Task.FromResult(PageResult.Back());
                    break;
                case MenuKey.Letter l:
                    JumpTo(sel, items, l.C.ToString());
                    if (HandleKey(l.C.ToString(), m, app)) return Task.FromResult(PageResult.Back());
                    break;
                case MenuKey.Escape:
                    return Task.FromResult(PageResult.Back());
                case MenuKey.Quit:
                    return Task.FromResult(PageResult.Exit());
            }
        }
    }

    private static IReadOnlyList<MenuItem> BuildItems(AuraTxt.Core.Models.ModelEntry m) =>
    [
        new MenuItem("1", "Full Name", m.TargetModel),
        new MenuItem("2", "Alias",     m.Alias),
        new MenuItem("3", "Profile",   string.IsNullOrEmpty(m.ProfileId) ? "(auto)" : m.ProfileId,
                     string.IsNullOrEmpty(m.ProfileId) ? ItemValueStyle.Muted : ItemValueStyle.Success),
        new MenuItem("4", "Status",    TuiRenderer.StatusBadge(m.Enabled),
                     TuiRenderer.StatusStyle(m.Enabled)),
    ];

    private bool HandleKey(string key, AuraTxt.Core.Models.ModelEntry m, TuiApp app)
    {
        switch (key)
        {
            case "1":
                var nm = app.Renderer.Ask("New full name", m.TargetModel);
                if (!string.IsNullOrWhiteSpace(nm)) { m.TargetModel = nm; app.MarkDirty(); app.Renderer.SetNotice($"Name → {nm}"); }
                break;
            case "2":
                var na = app.Renderer.Ask("New alias", m.Alias);
                if (!string.IsNullOrWhiteSpace(na)) { m.Alias = na; app.MarkDirty(); app.Renderer.SetNotice($"Alias → {na}"); }
                break;
            case "3":
                var pid = app.Renderer.Ask("Profile ID (empty for auto)", m.ProfileId);
                m.ProfileId = pid?.Trim() ?? "";
                app.MarkDirty();
                app.Renderer.SetNotice($"Profile → {(string.IsNullOrEmpty(m.ProfileId) ? "(auto)" : m.ProfileId)}");
                break;
            case "4":
                if (m.Enabled)
                {
                    var modelRef = $"{providerId}/{m.TargetModel}";
                    var bound    = app.Cfg.Actions.Where(a => a.ModelId == modelRef).ToList();
                    if (bound.Any())
                    { app.Renderer.SetNotice($"'{m.TargetModel}' used by {bound.Count} action(s). Update those first.", NoticeKind.Error); break; }
                }
                m.Enabled = !m.Enabled; app.MarkDirty();
                app.Renderer.SetNotice($"Status → {TuiRenderer.StatusBadge(m.Enabled)}",
                    m.Enabled ? NoticeKind.Success : NoticeKind.Warning);
                break;
        }
        return false;
    }
}

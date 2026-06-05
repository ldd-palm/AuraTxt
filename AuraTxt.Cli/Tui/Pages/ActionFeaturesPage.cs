using AuraTxt.Cli.Tui.Flows;
using AuraTxt.Core.Models;

namespace AuraTxt.Cli.Tui.Pages;

public class ActionFeaturesPage : PageBase
{
    public override string Title => "Action Features";

    public override Task<PageResult> RunAsync(TuiApp app, CancellationToken ct)
    {
        while (true)
        {
            var sorted = app.Cfg.Actions
                .OrderBy(a => a.Enabled ? 0 : 1)
                .ThenBy(a => a.Order)
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var items = BuildItems(sorted, app);
            var (cursor, sel) = BuildCursorState(items);
            app.Renderer.DrawFrame(app.GetBreadcrumb(), items, cursor,
                "↑↓ Navigate  │  [Enter] Edit  │  [A] Add  │  [D] Delete  │  [S] Save  │  [Esc] Back");

            var key = app.Renderer.ReadMenuKey();
            switch (key)
            {
                case MenuKey.Arrow a:
                    if (a.Up) MoveUp(sel.Count); else MoveDown(sel.Count);
                    break;
                case MenuKey.Confirm:
                    var r = Activate(items[cursor].Key, sorted, app);
                    if (r != null) return Task.FromResult(r);
                    break;
                case MenuKey.Number n:
                    JumpTo(sel, items, n.N.ToString());
                    var r2 = Activate(n.N.ToString(), sorted, app);
                    if (r2 != null) return Task.FromResult(r2);
                    break;
                case MenuKey.Letter l:
                    JumpTo(sel, items, l.C.ToString());
                    var r3 = Activate(l.C.ToString(), sorted, app);
                    if (r3 != null) return Task.FromResult(r3);
                    break;
                case MenuKey.Escape:
                    return Task.FromResult(PageResult.Back());
                case MenuKey.Quit:
                    return Task.FromResult(PageResult.Exit());
            }
        }
    }

    private IReadOnlyList<MenuItem> BuildItems(List<ActionItem> sorted, TuiApp app)
    {
        var list = new List<MenuItem>();
        for (int i = 0; i < sorted.Count; i++)
        {
            var a     = sorted[i];
            var hk    = string.IsNullOrEmpty(a.Hotkey) ? "—" : a.Hotkey;
            var model = string.IsNullOrEmpty(a.ModelId) ? "—" : app.ModelLabel(a.ModelId);
            var val   = $"{TuiRenderer.StatusBadge(a.Enabled)}  {model}  {hk}";
            list.Add(new MenuItem((i + 1).ToString(), a.Name, val, TuiRenderer.StatusStyle(a.Enabled)));
        }
        return list;
    }

    private PageResult? Activate(string key, List<ActionItem> sorted, TuiApp app)
    {
        if (int.TryParse(key, out var idx) && idx >= 1 && idx <= sorted.Count)
            return PageResult.Push(new ActionDetailPage(sorted[idx - 1].Id));

        switch (key)
        {
            case "A": AddActionFlow.Run(app);         break;
            case "D": DeleteAction(sorted, app);      break;
            case "S": app.SaveNow();                  break;
        }
        return null;
    }

    private static void DeleteAction(List<ActionItem> sorted, TuiApp app)
    {
        var deletable = sorted.Where(a => !a.IsSystem).ToList();
        if (deletable.Count == 0) { app.Renderer.SetNotice("No user actions to delete.", NoticeKind.Warning); return; }
        var labels = deletable.Select(a => $"{a.Name} ({a.Id})").Append("Cancel").ToList();
        var choice = app.Renderer.SelectFromList("Delete which action?", labels);
        if (choice == "Cancel") return;
        var action = deletable.First(a => $"{a.Name} ({a.Id})" == choice);
        app.Cfg.Actions.Remove(action);
        app.MarkDirty();
        app.Renderer.SetNotice($"Action '{action.Name}' deleted.");
    }
}

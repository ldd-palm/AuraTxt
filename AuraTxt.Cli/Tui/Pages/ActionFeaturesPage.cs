using AuraTxt.Cli.Tui.Flows;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Tui.Pages;

public class ActionFeaturesPage : PageBase
{
    public override string Title => "Action Features";

    public override Task<PageResult> RunAsync(TuiApp app, CancellationToken ct)
    {
        while (true)
        {
            var sorted = BuildSorted(app);

            var items = BuildItems(sorted, app);
            var (cursor, sel) = BuildCursorState(items);
            app.Renderer.DrawFrame(app.GetBreadcrumb(), items, cursor,
                "↑↓ Navigate  │  [Enter] Edit  │  [U/I] Move Up/Down  │  [A] Add  │  [D] Delete  │  [S] Save  │  [Esc] Back");

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
                case MenuKey.Letter l when l.C == 'D':
                    if (int.TryParse(items[cursor].Key, out var di) && di >= 1 && di <= sorted.Count)
                        DeleteAction(sorted[di - 1], app);
                    else
                        app.Renderer.SetNotice("Navigate to an action first.", NoticeKind.Warning);
                    break;
                case MenuKey.Letter l when l.C == 'U':
                    if (int.TryParse(items[cursor].Key, out var mui) && mui >= 1 && mui <= sorted.Count)
                        MoveAction(sorted[mui - 1], up: true, app);
                    else
                        app.Renderer.SetNotice("Navigate to an action first.", NoticeKind.Warning);
                    break;
                case MenuKey.Letter l when l.C == 'I':
                    if (int.TryParse(items[cursor].Key, out var mdi) && mdi >= 1 && mdi <= sorted.Count)
                        MoveAction(sorted[mdi - 1], up: false, app);
                    else
                        app.Renderer.SetNotice("Navigate to an action first.", NoticeKind.Warning);
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

    private static List<ActionItem> BuildSorted(TuiApp app) =>
        app.Cfg.Actions
            .OrderBy(a => a.Enabled ? 0 : 1)
            .ThenBy(a => a.Order)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private const int BadgeCol  = 14;
    private const int PromptCol = 20;
    private const int ModelCol  = 26;

    private IReadOnlyList<MenuItem> BuildItems(List<ActionItem> sorted, TuiApp app)
    {
        var list = new List<MenuItem>();
        for (int i = 0; i < sorted.Count; i++)
        {
            var a      = sorted[i];
            var hk     = a.Id == "copy" ? "Ctrl+C"
                       : string.IsNullOrEmpty(a.Hotkey) ? "—"
                       : a.Hotkey;
            var model  = a.IsSystem ? "(system)"
                       : string.IsNullOrEmpty(a.ModelId) ? "—"
                       : app.ModelLabel(a.ModelId);
            var badge  = TuiRenderer.StatusBadge(a.Enabled).PadRight(BadgeCol);
            var prompt = TuiRenderer.Truncate(PromptFileName(a), PromptCol - 2).PadRight(PromptCol);
            var modelC = TuiRenderer.Truncate(model, ModelCol - 2).PadRight(ModelCol);
            var val    = $"{badge}{prompt}{modelC}{hk}";
            list.Add(new MenuItem((i + 1).ToString(), a.Name, val, TuiRenderer.StatusStyle(a.Enabled)));
        }
        return list;
    }

    // IsFileRef alone would misclassify inline templates that merely contain '/'
    // (e.g. Terminal's "cmd.exe /C {SelectedText}", see SPEC.md §8.1) — confirm the
    // path actually resolves to a file, same pattern TuiApp.SamePath uses.
    private static string PromptFileName(ActionItem a)
    {
        if (string.IsNullOrEmpty(a.Prompt) || !PromptService.IsFileRef(a.Prompt)) return "-";
        var full = PromptService.ResolveFullPath(a.Prompt);
        return File.Exists(full) ? Path.GetFileName(a.Prompt) : "-";
    }

    private PageResult? Activate(string key, List<ActionItem> sorted, TuiApp app)
    {
        if (int.TryParse(key, out var idx) && idx >= 1 && idx <= sorted.Count)
            return PageResult.Push(new ActionDetailPage(sorted[idx - 1].Id));

        switch (key)
        {
            case "A": AddActionFlow.Run(app); break;
            case "S": app.SaveNow();          break;
        }
        return null;
    }

    private static void DeleteAction(ActionItem action, TuiApp app)
    {
        if (action.IsSystem) { app.Renderer.SetNotice("Cannot delete system actions.", NoticeKind.Warning); return; }
        if (!app.Renderer.Confirm($"Delete action '{action.Name}'?", defaultYes: false)) return;
        app.Cfg.Actions.Remove(action);
        app.MarkDirty();
        app.Renderer.SetNotice($"Action '{action.Name}' deleted.");
    }

    /// Reorders within the Enabled/disabled group only — Order only matters for
    /// enabled actions (it drives the real popup menu, see SPEC §7.1), so crossing
    /// that boundary would silently change Order without moving anything on screen.
    private void MoveAction(ActionItem action, bool up, TuiApp app)
    {
        var group = app.Cfg.Actions
            .Where(a => a.Enabled == action.Enabled)
            .OrderBy(a => a.Order)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var idx     = group.IndexOf(action);
        var swapIdx = up ? idx - 1 : idx + 1;
        if (swapIdx < 0 || swapIdx >= group.Count)
        {
            app.Renderer.SetNotice($"'{action.Name}' is already at the {(up ? "top" : "bottom")}.", NoticeKind.Warning);
            return;
        }

        (group[idx], group[swapIdx]) = (group[swapIdx], group[idx]);
        for (int i = 0; i < group.Count; i++) group[i].Order = i;

        app.MarkDirty();
        app.Renderer.SetNotice($"Moved '{action.Name}' {(up ? "up" : "down")}.");
        SetCursorPos(BuildSorted(app).IndexOf(action));
    }
}

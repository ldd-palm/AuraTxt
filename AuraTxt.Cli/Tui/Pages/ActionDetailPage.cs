using AuraTxt.Cli.Commands;
using AuraTxt.Cli.Tui.Flows;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Tui.Pages;

public class ActionDetailPage(string actionId) : PageBase
{
    public override string Title => $"Action: {actionId}";

    public override Task<PageResult> RunAsync(TuiApp app, CancellationToken ct)
    {
        while (true)
        {
            var action = app.Cfg.Actions.FirstOrDefault(a => a.Id == actionId);
            if (action is null) return Task.FromResult(PageResult.Back());

            var items = BuildItems(action, app);
            var (cursor, sel) = BuildCursorState(items);
            app.Renderer.DrawFrame(app.GetBreadcrumb(), items, cursor, FooterWith());

            var key = app.Renderer.ReadMenuKey();
            switch (key)
            {
                case MenuKey.Arrow a:
                    if (a.Up) MoveUp(sel.Count); else MoveDown(sel.Count);
                    break;
                case MenuKey.Confirm:
                    if (HandleKey(items[cursor].Key, action, app)) return Task.FromResult(PageResult.Back());
                    break;
                case MenuKey.Number n:
                    JumpTo(sel, items, n.N.ToString());
                    if (HandleKey(n.N.ToString(), action, app)) return Task.FromResult(PageResult.Back());
                    break;
                case MenuKey.Letter l:
                    JumpTo(sel, items, l.C.ToString());
                    if (HandleKey(l.C.ToString(), action, app)) return Task.FromResult(PageResult.Back());
                    break;
                case MenuKey.Escape:
                    return Task.FromResult(PageResult.Back());
                case MenuKey.Quit:
                    return Task.FromResult(PageResult.Exit());
            }
        }
    }

    private static IReadOnlyList<MenuItem> BuildItems(ActionItem a, TuiApp app)
    {
        var hk        = string.IsNullOrEmpty(a.Hotkey) ? "(none)" : a.Hotkey;
        var isBuiltin = a.ModelId.StartsWith("default/");
        var list = new List<MenuItem>
        {
            new MenuItem("1", "Name",   a.Name),
            new MenuItem("2", "Icon",   a.Icon),
        };
        if (!a.IsSystem)
        {
            list.Add(new MenuItem("3", "Model",       app.ModelLabel(a.ModelId)));
            list.Add(new MenuItem("4", "Interactive",  isBuiltin ? "(n/a — built-in)" : a.IsInteractive.ToString()));
            list.Add(new MenuItem("5", "Prompt",       isBuiltin ? "(n/a — built-in)" : TuiRenderer.PromptLabel(a.Prompt)));
            list.Add(new MenuItem("6", "Hotkey",       hk));
            list.Add(new MenuItem("7", "Status",       TuiRenderer.StatusBadge(a.Enabled), TuiRenderer.StatusStyle(a.Enabled)));
            list.Add(new MenuItem("8", "Order",        a.Order.ToString()));
        }
        else
        {
            list.Add(new MenuItem("3", "Hotkey",       hk));
            list.Add(new MenuItem("4", "Status",       TuiRenderer.StatusBadge(a.Enabled), TuiRenderer.StatusStyle(a.Enabled)));
            list.Add(new MenuItem("5", "Order",        a.Order.ToString()));
        }
        return list;
    }

    private bool HandleKey(string key, ActionItem a, TuiApp app)
    {
        var isBuiltin = a.ModelId.StartsWith("default/");
        switch (key)
        {
            case "1":
                var nm = app.Renderer.Ask("New name", a.Name);
                if (!string.IsNullOrWhiteSpace(nm)) { a.Name = nm; app.MarkDirty(); app.Renderer.SetNotice($"Name → {nm}"); }
                break;
            case "2":
                var ic = app.Renderer.Ask("New icon (lucide.dev)", a.Icon);
                if (!string.IsNullOrWhiteSpace(ic))
                {
                    a.Icon = ic; app.MarkDirty();
                    IconDownloadService.EnsureDownloadedAsync(ic).GetAwaiter().GetResult();
                    app.Renderer.SetNotice($"Icon → {ic}");
                }
                break;
            case "3":
                if (!a.IsSystem)
                {
                    var mid = SelectModelFlow.Run(app);
                    if (mid != null)
                    {
                        a.ModelId = mid; app.MarkDirty();
                        if (mid.StartsWith("default/")) { a.IsInteractive = false; a.Prompt = ""; }
                        app.Renderer.SetNotice($"Model → {app.ModelLabel(mid)}");
                    }
                }
                else // system: hotkey is slot 3
                {
                    EditHotkey(a, app);
                }
                break;
            case "4":
                if (!a.IsSystem)
                {
                    if (isBuiltin) { app.Renderer.SetNotice("Built-in service: interactive not applicable.", NoticeKind.Info); break; }
                    a.IsInteractive = !a.IsInteractive; app.MarkDirty();
                    app.Renderer.SetNotice($"Interactive → {a.IsInteractive}");
                }
                else // system: status is slot 4
                {
                    a.Enabled = !a.Enabled; app.MarkDirty();
                    app.Renderer.SetNotice($"Status → {TuiRenderer.StatusBadge(a.Enabled)}");
                }
                break;
            case "5":
                if (!a.IsSystem)
                {
                    if (isBuiltin) { app.Renderer.SetNotice("Built-in service: no prompt needed.", NoticeKind.Info); break; }
                    var pf = SelectPromptFileFlow.Run(app);
                    if (pf != null) { a.Prompt = pf; app.MarkDirty(); app.Renderer.SetNotice("Prompt updated."); }
                }
                else // system: order is slot 5
                {
                    EditOrder(a, app);
                }
                break;
            case "6":
                if (!a.IsSystem) { EditHotkey(a, app); }
                break;
            case "7":
                if (!a.IsSystem)
                { a.Enabled = !a.Enabled; app.MarkDirty(); app.Renderer.SetNotice($"Status → {TuiRenderer.StatusBadge(a.Enabled)}"); }
                break;
            case "8":
                if (!a.IsSystem) { EditOrder(a, app); }
                break;
        }
        return false;
    }

    private static void EditHotkey(ActionItem a, TuiApp app)
    {
        if (a.Id == "copy") { app.Renderer.SetNotice("Copy action hotkey is fixed (empty).", NoticeKind.Info); return; }
        var hk = HotkeyCapture.Capture(app.Cfg.Actions, excludeId: a.Id);
        a.Hotkey = hk; app.MarkDirty();
        if (!string.IsNullOrEmpty(hk)) app.Renderer.SetNotice($"Hotkey → {hk}");
    }

    private static void EditOrder(ActionItem a, TuiApp app)
    {
        var v = app.Renderer.Ask("Order (0-99)", a.Order.ToString());
        if (int.TryParse(v, out var ov)) { a.Order = ov; app.MarkDirty(); app.Renderer.SetNotice($"Order → {ov}"); }
    }
}

using AuraTxt.Cli.Commands;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Tui.Flows;

public static class AddActionFlow
{
    public static void Run(TuiApp app)
    {
        var id = app.Renderer.AskOrCancel("Action ID (no spaces, e.g. translate)");
        if (id is null) return;
        if (string.IsNullOrWhiteSpace(id)) return;
        if (id.Contains(' '))                          { app.Renderer.SetNotice("Action ID cannot contain spaces.", NoticeKind.Error); return; }
        if (app.Cfg.Actions.Any(a => a.Id == id))     { app.Renderer.SetNotice($"Action '{id}' already exists.", NoticeKind.Error); return; }

        var icon = app.Renderer.AskOrCancel("Icon name from lucide.dev (e.g. languages)");
        if (icon is null) return;
        if (!string.IsNullOrWhiteSpace(icon))
        {
            var ok = IconDownloadService.EnsureDownloadedAsync(icon).GetAwaiter().GetResult();
            if (!ok) app.Renderer.SetNotice($"Icon '{icon}' not found on lucide.dev — will use text fallback.", NoticeKind.Warning);
        }

        var modelId = SelectModelFlow.Run(app);
        if (modelId is null) return;

        var isBuiltin     = modelId.StartsWith("default/");
        var isTerminal    = modelId.Equals("default/Terminal", StringComparison.OrdinalIgnoreCase);
        var isInteractive = false;
        var prompt        = "";

        if (!isBuiltin)
        {
            isInteractive = app.Renderer.Confirm("Interactive action?", defaultYes: false);
            var pf = SelectPromptFileFlow.Run(app);
            if (pf != null) prompt = pf;
        }
        else if (isTerminal)
        {
            var cmd = app.Renderer.AskOrCancel("Command template (e.g. ping {SelectedText})");
            if (cmd != null) prompt = cmd;
        }

        var hotkey  = HotkeyCapture.Capture(app.Cfg.Actions);
        if (hotkey is null) return;
        var enabled = app.Renderer.Confirm("Enable this action?");

        var orderStr = app.Renderer.AskOrCancel("Display order (0-99, default 0)", "0");
        if (orderStr is null) return;
        int.TryParse(orderStr, out var order);

        app.Cfg.Actions.Add(new ActionItem
        {
            Id            = id,
            Name          = id,
            Icon          = icon,
            ModelId       = modelId,
            IsInteractive = isInteractive,
            Prompt        = prompt,
            Hotkey        = hotkey,
            Enabled       = enabled,
            Order         = order
        });
        app.MarkDirty();
        app.Renderer.SetNotice($"Action '{id}' added.");
    }
}

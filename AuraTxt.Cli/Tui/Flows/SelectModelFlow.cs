namespace AuraTxt.Cli.Tui.Flows;

public static class SelectModelFlow
{
    /// Returns modelRef (e.g. "openai/gpt-4o") or null if cancelled.
    public static string? Run(TuiApp app)
    {
        var all = app.Cfg.AllEnabledModelRefs().ToList();
        if (all.Count == 0)
        { app.Renderer.SetNotice("No models available. Add a provider first.", NoticeKind.Warning); return null; }

        var labels = all.Select(x => x.Label).Append("Cancel").ToList();
        var choice = app.Renderer.SelectFromList("Select model", labels);
        if (choice == "Cancel") return null;

        return all.FirstOrDefault(x => x.Label == choice).Ref;
    }
}

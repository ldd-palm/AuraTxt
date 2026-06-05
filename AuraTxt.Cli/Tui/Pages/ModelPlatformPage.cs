using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Tui.Pages;

public class ModelPlatformPage : PageBase
{
    public override string Title => "Model Platform";

    public override Task<PageResult> RunAsync(TuiApp app, CancellationToken ct)
    {
        while (true)
        {
            var providers = app.Cfg.Models
                .Where(kv => kv.Key != "default")
                .OrderBy(kv => kv.Key)
                .ToList();

            var items = BuildItems(providers, app);
            var (cursor, sel) = BuildCursorState(items);
            app.Renderer.DrawFrame(app.GetBreadcrumb(), items, cursor,
                "↑↓ Navigate  │  [Enter] Select  │  [A] Add  │  [D] Delete  │  [S] Save  │  [Esc] Back");

            var key = app.Renderer.ReadMenuKey();
            switch (key)
            {
                case MenuKey.Arrow a:
                    if (a.Up) MoveUp(sel.Count); else MoveDown(sel.Count);
                    break;

                case MenuKey.Confirm:
                    var r = Activate(items[cursor].Key, providers, app);
                    if (r != null) return Task.FromResult(r);
                    break;

                case MenuKey.Number n:
                    JumpTo(sel, items, n.N.ToString());
                    var r2 = Activate(n.N.ToString(), providers, app);
                    if (r2 != null) return Task.FromResult(r2);
                    break;

                case MenuKey.Letter l:
                    JumpTo(sel, items, l.C.ToString());
                    var r3 = Activate(l.C.ToString(), providers, app);
                    if (r3 != null) return Task.FromResult(r3);
                    break;

                case MenuKey.Escape:
                    return Task.FromResult(PageResult.Back());

                case MenuKey.Quit:
                    return Task.FromResult(PageResult.Exit());
            }
        }
    }

    private static IReadOnlyList<MenuItem> BuildItems(
        List<KeyValuePair<string, AuraTxt.Core.Models.ProviderConfig>> providers, TuiApp app)
    {
        var list = new List<MenuItem>();
        for (int i = 0; i < providers.Count; i++)
        {
            var (_, p) = providers[i];
            var enabled  = p.Models.Where(m => m.Enabled).Select(m => m.Alias).ToList();
            var disabled = p.Models.Where(m => !m.Enabled).Select(m => m.Alias).ToList();
            var modelSummary = string.Join(", ", enabled.Concat(disabled.Select(a => $"({a})")));
            list.Add(new MenuItem((i + 1).ToString(), p.DisplayName, modelSummary,
                enabled.Count > 0 ? ItemValueStyle.Success : ItemValueStyle.Muted));
        }
        list.Add(MenuItem.Sep());
        list.Add(new MenuItem("T", "Test Connection"));
        return list;
    }

    private PageResult? Activate(string key, List<KeyValuePair<string, AuraTxt.Core.Models.ProviderConfig>> providers, TuiApp app)
    {
        if (int.TryParse(key, out var idx) && idx >= 1 && idx <= providers.Count)
            return PageResult.Push(new ProviderDetailPage(providers[idx - 1].Key));

        switch (key)
        {
            case "A": AddProviderFlow(app);             break;
            case "D": DeleteProviderFlow(providers, app); break;
            case "T": TestModelFlow(app);               break;
            case "S": app.SaveNow();                    break;
        }
        return null;
    }

    private static void AddProviderFlow(TuiApp app)
    {
        var id = app.Renderer.Ask("Provider ID (no spaces, e.g. openai)");
        if (string.IsNullOrWhiteSpace(id)) return;
        if (id.Contains(' ')) { app.Renderer.SetNotice("Provider ID cannot contain spaces.", NoticeKind.Error); return; }
        if (app.Cfg.Models.ContainsKey(id)) { app.Renderer.SetNotice($"Provider '{id}' already exists.", NoticeKind.Error); return; }

        var url = app.Renderer.Ask("Base URL (e.g. https://api.openai.com/v1)");
        var secret = app.Renderer.AskSecret("API Key");
        var tm  = app.Renderer.Ask("First model full name (e.g. gpt-4o)");
        var al  = app.Renderer.Ask("Alias/short name", tm);
        if (string.IsNullOrWhiteSpace(al)) al = tm;

        var provider = new AuraTxt.Core.Models.ProviderConfig
        {
            DisplayName = id, BaseUrl = url, ApiKey = secret,
            Models = [new AuraTxt.Core.Models.ModelEntry { TargetModel = tm, Alias = al, Enabled = true }]
        };

        while (app.Renderer.Confirm("Add another model?", defaultYes: false))
        {
            var tm2 = app.Renderer.Ask("Model full name");
            var al2 = app.Renderer.Ask("Alias", tm2);
            if (string.IsNullOrWhiteSpace(al2)) al2 = tm2;
            provider.Models.Add(new AuraTxt.Core.Models.ModelEntry { TargetModel = tm2, Alias = al2, Enabled = true });
        }

        app.Cfg.Models[id] = provider;
        app.MarkDirty();
        app.Renderer.SetNotice($"Provider '{id}' added ({provider.Models.Count} model(s)).");
    }

    private static void DeleteProviderFlow(List<KeyValuePair<string, AuraTxt.Core.Models.ProviderConfig>> providers, TuiApp app)
    {
        if (providers.Count == 0) { app.Renderer.SetNotice("No user providers to delete.", NoticeKind.Warning); return; }
        var labels = providers.Select(p => $"{p.Value.DisplayName} ({p.Key})").Append("Cancel").ToList();
        var choice = app.Renderer.SelectFromList("Delete which provider?", labels);
        if (choice == "Cancel") return;

        var pid = providers.First(p => $"{p.Value.DisplayName} ({p.Key})" == choice).Key;
        var bound = app.Cfg.Actions.Where(a => a.ModelId.StartsWith($"{pid}/")).ToList();
        if (bound.Any())
        {
            app.Renderer.SetNotice($"'{pid}' is used by {bound.Count} action(s). Update those first.", NoticeKind.Error);
            return;
        }
        app.Cfg.Models.Remove(pid);
        app.MarkDirty();
        app.Renderer.SetNotice($"Provider '{pid}' deleted.");
    }

    private static void TestModelFlow(TuiApp app)
    {
        var testable = app.Cfg.Models
            .Where(kv => kv.Key != "default")
            .SelectMany(kv => kv.Value.Models.Select(m => (Ref: $"{kv.Key}/{m.TargetModel}", Provider: kv.Value, Model: m)))
            .OrderByDescending(x => x.Model.Enabled).ThenBy(x => x.Ref)
            .ToList();

        if (testable.Count == 0) { app.Renderer.SetNotice("No user models to test. Add a provider first.", NoticeKind.Warning); return; }

        var labels = testable.Select(x => x.Ref).Append("Cancel").ToList();
        var choice = app.Renderer.SelectFromList("Test which model?", labels);
        if (choice == "Cancel") return;

        var t = testable.First(x => x.Ref == choice);
        Spectre.Console.AnsiConsole.Status()
            .Start($"Testing {t.Ref}...", ctx =>
            {
                ctx.Spinner = Spectre.Console.Spinner.Known.Dots;
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                    var result = new AiClient().CompleteAsync(t.Provider, t.Model, "Hello, respond with OK only.", ct: cts.Token).GetAwaiter().GetResult();
                    sw.Stop();
                    app.Renderer.SetNotice($"✓ {t.Ref}: {result.Trim()} ({sw.Elapsed.TotalSeconds:F1}s)");
                }
                catch (Exception ex)
                {
                    app.Renderer.SetNotice($"✗ {ex.Message}", NoticeKind.Error);
                }
            });
    }
}

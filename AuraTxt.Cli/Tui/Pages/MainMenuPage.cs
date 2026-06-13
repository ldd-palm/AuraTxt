namespace AuraTxt.Cli.Tui.Pages;

public class MainMenuPage : PageBase
{
    public override string Title => "AuraCfg";

    private static readonly IReadOnlyList<MenuItem> Items =
    [
        new MenuItem("1", "General Settings"),
        new MenuItem("2", "Model Platform"),
        new MenuItem("3", "Prompt Library"),
        new MenuItem("4", "Action Features"),
        new MenuItem("5", "Profiles"),
        MenuItem.Sep(),
        new MenuItem("D", "Doctor — Validate Config"),
        new MenuItem("S", "Save Config"),
    ];

    public override Task<PageResult> RunAsync(TuiApp app, CancellationToken ct)
    {
        while (true)
        {
            var (cursorIndex, sel) = BuildCursorState(Items);
            app.Renderer.DrawFrame(app.GetBreadcrumb(), Items, cursorIndex,
                "↑↓ Navigate  │  [Enter] Select  │  [Q] Quit");

            var key = app.Renderer.ReadMenuKey();
            switch (key)
            {
                case MenuKey.Arrow a:
                    if (a.Up) MoveUp(sel.Count); else MoveDown(sel.Count);
                    break;

                case MenuKey.Confirm:
                    var r = Activate(Items[cursorIndex].Key, app);
                    if (r != null) return Task.FromResult(r);
                    break;

                case MenuKey.Number n:
                    JumpTo(sel, Items, n.N.ToString());
                    var r2 = Activate(n.N.ToString(), app);
                    if (r2 != null) return Task.FromResult(r2);
                    break;

                case MenuKey.Letter l:
                    JumpTo(sel, Items, l.C.ToString());
                    var r3 = Activate(l.C.ToString(), app);
                    if (r3 != null) return Task.FromResult(r3);
                    break;

                case MenuKey.Quit:
                case MenuKey.Escape:
                    return Task.FromResult(PageResult.Exit());
            }
        }
    }

    private static PageResult? Activate(string key, TuiApp app)
    {
        switch (key)
        {
            case "1": return PageResult.Push(new GeneralSettingsPage());
            case "2": return PageResult.Push(new ModelPlatformPage());
            case "3": return PageResult.Push(new PromptLibraryPage());
            case "4": return PageResult.Push(new ActionFeaturesPage());
            case "5": return PageResult.Push(new ProfilesPage());
            case "D": app.RunDoctor();  return null;
            case "S": app.SaveNow();    return null;
            case "Q": return PageResult.Exit();
            default:  return null;
        }
    }
}

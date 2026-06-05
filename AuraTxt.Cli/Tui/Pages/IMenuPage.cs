namespace AuraTxt.Cli.Tui.Pages;

public interface IMenuPage
{
    string Title { get; }
    Task<PageResult> RunAsync(TuiApp app, CancellationToken ct);
}

public enum PageResultKind { Back, Exit, Push }

public sealed record PageResult(PageResultKind Kind, IMenuPage? Next = null)
{
    public static PageResult Back()              => new(PageResultKind.Back);
    public static PageResult Exit()              => new(PageResultKind.Exit);
    public static PageResult Push(IMenuPage next) => new(PageResultKind.Push, next);
}

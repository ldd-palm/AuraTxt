using AuraTxt.Cli.Tui.Pages;

namespace AuraTxt.Cli.Tui;

public class NavStack
{
    private readonly Stack<IMenuPage> _stack = new();

    public void      Push(IMenuPage page) => _stack.Push(page);
    public IMenuPage? Pop()               => _stack.TryPop(out var p) ? p : null;
    public IMenuPage? Peek()              => _stack.TryPeek(out var p) ? p : null;
    public bool       IsEmpty             => _stack.Count == 0;
    public string[]   Breadcrumb          => _stack.Reverse().Select(p => p.Title).ToArray();
}

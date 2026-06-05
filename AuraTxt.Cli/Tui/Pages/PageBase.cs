namespace AuraTxt.Cli.Tui.Pages;

/// Tracks cursor position across a list that may include non-selectable separators.
public abstract class PageBase : IMenuPage
{
    private int _cursorPos; // index within selectable items

    public abstract string Title { get; }
    public abstract Task<PageResult> RunAsync(TuiApp app, CancellationToken ct);

    protected (int cursorIndex, IReadOnlyList<int> selectableIndices)
        BuildCursorState(IReadOnlyList<MenuItem> items)
    {
        var sel = items
            .Select((item, i) => (item, i))
            .Where(x => !x.item.IsSeparator)
            .Select(x => x.i)
            .ToList();

        if (sel.Count == 0) return (0, sel);
        if (_cursorPos >= sel.Count) _cursorPos = sel.Count - 1;
        return (sel[_cursorPos], sel);
    }

    protected void MoveUp(int selectableCount)   =>
        _cursorPos = (_cursorPos - 1 + selectableCount) % selectableCount;

    protected void MoveDown(int selectableCount) =>
        _cursorPos = (_cursorPos + 1) % selectableCount;

    protected void JumpTo(IReadOnlyList<int> selectableIndices, IReadOnlyList<MenuItem> items, string key)
    {
        for (int i = 0; i < selectableIndices.Count; i++)
        {
            if (string.Equals(items[selectableIndices[i]].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                _cursorPos = i;
                return;
            }
        }
    }

    protected string FooterWith(string extras = "") =>
        extras.Length > 0
            ? $"↑↓ Navigate  │  [Enter] Select  │  {extras}  │  [Esc] Back"
            : "↑↓ Navigate  │  [Enter] Select  │  [Esc] Back";
}

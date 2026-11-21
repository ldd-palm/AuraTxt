namespace AuraTxt.Core.Services;

/// One clipboard-history item. `Text` is the plain-text form (what Shift+Enter pastes,
/// what search matches, and the dedup key); `Formats` holds the richer text formats that
/// were on the clipboard alongside it (e.g. "HTML Format", "Rich Text Format"), restored
/// on a normal paste.
public sealed record ClipboardHistoryEntry(string Text, IReadOnlyDictionary<string, string> Formats);

/// In-memory, newest-first clipboard history (Settings.ClipboardHistorySize entries).
/// Not thread-safe — the app only touches it from the UI thread (the clipboard listener
/// and ClipboardHistoryWindow both live there).
public sealed class ClipboardHistory
{
    private readonly List<ClipboardHistoryEntry> _items = new();
    private int _capacity;

    public ClipboardHistory(int capacity) => Capacity = capacity;

    public IReadOnlyList<ClipboardHistoryEntry> Items => _items;

    /// At least 1; shrinking drops the oldest entries.
    public int Capacity
    {
        get => _capacity;
        set
        {
            _capacity = Math.Max(1, value);
            Trim();
        }
    }

    /// Adds `entry` as the newest item. Blank text is ignored; an item with identical text
    /// (case-sensitive) is replaced and moved to the top instead of duplicated.
    public void Add(ClipboardHistoryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Text)) return;
        _items.RemoveAll(i => i.Text == entry.Text);
        _items.Insert(0, entry);
        Trim();
    }

    /// Appends `older` (newest first, e.g. loaded from disk) after the current items —
    /// skipping blanks and texts already present — then trims to Capacity. The in-memory
    /// items count as newer, so they keep their place at the top.
    public void AppendOlder(IEnumerable<ClipboardHistoryEntry> older)
    {
        foreach (var e in older)
        {
            if (string.IsNullOrWhiteSpace(e.Text) || _items.Any(i => i.Text == e.Text)) continue;
            _items.Add(e);
        }
        Trim();
    }

    /// Moves an existing item to the top (after it was pasted). No-op if it has since
    /// been trimmed out of the history.
    public void MoveToTop(ClipboardHistoryEntry entry)
    {
        if (!_items.Remove(entry)) return;
        _items.Insert(0, entry);
    }

    /// Case-insensitive substring match on Text, newest first; blank query = everything.
    public IReadOnlyList<ClipboardHistoryEntry> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return _items.ToList();
        var q = query.Trim();
        return _items.Where(i => i.Text.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void Trim()
    {
        if (_items.Count > _capacity) _items.RemoveRange(_capacity, _items.Count - _capacity);
    }
}

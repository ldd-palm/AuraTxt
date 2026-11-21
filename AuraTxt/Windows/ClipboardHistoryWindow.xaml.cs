using System.Windows;
using System.Windows.Input;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using AuraTxt.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AuraTxt.Windows;

/// Clipboard-history picker (Settings.ClipboardHistoryHotkey). Keys: 0–9 paste the
/// numbered row immediately; Up/Down + Enter paste the selected one; the configured
/// plain-text key (Settings.ClipboardHistoryPlainTextKey, e.g. Shift+Enter) pastes it as
/// plain text. The search box only takes input once clicked — so digits never type into
/// it by accident — and while it has focus, digits type instead of pasting. Closes on
/// Esc or as soon as it loses focus.
public partial class ClipboardHistoryWindow : Window
{
    private const int NumberedRows = 10;

    private readonly ClipboardHistory _history;
    private readonly IntPtr _sourceHwnd;
    private readonly ModifierKeys _plainTextModifier;
    private bool _closing;

    public static ClipboardHistoryWindow? Current { get; private set; }

    private sealed record Row(string Number, string Preview, ClipboardHistoryEntry Entry);

    public ClipboardHistoryWindow(ClipboardHistory history, AppSettings settings, IntPtr sourceHwnd)
    {
        InitializeComponent();
        _history    = history;
        _sourceHwnd = sourceHwnd;
        _plainTextModifier = settings.ClipboardHistoryPlainTextKey.Split('+')[0].Trim().ToLowerInvariant() switch
        {
            "ctrl" => ModifierKeys.Control,
            "alt"  => ModifierKeys.Alt,
            _      => ModifierKeys.Shift
        };
        HistoryList.FontSize = settings.FontSize;
        Opacity = settings.ResultWindowOpacity;

        Refresh();
        Current = this;
        Closed += (_, _) => { if (Current == this) Current = null; };
        Deactivated += (_, _) => SafeClose();
        Loaded += (_, _) =>
        {
            Activate();
            FocusList();
        };
    }

    private void Refresh()
    {
        var items = _history.Search(SearchBox.Text);
        HistoryList.ItemsSource = items
            .Select((e, i) => new Row(i < NumberedRows ? i.ToString() : "", PreviewOf(e.Text), e))
            .ToList();
        if (items.Count > 0) HistoryList.SelectedIndex = 0;
        EmptyLabel.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryList.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private static string PreviewOf(string text)
    {
        var oneLine = text.Trim().Replace("\r\n", " ⏎ ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
        return oneLine.Length > 300 ? oneLine[..300] : oneLine;
    }

    private void FocusList()
    {
        HistoryList.Focus();
        if (HistoryList.SelectedIndex < 0) return;
        HistoryList.UpdateLayout();
        (HistoryList.ItemContainerGenerator.ContainerFromIndex(HistoryList.SelectedIndex) as UIElement)?.Focus();
    }

    private void MoveSelection(int delta)
    {
        var count = HistoryList.Items.Count;
        if (count == 0) return;
        var i = Math.Clamp(HistoryList.SelectedIndex + delta, 0, count - 1);
        HistoryList.SelectedIndex = i;
        HistoryList.ScrollIntoView(HistoryList.SelectedItem);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var searching = SearchBox.IsKeyboardFocusWithin;
        var mods = Keyboard.Modifiers;

        switch (e.Key)
        {
            case Key.Escape:
                if (searching && SearchBox.Text.Length > 0) { SearchBox.Clear(); FocusList(); }
                else if (searching) FocusList();
                else SafeClose();
                e.Handled = true;
                return;
            case Key.Up:
                MoveSelection(-1); e.Handled = true; return;
            case Key.Down:
                MoveSelection(+1); e.Handled = true; return;
            case Key.Enter:
                if (mods == ModifierKeys.None)                { PasteIndex(HistoryList.SelectedIndex, plainText: false); e.Handled = true; }
                else if (mods == _plainTextModifier)          { PasteIndex(HistoryList.SelectedIndex, plainText: true);  e.Handled = true; }
                return;
        }

        if (searching || mods != ModifierKeys.None) return;
        var digit = e.Key switch
        {
            >= Key.D0 and <= Key.D9           => e.Key - Key.D0,
            >= Key.NumPad0 and <= Key.NumPad9 => e.Key - Key.NumPad0,
            _ => -1
        };
        if (digit < 0) return;
        PasteIndex(digit, plainText: false);
        e.Handled = true;
    }

    private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e) =>
        PasteIndex(HistoryList.SelectedIndex, plainText: false);

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        Refresh();
    }

    private async void PasteIndex(int index, bool plainText)
    {
        if (_closing || index < 0 || index >= HistoryList.Items.Count) return;
        var entry = ((Row)HistoryList.Items[index]).Entry;

        // Close before pasting — see CLAUDE.md "Window closing race": the paste has to
        // SetForegroundWindow the source app, which a still-visible topmost window competes with.
        _closing = true;
        Close();
        _history.MoveToTop(entry);
        await ClipboardService.PasteHistoryEntryAsync(_sourceHwnd, entry, plainText);
    }

    private void SafeClose()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => SafeClose();
}

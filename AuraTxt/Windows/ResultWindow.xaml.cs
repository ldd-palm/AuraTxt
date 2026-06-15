using System.Windows;
using System.Windows.Input;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using AuraTxt.Services;
using Clipboard = System.Windows.Clipboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AuraTxt.Windows;

public partial class ResultWindow : Window
{
    private readonly ActionItem   _action;
    private readonly string       _selectedText;
    private readonly ConfigRoot   _cfg;
    private string                _currentPrompt;
    private (ProviderConfig provider, ModelEntry model)? _activeModel;
    private bool _closing;
    private bool _editing;
    private bool _pinned;
    private CancellationTokenSource? _streamCts;

    public ResultWindow(ActionItem action, string selectedText, ConfigRoot cfg)
    {
        InitializeComponent();
        _action        = action;
        _selectedText  = selectedText;
        _cfg           = cfg;
        _currentPrompt = PromptService.Resolve(action.Prompt);   // path → file content (or inline)

        // Allow resize by dragging window edges
        System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
        {
            ResizeBorderThickness = new Thickness(6),
            CaptionHeight = 0,
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            UseAeroCaptionButtons = false
        });
        MinWidth = 320;
        MinHeight = 200;
        if (AppState.SessionResultWindowWidth.HasValue)
            Width = AppState.SessionResultWindowWidth.Value;
        SizeChanged += (_, _) => AppState.SessionResultWindowWidth = Width;

        TitleLabel.Text     = action.Name;
        var titleIcon = IconCacheService.GetIconSync(action.Icon);
        if (titleIcon is not null) { TitleIcon.Source = titleIcon; TitleIcon.Visibility = Visibility.Visible; }
        ResultText.FontSize = cfg.Settings.FontSize;
        Opacity             = cfg.Settings.ResultWindowOpacity;

        // Populate model picker with enabled user models + all built-in models
        var items = cfg.AllEnabledModelRefs()
            .Select(r => new ModelPickerItem(r.Ref, r.Label, r.Ref.StartsWith("default/")))
            .ToList();
        ModelPicker.ItemsSource       = items;
        ModelPicker.SelectedValuePath = "Id";

        // Set initial model selection
        var initial = cfg.ResolveModel(action.ModelId);
        _activeModel = initial;
        if (initial is not null)
            ModelPicker.SelectedValue = action.ModelId;

        AppState.IsResultWindowOpen = true;
        // Keep LastProcessedText on close — the source text is still highlighted,
        // so clearing it here would let the next click re-pop the menu. It is
        // re-armed only when the user deselects (see GlobalHookService.OnMouseUp).
        Closed += (_, _) =>
        {
            AppState.IsResultWindowOpen = false;
            AppState.MenuSuppressUntil  = DateTime.UtcNow.AddSeconds(2);
            _streamCts?.Cancel();
        };
        Deactivated += (_, _) => SafeClose();

        Loaded += async (_, _) => await RunAsync();
    }

    private void ModelPicker_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ModelPicker.SelectedValue is string id)
        {
            _activeModel = _cfg.ResolveModel(id);
            _action.ModelId = id;
            // Read-modify-write on a fresh load: writing back our stale snapshot (_cfg)
            // would clobber any change auracfg saved while this window was open.
            try
            {
                var svc   = new ConfigService();
                var fresh = svc.Load();
                var a     = fresh.Actions.FirstOrDefault(x => x.Id == _action.Id);
                if (a is not null) { a.ModelId = id; svc.Save(fresh); }
            }
            catch (Exception ex) { LogService.Error($"Failed to persist model selection", ex); }
        }
    }

    private async Task RunAsync()
    {
        LogService.Info($"ACTION [{_action.Id}] model={_action.ModelId} text_len={_selectedText.Length}");

        _streamCts?.Cancel();
        _streamCts = new CancellationTokenSource();
        var ct = _streamCts.Token;

        var resolved = _activeModel ?? _cfg.ResolveModel(_action.ModelId);
        if (resolved is null)
        { ResultText.Text = FormatError(new InvalidOperationException($"Model not found: {_action.ModelId}")); return; }

        ResultText.Text = "Processing…";
        var firstChunk = true;
        var slash = _action.ModelId.IndexOf('/');
        var providerId = slash >= 0 ? _action.ModelId[..slash] : _action.ModelId;
        try
        {
            await foreach (var delta in new AiClient().StreamAsync(
                providerId, resolved.Value.provider, resolved.Value.model, _action, _selectedText, "", ct))
            {
                if (firstChunk) { ResultText.Text = ""; firstChunk = false; }
                ResultText.AppendText(delta);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            LogService.Error($"Stream failed for action [{_action.Id}]", ex);
            if (!ct.IsCancellationRequested)
                ResultText.Text = (string.IsNullOrEmpty(ResultText.Text) ? "" : ResultText.Text + "\n") + FormatError(ex);
        }
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) { _closing = true; Close(); }
    private void SafeClose() { if (_closing || _editing || _pinned) return; _closing = true; Close(); }
    private void PinBtn_Click(object sender, RoutedEventArgs e)
    {
        _pinned      = !_pinned;
        PinBtn.Opacity = _pinned ? 1.0 : 0.45;
    }
    private async void RegenBtn_Click(object sender, RoutedEventArgs e) => await RunAsync();
    private void CopyBtn_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(ResultText.Text); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Copy failed: {ex.Message}"); }
    }
    private async void ReplaceBtn_Click(object sender, RoutedEventArgs e)
    {
        _closing = true;
        await ClipboardService.ReplaceInSourceWindowAsync(AppState.SourceWindowHandle, ResultText.Text);
        Close();
    }

    private void EditBtn_Click(object sender, RoutedEventArgs e)
    {
        // Built-in models don't use custom prompts — show read-only notice instead.
        if (_activeModel?.model.TargetModel is "Google_Translate" or "Youdao_Dict")
        {
            _editing = true;
            try
            {
                var langCode = _cfg.Settings.TargetLanguage;
                var msg = $"Built-in models do not support custom prompts.\n\n" +
                          $"The current target language is {langCode}.\n" +
                          $"To change the target language, go to General Settings in auracfg.";
                var dlg = new PromptEditDialog(msg, readOnly: true) { Owner = this };
                dlg.ShowDialog();
            }
            finally { _editing = false; }
            return;
        }

        _editing = true;
        try
        {
            var dlg = new PromptEditDialog(_currentPrompt) { Owner = this };
            if (dlg.ShowDialog() == true) { _currentPrompt = dlg.Result; _ = RunAsync(); }
        }
        finally { _editing = false; }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)         { SafeClose(); e.Handled = true; return; }
        // Bare-letter shortcuts only: let Ctrl+C etc. tunnel to the TextBox (copy selection),
        // and never hijack typing in an editable TextBox.
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox { IsReadOnly: false }) return;

        if (e.Key == Key.P)      { EditBtn_Click(sender, new RoutedEventArgs());    e.Handled = true; }
        else if (e.Key == Key.G) { RegenBtn_Click(sender, new RoutedEventArgs());   e.Handled = true; }
        else if (e.Key == Key.R) { ReplaceBtn_Click(sender, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.C) { CopyBtn_Click(sender, new RoutedEventArgs());    e.Handled = true; }
        else if (e.Key == Key.T) { PinBtn_Click(sender, new RoutedEventArgs());     e.Handled = true; }
    }

    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private static string FormatError(Exception ex)
    {
        var msg = $"[Error] {ex.Message}";
        if (ex.InnerException is not null)
            msg += $"\n→ {ex.InnerException.Message}";
        return msg;
    }

    private record ModelPickerItem(string Id, string Label, bool IsBuiltIn);
}

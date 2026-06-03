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

        TitleLabel.Text     = action.Name;
        ResultText.FontSize = cfg.Settings.FontSize;
        Opacity             = cfg.Settings.ResultWindowOpacity;

        // Populate model picker with enabled user models + all built-in models
        var items = cfg.AllEnabledModelAliases()
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
            try { new ConfigService().Save(_cfg); }
            catch (Exception ex) { LogService.Error($"Failed to persist model selection", ex); }
        }
    }

    private async Task RunAsync()
    {
        LogService.Info($"ACTION [{_action.Id}] model={_action.ModelId} text_len={_selectedText.Length}");
        ResultText.Text = "Processing…";
        try { ResultText.Text = await CallModelAsync(); }
        catch (Exception ex)
        {
            LogService.Error($"CallModel failed for action [{_action.Id}]", ex);
            ResultText.Text = FormatError(ex);
        }
    }

    private async Task<string> CallModelAsync()
    {
        var resolved = _activeModel ?? _cfg.ResolveModel(_action.ModelId);

        // Built-in models: no prompt, call service directly
        if (resolved?.model.TargetModel == "Google_Translate")
        {
            LogService.Info($"Google_Translate → {_cfg.Settings.TargetLanguage}  text_len={_selectedText.Length}");
            return await new GoogleTranslateClient().TranslateAsync(_selectedText, to: _cfg.Settings.TargetLanguage);
        }
        if (resolved?.model.TargetModel == "Youdao_Dict")
        {
            var yt = YoudaoToCode(_cfg.Settings.TargetLanguage);
            LogService.Info($"Youdao_Dict → {yt}  text_len={_selectedText.Length}");
            return await new YoudaoClient().TranslateAsync(_selectedText, to: yt);
        }

        if (resolved is null)
            throw new InvalidOperationException($"Model not found: {_action.ModelId}");

        var userPrompt = _currentPrompt
            .Replace("{SelectedText}", _selectedText)
            .Replace("{UserInput}", "");

        var sysText = PromptService.Resolve(_cfg.Settings.SystemPrompt);
        var systemPrompt = string.IsNullOrWhiteSpace(sysText)
            ? null
            : sysText
                .Replace("{SelectedText}", _selectedText)
                .Replace("{UserInput}", "");

        return await new AiClient().CompleteAsync(
            resolved.Value.provider, resolved.Value.model, userPrompt, systemPrompt);
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) { _closing = true; Close(); }
    private void SafeClose() { if (_closing || _editing) return; _closing = true; Close(); }
    private async void RegenBtn_Click(object sender, RoutedEventArgs e) => await RunAsync();
    private void CopyBtn_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(ResultText.Text); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Copy failed: {ex.Message}"); }
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
        if (e.Key == Key.Escape)         { SafeClose(); e.Handled = true; }
        else if (e.Key == Key.P) { EditBtn_Click(sender, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.R) { RegenBtn_Click(sender, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.C) { CopyBtn_Click(sender, new RoutedEventArgs()); e.Handled = true; }
    }

    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    /// Map Google-style language code to Youdao code (only zh-CN → zh-CHS differs).
    private static string YoudaoToCode(string googleCode) =>
        googleCode == "zh-CN" ? "zh-CHS" : googleCode;

    private static string FormatError(Exception ex)
    {
        var msg = $"[Error] {ex.Message}";
        if (ex.InnerException is not null)
            msg += $"\n→ {ex.InnerException.Message}";
        return msg;
    }

    private record ModelPickerItem(string Id, string Label, bool IsBuiltIn);
}

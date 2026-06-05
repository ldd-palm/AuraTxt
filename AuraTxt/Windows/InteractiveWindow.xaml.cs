using System.Windows;
using System.Windows.Input;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using AuraTxt.Services;
using Clipboard = System.Windows.Clipboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AuraTxt.Windows;

public partial class InteractiveWindow : Window
{
    private readonly ActionItem _action;
    private readonly string     _selectedText;
    private readonly ConfigRoot _cfg;
    private string              _currentPrompt;
    private (ProviderConfig provider, ModelEntry model)? _activeModel;
    private bool _closing;
    private bool _editing;
    private bool _pinned;

    public InteractiveWindow(ActionItem action, string selectedText, ConfigRoot cfg)
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

        var items = cfg.AllEnabledModelAliases()
            .Where(r => !r.Ref.StartsWith("default/"))
            .Select(r => new ModelPickerItem(r.Ref, r.Label, false))
            .ToList();
        ModelPicker.ItemsSource       = items;
        ModelPicker.SelectedValuePath = "Id";

        var initial = cfg.ResolveModel(action.ModelId);
        if (initial is not null && !action.ModelId.StartsWith("default/"))
        {
            ModelPicker.SelectedValue = action.ModelId;
            _activeModel = initial;
        }

        AppState.IsResultWindowOpen = true;
        // Keep LastProcessedText on close (re-armed only on deselect — see GlobalHookService).
        Closed += (_, _) =>
        {
            AppState.IsResultWindowOpen = false;
            AppState.MenuSuppressUntil  = DateTime.UtcNow.AddSeconds(2);
        };
        Deactivated += (_, _) => SafeClose();
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

    private async void SendBtn_Click(object sender, RoutedEventArgs e)  => await GenerateAsync();
    private async void RegenBtn_Click(object sender, RoutedEventArgs e) => await GenerateAsync();

    private async Task GenerateAsync()
    {
        if (_activeModel is null)
        {
            ResultText.Text = "[Error] Please select a model first.";
            return;
        }
        ResultText.Text = "Processing…";

        var userPrompt = _currentPrompt
            .Replace("{SelectedText}", _selectedText)
            .Replace("{UserInput}", UserInput.Text);

        var sysText = PromptService.Resolve(_cfg.Settings.SystemPrompt);
        var systemPrompt = string.IsNullOrWhiteSpace(sysText)
            ? null
            : sysText
                .Replace("{SelectedText}", _selectedText)
                .Replace("{UserInput}", UserInput.Text);

        LogService.Info($"ACTION [{_action.Id}] model={_action.ModelId} text_len={_selectedText.Length} user_input_len={UserInput.Text.Length}");
        try
        {
            ResultText.Text = await new AiClient().CompleteAsync(
                _activeModel.Value.provider, _activeModel.Value.model, userPrompt, systemPrompt);
        }
        catch (Exception ex)
        {
            LogService.Error($"GenerateAsync failed for action [{_action.Id}]", ex);
            ResultText.Text = FormatError(ex);
        }
    }

    private void CopyBtn_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(ResultText.Text); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Copy failed: {ex.Message}"); }
    }

    private void EditBtn_Click(object sender, RoutedEventArgs e)
    {
        _editing = true;
        try
        {
            var dlg = new PromptEditDialog(_currentPrompt) { Owner = this };
            if (dlg.ShowDialog() == true) _currentPrompt = dlg.Result;
        }
        finally { _editing = false; }
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) { _closing = true; Close(); }
    private void SafeClose() { if (_closing || _editing || _pinned) return; _closing = true; Close(); }
    private void PinBtn_Click(object sender, RoutedEventArgs e)
    {
        _pinned        = !_pinned;
        PinBtn.Opacity = _pinned ? 1.0 : 0.45;
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)             { SafeClose(); e.Handled = true; }
        else if (e.Key == Key.P) { EditBtn_Click(sender, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.R) { RegenBtn_Click(sender, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.C) { CopyBtn_Click(sender, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.T) { PinBtn_Click(sender, new RoutedEventArgs()); e.Handled = true; }
    }

    private static string FormatError(Exception ex)
    {
        var msg = $"[Error] {ex.Message}";
        if (ex.InnerException is not null)
            msg += $"\n→ {ex.InnerException.Message}";
        return msg;
    }

    private void UserInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            _ = GenerateAsync();
        }
    }

    private record ModelPickerItem(string Id, string Label, bool IsBuiltIn);
}

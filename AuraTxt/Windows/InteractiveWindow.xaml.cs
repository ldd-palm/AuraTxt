using System.Windows;
using System.Windows.Input;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
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

    public InteractiveWindow(ActionItem action, string selectedText, ConfigRoot cfg)
    {
        InitializeComponent();
        _action        = action;
        _selectedText  = selectedText;
        _cfg           = cfg;
        _currentPrompt = action.Prompt;

        TitleLabel.Text     = action.Name;
        ResultText.FontSize = cfg.Settings.FontSize;
        Opacity             = cfg.Settings.ResultWindowOpacity;

        var items = cfg.AllModelRefs()
            .Where(r => !r.Ref.StartsWith("default/"))
            .Select(r => new ModelPickerItem(r.Ref, r.Label))
            .ToList();
        ModelPicker.ItemsSource       = items;
        ModelPicker.DisplayMemberPath = "Label";
        ModelPicker.SelectedValuePath = "Id";

        var initial = cfg.ResolveModel(action.ModelId);
        if (initial is not null && !action.ModelId.StartsWith("default/"))
        {
            ModelPicker.SelectedValue = action.ModelId;
            _activeModel = initial;
        }
    }

    private void ModelPicker_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ModelPicker.SelectedValue is string id)
            _activeModel = _cfg.ResolveModel(id);
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
        var prompt = _currentPrompt
            .Replace("{SelectedText}", _selectedText)
            .Replace("{UserInput}", UserInput.Text);
        try { ResultText.Text = await new AiClient().CompleteAsync(_activeModel.Value.provider, _activeModel.Value.model, prompt); }
        catch (Exception ex) { ResultText.Text = $"[Error] {ex.Message}"; }
    }

    private void CopyBtn_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(ResultText.Text);

    private void EditBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PromptEditDialog(_currentPrompt);
        if (dlg.ShowDialog() == true) _currentPrompt = dlg.Result;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.P) EditBtn_Click(sender, new RoutedEventArgs());
        else if (e.Key == Key.R) RegenBtn_Click(sender, new RoutedEventArgs());
        else if (e.Key == Key.C) CopyBtn_Click(sender, new RoutedEventArgs());
    }

    private void UserInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            _ = GenerateAsync();
        }
    }

    private record ModelPickerItem(string Id, string Label);
}

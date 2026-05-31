using System.Windows;
using System.Windows.Input;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using Clipboard = System.Windows.Clipboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AuraTxt.Windows;

public partial class ResultWindow : Window
{
    private readonly ActionItem _action;
    private readonly string _selectedText;
    private readonly ConfigRoot _cfg;
    private string _currentPrompt;

    public ResultWindow(ActionItem action, string selectedText, ConfigRoot cfg)
    {
        InitializeComponent();
        _action        = action;
        _selectedText  = selectedText;
        _cfg           = cfg;
        _currentPrompt = action.Prompt;

        TitleLabel.Text     = $"{action.Name} · {GetModelDisplayName(action, cfg)}";
        ResultText.FontSize = cfg.Settings.FontSize;
        Opacity             = cfg.Settings.ResultWindowOpacity;

        Loaded += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        ResultText.Text = "正在处理…";
        try { ResultText.Text = await CallModelAsync(); }
        catch (Exception ex) { ResultText.Text = $"[错误] {ex.Message}"; }
    }

    private async Task<string> CallModelAsync()
    {
        var prompt = _currentPrompt
            .Replace("{SelectedText}", _selectedText)
            .Replace("{UserInput}", "");

        if (_action.ModelId == "$google-translate")
            return await new GoogleTranslateClient().TranslateAsync(_selectedText);
        if (_action.ModelId == "$youdao-dict")
            return await new YoudaoClient().TranslateAsync(_selectedText);

        if (!_cfg.Models.TryGetValue(_action.ModelId, out var model))
            throw new InvalidOperationException($"未找到模型：{_action.ModelId}");

        return await new AiClient().CompleteAsync(model, prompt);
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    private async void RegenBtn_Click(object sender, RoutedEventArgs e) => await RunAsync();

    private void CopyBtn_Click(object sender, RoutedEventArgs e) =>
        Clipboard.SetText(ResultText.Text);

    private void EditBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PromptEditDialog(_currentPrompt);
        if (dlg.ShowDialog() == true) { _currentPrompt = dlg.Result; _ = RunAsync(); }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.P) EditBtn_Click(sender, new RoutedEventArgs());
        else if (e.Key == Key.R) RegenBtn_Click(sender, new RoutedEventArgs());
        else if (e.Key == Key.C) CopyBtn_Click(sender, new RoutedEventArgs());
    }

    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private static string GetModelDisplayName(ActionItem action, ConfigRoot cfg)
    {
        if (action.ModelId == "$google-translate") return "Google 翻译";
        if (action.ModelId == "$youdao-dict")      return "有道词典";
        return cfg.Models.TryGetValue(action.ModelId, out var m) ? m.DisplayName : action.ModelId;
    }
}

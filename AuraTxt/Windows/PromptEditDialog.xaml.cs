using System.Windows;
using AuraTxt.Core.Services;
using AuraTxt.Resources;

namespace AuraTxt.Windows;

public partial class PromptEditDialog : Window
{
    public string Result { get; private set; } = "";

    // The action's stored prompt reference (e.g. "prompts/local-mt.md" or inline text).
    // Only a file reference can be shown as a path and overwritten by Save.
    private readonly string? _promptRef;

    public PromptEditDialog(string text, string? promptRef = null, bool readOnly = false)
    {
        InitializeComponent();
        PromptBox.Text = text;
        _promptRef = promptRef;

        var isFile = PromptService.IsFileRef(promptRef);
        // Window title carries the prompt file, e.g. "Edit Prompt (prompts/local-mt.md)";
        // inline prompts have no file to show or overwrite.
        Title = isFile
            ? $"{Strings.PromptEdit_Title} ({PromptService.ToRelativeIfInsideBase(promptRef!)})"
            : $"{Strings.PromptEdit_Title} {Strings.PromptEdit_InlineNoFile}";

        if (readOnly)
        {
            PromptBox.IsReadOnly = true;
            Title = Strings.PromptEdit_BuiltinTitle;
            CancelBtn.Visibility = Visibility.Collapsed;
            SaveBtn.Visibility   = Visibility.Collapsed;
            OkBtn.Content = Strings.Common_Close;
        }
        else if (!isFile)
        {
            // Nothing to overwrite — Generate (temporary) is still available.
            SaveBtn.Visibility = Visibility.Collapsed;
        }
    }

    // Generate: use the edited text for this run only; never touches the file.
    private void OK_Click(object sender, RoutedEventArgs e)     { Result = PromptBox.Text; DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    // Save: permanently overwrite the prompt file (after confirmation), then behave like
    // Generate so the just-saved text is used immediately.
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!PromptService.IsFileRef(_promptRef)) return; // button hidden in this case anyway
        var rel = PromptService.ToRelativeIfInsideBase(_promptRef!);
        var confirm = System.Windows.MessageBox.Show(this,
            string.Format(Strings.PromptEdit_SaveConfirmFormat, rel),
            Strings.PromptEdit_SaveButton,
            MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        try
        {
            PromptService.Save(_promptRef!, PromptBox.Text);
        }
        catch (System.Exception ex)
        {
            System.Windows.MessageBox.Show(this, string.Format(Strings.PromptEdit_SaveFailedFormat, ex.Message),
                Strings.PromptEdit_SaveButton, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        Result = PromptBox.Text;
        DialogResult = true;
    }
}

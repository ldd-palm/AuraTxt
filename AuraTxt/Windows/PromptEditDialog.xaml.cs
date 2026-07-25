using System.Windows;
using AuraTxt.Resources;

namespace AuraTxt.Windows;

public partial class PromptEditDialog : Window
{
    public string Result { get; private set; } = "";

    public PromptEditDialog(string text, bool readOnly = false)
    {
        InitializeComponent();
        PromptBox.Text = text;
        if (readOnly)
        {
            PromptBox.IsReadOnly = true;
            Title = Strings.PromptEdit_BuiltinTitle;
            CancelBtn.Visibility = Visibility.Collapsed;
            OkBtn.Content = Strings.Common_Close;
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)     { Result = PromptBox.Text; DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

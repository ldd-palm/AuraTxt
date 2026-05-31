using System.Windows;

namespace AuraTxt.Windows;

public partial class PromptEditDialog : Window
{
    public string Result { get; private set; } = "";

    public PromptEditDialog(string currentPrompt)
    {
        InitializeComponent();
        PromptBox.Text = currentPrompt;
    }

    private void OK_Click(object sender, RoutedEventArgs e)     { Result = PromptBox.Text; DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

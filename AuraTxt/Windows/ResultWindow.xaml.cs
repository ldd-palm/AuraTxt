using System.Windows;
using AuraTxt.Core.Models;

namespace AuraTxt.Windows;

public partial class ResultWindow : Window
{
    public ResultWindow(ActionItem action, string selectedText, ConfigRoot cfg)
    {
        InitializeComponent();
    }
}

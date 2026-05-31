using System.Windows;
using AuraTxt.Core.Models;

namespace AuraTxt.Windows;

public partial class InteractiveWindow : Window
{
    public InteractiveWindow(ActionItem action, string selectedText, ConfigRoot cfg)
    {
        InitializeComponent();
    }
}

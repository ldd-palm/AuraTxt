using System.Windows;
using AuraTxt.Core.Models;

namespace AuraTxt.Windows;

public partial class ActionMenuWindow : Window
{
    public ActionMenuWindow(ConfigRoot cfg, string selectedText, System.Drawing.Point cursor)
    {
        InitializeComponent();
    }
}

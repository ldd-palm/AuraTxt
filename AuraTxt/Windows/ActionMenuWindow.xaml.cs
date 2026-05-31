using System.Windows;
using System.Windows.Media;
using AuraTxt.Core.Models;
using AuraTxt.Services;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using Separator = System.Windows.Controls.Separator;
using TextBlock = System.Windows.Controls.TextBlock;
using ToolTipService = System.Windows.Controls.ToolTipService;

namespace AuraTxt.Windows;

public partial class ActionMenuWindow : Window
{
    private readonly ConfigRoot _cfg;
    private readonly string _selectedText;

    public ActionMenuWindow(ConfigRoot cfg, string selectedText, System.Drawing.Point cursor)
    {
        InitializeComponent();
        _cfg          = cfg;
        _selectedText = selectedText;

        Left = cursor.X + 4;
        Top  = cursor.Y - 44;

        Loaded      += async (_, _) => await BuildMenuAsync();
        Deactivated += (_, _) => Close();
    }

    private async Task BuildMenuAsync()
    {
        // Fixed left: Copy
        IconPanel.Children.Add(MakeEmojiButton("📋", "复制", () =>
        {
            Clipboard.SetText(_selectedText);
            Close();
        }));
        IconPanel.Children.Add(MakeSeparator());

        // Dynamic: actions from config
        foreach (var action in _cfg.Actions)
        {
            var a    = action;
            var img  = await IconCacheService.GetIconAsync(a.Icon);
            var tip  = $"{a.Name}{(string.IsNullOrEmpty(a.Hotkey) ? "" : $" ({a.Hotkey})")}";
            var btn  = img is not null
                ? MakeImageButton(img, tip, () => { Close(); HotkeyService.ShowResultFor(a, _selectedText, _cfg); })
                : MakeEmojiButton("?", tip, () => { Close(); HotkeyService.ShowResultFor(a, _selectedText, _cfg); });
            IconPanel.Children.Add(btn);
        }

        IconPanel.Children.Add(MakeSeparator());

        // Fixed right: Settings
        IconPanel.Children.Add(MakeEmojiButton("⚙️", "设置 (auracfg)", () =>
        {
            var exe = System.IO.Path.Combine(AppContext.BaseDirectory, "auracfg.exe");
            if (System.IO.File.Exists(exe)) System.Diagnostics.Process.Start(exe);
            Close();
        }));
    }

    private static Button MakeEmojiButton(string emoji, string tooltip, Action onClick)
    {
        var btn = new Button
        {
            Content     = new TextBlock { Text = emoji, FontSize = 14, VerticalAlignment = VerticalAlignment.Center },
            Width       = 28,
            Height      = 28,
            Background  = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Cursor      = Cursors.Hand,
            ToolTip     = tooltip
        };
        ToolTipService.SetInitialShowDelay(btn, 300);
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button MakeImageButton(DrawingImage img, string tooltip, Action onClick)
    {
        var btn = new Button
        {
            Content     = new System.Windows.Controls.Image { Source = img, Width = 14, Height = 14 },
            Width       = 28,
            Height      = 28,
            Background  = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Cursor      = Cursors.Hand,
            ToolTip     = tooltip
        };
        ToolTipService.SetInitialShowDelay(btn, 300);
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Separator MakeSeparator() => new()
    {
        Width      = 1,
        Height     = 18,
        Background = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
        Margin     = new Thickness(3, 0, 3, 0)
    };
}

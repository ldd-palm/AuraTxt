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
    private readonly System.Drawing.Point _physicalCursor;
    private bool _closing;

    public ActionMenuWindow(ConfigRoot cfg, string selectedText, System.Drawing.Point physicalCursor)
    {
        InitializeComponent();
        _cfg            = cfg;
        _selectedText   = selectedText;
        _physicalCursor = physicalCursor;

        // Fallback position before DPI conversion (corrected in Loaded)
        Left = physicalCursor.X + 4;
        Top  = physicalCursor.Y - 44;

        Loaded      += OnLoaded;
        Deactivated += (_, _) => SafeClose();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Convert physical screen pixels → WPF device-independent pixels
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        Left = _physicalCursor.X / dpi.DpiScaleX + 4;
        Top  = _physicalCursor.Y / dpi.DpiScaleY - 44;

        await BuildMenuAsync();
    }

    private void SafeClose()
    {
        if (_closing) return;
        _closing = true;
        // Suppress menu re-trigger for 2s (the mouse-up that clicked us fires globally)
        AppState.MenuSuppressUntil = DateTime.UtcNow.AddSeconds(2);
        Close();
    }

    private async Task BuildMenuAsync()
    {
        // Dynamic: enabled actions from config (includes system actions like copy, speech)
        foreach (var action in _cfg.Actions.Where(a => a.Enabled))
        {
            var a   = action;
            var img = await IconCacheService.GetIconAsync(a.Icon);
            var tip = $"{a.Name}{(string.IsNullOrEmpty(a.Hotkey) ? "" : $" ({a.Hotkey})")}";

            Button btn;
            if (string.IsNullOrEmpty(a.ModelId))
            {
                // System action — route by ID
                btn = img is not null
                    ? MakeImageButton(img, tip, () => ExecuteSystemAction(a.Id))
                    : MakeEmojiButton("?", tip, () => ExecuteSystemAction(a.Id));
            }
            else
            {
                // Normal action
                btn = img is not null
                    ? MakeImageButton(img, tip, () => { SafeClose(); HotkeyService.ShowResultFor(a, _selectedText, _cfg); })
                    : MakeEmojiButton("?", tip, () => { SafeClose(); HotkeyService.ShowResultFor(a, _selectedText, _cfg); });
            }
            IconPanel.Children.Add(btn);
        }

        IconPanel.Children.Add(MakeSeparator());

        // Fixed right: Settings (Lucide: settings)
        var settingsImg = await IconCacheService.GetIconAsync("settings");
        var settingsBtn = settingsImg is not null
            ? MakeImageButton(settingsImg, "Settings (auracfg)", () =>
              {
                  SafeClose();
                  var exe = System.IO.Path.Combine(AppContext.BaseDirectory, "auracfg.exe");
                  if (System.IO.File.Exists(exe)) System.Diagnostics.Process.Start(exe);
              })
            : MakeEmojiButton("⚙️", "Settings (auracfg)", () =>
              {
                  SafeClose();
                  var exe = System.IO.Path.Combine(AppContext.BaseDirectory, "auracfg.exe");
                  if (System.IO.File.Exists(exe)) System.Diagnostics.Process.Start(exe);
              });
        IconPanel.Children.Add(settingsBtn);
    }

    private void ExecuteSystemAction(string id)
    {
        switch (id)
        {
            case "copy":
                Clipboard.SetText(_selectedText);
                break;
            case "speech":
                var synthesizer = new System.Speech.Synthesis.SpeechSynthesizer();
                synthesizer.SpeakAsync(_selectedText);
                break;
        }
        SafeClose();
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

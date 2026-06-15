using System.Windows;
using System.Windows.Input;
using NHotkey;
using NHotkey.Wpf;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using AuraTxt.Windows;

namespace AuraTxt.Services;

public class HotkeyService
{
    private readonly ConfigService _config;
    private readonly List<string> _registered = new();

    public HotkeyService(ConfigService config) => _config = config;

    public void RegisterAll(ConfigRoot cfg)
    {
        UnregisterAll();
        foreach (var action in cfg.Actions.Where(a => !string.IsNullOrEmpty(a.Hotkey)))
        {
            if (!TryParseHotkey(action.Hotkey, out var key, out var mods)) continue;
            try
            {
                var captured = action;
                HotkeyManager.Current.AddOrReplace(action.Id, key, mods,
                    (_, _) => _ = FireActionAsync(captured));
                _registered.Add(action.Id);
            }
            catch
            {
                // Another app owns this hotkey — silently skip (tray notification not implemented yet)
            }
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _registered)
            try { HotkeyManager.Current.Remove(id); } catch { }
        _registered.Clear();
    }

    private async Task FireActionAsync(ActionItem action)
    {
        AppState.SourceWindowHandle = ClipboardService.CaptureSourceWindow();
        var cfg  = _config.Load();
        var text = await ClipboardService.GetSelectedTextAsync(50);
        if (string.IsNullOrWhiteSpace(text)) return;

        // System actions (speech/copy) don't have a ModelId — handle inline.
        if (string.IsNullOrEmpty(action.ModelId))
        {
            AppState.SelectionActioned = true;
            switch (action.Id)
            {
                case "speech":
                    SpeechService.Speak(text, cfg.Settings.SpeechVoice);
                    break;
                case "copy":
                    System.Windows.Clipboard.SetText(text);
                    break;
                case "google":
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                            "https://www.google.com/search?q=" + Uri.EscapeDataString(text))
                            { UseShellExecute = true });
                    }
                    catch { }
                    break;
            }
            return;
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
            ShowResultFor(action, text, cfg));
    }

    public static void ShowResultFor(ActionItem action, string selectedText, ConfigRoot cfg)
    {
        AppState.SelectionActioned = true;
        if (action.IsInteractive)
            new InteractiveWindow(action, selectedText, cfg).Show();
        else
            new ResultWindow(action, selectedText, cfg).Show();
    }

    private static bool TryParseHotkey(string hotkey, out Key key, out ModifierKeys mods)
    {
        key  = Key.None;
        mods = ModifierKeys.None;
        var parts = hotkey.Split('+');
        if (parts.Length < 2) return false;

        foreach (var mod in parts[..^1])
        {
            ModifierKeys? m = mod.Trim().ToLower() switch
            {
                "ctrl"  => ModifierKeys.Control,
                "alt"   => ModifierKeys.Alt,
                "shift" => ModifierKeys.Shift,
                "win"   => ModifierKeys.Windows,
                _       => null
            };
            // Unknown modifier (e.g. hand-edited config) → reject; otherwise "Foo+T"
            // would register a bare T as a system-wide hotkey.
            if (m is null) return false;
            mods |= m.Value;
        }

        return Enum.TryParse(parts[^1].Trim(), true, out key) && key != Key.None;
    }
}

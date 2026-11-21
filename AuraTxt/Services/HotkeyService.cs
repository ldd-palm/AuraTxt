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
    private readonly ClipboardHistoryService _clipHistory;
    private readonly List<string> _registered = new();
    private const string ClipboardHistoryHotkeyId = "__clipboard_history";

    public HotkeyService(ConfigService config, ClipboardHistoryService clipHistory)
    {
        _config      = config;
        _clipHistory = clipHistory;
    }

    public void RegisterAll(ConfigRoot cfg)
    {
        UnregisterAll();
        RegisterClipboardHistory(cfg.Settings);
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

    /// Same RegisterHotKey mechanism (and pause/reload handling) as action hotkeys.
    /// Pressing it again while the window is open closes it.
    private void RegisterClipboardHistory(AppSettings settings)
    {
        if (!settings.IsClipboardHistoryActive) return;
        if (!TryParseHotkey(settings.ClipboardHistoryHotkey, out var key, out var mods)) return;
        try
        {
            HotkeyManager.Current.AddOrReplace(ClipboardHistoryHotkeyId, key, mods, (_, _) =>
            {
                if (ClipboardHistoryWindow.Current is { } open) { open.Close(); return; }
                var source = ClipboardService.CaptureSourceWindow();
                new ClipboardHistoryWindow(_clipHistory.History, settings, source).Show();
            });
            _registered.Add(ClipboardHistoryHotkeyId);
        }
        catch (Exception ex)
        {
            // Another app (or one of our own action hotkeys) already owns this combo.
            LogService.Error($"Clipboard history hotkey {settings.ClipboardHistoryHotkey} could not be registered", ex);
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
            AppState.MarkActionTaken();
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
        AppState.MarkActionTaken();

        // Clipboard Paste: not an AI action and produces no text to show — it triggers a
        // paste into the source window and returns immediately. Same short-circuit shape
        // as the Terminal console-window branch below, just with nothing to resolve first.
        if (action.ModelId.Equals("default/Clipboard_Paste", StringComparison.OrdinalIgnoreCase))
        {
            _ = Task.Run(async () =>
            {
                try { await ClipboardPasteService.RunAsync(AppState.SourceWindowHandle, cfg.Settings.PasteUseClipboardHistory); }
                catch (Exception ex) { LogService.Error("Clipboard paste failed", ex); }
            });
            return;
        }

        // Terminal console-window mode: the console window IS the output surface, so
        // skip ResultWindow/InteractiveWindow entirely and fire the command unawaited.
        var isTerminalConsole = action.ModelId.Equals("default/Terminal", StringComparison.OrdinalIgnoreCase)
                              && cfg.Settings.TerminalUseConsoleWindow;
        if (isTerminalConsole)
        {
            var resolved = cfg.ResolveModel(action.ModelId);
            if (resolved is { } r)
            {
                _ = Task.Run(async () =>
                {
                    try { await new AiClient().CompleteAsync("default", r.provider, r.model, action, selectedText, "", CancellationToken.None); }
                    catch (Exception ex) { LogService.Error("Terminal console launch failed", ex); }
                });
            }
            return;
        }

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

        var keyName = parts[^1].Trim();
        if (keyName == "`") { key = Key.OemTilde; return true; } // no Key enum name for it
        // "0"–"9": Enum.TryParse would read a bare digit as the enum's numeric value
        // ("1" → (Key)1 = Key.Cancel), not the digit key — the enum names are D0–D9.
        if (keyName.Length == 1 && char.IsAsciiDigit(keyName[0])) { key = Key.D0 + (keyName[0] - '0'); return true; }
        return Enum.TryParse(keyName, true, out key) && key != Key.None;
    }
}

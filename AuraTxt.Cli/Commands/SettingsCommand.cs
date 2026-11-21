using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Commands;

public class SettingsCommand(ConfigService config)
{
    public Task<int> ExecuteAsync(string[] args) => Task.FromResult(
        args.Length == 0 || args[0] == "--show" ? Show() :
        args[0] == "--set" ? Set(ArgParser.Parse(args[1..])) : PrintHelp());

    private int Show()
    {
        var s = config.Load().Settings;
        Console.WriteLine($"font-size:      {s.FontSize}");
        Console.WriteLine($"opacity:        {s.ResultWindowOpacity}");
        Console.WriteLine($"delay-ms:       {s.MenuTriggerDelayMs}");
        Console.WriteLine($"target-lang:    {s.TargetLanguage}");
        Console.WriteLine($"theme:          {s.Theme}");
        Console.WriteLine($"voice:          {s.SpeechVoice}");
        Console.WriteLine($"prompt-editor:  {(string.IsNullOrEmpty(s.PromptEditor) ? "(notepad.exe)" : s.PromptEditor)}");
        Console.WriteLine($"config-editor:  {(string.IsNullOrEmpty(s.ConfigEditor) ? "(auracfg)" : s.ConfigEditor)}");
        Console.WriteLine($"terminal-console: {(s.TerminalUseConsoleWindow ? "on" : "off")}");
        Console.WriteLine($"force-popup-key: {(string.IsNullOrEmpty(s.ForcePopupDoubleTapKey) ? "none" : s.ForcePopupDoubleTapKey)}");
        Console.WriteLine($"auto-close:     {(s.AutoCloseOnUnfocus ? "on" : "off")}");
        Console.WriteLine($"clipboard-history: {(s.ClipboardHistoryEnabled ? "on" : "off")}");
        Console.WriteLine($"clipboard-hotkey:{(string.IsNullOrEmpty(s.ClipboardHistoryHotkey) ? "none" : s.ClipboardHistoryHotkey)}");
        Console.WriteLine($"clipboard-size: {s.ClipboardHistorySize}");
        Console.WriteLine($"clipboard-plain-key: {s.ClipboardHistoryPlainTextKey}");
        return 0;
    }

    private int Set(Dictionary<string, string> opts)
    {
        var cfg = config.Load();
        var s   = cfg.Settings;
        if (opts.TryGetValue("font-size", out var fs) && int.TryParse(fs, out var fsi))
            s.FontSize = fsi;
        if (opts.TryGetValue("opacity",   out var op) && double.TryParse(op, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var opd))
            s.ResultWindowOpacity = Math.Clamp(opd, 0.1, 1.0);
        if (opts.TryGetValue("delay-ms",  out var dm) && int.TryParse(dm, out var dmi))
            s.MenuTriggerDelayMs = Math.Max(0, dmi);
        if (opts.TryGetValue("target-lang", out var tl) && !string.IsNullOrWhiteSpace(tl))
            s.TargetLanguage = tl.Trim();
        if (opts.TryGetValue("theme", out var th) && !string.IsNullOrWhiteSpace(th))
            s.Theme = th.Trim();
        if (opts.TryGetValue("voice", out var vc) && !string.IsNullOrWhiteSpace(vc))
            s.SpeechVoice = vc.Trim();
        if (opts.TryGetValue("prompt-editor", out var pe))
            s.PromptEditor = pe.Trim();
        if (opts.TryGetValue("config-editor", out var ce))
            s.ConfigEditor = ce.Trim();
        if (opts.TryGetValue("terminal-console", out var tc) && bool.TryParse(tc, out var tcb))
            s.TerminalUseConsoleWindow = tcb;
        if (opts.TryGetValue("auto-close", out var ac) && bool.TryParse(ac, out var acb))
            s.AutoCloseOnUnfocus = acb;
        if (opts.TryGetValue("clipboard-history", out var chv) && bool.TryParse(chv, out var chb))
            s.ClipboardHistoryEnabled = chb;
        if (opts.TryGetValue("clipboard-size", out var cs) && int.TryParse(cs, out var csi))
            s.ClipboardHistorySize = Math.Clamp(csi, 1, AppSettings.ClipboardHistoryMaxSize);
        if (opts.TryGetValue("clipboard-plain-key", out var cp))
        {
            var match = AppSettings.ClipboardPlainTextKeys.FirstOrDefault(k => k.Equals(cp.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                Console.WriteLine($"✗ Invalid --clipboard-plain-key: \"{cp}\" (one of {string.Join(", ", AppSettings.ClipboardPlainTextKeys)})");
                return 1;
            }
            s.ClipboardHistoryPlainTextKey = match;
        }
        if (opts.TryGetValue("clipboard-hotkey", out var ch))
        {
            if (string.IsNullOrWhiteSpace(ch) || ch.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                s.ClipboardHistoryHotkey = "";
            }
            else
            {
                // RegisterHotKey-backed like action hotkeys → same validator and defaults.
                var (res, conflict) = new HotkeyValidator().Validate(ch, cfg.Actions);
                if (res != HotkeyValidationResult.Valid)
                {
                    Console.WriteLine(res == HotkeyValidationResult.Conflict
                        ? $"✗ \"{ch}\" is already used as action \"{conflict}\"'s hotkey"
                        : $"✗ Invalid --clipboard-hotkey: \"{ch}\" (e.g. Ctrl+`, Alt+V, or \"none\")");
                    return 1;
                }
                s.ClipboardHistoryHotkey = ch;
            }
        }
        if (opts.TryGetValue("force-popup-key", out var fp))
        {
            if (string.IsNullOrWhiteSpace(fp) || fp.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                s.ForcePopupDoubleTapKey = "";
            }
            else
            {
                // Same validator as action hotkeys (AuraTxt.Cli.Commands.ActionCommand),
                // just requireModifier:false + CtrlAltShiftOnly — see
                // GlobalHookService.LoadForcePopupCombo for why "Win" isn't allowed here.
                var (res, conflict) = new HotkeyValidator().Validate(fp, cfg.Actions,
                    requireModifier: false, allowedModifiers: HotkeyValidator.CtrlAltShiftOnly);
                if (res == HotkeyValidationResult.InvalidFormat)
                {
                    Console.WriteLine($"✗ Invalid --force-popup-key format: \"{fp}\" (e.g. F1, Ctrl+F1, or \"none\")");
                    return 1;
                }
                if (res == HotkeyValidationResult.SystemReserved)
                {
                    Console.WriteLine($"✗ System reserved key: {fp}");
                    return 1;
                }
                if (res == HotkeyValidationResult.Conflict)
                {
                    Console.WriteLine($"✗ \"{fp}\" is already used as action \"{conflict}\"'s hotkey");
                    return 1;
                }
                s.ForcePopupDoubleTapKey = fp;
            }
        }
        config.Save(cfg);
        Console.WriteLine("✓ Settings saved");
        return 0;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("auracfg settings --show");
        Console.WriteLine("auracfg settings --set [--font-size <n>] [--opacity <0-1>] [--delay-ms <n>] [--target-lang <code>] [--theme <id>] [--voice <name>] [--prompt-editor <exe>] [--config-editor <exe>] [--terminal-console true|false] [--force-popup-key <combo>|none] [--auto-close true|false] [--clipboard-history true|false] [--clipboard-hotkey <combo>|none] [--clipboard-size <1-100>] [--clipboard-plain-key Shift+Enter|Ctrl+Enter|Alt+Enter]");
        return 1;
    }
}

using System.Windows.Forms;
using AuraTxt.Core.Services;
using AuraTxt.Core.Util;

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
        if (opts.TryGetValue("force-popup-key", out var fp))
        {
            var spec = KeyComboSpec.TryParse(fp);
            if (spec is not { } parsed || (!parsed.IsEmpty && !Enum.TryParse<Keys>(parsed.Key, true, out _)))
            {
                Console.WriteLine($"✗ Invalid --force-popup-key: \"{fp}\" (e.g. F1, Ctrl+F1, or \"none\")");
                return 1;
            }
            s.ForcePopupDoubleTapKey = parsed.IsEmpty ? "" : parsed.ToString();
        }
        config.Save(cfg);
        Console.WriteLine("✓ Settings saved");
        return 0;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("auracfg settings --show");
        Console.WriteLine("auracfg settings --set [--font-size <n>] [--opacity <0-1>] [--delay-ms <n>] [--target-lang <code>] [--theme <id>] [--voice <name>] [--prompt-editor <exe>] [--config-editor <exe>] [--terminal-console true|false] [--force-popup-key <combo>|none]");
        return 1;
    }
}

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
        Console.WriteLine($"font-size:     {s.FontSize}");
        Console.WriteLine($"opacity:       {s.ResultWindowOpacity}");
        Console.WriteLine($"delay-ms:      {s.MenuTriggerDelayMs}");
        Console.WriteLine($"target-lang:   {s.TargetLanguage}");
        Console.WriteLine($"theme:         {s.Theme}");
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
        config.Save(cfg);
        Console.WriteLine("✓ Settings saved");
        return 0;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("auracfg settings --show");
        Console.WriteLine("auracfg settings --set [--font-size <n>] [--opacity <0-1>] [--delay-ms <n>] [--target-lang <code>] [--theme <id>]");
        return 1;
    }
}

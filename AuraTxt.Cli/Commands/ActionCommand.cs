using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Commands;

public class ActionCommand(ConfigService config)
{
    private readonly HotkeyValidator _hv = new();

    public Task<int> ExecuteAsync(string[] args) => Task.FromResult(args.Length == 0
        ? PrintHelp()
        : args[0] switch
        {
            "--list"   => List(),
            "--set"    => Set(ArgParser.Parse(args[1..])),
            "--update" => Update(ArgParser.Parse(args[1..])),
            "--delete" => Delete(ArgParser.Parse(args[1..])),
            _          => PrintHelp()
        });

    private int List()
    {
        var cfg = config.Load();
        if (!cfg.Actions.Any()) { Console.WriteLine("（无配置动作）"); return 0; }
        Console.WriteLine($"{"ID",-15} {"名称",-15} {"模型",-20} {"交互",-6} {"快捷键"}");
        Console.WriteLine(new string('-', 70));
        foreach (var a in cfg.Actions)
            Console.WriteLine($"{a.Id,-15} {a.Name,-15} {a.ModelId,-20} {a.IsInteractive,-6} {a.Hotkey}");
        return 0;
    }

    private int Set(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id", out var id)) return Err("Missing --id");
        var cfg = config.Load();

        if (opts.TryGetValue("hotkey", out var hk) && !string.IsNullOrEmpty(hk))
        {
            var (res, conflict) = _hv.Validate(hk, cfg.Actions.Where(a => a.Id != id));
            if (res == HotkeyValidationResult.InvalidFormat)
                return Err($"Invalid hotkey format: {hk} (example: Alt+T)");
            if (res == HotkeyValidationResult.SystemReserved)
                return Err($"System reserved key: {hk}", 2);
            if (res == HotkeyValidationResult.Conflict)
                return Err($"Hotkey {hk} already used by \"{conflict}\"", 2);
        }

        if (opts.TryGetValue("model-id", out var mid) && !string.IsNullOrEmpty(mid)
            && !mid.StartsWith("$") && !mid.Contains('/'))
            return Err($"ModelId \"{mid}\" must use format providerId/TargetModel (e.g. openai/gpt-4o)", 1);

        var idx  = cfg.Actions.FindIndex(a => a.Id == id);
        var item = new ActionItem
        {
            Id            = id,
            Name          = opts.GetValueOrDefault("name", ""),
            Icon          = opts.GetValueOrDefault("icon", ""),
            ModelId       = opts.GetValueOrDefault("model-id", ""),
            IsInteractive = opts.GetValueOrDefault("interactive", "false") == "true",
            Hotkey        = opts.GetValueOrDefault("hotkey", ""),
            Prompt        = opts.GetValueOrDefault("prompt", ""),
            Enabled       = opts.GetValueOrDefault("enabled", "true") == "true"
        };

        if (idx >= 0) cfg.Actions[idx] = item;
        else          cfg.Actions.Add(item);
        config.Save(cfg);
        Console.WriteLine($"✓ Action '{id}' saved");
        return 0;
    }

    private int Update(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id", out var id)) return Err("缺少 --id");
        var cfg  = config.Load();
        var item = cfg.Actions.FirstOrDefault(a => a.Id == id);
        if (item is null) return Err($"未找到动作 '{id}'", 2);

        if (opts.TryGetValue("hotkey", out var hk) && !string.IsNullOrEmpty(hk))
        {
            var (res, conflict) = _hv.Validate(hk, cfg.Actions, excludeId: id);
            if (res == HotkeyValidationResult.InvalidFormat)
                return Err($"快捷键格式无效：{hk}");
            if (res == HotkeyValidationResult.SystemReserved)
                return Err($"系统保留热键：{hk}", 2);
            if (res == HotkeyValidationResult.Conflict)
                return Err($"快捷键 {hk} 已被「{conflict}」使用", 2);
            item.Hotkey = hk;
        }

        if (opts.TryGetValue("name",        out var n))  item.Name          = n;
        if (opts.TryGetValue("icon",        out var ic)) item.Icon          = ic;
        if (opts.TryGetValue("model-id",    out var m))  item.ModelId       = m;
        if (opts.TryGetValue("prompt",      out var p))  item.Prompt        = p;
        if (opts.TryGetValue("interactive", out var iv)) item.IsInteractive = iv == "true";
        if (opts.TryGetValue("enabled",     out var en)) item.Enabled      = en == "true";

        config.Save(cfg);
        Console.WriteLine($"✓ 动作 '{id}' 已更新");
        return 0;
    }

    private int Delete(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id", out var id)) return Err("Missing --id");
        var cfg  = config.Load();
        var item = cfg.Actions.FirstOrDefault(a => a.Id == id);
        if (item is null) return Err($"Action '{id}' not found", 2);

        if (item.IsSystem) return Err($"Cannot delete system action '{id}'", 2);

        cfg.Actions.RemoveAll(a => a.Id == id);
        config.Save(cfg);
        Console.WriteLine("✓ Deleted");
        return 0;
    }

    private static int Err(string msg, int code = 1)
    {
        Console.Error.WriteLine(msg);
        return code;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("auracfg action --list");
        Console.WriteLine("auracfg action --set    --id <id> --name <name> --icon <lucide> --model-id <id> --interactive <true|false> --prompt \"<text>\" [--hotkey <key>] [--enabled <true|false>]");
        Console.WriteLine("auracfg action --update --id <id> [any field including --enabled]");
        Console.WriteLine("auracfg action --delete --id <id>");
        return 1;
    }
}

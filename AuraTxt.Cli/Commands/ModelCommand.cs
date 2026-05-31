using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Commands;

public class ModelCommand(ConfigService config)
{
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
        if (!cfg.Models.Any()) { Console.WriteLine("（无配置平台）"); return 0; }
        Console.WriteLine($"{"ID",-15} {"名称",-20} {"别名"}");
        Console.WriteLine(new string('-', 60));
        foreach (var (id, m) in cfg.Models)
            Console.WriteLine($"{id,-15} {m.DisplayName,-20} {m.Alias}");
        return 0;
    }

    private int Set(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id", out var id)) return Err("缺少 --id");
        var cfg = config.Load();
        cfg.Models[id] = new ModelPlatform
        {
            DisplayName = opts.GetValueOrDefault("display", ""),
            BaseUrl     = opts.GetValueOrDefault("url", ""),
            ApiKey      = opts.GetValueOrDefault("key", ""),
            TargetModel = opts.GetValueOrDefault("model", "")
        };
        config.Save(cfg);
        Console.WriteLine($"✓ 平台 '{id}' 已保存");
        return 0;
    }

    private int Update(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id", out var id)) return Err("缺少 --id");
        var cfg = config.Load();
        if (!cfg.Models.TryGetValue(id, out var m)) return Err($"未找到平台 '{id}'", 2);
        if (opts.TryGetValue("display", out var d)) m.DisplayName = d;
        if (opts.TryGetValue("url",     out var u)) m.BaseUrl     = u;
        if (opts.TryGetValue("key",     out var k)) m.ApiKey      = k;
        if (opts.TryGetValue("model",   out var v)) m.TargetModel = v;
        config.Save(cfg);
        Console.WriteLine($"✓ 平台 '{id}' 已更新");
        return 0;
    }

    private int Delete(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id", out var id)) return Err("缺少 --id");
        if (id.StartsWith('$')) return Err("系统服务不可删除", 2);
        var cfg   = config.Load();
        if (!cfg.Models.ContainsKey(id)) return Err($"未找到平台 '{id}'", 2);
        var bound = cfg.Actions.Where(a => a.ModelId == id).ToList();
        bool force = opts.ContainsKey("force");
        if (bound.Count > 0 && !force)
        {
            Console.Error.WriteLine($"平台 '{id}' 被以下动作绑定：");
            bound.ForEach(a => Console.Error.WriteLine($"  - {a.Name} ({a.Id})"));
            Console.Error.WriteLine("加 --force 强制删除（连同相关动作）");
            return 2;
        }
        if (force) cfg.Actions.RemoveAll(a => a.ModelId == id);
        cfg.Models.Remove(id);
        config.Save(cfg);
        Console.WriteLine("✓ 已删除");
        return 0;
    }

    private static int Err(string msg, int code = 1)
    {
        Console.Error.WriteLine(msg);
        return code;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("auracfg model --list");
        Console.WriteLine("auracfg model --set    --id <id> --display <name> --url <url> --key <key> --model <model>");
        Console.WriteLine("auracfg model --update --id <id> [--display|--url|--key|--model]");
        Console.WriteLine("auracfg model --delete --id <id> [--force]");
        return 1;
    }
}

using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Commands;

public class ProviderCommand(ConfigService config)
{
    public Task<int> ExecuteAsync(string[] args) => Task.FromResult(args.Length == 0
        ? PrintHelp()
        : args[0] switch
        {
            "--list"         => List(),
            "--set"          => Set(ArgParser.Parse(args[1..])),
            "--add-model"    => AddModel(ArgParser.Parse(args[1..])),
            "--update"       => Update(ArgParser.Parse(args[1..])),
            "--delete"       => Delete(ArgParser.Parse(args[1..])),
            "--delete-model" => DeleteModel(ArgParser.Parse(args[1..])),
            "--update-model" => UpdateModel(ArgParser.Parse(args[1..])),
            _                => PrintHelp()
        });

    private int List()
    {
        var cfg = config.Load();
        if (!cfg.Models.Any()) { Console.WriteLine("(no providers configured)"); return 0; }
        Console.WriteLine($"{"ID",-14} {"Name",-16} Models");
        Console.WriteLine(new string('-', 55));
        foreach (var (id, p) in cfg.Models)
        {
            var aliases = string.Join(", ", p.Models.Select(m => m.Alias));
            Console.WriteLine($"{id,-14} {p.DisplayName,-16} {aliases}");
        }
        return 0;
    }

    private int Set(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id", out var id)) return Err("Missing --id");
        if (id == "default") return Err("Cannot modify built-in 'default' provider", 2);
        var cfg = config.Load();
        cfg.Models[id] = new ProviderConfig
        {
            DisplayName = opts.GetValueOrDefault("display", ""),
            BaseUrl     = opts.GetValueOrDefault("url", ""),
            ApiKey      = opts.GetValueOrDefault("key", ""),
            Models      = new()
        };
        if (opts.TryGetValue("model", out var tm))
            cfg.Models[id].Models.Add(new ModelEntry
            {
                TargetModel = tm,
                Alias       = opts.GetValueOrDefault("alias", tm),
                ProfileId   = opts.GetValueOrDefault("profile", "")
            });
        config.Save(cfg);
        Console.WriteLine($"✓ Provider '{id}' saved");
        return 0;
    }

    private int AddModel(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id", out var id))    return Err("Missing --id");
        if (!opts.TryGetValue("model", out var tm)) return Err("Missing --model");
        if (id == "default") return Err("Cannot modify built-in 'default' provider", 2);
        var cfg = config.Load();
        if (!cfg.Models.TryGetValue(id, out var p)) return Err($"Provider '{id}' not found", 2);
        p.Models.Add(new ModelEntry
        {
            TargetModel = tm,
            Alias       = opts.GetValueOrDefault("alias", tm),
            ProfileId   = opts.GetValueOrDefault("profile", "")
        });
        config.Save(cfg);
        Console.WriteLine($"✓ Model '{tm}' added to '{id}'");
        return 0;
    }

    private int Update(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id", out var id)) return Err("Missing --id");
        if (id == "default") return Err("Cannot modify built-in 'default' provider", 2);
        var cfg = config.Load();
        if (!cfg.Models.TryGetValue(id, out var p)) return Err($"Provider '{id}' not found", 2);
        if (opts.TryGetValue("display", out var d)) p.DisplayName = d;
        if (opts.TryGetValue("url",     out var u)) p.BaseUrl     = u;
        if (opts.TryGetValue("key",     out var k)) p.ApiKey      = k;
        config.Save(cfg);
        Console.WriteLine($"✓ Provider '{id}' updated");
        return 0;
    }

    private int Delete(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id", out var id)) return Err("Missing --id");
        if (id == "default") return Err("Cannot delete built-in 'default' provider", 2);
        var cfg   = config.Load();
        if (!cfg.Models.ContainsKey(id)) return Err($"Provider '{id}' not found", 2);
        var bound = cfg.Actions.Where(a => a.ModelId.StartsWith($"{id}/")).ToList();
        bool force = opts.ContainsKey("force");
        if (bound.Count > 0 && !force)
        {
            Console.Error.WriteLine($"Provider '{id}' is used by {bound.Count} action(s):");
            bound.ForEach(a => Console.Error.WriteLine($"  - {a.Name} ({a.Id})"));
            Console.Error.WriteLine("Use --force to delete along with all bound actions");
            return 2;
        }
        if (force) cfg.Actions.RemoveAll(a => a.ModelId.StartsWith($"{id}/"));
        cfg.Models.Remove(id);
        config.Save(cfg);
        Console.WriteLine("✓ Deleted");
        return 0;
    }

    private int DeleteModel(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id",    out var id)) return Err("Missing --id");
        if (!opts.TryGetValue("model", out var tm)) return Err("Missing --model");
        if (id == "default") return Err("Cannot modify built-in 'default' provider", 2);
        var cfg = config.Load();
        if (!cfg.Models.TryGetValue(id, out var p)) return Err($"Provider '{id}' not found", 2);
        if (p.Models.FirstOrDefault(m => m.TargetModel == tm) is null)
            return Err($"Model '{tm}' not found in provider '{id}'", 2);
        var modelRef = $"{id}/{tm}";
        var bound = cfg.Actions.Where(a => a.ModelId == modelRef).ToList();
        if (bound.Count > 0)
        {
            Console.Error.WriteLine($"Model '{tm}' is used by {bound.Count} action(s):");
            bound.ForEach(a => Console.Error.WriteLine($"  - {a.Name} ({a.Id})"));
            return 2;
        }
        p.Models.RemoveAll(m => m.TargetModel == tm);
        config.Save(cfg);
        Console.WriteLine($"✓ Model '{tm}' removed from '{id}'");
        if (p.Models.Count == 0) Console.WriteLine($"  Warning: provider '{id}' now has no models");
        return 0;
    }

    private int UpdateModel(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id",    out var id)) return Err("Missing --id");
        if (!opts.TryGetValue("model", out var tm)) return Err("Missing --model");
        if (id == "default") return Err("Cannot modify built-in 'default' provider", 2);
        var cfg = config.Load();
        if (!cfg.Models.TryGetValue(id, out var p)) return Err($"Provider '{id}' not found", 2);
        var entry = p.Models.FirstOrDefault(m => m.TargetModel == tm);
        if (entry is null) return Err($"Model '{tm}' not found in provider '{id}'", 2);
        if (opts.TryGetValue("alias",   out var al)) entry.Alias     = al;
        if (opts.TryGetValue("profile", out var pf)) entry.ProfileId = pf;
        if (opts.TryGetValue("enabled", out var en)) entry.Enabled   = en == "true";
        config.Save(cfg);
        Console.WriteLine($"✓ Model '{tm}' in '{id}' updated");
        return 0;
    }

    private static int Err(string msg, int code = 1)
    {
        Console.Error.WriteLine(msg);
        return code;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("auracfg provider --list");
        Console.WriteLine("auracfg provider --set    --id <id> --display <name> --url <url> --key <key> [--model <name>] [--alias <alias>] [--thinking]");
        Console.WriteLine("auracfg provider --add-model --id <id> --model <name> [--alias <alias>] [--thinking]");
        Console.WriteLine("auracfg provider --update --id <id> [--display|--url|--key]");
        Console.WriteLine("auracfg provider --delete       --id <id> [--force]");
        Console.WriteLine("auracfg provider --delete-model --id <id> --model <name>");
        Console.WriteLine("auracfg provider --update-model --id <id> --model <name> [--alias <alias>] [--thinking|--no-thinking] [--enabled <true|false>]");
        Console.WriteLine("Note: --thinking enables thinking; --no-thinking disables it (default)");
        return 1;
    }
}

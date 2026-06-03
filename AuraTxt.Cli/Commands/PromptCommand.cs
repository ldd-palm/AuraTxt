using System.IO;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Commands;

public class PromptCommand(ConfigService config)
{
    public Task<int> ExecuteAsync(string[] args) => Task.FromResult(args.Length == 0
        ? PrintHelp()
        : args[0] switch
        {
            "--list"   => List(),
            "--show"   => Show(ArgParser.Parse(args[1..])),
            "--add"    => Add(ArgParser.Parse(args[1..])),
            "--update" => Update(ArgParser.Parse(args[1..])),
            "--delete" => Delete(ArgParser.Parse(args[1..])),
            _          => PrintHelp()
        });

    private int List()
    {
        PromptService.EnsureScaffold();
        var prompts = PromptService.ListPrompts();
        if (prompts.Count == 0) { Console.WriteLine("(no prompts)"); return 0; }

        var cfg = config.Load();
        Console.WriteLine($"Prompts folder: {PromptService.PromptsDir}");
        foreach (var p in prompts)
        {
            var used = Users(cfg, p);
            var tag  = used.Count > 0 ? $"used by: {string.Join(",", used)}" : "unused";
            Console.WriteLine($"  {Path.GetFileName(p),-28} {tag}");
        }
        return 0;
    }

    private int Show(Dictionary<string, string> o)
    {
        if (!o.TryGetValue("name", out var name)) return Err("Missing --name");
        var path = Path.Combine(PromptService.PromptsDir, $"{name}.md");
        if (!File.Exists(path)) return Err($"Prompt '{name}' not found", 2);
        Console.WriteLine(File.ReadAllText(path));
        return 0;
    }

    private int Add(Dictionary<string, string> o)
    {
        if (!o.TryGetValue("name", out var name)) return Err("Missing --name");
        if (PromptService.Exists(name)) return Err($"Prompt '{name}' already exists", 2);
        try
        {
            var path = PromptService.CreateFromTemplate(name);
            WriteContentIfProvided(o, path);
            Console.WriteLine($"✓ Created {path}");
            return 0;
        }
        catch (Exception ex) { return Err(ex.Message); }
    }

    private int Update(Dictionary<string, string> o)
    {
        if (!o.TryGetValue("name", out var name)) return Err("Missing --name");
        var path = Path.Combine(PromptService.PromptsDir, $"{name}.md");
        if (!File.Exists(path)) return Err($"Prompt '{name}' not found", 2);
        if (!WriteContentIfProvided(o, path))
            return Err("Provide --content \"<text>\" or --file <path>");
        Console.WriteLine($"✓ Updated {name}.md");
        return 0;
    }

    private int Delete(Dictionary<string, string> o)
    {
        if (!o.TryGetValue("name", out var name)) return Err("Missing --name");
        var path = Path.Combine(PromptService.PromptsDir, $"{name}.md");
        if (!File.Exists(path)) return Err($"Prompt '{name}' not found", 2);

        var cfg  = config.Load();
        var used = Users(cfg, path);
        if (used.Count > 0)
            return Err($"'{name}' is in use by: {string.Join(",", used)}. Unmount first.", 2);

        File.Delete(path);
        Console.WriteLine($"✓ Deleted {name}.md");
        return 0;
    }

    private static bool WriteContentIfProvided(Dictionary<string, string> o, string path)
    {
        if (o.TryGetValue("content", out var content)) { File.WriteAllText(path, content); return true; }
        if (o.TryGetValue("file", out var src) && File.Exists(src)) { File.Copy(src, path, overwrite: true); return true; }
        return false;
    }

    private static List<string> Users(ConfigRoot cfg, string path)
    {
        var u = cfg.Actions.Where(a => SamePath(a.Prompt, path)).Select(a => a.Id).ToList();
        if (SamePath(cfg.Settings.SystemPrompt, path)) u.Add("(system prompt)");
        return u;
    }

    private static bool SamePath(string? a, string b)
    {
        if (string.IsNullOrEmpty(a)) return false;
        try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    private static int Err(string msg, int code = 1)
    {
        Console.Error.WriteLine(msg);
        return code;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("auracfg prompt --list");
        Console.WriteLine("auracfg prompt --show   --name <name>");
        Console.WriteLine("auracfg prompt --add    --name <name> [--content \"<text>\" | --file <path>]");
        Console.WriteLine("auracfg prompt --update --name <name> (--content \"<text>\" | --file <path>)");
        Console.WriteLine("auracfg prompt --delete --name <name>");
        return 1;
    }
}

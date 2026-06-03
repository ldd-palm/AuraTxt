using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Commands;

public class ShowCommand(ConfigService config)
{
    public Task<int> ExecuteAsync(string[] args)
    {
        var cfg = config.Load();

        if (args.Length == 0)
            return Task.FromResult(ShowOverview(cfg));

        return Task.FromResult(args[0] switch
        {
            "provider" => ShowProvider(cfg, args.Skip(1).ToArray()),
            "action"   => ShowAction(cfg, args.Skip(1).ToArray()),
            _          => ShowOverview(cfg)
        });
    }

    private static int ShowOverview(ConfigRoot cfg)
    {
        Dim();
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("  AuraTxt Config");
        Console.WriteLine("═══════════════════════════════════════════════════");
        Reset();

        // Providers
        Console.WriteLine();
        Bold(); Console.Write("Providers"); Reset();
        Console.WriteLine($" ({cfg.Models.Count})");
        Console.WriteLine();

        foreach (var (id, p) in cfg.Models.OrderBy(kv => kv.Key))
        {
            var tag = id == "default" ? " [built-in]" : "";
            Cyan();  Console.Write($"  {id}"); Reset();
            Dim();   Console.WriteLine(tag);
            Console.WriteLine($"    Display: {p.DisplayName}");
            Console.Write("    Models:  ");
            if (!p.Models.Any())
            {
                Dim(); Console.WriteLine("(none)"); Reset();
            }
            else
            {
                Console.WriteLine();
                foreach (var m in p.Models)
                {
                    var thinking = m.DisableThinking ? "off" : "on";
                    Console.WriteLine($"      • {m.TargetModel}  (alias: {m.Alias}, thinking: {thinking})");
                }
            }
            if (!string.IsNullOrEmpty(p.BaseUrl))
                Console.WriteLine($"    URL:     {p.BaseUrl}");
            Console.Write("    API Key: ");
            Console.WriteLine(MaskKey(p.ApiKey));
        }

        // Actions
        Console.WriteLine();
        Bold(); Console.Write("Actions"); Reset();
        Console.WriteLine($" ({cfg.Actions.Count})");
        Console.WriteLine();

        foreach (var a in cfg.Actions
            .OrderBy(a => a.Enabled ? 0 : 1)
            .ThenBy(a => a.Order)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
        {
            var hk   = string.IsNullOrEmpty(a.Hotkey) ? "—" : a.Hotkey;
            var model = string.IsNullOrEmpty(a.ModelId) ? "—" : FormatModelRef(cfg, a.ModelId);
            var tag  = a.IsSystem ? " [system]" : "";

            Cyan();  Console.Write($"  {a.Id}"); Reset();
            Dim();   Console.WriteLine(tag);
            Console.WriteLine($"    Name:    {a.Name}");
            Console.WriteLine($"    Icon:    {a.Icon}");
            Console.WriteLine($"    Model:   {model}");
            Console.WriteLine($"    Hotkey:  {hk}");
            Console.Write("    Status:  ");
            PrintEnabled(a.Enabled);
            Console.WriteLine();
            Console.WriteLine($"    Order:   {a.Order}");

            if (!a.IsSystem)
            {
                Console.WriteLine($"    Interactive: {a.IsInteractive}");
                if (!string.IsNullOrEmpty(a.Prompt))
                {
                    var lines = a.Prompt.Split('\n');
                    Console.WriteLine($"    Prompt:  {lines[0]}");
                    for (int i = 1; i < lines.Length; i++)
                        Console.WriteLine($"             {lines[i]}");
                }
            }
        }

        // Settings
        Console.WriteLine();
        Bold(); Console.Write("Settings"); Reset();
        Console.WriteLine();
        Console.WriteLine();
        var s = cfg.Settings;
        Console.WriteLine($"    Font Size:         {s.FontSize}");
        Console.WriteLine($"    Window Opacity:    {s.ResultWindowOpacity}");
        Console.WriteLine($"    Trigger Delay:     {s.MenuTriggerDelayMs} ms");
        Console.WriteLine($"    Target Language:   {s.TargetLanguage}");
        Console.WriteLine();
        Bold(); Console.Write("System Prompt"); Reset();
        Console.WriteLine(" (global wrapper — prepended before each action prompt)");
        Console.WriteLine();
        if (string.IsNullOrEmpty(s.SystemPrompt))
        {
            Dim(); Console.WriteLine("    (not set)"); Reset();
        }
        else
        {
            foreach (var line in s.SystemPrompt.Split('\n'))
                Console.WriteLine($"    │ {line}");
        }

        Console.WriteLine();
        return 0;
    }

    private static int ShowProvider(ConfigRoot cfg, string[] args)
    {
        if (args.Length > 0)
        {
            var id = args[0];
            if (!cfg.Models.TryGetValue(id, out var p))
                return Err($"Provider '{id}' not found");

            Bold(); Console.Write("Provider: "); Cyan(); Console.WriteLine(id); Reset();
            var tag = id == "default" ? " [built-in]" : "";
            if (id == "default") { Dim(); Console.WriteLine(tag); Reset(); }
            Console.WriteLine();

            PrintLabel("Display", p.DisplayName);
            PrintLabel("Base URL", string.IsNullOrEmpty(p.BaseUrl) ? "—" : p.BaseUrl);
            PrintLabel("API Key", MaskKey(p.ApiKey));
            Console.WriteLine();

            Bold(); Console.WriteLine($"Models ({p.Models.Count})"); Reset();
            Console.WriteLine();
            foreach (var m in p.Models)
            {
                var thinking = m.DisableThinking ? "off" : "on";
                Console.WriteLine($"  • {m.TargetModel}");
                PrintLabel("    Alias", m.Alias);
                PrintLabel("    Thinking", thinking);
                Console.WriteLine();
            }
            return 0;
        }

        // List all
        Bold(); Console.WriteLine("Providers"); Reset();
        Console.WriteLine();
        foreach (var (id, p) in cfg.Models.OrderBy(kv => kv.Key))
        {
            var tag = id == "default" ? " [built-in]" : "";
            Cyan();  Console.Write($"  {id}"); Reset();
            Dim();   Console.WriteLine(tag);
            Console.WriteLine($"    Display:    {p.DisplayName}");
            Console.WriteLine($"    Models:     {p.Models.Count}");
            Console.Write("    Model list: ");
            if (!p.Models.Any())
            {
                Dim(); Console.WriteLine("(none)"); Reset();
            }
            else
            {
                Console.WriteLine(string.Join(", ", p.Models.Select(m => m.Alias)));
            }
            Console.Write("    API Key:    ");
            Console.WriteLine(MaskKey(p.ApiKey));
            Console.WriteLine();
        }
        return 0;
    }

    private static int ShowAction(ConfigRoot cfg, string[] args)
    {
        if (args.Length > 0)
        {
            var id = args[0];
            var a  = cfg.Actions.FirstOrDefault(x => x.Id == id);
            if (a is null) return Err($"Action '{id}' not found");

            Bold(); Console.Write("Action: "); Cyan(); Console.WriteLine(id); Reset();
            var tag = a.IsSystem ? " [system]" : "";
            if (a.IsSystem) { Dim(); Console.WriteLine(tag); Reset(); }
            Console.WriteLine();

            PrintLabel("Name", a.Name);
            PrintLabel("Icon", a.Icon);
            PrintLabel("Hotkey", string.IsNullOrEmpty(a.Hotkey) ? "—" : a.Hotkey);
            Console.Write("  Status:    ");
            PrintEnabled(a.Enabled);
            Console.WriteLine();
            PrintLabel("Order", a.Order.ToString());

            if (!a.IsSystem)
            {
                var model = string.IsNullOrEmpty(a.ModelId) ? "—" : FormatModelRef(cfg, a.ModelId);
                PrintLabel("Model", model);
                PrintLabel("Interactive", a.IsInteractive.ToString());
                Console.WriteLine("  Prompt:");
                if (string.IsNullOrEmpty(a.Prompt))
                {
                    Dim(); Console.WriteLine("    (empty)"); Reset();
                }
                else
                {
                    foreach (var line in a.Prompt.Split('\n'))
                        Console.WriteLine($"    │ {line}");
                }
            }
            return 0;
        }

        // List all
        Bold(); Console.WriteLine("Actions"); Reset();
        Console.WriteLine();
        foreach (var a in cfg.Actions
            .OrderBy(a => a.Enabled ? 0 : 1)
            .ThenBy(a => a.Order)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
        {
            var hk    = string.IsNullOrEmpty(a.Hotkey) ? "—" : a.Hotkey;
            var model = string.IsNullOrEmpty(a.ModelId) ? "—" : FormatModelRef(cfg, a.ModelId);
            var tag   = a.IsSystem ? " [system]" : "";

            Cyan();  Console.Write($"  {a.Id}"); Reset();
            Dim();   Console.WriteLine(tag);
            Console.WriteLine($"    Name:        {a.Name}");
            Console.WriteLine($"    Icon:        {a.Icon}");
            Console.WriteLine($"    Model:       {model}");
            Console.WriteLine($"    Hotkey:      {hk}");
            Console.Write("    Status:      ");
            PrintEnabled(a.Enabled);
            Console.WriteLine();
            Console.WriteLine($"    Order:       {a.Order}");

            if (!a.IsSystem)
            {
                Console.WriteLine($"    Interactive: {a.IsInteractive}");
                if (!string.IsNullOrEmpty(a.Prompt))
                {
                    var firstLine = a.Prompt.Split('\n')[0];
                    var truncated = firstLine.Length > 60 ? firstLine[..60] + "…" : firstLine;
                    Console.WriteLine($"    Prompt:      {truncated}");
                }
            }
            Console.WriteLine();
        }
        return 0;
    }

    // ─── helpers ───

    private static void PrintLabel(string label, string value)
    {
        Console.Write($"  {label}:".PadRight(18));
        Console.WriteLine(value);
    }

    private static string FormatModelRef(ConfigRoot cfg, string modelRef)
    {
        var r = cfg.ResolveModel(modelRef);
        return r is null ? modelRef : $"{r.Value.provider.DisplayName} / {r.Value.model.Alias}";
    }

    private static string MaskKey(string key) =>
        string.IsNullOrEmpty(key) ? "(not set)" :
        key.Length <= 8 ? new string('•', key.Length) :
        key[..4] + new string('•', Math.Min(8, key.Length - 4));

    private static void PrintEnabled(bool enabled)
    {
        if (enabled) { Green(); Console.Write("enabled"); }
        else         { Dim();  Console.Write("disabled"); }
        Reset();
    }

    private static int Err(string msg, int code = 2)
    {
        Red(); Console.WriteLine(msg); Reset();
        return code;
    }

    private static void Bold()    => Console.Write("\x1b[1m");
    private static void Dim()     => Console.Write("\x1b[2m");
    private static void Cyan()    => Console.Write("\x1b[36m");
    private static void Green()   => Console.Write("\x1b[32m");
    private static void Red()     => Console.Write("\x1b[31m");
    private static void Reset()   => Console.Write("\x1b[0m");
}

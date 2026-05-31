using AuraTxt.Cli.Commands;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Menus;

public class InteractiveMenu(ConfigService configService)
{
    private ConfigRoot _cfg = null!;
    private bool _dirty;

    public async Task RunAsync()
    {
        _cfg   = configService.Load();
        _dirty = false;

        while (true)
        {
            Console.Clear();
            H1("AuraTxt Config Tool");
            Item("1", "Model Platform");
            Item("2", "Action Features");
            Item("3", "UI Settings");
            Item("D", "Doctor — Validate Config");
            Item("X", "Exit");
            Prompt();

            var key = char.ToUpper(ReadKey());
            switch (key)
            {
                case '1': ModelPlatformMenu(); break;
                case '2': ActionFeaturesMenu(); break;
                case '3': UiSettingsMenu(); break;
                case 'D': RunDoctor(); break;
                case 'X': await ExitAsync(); return;
            }
        }
    }

    // ──────────────────────────── MODEL PLATFORM ────────────────────────────

    private void ModelPlatformMenu()
    {
        while (true)
        {
            Console.Clear();
            H1("Model Platform");
            var providers = _cfg.Models
                .Where(kv => kv.Key != "default")
                .OrderBy(kv => kv.Key)
                .ToList();

            for (int i = 0; i < providers.Count; i++)
            {
                var (id, p) = providers[i];
                var aliases = string.Join(", ", p.Models.Select(m => m.Alias));
                Item((i + 1).ToString(), $"{p.DisplayName,-16}", $"({aliases})");
            }

            Sep();
            Item("0", "Back");
            Item("A", "Add Provider");
            Item("D", "Delete Provider");
            Item("T", "Test Model");
            Item("X", "Exit");
            Prompt();

            var input = Console.ReadLine()?.Trim().ToUpper() ?? "";

            if (input == "0") return;
            if (input == "X") Environment.Exit(0);
            if (input == "A") { AddProviderFlow(); continue; }
            if (input == "D") { DeleteProviderFlow(providers); continue; }
            if (input == "T") { TestModelFlow(); continue; }

            if (int.TryParse(input, out var idx) && idx >= 1 && idx <= providers.Count)
                ProviderDetailMenu(providers[idx - 1].Key);
        }
    }

    private void AddProviderFlow()
    {
        Console.Clear();
        H2("Add Provider");
        var id = Ask("Provider ID (e.g. openai)");
        if (string.IsNullOrWhiteSpace(id)) return;
        if (_cfg.Models.ContainsKey(id)) { WriteError($"Provider '{id}' already exists."); Pause(); return; }

        var display = Ask("Display Name");
        var url     = Ask("Base URL (e.g. https://api.openai.com/v1)");
        var key     = AskSecret("API Key");

        Console.WriteLine();
        H3("Add first model");
        var targetModel = Ask("Model full name (e.g. gpt-4o)");
        var alias       = Ask($"Alias/short name [{targetModel}]");
        if (string.IsNullOrWhiteSpace(alias)) alias = targetModel;

        var provider = new ProviderConfig
        {
            DisplayName = display,
            BaseUrl     = url,
            ApiKey      = key,
            Models      = new() { new ModelEntry { TargetModel = targetModel, Alias = alias } }
        };

        while (true)
        {
            Console.Write("  Add another model? (Y/n): ");
            var ans = char.ToLower(ReadKey());
            Console.WriteLine();
            if (ans == 'n') break;
            var tm2 = Ask("  Model full name");
            var al2 = Ask($"  Alias [{tm2}]");
            if (string.IsNullOrWhiteSpace(al2)) al2 = tm2;
            provider.Models.Add(new ModelEntry { TargetModel = tm2, Alias = al2 });
        }

        _cfg.Models[id] = provider;
        _dirty = true;
        WriteSuccess($"Provider '{id}' added ({provider.Models.Count} model(s)).");
        Pause();
    }

    private void DeleteProviderFlow(List<KeyValuePair<string, ProviderConfig>> providers)
    {
        if (!providers.Any()) { WriteError("No user providers to delete."); Pause(); return; }
        Console.WriteLine("  Enter number of provider to delete (0 to cancel): ");
        for (int i = 0; i < providers.Count; i++)
            Console.WriteLine($"    [{i + 1}] {providers[i].Value.DisplayName} ({providers[i].Key})");
        Console.Write("  Select: ");
        if (!int.TryParse(Console.ReadLine()?.Trim(), out var idx) || idx < 1 || idx > providers.Count) return;

        var (pid, _) = providers[idx - 1];
        var bound = _cfg.Actions.Where(a => a.ModelId.StartsWith($"{pid}/")).ToList();
        if (bound.Any())
        {
            WriteWarning($"Provider '{pid}' is used by {bound.Count} action(s): " +
                         string.Join(", ", bound.Select(a => a.Name)));
            Console.Write("  Delete provider and all bound actions? (y/N): ");
            if (char.ToLower(ReadKey()) != 'y') { Console.WriteLine(); return; }
            Console.WriteLine();
            _cfg.Actions.RemoveAll(a => a.ModelId.StartsWith($"{pid}/"));
        }

        _cfg.Models.Remove(pid);
        _dirty = true;
        WriteSuccess($"Provider '{pid}' deleted.");
        Pause();
    }

    private void ProviderDetailMenu(string providerId)
    {
        while (true)
        {
            Console.Clear();
            var p = _cfg.Models[providerId];
            H2($"Provider: {p.DisplayName}");

            Item("1", "Base URL ", $": {p.BaseUrl}");
            Item("2", "API Key  ", $": {MaskKey(p.ApiKey)}");

            for (int i = 0; i < p.Models.Count; i++)
            {
                var m  = p.Models[i];
                var th = m.DisableThinking ? "off" : "on";
                Item((i + 3).ToString(), $"Model    ", $": {m.TargetModel,-20} (alias: {m.Alias,-10} | thinking: {th})");
            }

            Sep();
            Item("0", "Back");
            Item("A", "Add Model");
            Item("D", "Delete Model");
            Item("X", "Exit");
            Prompt();

            var input = Console.ReadLine()?.Trim().ToUpper() ?? "";
            if (input == "0") return;
            if (input == "X") Environment.Exit(0);

            if (input == "1")
            {
                var newUrl = Ask($"New Base URL [{p.BaseUrl}]");
                if (!string.IsNullOrWhiteSpace(newUrl)) { p.BaseUrl = newUrl; _dirty = true; }
                continue;
            }
            if (input == "2")
            {
                var newKey = AskSecret("New API Key");
                if (!string.IsNullOrWhiteSpace(newKey)) { p.ApiKey = newKey; _dirty = true; }
                continue;
            }
            if (input == "A")
            {
                var tm = Ask("Model full name");
                var al = Ask($"Alias [{tm}]");
                if (string.IsNullOrWhiteSpace(al)) al = tm;
                p.Models.Add(new ModelEntry { TargetModel = tm, Alias = al });
                _dirty = true;
                WriteSuccess("Model added.");
                Pause();
                continue;
            }
            if (input == "D")
            {
                if (!p.Models.Any()) { WriteError("No models to delete."); Pause(); continue; }
                Console.Write("  Enter model number to delete: ");
                if (int.TryParse(Console.ReadLine()?.Trim(), out var mi))
                {
                    var modelIdx = mi - 3;
                    if (modelIdx >= 0 && modelIdx < p.Models.Count)
                    {
                        var removed = p.Models[modelIdx].TargetModel;
                        p.Models.RemoveAt(modelIdx);
                        _cfg.Actions.RemoveAll(a => a.ModelId == $"{providerId}/{removed}");
                        _dirty = true;
                        WriteSuccess($"Model '{removed}' removed.");
                        Pause();
                    }
                }
                continue;
            }

            if (int.TryParse(input, out var num) && num >= 3 && num < 3 + p.Models.Count)
                ModelDetailMenu(p.Models[num - 3]);
        }
    }

    private void ModelDetailMenu(ModelEntry model)
    {
        while (true)
        {
            Console.Clear();
            H2($"Model: {model.TargetModel}");
            Item("1", "Full Name       ", $": {model.TargetModel}");
            Item("2", "Alias           ", $": {model.Alias}");
            Item("3", "Disable Thinking", $": {(model.DisableThinking ? "on" : "off")}");
            Sep();
            Item("0", "Back");
            Prompt();

            var input = Console.ReadLine()?.Trim() ?? "";
            if (input == "0") return;
            if (input == "1")
            {
                var v = Ask($"New full name [{model.TargetModel}]");
                if (!string.IsNullOrWhiteSpace(v)) { model.TargetModel = v; _dirty = true; }
            }
            else if (input == "2")
            {
                var v = Ask($"New alias [{model.Alias}]");
                if (!string.IsNullOrWhiteSpace(v)) { model.Alias = v; _dirty = true; }
            }
            else if (input == "3")
            {
                model.DisableThinking = !model.DisableThinking;
                _dirty = true;
                WriteSuccess($"Disable Thinking → {(model.DisableThinking ? "on" : "off")}");
                Pause();
            }
        }
    }

    private void TestModelFlow()
    {
        var testable = _cfg.Models
            .Where(kv => kv.Key != "default")
            .SelectMany(kv => kv.Value.Models.Select(m => (Ref: $"{kv.Key}/{m.TargetModel}", Provider: kv.Value, Model: m)))
            .ToList();

        if (!testable.Any()) { WriteError("No user models to test. Add a provider first."); Pause(); return; }

        Console.Clear();
        H2("Test Model");
        for (int i = 0; i < testable.Count; i++)
            Item((i + 1).ToString(), testable[i].Ref);
        Item("0", "Cancel");
        Prompt();

        if (!int.TryParse(Console.ReadLine()?.Trim(), out var idx) || idx < 1 || idx > testable.Count) return;
        var (modelRef, prov, mod) = testable[idx - 1];

        Console.Write($"  Testing {modelRef}...");
        try
        {
            var sw     = System.Diagnostics.Stopwatch.StartNew();
            var client = new AiClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var result = client.CompleteAsync(prov, mod, "Hello, respond with OK only.", cts.Token).GetAwaiter().GetResult();
            sw.Stop();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($" ✓ Response: {result.Trim()} ({sw.Elapsed.TotalSeconds:F1}s)");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($" ✗ {ex.Message}");
            Console.ResetColor();
        }
        Pause();
    }

    // ──────────────────────────── ACTION FEATURES ────────────────────────────

    private void ActionFeaturesMenu()
    {
        while (true)
        {
            Console.Clear();
            H1("Action Features");
            for (int i = 0; i < _cfg.Actions.Count; i++)
            {
                var a  = _cfg.Actions[i];
                var hk = string.IsNullOrEmpty(a.Hotkey) ? "—" : a.Hotkey;
                Item((i + 1).ToString(), $"{a.Name,-18}", $"({hk} | {a.ModelId})");
            }

            Sep();
            Item("0", "Back");
            Item("A", "Add Action");
            Item("D", "Delete Action");
            Item("X", "Exit");
            Prompt();

            var input = Console.ReadLine()?.Trim().ToUpper() ?? "";
            if (input == "0") return;
            if (input == "X") Environment.Exit(0);
            if (input == "A") { AddActionFlow(); continue; }
            if (input == "D") { DeleteActionFlow(); continue; }

            if (int.TryParse(input, out var idx) && idx >= 1 && idx <= _cfg.Actions.Count)
                ActionDetailMenu(_cfg.Actions[idx - 1]);
        }
    }

    private void AddActionFlow()
    {
        Console.Clear();
        H2("Add Action");

        var id = Ask("Action ID (unique key, e.g. translate)");
        if (string.IsNullOrWhiteSpace(id)) return;
        if (_cfg.Actions.Any(a => a.Id == id)) { WriteError($"Action '{id}' already exists."); Pause(); return; }

        var name = Ask("Display Name (e.g. Quick Translate)");
        Console.WriteLine("  💡 Find icons at https://lucide.dev/icons/");
        var icon = Ask("Icon name (e.g. languages)");

        var modelId = SelectModel();
        if (modelId is null) return;

        Console.Write("  Interactive action? (y/N): ");
        var isInteractive = char.ToLower(ReadKey()) == 'y';
        Console.WriteLine();

        string prompt = "";
        if (!modelId.StartsWith("default/"))
            prompt = Ask("Prompt text (use {SelectedText} and {UserInput} placeholders)");
        else
            WriteGray("  (No prompt needed — selected text passed directly to built-in service)");

        var hotkey = HotkeyCapture.Capture(_cfg.Actions);

        _cfg.Actions.Add(new ActionItem
        {
            Id            = id,
            Name          = name,
            Icon          = icon,
            ModelId       = modelId,
            IsInteractive = isInteractive,
            Prompt        = prompt,
            Hotkey        = hotkey
        });
        _dirty = true;
        WriteSuccess($"Action '{name}' added.");
        Pause();
    }

    private void DeleteActionFlow()
    {
        if (!_cfg.Actions.Any()) { WriteError("No actions to delete."); Pause(); return; }
        for (int i = 0; i < _cfg.Actions.Count; i++)
            Console.WriteLine($"    [{i + 1}] {_cfg.Actions[i].Name} ({_cfg.Actions[i].Id})");
        Console.Write("  Enter number to delete (0 to cancel): ");
        if (!int.TryParse(Console.ReadLine()?.Trim(), out var idx) || idx < 1 || idx > _cfg.Actions.Count) return;
        var name = _cfg.Actions[idx - 1].Name;
        _cfg.Actions.RemoveAt(idx - 1);
        _dirty = true;
        WriteSuccess($"Action '{name}' deleted.");
        Pause();
    }

    private void ActionDetailMenu(ActionItem action)
    {
        while (true)
        {
            Console.Clear();
            var hk = string.IsNullOrEmpty(action.Hotkey) ? "(none)" : action.Hotkey;
            H2($"Action: {action.Name}");
            Item("1", "Name       ", $": {action.Name}");
            Item("2", "Icon       ", $": {action.Icon}");
            Item("3", "Model      ", $": {action.ModelId}");
            Item("4", "Prompt     ", $": {Truncate(action.Prompt, 50)}");
            Item("5", "Hotkey     ", $": {hk}");
            Item("6", "Interactive", $": {action.IsInteractive}");
            Sep();
            Item("0", "Back");
            Item("X", "Exit");
            Prompt();

            var input = Console.ReadLine()?.Trim() ?? "";
            if (input == "0") return;
            if (input.ToUpper() == "X") Environment.Exit(0);

            switch (input)
            {
                case "1":
                    var n = Ask($"New name [{action.Name}]");
                    if (!string.IsNullOrWhiteSpace(n)) { action.Name = n; _dirty = true; }
                    break;
                case "2":
                    Console.WriteLine("  💡 Find icons at https://lucide.dev/icons/");
                    var ic = Ask($"New icon [{action.Icon}]");
                    if (!string.IsNullOrWhiteSpace(ic)) { action.Icon = ic; _dirty = true; }
                    break;
                case "3":
                    var mid = SelectModel();
                    if (mid is not null) { action.ModelId = mid; _dirty = true; }
                    break;
                case "4":
                    if (action.ModelId.StartsWith("default/"))
                    { WriteGray("  Built-in service: no prompt needed."); Pause(); break; }
                    var pr = Ask($"New prompt (current: {Truncate(action.Prompt, 40)})");
                    if (!string.IsNullOrWhiteSpace(pr)) { action.Prompt = pr; _dirty = true; }
                    break;
                case "5":
                    var newHk = HotkeyCapture.Capture(_cfg.Actions, excludeId: action.Id);
                    action.Hotkey = newHk;
                    _dirty = true;
                    if (!string.IsNullOrEmpty(newHk)) WriteSuccess($"Hotkey set to {newHk}.");
                    Pause();
                    break;
                case "6":
                    action.IsInteractive = !action.IsInteractive;
                    _dirty = true;
                    WriteSuccess($"Interactive → {action.IsInteractive}");
                    Pause();
                    break;
            }
        }
    }

    // ──────────────────────────── UI SETTINGS ────────────────────────────

    private void UiSettingsMenu()
    {
        while (true)
        {
            Console.Clear();
            var s = _cfg.Settings;
            H1("UI Settings");
            Item("1", "Font Size      ", $": {s.FontSize}");
            Item("2", "Window Opacity ", $": {s.ResultWindowOpacity}");
            Item("3", "Trigger Delay  ", $": {s.MenuTriggerDelayMs} ms");
            Sep();
            Item("0", "Back");
            Item("X", "Exit");
            Prompt();

            var input = Console.ReadLine()?.Trim() ?? "";
            if (input == "0") return;
            if (input.ToUpper() == "X") Environment.Exit(0);

            switch (input)
            {
                case "1":
                    Console.Write($"  Font size [{s.FontSize}]: ");
                    if (int.TryParse(Console.ReadLine()?.Trim(), out var fs) && fs > 0)
                    { s.FontSize = fs; _dirty = true; WriteSuccess($"Font size → {fs}"); Pause(); }
                    break;
                case "2":
                    Console.Write($"  Opacity (0.1–1.0) [{s.ResultWindowOpacity}]: ");
                    if (double.TryParse(Console.ReadLine()?.Trim(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var op))
                    {
                        s.ResultWindowOpacity = Math.Clamp(op, 0.1, 1.0);
                        _dirty = true;
                        WriteSuccess($"Opacity → {s.ResultWindowOpacity}");
                        Pause();
                    }
                    break;
                case "3":
                    Console.Write($"  Trigger delay ms [{s.MenuTriggerDelayMs}]: ");
                    if (int.TryParse(Console.ReadLine()?.Trim(), out var dm) && dm >= 0)
                    { s.MenuTriggerDelayMs = dm; _dirty = true; WriteSuccess($"Delay → {dm} ms"); Pause(); }
                    break;
            }
        }
    }

    // ──────────────────────────── HELPERS ────────────────────────────

    private void RunDoctor()
    {
        Console.Clear();
        var tmpPath = Path.GetTempFileName();
        try
        {
            var tmpSvc = new ConfigService(tmpPath);
            tmpSvc.Save(_cfg);
            new DoctorCommand(tmpSvc).Execute();
        }
        finally { File.Delete(tmpPath); }
        Pause();
    }

    private async Task ExitAsync()
    {
        if (!_dirty) return;
        Console.WriteLine();
        Console.Write("  Changes detected. Save before exit? (Y/n): ");
        var ans = char.ToLower(ReadKey());
        Console.WriteLine();
        if (ans == 'n' || ans == 'q')
        {
            WriteGray("  Changes discarded.");
            return;
        }
        configService.SaveWithBackup(_cfg);
        WriteSuccess("  Config saved (backup written to config.json.bak).");
        await Task.Delay(800);
    }

    private string? SelectModel()
    {
        var all = new List<(string Ref, string Label)>(_cfg.AllModelRefs());
        if (!all.Any()) { WriteError("No models available. Add a provider first."); Pause(); return null; }

        Console.WriteLine("  Available models:");
        for (int i = 0; i < all.Count; i++)
            Console.WriteLine($"    [{i + 1}] {all[i].Label}");
        Console.Write("  Select model (0 to cancel): ");
        if (!int.TryParse(Console.ReadLine()?.Trim(), out var idx) || idx < 1 || idx > all.Count) return null;
        return all[idx - 1].Ref;
    }

    // ──── Console UI primitives ────

    private static void H1(string title)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"=== {title} ===");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void H2(string title)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"--- {title} ---");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void H3(string title)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {title}");
        Console.ResetColor();
    }

    private static void Item(string key, string label, string? value = null)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  [{key}] ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(label);
        if (value is not null)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(value);
        }
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void Sep()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  ─────────────────────");
        Console.ResetColor();
    }

    private static void Prompt()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("  Select: ");
        Console.ResetColor();
    }

    private static void WriteSuccess(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ {msg}");
        Console.ResetColor();
    }

    private static void WriteError(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ✗ {msg}");
        Console.ResetColor();
    }

    private static void WriteWarning(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ! {msg}");
        Console.ResetColor();
    }

    private static void WriteGray(string msg)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(msg);
        Console.ResetColor();
    }

    private static void Pause()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  Press any key to continue...");
        Console.ResetColor();
        Console.ReadKey(true);
    }

    private static char ReadKey() => Console.ReadKey(intercept: true).KeyChar;

    private static string Ask(string prompt)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  {prompt}: ");
        Console.ResetColor();
        return Console.ReadLine()?.Trim() ?? "";
    }

    private static string AskSecret(string prompt)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  {prompt}: ");
        Console.ResetColor();
        var sb = new System.Text.StringBuilder();
        while (true)
        {
            var k = Console.ReadKey(intercept: true);
            if (k.Key == ConsoleKey.Enter) { Console.WriteLine(); break; }
            if (k.Key == ConsoleKey.Backspace && sb.Length > 0)
            { sb.Remove(sb.Length - 1, 1); Console.Write("\b \b"); continue; }
            if (k.KeyChar != '\0') { sb.Append(k.KeyChar); Console.Write('•'); }
        }
        return sb.ToString();
    }

    private static string MaskKey(string key) =>
        string.IsNullOrEmpty(key) ? "(not set)" :
        key.Length <= 8 ? new string('•', key.Length) :
        key[..4] + new string('•', Math.Min(8, key.Length - 4));

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

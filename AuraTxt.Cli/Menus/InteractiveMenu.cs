using AuraTxt.Cli.Commands;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Menus;

public class InteractiveMenu(ConfigService configService)
{
    private ConfigRoot _cfg = null!;
    private bool _dirty;

    public Task RunAsync()
    {
        PromptService.EnsureScaffold();
        _cfg   = configService.Load();
        _dirty = false;

        while (true)
        {
            Console.Clear();
            H1("AuraTxt Config Tool");
            Item("1", "General Settings");
            Item("2", "Model Platform");
            Item("3", "Prompt Library");
            Item("4", "Action Features");
            Item("D", "Doctor — Validate Config");
            Item("S", "Save Config");
            Item("X", "Exit");
            Prompt();

            var key = char.ToUpper(ReadKey());
            switch (key)
            {
                case '1': GeneralSettingsMenu(); break;
                case '2': ModelPlatformMenu(); break;
                case '3': PromptLibraryMenu(); break;
                case '4': ActionFeaturesMenu(); break;
                case 'D': RunDoctor(); break;
                case 'S': SaveNow(); break;
                case 'X': ExitFlow(); break;
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
                var enabled  = p.Models.Where(m => m.Enabled).Select(m => m.Alias).ToList();
                var disabled = p.Models.Where(m => !m.Enabled).Select(m => m.Alias).ToList();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [{(i + 1)}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{p.DisplayName,-16} ");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("(");

                if (enabled.Count > 0)
                {
                    Console.Write(string.Join(", ", enabled));
                    if (disabled.Count > 0) Console.Write(", ");
                }

                if (disabled.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write(string.Join(", ", disabled));
                }
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(")");
                Console.ResetColor();
            }

            Sep();
            Item("B", "Back");
            Item("A", "Add Provider");
            Item("D", "Delete Provider");
            Item("T", "Test Model");
            Item("S", "Save Config");
            Item("X", "Exit");
            Prompt();

            var input = Console.ReadLine()?.Trim().ToUpper() ?? "";

            if (input == "B") return;
            if (input == "X") ExitFlow();
            if (input == "A") { AddProviderFlow(); continue; }
            if (input == "D") { DeleteProviderFlow(providers); continue; }
            if (input == "T") { TestModelFlow(); continue; }
            if (input == "S") { SaveNow(); continue; }

            if (int.TryParse(input, out var idx) && idx >= 1 && idx <= providers.Count)
                ProviderDetailMenu(providers[idx - 1].Key);
        }
    }

    private void AddProviderFlow()
    {
        Console.Clear();
        H2("Add Provider");
        var id = Ask("Provider ID (no spaces, e.g. openai)");
        if (string.IsNullOrWhiteSpace(id)) return;
        if (id.Contains(' ')) { WriteError("Provider ID cannot contain spaces."); Pause(); return; }
        if (_cfg.Models.ContainsKey(id)) { WriteError($"Provider '{id}' already exists."); Pause(); return; }

        var url = Ask("Base URL (e.g. https://api.openai.com/v1)");
        var key = AskSecret("API Key");

        Console.WriteLine();
        H3("Add first model");
        var targetModel = Ask("Model full name (e.g. gpt-4o)");
        var alias       = Ask("Alias/short name", targetModel);
        if (string.IsNullOrWhiteSpace(alias)) alias = targetModel;

        var provider = new ProviderConfig
        {
            DisplayName = id,
            BaseUrl     = url,
            ApiKey      = key,
            Models      = new() { new ModelEntry { TargetModel = targetModel, Alias = alias, Enabled = true } }
        };

        while (true)
        {
            Console.Write("  Add another model? (Y/n): ");
            var ans = char.ToLower(ReadKey());
            Console.WriteLine();
            if (ans == 'n') break;
            var tm2 = Ask("  Model full name");
            var al2 = Ask("  Alias", tm2);
            if (string.IsNullOrWhiteSpace(al2)) al2 = tm2;
            provider.Models.Add(new ModelEntry { TargetModel = tm2, Alias = al2, Enabled = true });
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
            WriteWarning($"Provider '{pid}' is used by {bound.Count} action(s):");
            foreach (var a in bound)
            {
                var mLabel = ModelLabel(a.ModelId);
                Console.WriteLine($"      — {a.Name} ({a.Id}) → {mLabel}");
            }
            WriteError("Update or delete those actions first before removing this provider.");
            Pause();
            return;
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
                var status = m.Enabled ? "enabled" : "disabled";

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [{(i + 3)}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"Model    ");
                Console.ForegroundColor = m.Enabled ? ConsoleColor.White : ConsoleColor.DarkGray;
                Console.Write($": {m.TargetModel,-20} (alias: {m.Alias,-10} | thinking: {th} | ");
                Console.ForegroundColor = m.Enabled ? ConsoleColor.Green : ConsoleColor.DarkGray;
                Console.Write($"{status}");
                Console.ForegroundColor = m.Enabled ? ConsoleColor.White : ConsoleColor.DarkGray;
                Console.Write(")");
                Console.ResetColor();
                Console.WriteLine();
            }

            Sep();
            Item("B", "Back");
            Item("A", "Add Model");
            Item("D", "Delete Model");
            Prompt();

            var input = Console.ReadLine()?.Trim().ToUpper() ?? "";
            if (input == "B") return;

            if (input == "1")
            {
                var newUrl = Ask("New Base URL", p.BaseUrl);
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
                var al = Ask("Alias", tm);
                if (string.IsNullOrWhiteSpace(al)) al = tm;
                p.Models.Add(new ModelEntry { TargetModel = tm, Alias = al, Enabled = true });
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
                        var target    = p.Models[modelIdx].TargetModel;
                        var modelRef  = $"{providerId}/{target}";
                        var bound = _cfg.Actions.Where(a => a.ModelId == modelRef).ToList();
                        if (bound.Any())
                        {
                            WriteWarning($"Model '{target}' is used by {bound.Count} action(s):");
                            foreach (var a in bound)
                                Console.WriteLine($"      — {a.Name} ({a.Id})");
                            WriteError("Update or delete those actions first before removing this model.");
                            Pause();
                        }
                        else
                        {
                            p.Models.RemoveAt(modelIdx);
                            _dirty = true;
                            WriteSuccess($"Model '{target}' removed.");
                            Pause();
                        }
                    }
                }
                continue;
            }

            if (int.TryParse(input, out var num) && num >= 3 && num < 3 + p.Models.Count)
                ModelDetailMenu(p.Models[num - 3], providerId);
        }
    }

    private void ModelDetailMenu(ModelEntry model, string providerId)
    {
        while (true)
        {
            Console.Clear();
            H2($"Model: {model.TargetModel}");
            Item("1", "Full Name       ", $": {model.TargetModel}");
            Item("2", "Alias           ", $": {model.Alias}");
            Item("3", "Disable Thinking", $": {(model.DisableThinking ? "on" : "off")}");
            Item("4", "Status          ", $": {(model.Enabled ? "enabled" : "disabled")}");
            Sep();
            Item("B", "Back");
            Prompt();

            var input = Console.ReadLine()?.Trim().ToUpper() ?? "";
            if (input == "B") return;
            if (input == "1")
            {
                var v = Ask("New full name", model.TargetModel);
                if (!string.IsNullOrWhiteSpace(v)) { model.TargetModel = v; _dirty = true; }
            }
            else if (input == "2")
            {
                var v = Ask("New alias", model.Alias);
                if (!string.IsNullOrWhiteSpace(v)) { model.Alias = v; _dirty = true; }
            }
            else if (input == "3")
            {
                model.DisableThinking = !model.DisableThinking;
                _dirty = true;
                WriteSuccess($"Disable Thinking → {(model.DisableThinking ? "on" : "off")}");
                Pause();
            }
            else if (input == "4")
            {
                // If disabling, check no action is using this model
                if (model.Enabled)
                {
                    var modelRef = $"{providerId}/{model.TargetModel}";
                    var bound = _cfg.Actions.Where(a => a.ModelId == modelRef).ToList();
                    if (bound.Any())
                    {
                        WriteWarning($"Model '{model.TargetModel}' is used by {bound.Count} action(s):");
                        foreach (var a in bound)
                            Console.WriteLine($"      — {a.Name} ({a.Id})");
                        WriteError("Update or delete those actions first before disabling this model.");
                        Pause();
                        break;
                    }
                }
                model.Enabled = !model.Enabled;
                _dirty = true;
                WriteSuccess($"Status → {(model.Enabled ? "enabled" : "disabled")}");
                Pause();
            }
        }
    }

    private void TestModelFlow()
    {
        var testable = _cfg.Models
            .Where(kv => kv.Key != "default")
            .SelectMany(kv => kv.Value.Models.Select(m => (Ref: $"{kv.Key}/{m.TargetModel}", Provider: kv.Value, Model: m)))
            .OrderByDescending(x => x.Model.Enabled)
            .ThenBy(x => x.Ref)
            .ToList();

        if (!testable.Any()) { WriteError("No user models to test. Add a provider first."); Pause(); return; }

        Console.Clear();
        H2("Test Model");
        for (int i = 0; i < testable.Count; i++)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"  [{(i + 1)}] ");
            Console.ForegroundColor = testable[i].Model.Enabled ? ConsoleColor.Green : ConsoleColor.DarkGray;
            Console.WriteLine(testable[i].Ref);
            Console.ResetColor();
        }
        Item("B", "Back");
        Prompt();

        var rawInput = Console.ReadLine()?.Trim() ?? "";
        if (rawInput.Equals("B", StringComparison.OrdinalIgnoreCase)) return;
        if (!int.TryParse(rawInput, out var idx) || idx < 1 || idx > testable.Count) return;
        var (modelRef, prov, mod) = testable[idx - 1];

        Console.Write($"  Testing {modelRef}...");
        try
        {
            var sw     = System.Diagnostics.Stopwatch.StartNew();
            var client = new AiClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var result = client.CompleteAsync(prov, mod, "Hello, respond with OK only.", ct: cts.Token).GetAwaiter().GetResult();
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

    // ──────────────────────────── PROMPT LIBRARY ────────────────────────────

    private void PromptLibraryMenu()
    {
        PromptService.EnsureScaffold();
        while (true)
        {
            Console.Clear();
            H1("Prompt Library");
            WriteGray($"  Folder: {PromptService.PromptsDir}");
            Console.WriteLine();

            var prompts = PromptService.ListPrompts();
            for (int i = 0; i < prompts.Count; i++)
            {
                var name   = Path.GetFileName(prompts[i]);
                var usedBy = PromptUsers(prompts[i]);
                var tag = usedBy.Count > 0 ? $"(used by: {string.Join(", ", usedBy)})" : "(unused)";
                Item((i + 1).ToString(), $"{name,-24}", tag);
            }

            Sep();
            Item("B", "Back");
            Item("A", "Add Prompt");
            Item("D", "Delete Prompt");
            Item("X", "Exit");
            Console.WriteLine();
            WriteGray("  Tip: enter a number to edit that prompt in Notepad.");
            Prompt();

            var input = Console.ReadLine()?.Trim().ToUpper() ?? "";
            if (input == "B") return;
            if (input == "X") ExitFlow();
            if (input == "A") { AddPromptFlow(); continue; }
            if (input == "D") { DeletePromptFlow(prompts); continue; }
            if (int.TryParse(input, out var idx) && idx >= 1 && idx <= prompts.Count)
                OpenInNotepad(prompts[idx - 1]);
        }
    }

    private void AddPromptFlow()
    {
        Console.Clear();
        H2("Add Prompt");
        var name = Ask("Prompt name (no spaces, e.g. summarize)");
        if (string.IsNullOrWhiteSpace(name)) return;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains(' '))
        { WriteError("Name contains invalid characters or spaces."); Pause(); return; }
        if (PromptService.Exists(name)) { WriteError($"Prompt '{name}' already exists."); Pause(); return; }

        var dest = Path.Combine(PromptService.PromptsDir, $"{name}.md");
        Console.WriteLine($"  Will create: {dest}");
        Console.Write("  Confirm? (Y/n): ");
        var ans = char.ToLower(ReadKey()); Console.WriteLine();
        if (ans == 'n') { WriteGray("  Cancelled."); Pause(); return; }

        try
        {
            var path = PromptService.CreateFromTemplate(name);
            WriteSuccess($"Created {name}.md from template — opening Notepad...");
            OpenInNotepad(path);
        }
        catch (Exception ex) { WriteError(ex.Message); }
        Pause();
    }

    private void DeletePromptFlow(List<string> prompts)
    {
        if (prompts.Count == 0) { WriteError("No prompts to delete."); Pause(); return; }
        Console.Write("  Enter number to delete (0 to cancel): ");
        if (!int.TryParse(Console.ReadLine()?.Trim(), out var idx) || idx < 1 || idx > prompts.Count) return;

        var path = prompts[idx - 1];
        var name = Path.GetFileName(path);
        var usedBy = PromptUsers(path);
        if (usedBy.Count > 0)
        {
            WriteError($"'{name}' is in use by: {string.Join(", ", usedBy)}");
            WriteError("Unmount it first.");
            Pause(); return;
        }

        Console.Write($"  Delete '{name}'? (y/N): ");
        var ans = char.ToLower(ReadKey()); Console.WriteLine();
        if (ans != 'y') return;
        try { File.Delete(path); WriteSuccess($"Deleted {name}."); }
        catch (Exception ex) { WriteError(ex.Message); }
        Pause();
    }

    /// Lets the user pick a prompt file (from the library or any absolute path).
    /// Returns the chosen file's absolute path, or null if cancelled / not found.
    private string? SelectPromptFile()
    {
        var prompts = PromptService.ListPrompts();
        Console.WriteLine();
        H3("Select a prompt file");
        for (int i = 0; i < prompts.Count; i++)
            Item((i + 1).ToString(), Path.GetFileName(prompts[i]));
        WriteGray("  Or type an absolute path to any file.");
        Console.Write("  Number or path (blank to cancel): ");
        var input = Console.ReadLine()?.Trim() ?? "";
        if (input.Length == 0) return null;

        string path = int.TryParse(input, out var idx) && idx >= 1 && idx <= prompts.Count
            ? prompts[idx - 1]
            : input.Trim('"');

        if (!File.Exists(path)) { WriteError($"File not found: {path}"); Pause(); return null; }

        Console.WriteLine();
        WriteGray($"  {Path.GetFileName(path)} content:");
        PreviewPrompt(path);
        return Path.GetFullPath(path);
    }

    private static void PreviewPrompt(string path)
    {
        try
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            foreach (var line in File.ReadAllText(path).Split('\n'))
                Console.WriteLine($"    │ {line.TrimEnd('\r')}");
            Console.ResetColor();
        }
        catch { }
    }

    private static void OpenInNotepad(string path)
    {
        try
        {
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "notepad.exe", Arguments = $"\"{path}\"", UseShellExecute = true
            });
            p?.WaitForExit();
        }
        catch (Exception ex) { WriteError($"Cannot open editor: {ex.Message}"); }
    }

    private static bool SamePath(string? a, string b)
    {
        if (string.IsNullOrEmpty(a)) return false;
        try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    /// Who references this prompt file — actions by id, plus the global system prompt.
    private List<string> PromptUsers(string path)
    {
        var users = _cfg.Actions.Where(a => SamePath(a.Prompt, path)).Select(a => a.Id).ToList();
        if (SamePath(_cfg.Settings.SystemPrompt, path)) users.Add("(system prompt)");
        return users;
    }

    // ──────────────────────────── ACTION FEATURES ────────────────────────────

    private void ActionFeaturesMenu()
    {
        while (true)
        {
            Console.Clear();
            H1("Action Features");
            var sorted = _cfg.Actions
                .OrderBy(a => a.Enabled ? 0 : 1)
                .ThenBy(a => a.Order)
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                var a  = sorted[i];
                var hk = string.IsNullOrEmpty(a.Hotkey) ? "—" : a.Hotkey;
                var model = string.IsNullOrEmpty(a.ModelId) ? "—" : ModelLabel(a.ModelId);

                Console.Write($"  [{i + 1}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{a.Id,-18}");
                Console.ResetColor();
                Console.Write($"({hk} | {model} | ");

                if (a.Enabled)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("enabled");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("disabled");
                }
                Console.ResetColor();
                Console.WriteLine($" | {a.Order})");
            }

            Sep();
            Item("B", "Back");
            Item("A", "Add Action");
            Item("D", "Delete Action");
            Item("S", "Save Config");
            Item("X", "Exit");
            Prompt();

            var input = Console.ReadLine()?.Trim().ToUpper() ?? "";
            if (input == "B") return;
            if (input == "X") ExitFlow();
            if (input == "A") { AddActionFlow(); continue; }
            if (input == "D") { DeleteActionFlow(); continue; }
            if (input == "S") { SaveNow(); continue; }

            if (int.TryParse(input, out var idx) && idx >= 1 && idx <= sorted.Count)
                ActionDetailMenu(sorted[idx - 1]);
        }
    }

    private void AddActionFlow()
    {
        Console.Clear();
        H2("Add Action");

        var id = Ask("Action ID (no spaces, e.g. translate)");
        if (string.IsNullOrWhiteSpace(id)) return;
        if (id.Contains(' ')) { WriteError("Action ID cannot contain spaces."); Pause(); return; }
        if (_cfg.Actions.Any(a => a.Id == id)) { WriteError($"Action '{id}' already exists."); Pause(); return; }

        Console.WriteLine("  💡 Find icons at https://lucide.dev/icons/");
        var icon = Ask("Icon name (e.g. languages)");
        if (!string.IsNullOrWhiteSpace(icon))
        {
            Console.Write($"  Downloading icon '{icon}'... ");
            var ok = IconDownloadService.EnsureDownloadedAsync(icon).GetAwaiter().GetResult();
            Console.WriteLine(ok ? "✓" : "✗ (not found on lucide.dev — will use text fallback)");
        }

        var modelId = SelectModel();
        if (modelId is null) return;

        bool isBuiltin     = modelId.StartsWith("default/");
        bool isInteractive = false;
        string prompt      = "";

        if (isBuiltin)
        {
            WriteGray("  (Built-in service — no prompt or interaction needed; selected text is sent directly.)");
        }
        else
        {
            Console.Write("  Interactive action? (y/N): ");
            isInteractive = char.ToLower(ReadKey()) == 'y';
            Console.WriteLine();
            prompt = SelectPromptFile() ?? "";   // stores the prompt file PATH
        }

        var hotkey = HotkeyCapture.Capture(_cfg.Actions);

        Console.Write("  Enable this action? (Y/n): ");
        var enableAns = char.ToLower(ReadKey());
        Console.WriteLine();
        var enabled = enableAns != 'n';

        Console.Write("  Display order (0-99, default 0): ");
        var orderStr = Console.ReadLine()?.Trim();
        int.TryParse(orderStr, out var order);

        _cfg.Actions.Add(new ActionItem
        {
            Id            = id,
            Name          = id,
            Icon          = icon,
            ModelId       = modelId,
            IsInteractive = isInteractive,
            Prompt        = prompt,
            Hotkey        = hotkey,
            Enabled       = enabled,
            Order         = order
        });
        _dirty = true;
        WriteSuccess($"Action '{id}' added.");
        Pause();
    }

    private void DeleteActionFlow()
    {
        var deletable = _cfg.Actions.Where(a => !a.IsSystem).ToList();
        if (!deletable.Any()) { WriteError("No user actions to delete."); Pause(); return; }
        for (int i = 0; i < deletable.Count; i++)
            Console.WriteLine($"    [{i + 1}] {deletable[i].Name} ({deletable[i].Id})");
        Console.Write("  Enter number to delete (0 to cancel): ");
        if (!int.TryParse(Console.ReadLine()?.Trim(), out var idx) || idx < 1 || idx > deletable.Count) return;
        var name = deletable[idx - 1].Name;
        _cfg.Actions.Remove(deletable[idx - 1]);
        _dirty = true;
        WriteSuccess($"Action '{name}' deleted.");
        Pause();
    }

    private void ActionDetailMenu(ActionItem action)
    {
        var isSystem = action.IsSystem;

        while (true)
        {
            Console.Clear();
            var hk  = string.IsNullOrEmpty(action.Hotkey) ? "(none)" : action.Hotkey;
            var tag = isSystem ? " (system)" : "";

            H2($"Action: {action.Id}{tag}");

            if (isSystem)
            {
                // System action: name, icon, hotkey, status, order
                Item("1", "Name  ", $": {action.Name}");
                Item("2", "Icon  ", $": {action.Icon}");
                Item("3", "Hotkey", $": {hk}");
                Console.Write("  [4] Status  : ");
                PrintStatus(action.Enabled);
                Console.WriteLine();
                Item("5", "Order ", $": {action.Order}");
            }
            else
            {
                var isBuiltin = action.ModelId.StartsWith("default/");
                Item("1", "Name       ", $": {action.Name}");
                Item("2", "Icon       ", $": {action.Icon}");
                Item("3", "Model      ", $": {ModelLabel(action.ModelId)}");
                Item("4", "Interactive", $": {(isBuiltin ? "(n/a — built-in)" : action.IsInteractive.ToString())}");
                Item("5", "Prompt     ", $": {(isBuiltin ? "(n/a — built-in)" : PromptLabel(action.Prompt))}");
                Item("6", "Hotkey     ", $": {hk}");
                Console.Write("  [7] Status     : ");
                PrintStatus(action.Enabled);
                Console.WriteLine();
                Item("8", "Order      ", $": {action.Order}");
            }

            Sep();
            Item("B", "Back");
            Prompt();

            var input = Console.ReadLine()?.Trim().ToUpper() ?? "";
            if (input == "B") return;

            if (isSystem)
            {
                switch (input)
                {
                    case "1":
                        var newName = Ask("New name", action.Name);
                        if (!string.IsNullOrWhiteSpace(newName)) { action.Name = newName; _dirty = true; WriteSuccess($"Name → {newName}"); }
                        else WriteGray("  (unchanged)");
                        Pause();
                        break;
                    case "2":
                        Console.WriteLine("  💡 Find icons at https://lucide.dev/icons/");
                        var ic = Ask("New icon", action.Icon);
                        if (!string.IsNullOrWhiteSpace(ic))
                        {
                            action.Icon = ic; _dirty = true;
                            Console.Write($"  Downloading icon '{ic}'... ");
                            var ok = IconDownloadService.EnsureDownloadedAsync(ic).GetAwaiter().GetResult();
                            Console.WriteLine(ok ? "✓" : "✗ (not found on lucide.dev — will use text fallback)");
                        }
                        break;
                    case "3":
                        if (action.Id == "copy")
                        { WriteGray("  Copy action hotkey is fixed (empty)."); Pause(); break; }
                        var newHk = HotkeyCapture.Capture(_cfg.Actions, excludeId: action.Id);
                        action.Hotkey = newHk;
                        _dirty = true;
                        if (!string.IsNullOrEmpty(newHk)) WriteSuccess($"Hotkey set to {newHk}.");
                        Pause();
                        break;
                    case "4":
                        action.Enabled = !action.Enabled;
                        _dirty = true;
                        WriteSuccess($"Status → {(action.Enabled ? "enabled" : "disabled")}");
                        Pause();
                        break;
                    case "5":
                        Console.Write($"  New order (0-99, current {action.Order}): ");
                        var orderStr = Console.ReadLine()?.Trim();
                        if (int.TryParse(orderStr, out var ov)) { action.Order = ov; _dirty = true; WriteSuccess($"Order → {ov}"); }
                        else WriteGray("  (unchanged)");
                        Pause();
                        break;
                }
            }
            else
            {
                switch (input)
                {
                    case "1":
                        var newName = Ask("New name", action.Name);
                        if (!string.IsNullOrWhiteSpace(newName)) { action.Name = newName; _dirty = true; WriteSuccess($"Name → {newName}"); }
                        else WriteGray("  (unchanged)");
                        Pause();
                        break;
                    case "2":
                        Console.WriteLine("  💡 Find icons at https://lucide.dev/icons/");
                        var ic = Ask("New icon", action.Icon);
                        if (!string.IsNullOrWhiteSpace(ic))
                        {
                            action.Icon = ic; _dirty = true;
                            Console.Write($"  Downloading icon '{ic}'... ");
                            var ok = IconDownloadService.EnsureDownloadedAsync(ic).GetAwaiter().GetResult();
                            Console.WriteLine(ok ? "✓" : "✗ (not found on lucide.dev — will use text fallback)");
                        }
                        break;
                    case "3":
                        var mid = SelectModel();
                        if (mid is not null)
                        {
                            action.ModelId = mid;
                            if (mid.StartsWith("default/")) { action.IsInteractive = false; action.Prompt = ""; }
                            _dirty = true;
                        }
                        break;
                    case "4":
                        if (action.ModelId.StartsWith("default/"))
                        { WriteGray("  Built-in service: interaction not applicable."); Pause(); break; }
                        action.IsInteractive = !action.IsInteractive;
                        _dirty = true;
                        WriteSuccess($"Interactive → {action.IsInteractive}");
                        Pause();
                        break;
                    case "5":
                        if (action.ModelId.StartsWith("default/"))
                        { WriteGray("  Built-in service: no prompt needed."); Pause(); break; }
                        var pf = SelectPromptFile();
                        if (pf is not null) { action.Prompt = pf; _dirty = true; }
                        break;
                    case "6":
                        var newHk = HotkeyCapture.Capture(_cfg.Actions, excludeId: action.Id);
                        action.Hotkey = newHk;
                        _dirty = true;
                        if (!string.IsNullOrEmpty(newHk)) WriteSuccess($"Hotkey set to {newHk}.");
                        Pause();
                        break;
                    case "7":
                        action.Enabled = !action.Enabled;
                        _dirty = true;
                        WriteSuccess($"Status → {(action.Enabled ? "enabled" : "disabled")}");
                        Pause();
                        break;
                    case "8":
                        Console.Write($"  New order (0-99, current {action.Order}): ");
                        var orderStr = Console.ReadLine()?.Trim();
                        if (int.TryParse(orderStr, out var ov)) { action.Order = ov; _dirty = true; WriteSuccess($"Order → {ov}"); }
                        else WriteGray("  (unchanged)");
                        Pause();
                        break;
                }
            }
        }
    }

    // ──────────────────────────── GENERAL SETTINGS ────────────────────────────

    private void GeneralSettingsMenu()
    {
        while (true)
        {
            Console.Clear();
            var s = _cfg.Settings;
            H1("General Settings");
            Item("1", "Font Size      ", $": {s.FontSize}");
            Item("2", "Window Opacity ", $": {s.ResultWindowOpacity}");
            Item("3", "Trigger Delay  ", $": {s.MenuTriggerDelayMs} ms");
            Item("4", "System Prompt  ", $": {PromptLabel(s.SystemPrompt)}");
            Item("5", "Target Language", $": {LangLabel(s.TargetLanguage)}");
            Item("6", "Theme          ", $": {s.Theme}");
            Item("7", "Speech Voice   ", $": {s.SpeechVoice}");
            Sep();
            Item("B", "Back");
            Item("X", "Exit");
            Prompt();

            var input = Console.ReadLine()?.Trim().ToUpper() ?? "";
            if (input == "B") return;
            if (input == "X") ExitFlow();

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
                case "4":
                    SystemPromptMenu(s);
                    break;
                case "5":
                    SelectTargetLanguage(s);
                    break;
                case "6":
                    SelectTheme(s);
                    break;
                case "7":
                    SelectVoice(s);
                    break;
            }
        }
    }

    // ──────────────────────────── HELPERS ────────────────────────────

    private void SystemPromptMenu(AppSettings s)
    {
        Console.WriteLine();
        Console.WriteLine($"  Current system prompt file: {s.SystemPrompt}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Sent as a system message before every action prompt.");
        Console.ResetColor();

        // Show current prompt content
        var content = PromptService.Resolve(s.SystemPrompt);
        if (string.IsNullOrWhiteSpace(content))
        {
            WriteGray("  (empty)");
        }
        else
        {
            foreach (var line in content.Split('\n'))
                Console.WriteLine($"  │ {line}");
        }
        Console.WriteLine();

        Console.WriteLine("  [E] Edit current file in Notepad   [P] Point to a different file   [B] Back");
        Console.Write("  Select: ");
        var k = char.ToUpper(ReadKey());
        Console.WriteLine();

        if (k == 'E')
        {
            if (File.Exists(s.SystemPrompt)) OpenInNotepad(s.SystemPrompt);
            else { WriteGray("  Current value is not a file — opening default system.md."); OpenInNotepad(PromptService.SystemFile); }
        }
        else if (k == 'P')
        {
            var np = SelectPromptFile();
            if (np is not null) { s.SystemPrompt = np; _dirty = true; WriteSuccess("System prompt file updated."); Pause(); }
        }
    }

    private static readonly IReadOnlyList<(string Code, string Name)> TranslateLanguages =
    [
        ("zh-CN", "简体中文"),
        ("en",    "English"),
        ("ja",    "日本語"),
        ("ko",    "한국어"),
        ("fr",    "Français"),
        ("de",    "Deutsch"),
        ("es",    "Español"),
        ("pt",    "Português"),
        ("ru",    "Русский"),
        ("ar",    "العربية"),
    ];

    private void SelectTargetLanguage(AppSettings s)
    {
        Console.WriteLine();
        Console.WriteLine($"  Select target language (current: {LangLabel(s.TargetLanguage)}):");
        for (int i = 0; i < TranslateLanguages.Count; i++)
            Console.WriteLine($"    {i + 1}. {TranslateLanguages[i].Name} ({TranslateLanguages[i].Code})");
        Console.WriteLine("  Or type a language code directly (e.g. th, vi, it).");
        Console.Write("  Selection (blank to cancel): ");
        var input = Console.ReadLine()?.Trim() ?? "";
        if (input.Length == 0) return;

        string code;
        if (int.TryParse(input, out var idx) && idx >= 1 && idx <= TranslateLanguages.Count)
            code = TranslateLanguages[idx - 1].Code;
        else
            code = input;

        var name = TranslateLanguages.FirstOrDefault(x => x.Code == code).Name;
        s.TargetLanguage = code;
        _dirty = true;
        WriteSuccess(name is not null
            ? $"Translate to → {name} ({code})"
            : $"Translate to → {code}");
        Pause();
    }

    /// Short label for a prompt reference — file name if it's a path, else truncated inline text.
    private static string PromptLabel(string? p) =>
        string.IsNullOrEmpty(p)                       ? "(none)"
        : (p.Contains('\\') || p.Contains('/'))       ? Path.GetFileName(p)
        :                                               Truncate(p, 50);

    private void SelectTheme(AppSettings s)
    {
        ThemeService.EnsureScaffold();
        var themes = ThemeService.ListThemes();

        Console.WriteLine();
        Console.WriteLine($"  Select theme (current: {s.Theme}):");
        for (int i = 0; i < themes.Count; i++)
            Console.WriteLine($"    {i + 1}. {themes[i].Name} — {themes[i].Description}");
        Console.WriteLine("  Or type a theme ID directly.");
        Console.Write("  Selection (blank to cancel): ");
        var input = Console.ReadLine()?.Trim() ?? "";
        if (input.Length == 0) return;

        string id;
        if (int.TryParse(input, out var idx) && idx >= 1 && idx <= themes.Count)
            id = themes[idx - 1].Id;
        else
            id = input;

        s.Theme = id;
        _dirty = true;
        WriteSuccess($"Theme → {id}");
        Pause();
    }

    private void SelectVoice(AppSettings s)
    {
        var voices = SpeechService.GetInstalledVoices();

        Console.WriteLine();
        Console.WriteLine($"  Select speech voice (current: {s.SpeechVoice}):");
        for (int i = 0; i < voices.Count; i++)
            Console.WriteLine($"    {i + 1}. {voices[i]}");
        Console.WriteLine("  Or type a voice name directly.");
        Console.Write("  Selection (blank to cancel): ");
        var input = Console.ReadLine()?.Trim() ?? "";
        if (input.Length == 0) return;

        string voice;
        if (int.TryParse(input, out var idx) && idx >= 1 && idx <= voices.Count)
            voice = voices[idx - 1];
        else
            voice = input;

        s.SpeechVoice = voice;
        _dirty = true;
        WriteSuccess($"Speech Voice → {voice}");
        Pause();
    }

    private static string LangLabel(string code) =>
        TranslateLanguages.FirstOrDefault(x => x.Code == code) is var (_, name) && name is not null
            ? $"{name} ({code})"
            : code;

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

    /// Save-aware exit. Callable from ANY menu level so X always offers to save.
    private void SaveNow()
    {
        Console.WriteLine();
        configService.SaveWithBackup(_cfg);
        _dirty = false;
        WriteSuccess("Config saved (backup written to config.json.bak).");
        Pause();
    }

    private void ExitFlow()
    {
        if (_dirty)
        {
            Console.WriteLine();
            Console.Write("  Changes detected. Save before exit? (Y/n): ");
            var ans = char.ToLower(ReadKey());
            Console.WriteLine();
            if (ans == 'n' || ans == 'q')
            {
                WriteGray("  Changes discarded.");
            }
            else
            {
                configService.SaveWithBackup(_cfg);
                WriteSuccess("  Config saved (backup written to config.json.bak).");
                System.Threading.Thread.Sleep(800);
            }
        }
        Environment.Exit(0);
    }

    private string? SelectModel()
    {
        var all = new List<(string Ref, string Label)>(_cfg.AllEnabledModelRefs());
        if (!all.Any()) { WriteError("No models available. Add a provider first."); Pause(); return null; }

        Console.WriteLine("  Available models:");
        for (int i = 0; i < all.Count; i++)
            Console.WriteLine($"    [{i + 1}] {all[i].Label}");
        Console.Write("  Select model (0 to cancel): ");
        if (!int.TryParse(Console.ReadLine()?.Trim(), out var idx) || idx < 1 || idx > all.Count) return null;
        return all[idx - 1].Ref;
    }

    /// Friendly label for a model ref like "default/Google_Translate" → "Built-in / GTrans".
    private string ModelLabel(string modelRef)
    {
        var r = _cfg.ResolveModel(modelRef);
        return r is null ? modelRef : $"{r.Value.provider.DisplayName} / {r.Value.model.Alias}";
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

    private static string Ask(string prompt, string? defaultValue = null)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  {prompt}: ");
        Console.ResetColor();
        if (!string.IsNullOrEmpty(defaultValue))
        {
            // Escape SendKeys special chars: + ^ % ~ ( )
            var escaped = System.Text.RegularExpressions.Regex.Replace(defaultValue, @"([+^%~()])", "{$1}");
            SendKeys.SendWait(escaped);
        }
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

    private static void PrintStatus(bool enabled)
    {
        if (enabled)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("enabled");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("disabled");
        }
        Console.ResetColor();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

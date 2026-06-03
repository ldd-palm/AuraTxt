# auracfg Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign auracfg with a two-level Provider→Model data model, full English interactive menu, real hotkey capture, doctor/restore commands, and DisableThinking toggle — plus minimal WPF adapter updates.

**Architecture:** Three sequential phases: (1) Core data model swap (ProviderConfig replaces ModelPlatform, ConfigRoot helpers added), (2) full CLI rewrite (InteractiveMenu + new commands), (3) WPF minimal adapter. Each phase is independently buildable but deploy together.

**Tech Stack:** C# 12 / .NET 8, System.Text.Json, xUnit, existing MouseKeyHook/NHotkey/H.NotifyIcon packages

**Spec:** `C:\Users\ldd\.claude\plans\requirment-md-superpowers-brainstorming-scalable-pnueli.md`

---

## Phase 1 — Core Data Model

### Task 1: ProviderConfig, ModelEntry, ConfigRoot, ActionItem

**Files:**
- Delete: `AuraTxt.Core/Models/ModelPlatform.cs`
- Create: `AuraTxt.Core/Models/ProviderConfig.cs`
- Create: `AuraTxt.Core/Models/ModelEntry.cs`
- Modify: `AuraTxt.Core/Models/ConfigRoot.cs`
- Modify: `AuraTxt.Core/Models/ActionItem.cs`

- [ ] **Step 1: Create `AuraTxt.Core/Models/ModelEntry.cs`**

```csharp
namespace AuraTxt.Core.Models;

public class ModelEntry
{
    public string TargetModel     { get; set; } = "";
    public string Alias           { get; set; } = "";
    public bool   DisableThinking { get; set; } = true;
}
```

- [ ] **Step 2: Create `AuraTxt.Core/Models/ProviderConfig.cs`**

```csharp
namespace AuraTxt.Core.Models;

public class ProviderConfig
{
    public string           DisplayName { get; set; } = "";
    public string           BaseUrl     { get; set; } = "";
    public string           ApiKey      { get; set; } = "";
    public List<ModelEntry> Models      { get; set; } = new();
}
```

- [ ] **Step 3: Replace `AuraTxt.Core/Models/ConfigRoot.cs` entirely**

```csharp
namespace AuraTxt.Core.Models;

public class ConfigRoot
{
    public Dictionary<string, ProviderConfig> Models   { get; set; } = new();
    public List<ActionItem>                   Actions  { get; set; } = new();
    public AppSettings                        Settings { get; set; } = new();

    /// Resolves "openai/gpt-4o" → (ProviderConfig, ModelEntry). Returns null if not found.
    public (ProviderConfig provider, ModelEntry model)? ResolveModel(string modelRef)
    {
        if (string.IsNullOrEmpty(modelRef)) return null;
        var slash = modelRef.IndexOf('/');
        if (slash < 0) return null;
        var providerId  = modelRef[..slash];
        var targetModel = modelRef[(slash + 1)..];
        if (!Models.TryGetValue(providerId, out var p)) return null;
        var m = p.Models.FirstOrDefault(x => x.TargetModel == targetModel);
        return m is null ? null : (p, m);
    }

    /// Returns all model refs for WPF ComboBox.
    /// Order: user providers alphabetically, then default/Google_Translate, then default/Youdao_Dict.
    public IEnumerable<(string Ref, string Label)> AllModelRefs()
    {
        foreach (var (pid, p) in Models.Where(kv => kv.Key != "default").OrderBy(kv => kv.Key))
            foreach (var m in p.Models)
                yield return ($"{pid}/{m.TargetModel}", $"{p.DisplayName} / {m.Alias}");

        if (!Models.TryGetValue("default", out var def)) yield break;
        var gtrans = def.Models.FirstOrDefault(m => m.TargetModel == "Google_Translate");
        var youdao = def.Models.FirstOrDefault(m => m.TargetModel == "Youdao_Dict");
        if (gtrans is not null) yield return ("default/Google_Translate", $"Built-in / {gtrans.Alias}");
        if (youdao is not null) yield return ("default/Youdao_Dict",      $"Built-in / {youdao.Alias}");
    }
}
```

- [ ] **Step 4: Update `AuraTxt.Core/Models/ActionItem.cs` — change IsSystemModel**

Replace line 13:
```csharp
    public bool IsSystemModel => ModelId.StartsWith("default/", StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 5: Delete `AuraTxt.Core/Models/ModelPlatform.cs`**

```powershell
Remove-Item C:\Users\ldd\Documents\Works\AuraTxt\AuraTxt.Core\Models\ModelPlatform.cs
```

- [ ] **Step 6: Attempt build — expect FAIL (many references to ModelPlatform)**

```powershell
dotnet build C:\Users\ldd\Documents\Works\AuraTxt\AuraTxt.sln 2>&1 | Select-String "error" | Select-Object -First 20
```
Expected: compile errors in ConfigServiceTests, InteractiveWindow, ResultWindow, etc. — this is expected; we fix them in subsequent tasks.

---

### Task 2: ConfigService — SaveWithBackup, Restore, CreateDefault

**Files:**
- Modify: `AuraTxt.Core/Services/ConfigService.cs`

- [ ] **Step 1: Replace `AuraTxt.Core/Services/ConfigService.cs` entirely**

```csharp
using System.Text.Json;
using AuraTxt.Core.Models;

namespace AuraTxt.Core.Services;

public class ConfigService
{
    private static readonly string DefaultConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AuraTxt");

    public static string DefaultConfigPath =>
        Path.Combine(DefaultConfigDir, "config.json");

    private readonly string _path;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ConfigService() : this(DefaultConfigPath) { }
    public ConfigService(string path) => _path = path;

    public ConfigRoot Load()
    {
        if (!File.Exists(_path))
            return CreateDefault();
        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<ConfigRoot>(json, JsonOpts) ?? CreateDefault();
    }

    public void Save(ConfigRoot config)
    {
        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, JsonOpts));
        File.Move(tmp, _path, overwrite: true);
    }

    /// Copies current config.json → config.json.bak, then saves new content.
    public void SaveWithBackup(ConfigRoot config)
    {
        if (File.Exists(_path))
            File.Copy(_path, _path + ".bak", overwrite: true);
        Save(config);
    }

    /// Restores config.json from config.json.bak.
    public void Restore()
    {
        var bak = _path + ".bak";
        if (!File.Exists(bak))
            throw new FileNotFoundException("No backup found at " + bak, bak);
        File.Copy(bak, _path, overwrite: true);
    }

    private ConfigRoot CreateDefault()
    {
        var cfg = new ConfigRoot();
        cfg.Models["default"] = new ProviderConfig
        {
            DisplayName = "Built-in",
            BaseUrl     = "",
            ApiKey      = "",
            Models      = new()
            {
                new ModelEntry { TargetModel = "Google_Translate", Alias = "GTrans", DisableThinking = false },
                new ModelEntry { TargetModel = "Youdao_Dict",      Alias = "Youdao",  DisableThinking = false }
            }
        };
        Save(cfg);
        return cfg;
    }
}
```

---

### Task 3: AiClient — new signature + DisableThinking

**Files:**
- Modify: `AuraTxt.Core/Services/AiClient.cs`

- [ ] **Step 1: Replace `AuraTxt.Core/Services/AiClient.cs` entirely**

```csharp
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AuraTxt.Core.Models;

namespace AuraTxt.Core.Services;

public class AiClient
{
    private readonly HttpClient _http;

    public AiClient(HttpClient? http = null)
        => _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

    public async Task<string> CompleteAsync(
        ProviderConfig provider, ModelEntry model, string prompt, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"{provider.BaseUrl.TrimEnd('/')}/chat/completions");

        req.Headers.Add("Authorization", $"Bearer {provider.ApiKey}");

        // Build request body as dict so we can conditionally add thinking disable
        var body = new Dictionary<string, object>
        {
            ["model"]    = model.TargetModel,
            ["messages"] = new[] { new { role = "user", content = prompt } },
            ["stream"]   = (object)false
        };

        if (model.DisableThinking)
            body["thinking"] = new { type = "disabled" };

        req.Content = JsonContent.Create(body);

        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }
}
```

---

### Task 4: Core Tests — update for new model

**Files:**
- Modify: `AuraTxt.Core.Tests/Models/ModelTests.cs`
- Modify: `AuraTxt.Core.Tests/Services/ConfigServiceTests.cs`

- [ ] **Step 1: Replace `AuraTxt.Core.Tests/Models/ModelTests.cs`**

```csharp
using AuraTxt.Core.Models;
using Xunit;

namespace AuraTxt.Core.Tests.Models;

public class ModelTests
{
    [Fact]
    public void ModelEntry_DisableThinking_DefaultsToTrue()
    {
        var m = new ModelEntry { TargetModel = "gpt-4o", Alias = "gpt-4o" };
        Assert.True(m.DisableThinking);
    }

    [Fact]
    public void ActionItem_IsSystemModel_TrueForDefaultPrefix()
    {
        var a = new ActionItem { ModelId = "default/Google_Translate" };
        Assert.True(a.IsSystemModel);
    }

    [Fact]
    public void ActionItem_IsSystemModel_FalseForUserProvider()
    {
        var a = new ActionItem { ModelId = "openai/gpt-4o" };
        Assert.False(a.IsSystemModel);
    }

    [Fact]
    public void ConfigRoot_ResolveModel_FindsCorrectEntry()
    {
        var cfg = new ConfigRoot();
        cfg.Models["openai"] = new ProviderConfig
        {
            DisplayName = "OpenAI",
            Models      = new() { new ModelEntry { TargetModel = "gpt-4o", Alias = "gpt-4o" } }
        };
        var result = cfg.ResolveModel("openai/gpt-4o");
        Assert.NotNull(result);
        Assert.Equal("OpenAI",  result.Value.provider.DisplayName);
        Assert.Equal("gpt-4o", result.Value.model.TargetModel);
    }

    [Fact]
    public void ConfigRoot_ResolveModel_ReturnsNullForMissing()
    {
        var cfg = new ConfigRoot();
        Assert.Null(cfg.ResolveModel("nonexistent/model"));
        Assert.Null(cfg.ResolveModel("noslash"));
        Assert.Null(cfg.ResolveModel(""));
    }

    [Fact]
    public void ConfigRoot_AllModelRefs_DefaultModelsLast()
    {
        var cfg = new ConfigRoot();
        cfg.Models["openai"] = new ProviderConfig
        {
            DisplayName = "OpenAI",
            Models      = new() { new ModelEntry { TargetModel = "gpt-4o", Alias = "4o" } }
        };
        cfg.Models["default"] = new ProviderConfig
        {
            DisplayName = "Built-in",
            Models = new()
            {
                new ModelEntry { TargetModel = "Google_Translate", Alias = "GTrans" },
                new ModelEntry { TargetModel = "Youdao_Dict",      Alias = "Youdao" }
            }
        };
        var refs = cfg.AllModelRefs().ToList();
        Assert.Equal(3, refs.Count);
        Assert.Equal("openai/gpt-4o",          refs[0].Ref);
        Assert.Equal("default/Google_Translate", refs[1].Ref);
        Assert.Equal("default/Youdao_Dict",      refs[2].Ref);
    }
}
```

- [ ] **Step 2: Replace `AuraTxt.Core.Tests/Services/ConfigServiceTests.cs`**

```csharp
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class ConfigServiceTests : IDisposable
{
    private readonly string _tmpPath =
        Path.Combine(Path.GetTempPath(), $"auratxt_test_{Guid.NewGuid()}.json");
    private readonly ConfigService _svc;

    public ConfigServiceTests() => _svc = new ConfigService(_tmpPath);

    [Fact]
    public void Load_CreatesDefaultWithBuiltinProvider_WhenFileAbsent()
    {
        var cfg = _svc.Load();
        Assert.True(cfg.Models.ContainsKey("default"));
        var def = cfg.Models["default"];
        Assert.Equal(2, def.Models.Count);
        Assert.Equal("Google_Translate", def.Models[0].TargetModel);
        Assert.Equal("Youdao_Dict",      def.Models[1].TargetModel);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var cfg = _svc.Load();
        cfg.Models["openai"] = new ProviderConfig
        {
            DisplayName = "OpenAI",
            Models      = new() { new ModelEntry { TargetModel = "gpt-4o", Alias = "4o" } }
        };
        _svc.Save(cfg);
        var loaded = _svc.Load();
        Assert.True(loaded.Models.ContainsKey("openai"));
        Assert.Equal("OpenAI", loaded.Models["openai"].DisplayName);
        Assert.Equal("gpt-4o", loaded.Models["openai"].Models[0].TargetModel);
    }

    [Fact]
    public void Save_NoTempFileRemains()
    {
        _svc.Save(_svc.Load());
        Assert.False(File.Exists(_tmpPath + ".tmp"));
    }

    [Fact]
    public void SaveWithBackup_CreatesBakFile()
    {
        var cfg = _svc.Load();
        _svc.SaveWithBackup(cfg);
        Assert.True(File.Exists(_tmpPath + ".bak"));
    }

    [Fact]
    public void Restore_RestoresFromBak()
    {
        // 1. Save a config with FontSize 14, then call SaveWithBackup with the same
        //    content so config.json.bak captures FontSize 14.
        var original = _svc.Load();
        original.Settings.FontSize = 14;
        _svc.SaveWithBackup(original); // backup now holds FontSize 14

        // 2. Mutate and save → config.json now has FontSize 99.
        var changed = _svc.Load();
        changed.Settings.FontSize = 99;
        _svc.Save(changed);
        Assert.Equal(99, _svc.Load().Settings.FontSize);

        // 3. Restore → config.json reverts to the backup (FontSize 14).
        _svc.Restore();
        Assert.Equal(14, _svc.Load().Settings.FontSize);
    }

    [Fact]
    public void Restore_ThrowsWhenNoBakExists()
    {
        Assert.Throws<FileNotFoundException>(() => _svc.Restore());
    }

    public void Dispose()
    {
        foreach (var f in new[] { _tmpPath, _tmpPath + ".bak", _tmpPath + ".tmp" })
            if (File.Exists(f)) File.Delete(f);
    }
}
```

- [ ] **Step 3: Run all Core tests**

```powershell
dotnet test C:\Users\ldd\Documents\Works\AuraTxt\AuraTxt.Core.Tests -v minimal
```
Expected: 6 ModelTests + 6 ConfigServiceTests + 11 HotkeyValidatorTests = `Passed: 23` total.

- [ ] **Step 4: Commit Phase 1**

```powershell
cd C:\Users\ldd\Documents\Works\AuraTxt
git add AuraTxt.Core/
git add AuraTxt.Core.Tests/
git commit -m "feat(core): replace ModelPlatform with ProviderConfig/ModelEntry, add ResolveModel/AllModelRefs/SaveWithBackup/Restore"
```

---

## Phase 2 — CLI Rewrite

### Task 5: HotkeyCapture + ProviderCommand + DoctorCommand

**Files:**
- Create: `AuraTxt.Cli/HotkeyCapture.cs`
- Create: `AuraTxt.Cli/Commands/ProviderCommand.cs`
- Create: `AuraTxt.Cli/Commands/DoctorCommand.cs`

- [ ] **Step 1: Create `AuraTxt.Cli/HotkeyCapture.cs`**

```csharp
using AuraTxt.Core.Constants;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli;

public static class HotkeyCapture
{
    /// Returns a hotkey string like "Alt+T", or "" if user pressed ESC to skip.
    public static string Capture(IEnumerable<ActionItem> actions, string? excludeId = null)
    {
        var validator   = new HotkeyValidator();
        var actionList  = actions.ToList();

        while (true)
        {
            Console.Write("  Press shortcut key (ESC to skip — optional): ");
            var info = Console.ReadKey(intercept: true);
            Console.WriteLine();

            if (info.Key == ConsoleKey.Escape)
            {
                WriteGray("  (Skipped — no hotkey assigned)");
                return "";
            }

            var mods = new List<string>();
            if ((info.Modifiers & ConsoleModifiers.Control) != 0) mods.Add("Ctrl");
            if ((info.Modifiers & ConsoleModifiers.Alt)     != 0) mods.Add("Alt");
            if ((info.Modifiers & ConsoleModifiers.Shift)   != 0) mods.Add("Shift");

            if (mods.Count == 0)
            {
                WriteError("  Modifier required (Ctrl/Alt/Shift). Try again or ESC to skip.");
                continue;
            }

            var keyName = MapKey(info.Key);
            if (keyName is null)
            {
                WriteError("  Unsupported key. Use A-Z, 0-9, or F1-F12. Try again or ESC to skip.");
                continue;
            }

            var hotkey = string.Join("+", mods) + "+" + keyName;

            if (SystemKeys.Reserved.Contains(hotkey))
            {
                WriteError($"  \"{hotkey}\" is a system-reserved key. Try again or ESC to skip.");
                continue;
            }

            var (res, conflictName) = validator.Validate(hotkey, actionList, excludeId);
            if (res == HotkeyValidationResult.Conflict)
            {
                WriteError($"  \"{hotkey}\" conflicts with action \"{conflictName}\". Try again or ESC to skip.");
                continue;
            }

            Console.Write("  Detected: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(hotkey);
            Console.ResetColor();
            Console.Write(" — confirm? (Y/n/ESC): ");

            var confirm = Console.ReadKey(intercept: true);
            Console.WriteLine();

            if (confirm.Key == ConsoleKey.Escape)
            {
                WriteGray("  (Skipped)");
                return "";
            }
            if (confirm.Key == ConsoleKey.Enter || char.ToLower(confirm.KeyChar) == 'y')
                return hotkey;
            // N → retry
        }
    }

    private static string? MapKey(ConsoleKey key) => key switch
    {
        >= ConsoleKey.A and <= ConsoleKey.Z    => key.ToString(),
        >= ConsoleKey.D0 and <= ConsoleKey.D9  => ((int)(key - ConsoleKey.D0)).ToString(),
        >= ConsoleKey.F1 and <= ConsoleKey.F12 => key.ToString(),
        ConsoleKey.Spacebar  => "Space",
        ConsoleKey.Tab       => "Tab",
        ConsoleKey.Delete    => "Delete",
        ConsoleKey.Insert    => "Insert",
        ConsoleKey.Home      => "Home",
        ConsoleKey.End       => "End",
        ConsoleKey.PageUp    => "PageUp",
        ConsoleKey.PageDown  => "PageDown",
        ConsoleKey.LeftArrow  => "Left",
        ConsoleKey.RightArrow => "Right",
        ConsoleKey.UpArrow    => "Up",
        ConsoleKey.DownArrow  => "Down",
        _ => null
    };

    private static void WriteError(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(msg);
        Console.ResetColor();
    }

    private static void WriteGray(string msg)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(msg);
        Console.ResetColor();
    }
}
```

- [ ] **Step 2: Create `AuraTxt.Cli/Commands/ProviderCommand.cs`**

```csharp
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Commands;

public class ProviderCommand(ConfigService config)
{
    public Task<int> ExecuteAsync(string[] args) => Task.FromResult(args.Length == 0
        ? PrintHelp()
        : args[0] switch
        {
            "--list"      => List(),
            "--set"       => Set(ArgParser.Parse(args[1..])),
            "--add-model" => AddModel(ArgParser.Parse(args[1..])),
            "--update"    => Update(ArgParser.Parse(args[1..])),
            "--delete"    => Delete(ArgParser.Parse(args[1..])),
            _             => PrintHelp()
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
                TargetModel     = tm,
                Alias           = opts.GetValueOrDefault("alias", tm),
                DisableThinking = !opts.ContainsKey("thinking") // default: thinking disabled
            });
        config.Save(cfg);
        Console.WriteLine($"✓ Provider '{id}' saved");
        return 0;
    }

    private int AddModel(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id", out var id))  return Err("Missing --id");
        if (!opts.TryGetValue("model", out var tm)) return Err("Missing --model");
        if (id == "default") return Err("Cannot modify built-in 'default' provider", 2);
        var cfg = config.Load();
        if (!cfg.Models.TryGetValue(id, out var p)) return Err($"Provider '{id}' not found", 2);
        p.Models.Add(new ModelEntry
        {
            TargetModel     = tm,
            Alias           = opts.GetValueOrDefault("alias", tm),
            DisableThinking = !opts.ContainsKey("thinking")
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
        Console.WriteLine("auracfg provider --delete --id <id> [--force]");
        Console.WriteLine("Note: --thinking flag enables thinking (default is disabled)");
        return 1;
    }
}
```

- [ ] **Step 3: Create `AuraTxt.Cli/Commands/DoctorCommand.cs`**

```csharp
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Commands;

public class DoctorCommand(ConfigService config)
{
    private int _errors, _warnings;

    public int Execute()
    {
        _errors = _warnings = 0;
        Console.WriteLine("Running config diagnostics...");

        ConfigRoot cfg;
        try
        {
            cfg = config.Load();
            Ok("JSON syntax valid");
        }
        catch (Exception ex)
        {
            Error($"JSON parse error: {ex.Message}");
            PrintSummary();
            return 2;
        }

        // Duplicate Action IDs
        var dupIds = cfg.Actions
            .GroupBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        foreach (var id in dupIds)
            Error($"Duplicate Action ID: \"{id}\"");
        if (!dupIds.Any())
            Ok("No duplicate Action IDs");

        // Action ModelId format + resolution
        int badRefs = 0;
        foreach (var action in cfg.Actions)
        {
            if (!action.ModelId.Contains('/'))
            {
                Error($"Action \"{action.Id}\": ModelId \"{action.ModelId}\" missing '/' — expected format providerId/TargetModel");
                badRefs++;
                continue;
            }
            if (cfg.ResolveModel(action.ModelId) is null)
            {
                Error($"Action \"{action.Id}\": ModelId \"{action.ModelId}\" — provider or model not found");
                badRefs++;
            }
        }
        if (badRefs == 0 && cfg.Actions.Any())
            Ok("All Action ModelIds resolve correctly");

        // Providers with no models
        foreach (var (id, p) in cfg.Models.Where(kv => kv.Key != "default"))
            if (p.Models.Count == 0)
                Warn($"Provider \"{id}\": has no models configured");

        // Hotkey format + conflicts
        var hv   = new HotkeyValidator();
        var seen = new List<ActionItem>();
        foreach (var a in cfg.Actions.Where(a => !string.IsNullOrEmpty(a.Hotkey)))
        {
            var (res, conflictName) = hv.Validate(a.Hotkey, seen);
            if (res == HotkeyValidationResult.InvalidFormat)
                Error($"Action \"{a.Id}\": invalid hotkey format \"{a.Hotkey}\"");
            else if (res == HotkeyValidationResult.Conflict)
                Warn($"Action \"{a.Id}\" and \"{conflictName}\" share hotkey {a.Hotkey}");
            seen.Add(a);
        }

        PrintSummary();
        return _errors > 0 ? 2 : _warnings > 0 ? 1 : 0;
    }

    private void PrintSummary()
    {
        Console.WriteLine();
        if (_errors == 0 && _warnings == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ 0 errors, 0 warnings — config is healthy");
        }
        else
        {
            Console.WriteLine($"{_errors} error(s), {_warnings} warning(s) found. Edit config.json manually to fix.");
        }
        Console.ResetColor();
    }

    private void Ok(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  [OK]    ");
        Console.ResetColor();
        Console.WriteLine(msg);
    }

    private void Warn(string msg)
    {
        _warnings++;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("  [WARN]  ");
        Console.ResetColor();
        Console.WriteLine(msg);
    }

    private void Error(string msg)
    {
        _errors++;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("  [ERROR] ");
        Console.ResetColor();
        Console.WriteLine(msg);
    }
}
```

- [ ] **Step 4: Verify build**

```powershell
dotnet build C:\Users\ldd\Documents\Works\AuraTxt\AuraTxt.Cli\AuraTxt.Cli.csproj 2>&1 | Select-String "error" | Select-Object -First 10
```

---

### Task 6: Update ActionCommand, SettingsCommand, Program.cs

**Files:**
- Modify: `AuraTxt.Cli/Commands/ActionCommand.cs` (ModelId validation)
- Modify: `AuraTxt.Cli/Program.cs` (new routes)

- [ ] **Step 1: Replace the full `Set` method in `AuraTxt.Cli/Commands/ActionCommand.cs`**

This adds ModelId format validation (must contain `/` or start with `$` for back-compat) and switches messages to English. Locate the existing `private int Set(Dictionary<string, string> opts)` method and replace it entirely with:

```csharp
    private int Set(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id", out var id)) return Err("缺少 --id");
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
            Prompt        = opts.GetValueOrDefault("prompt", "")
        };

        if (idx >= 0) cfg.Actions[idx] = item;
        else          cfg.Actions.Add(item);
        config.Save(cfg);
        Console.WriteLine($"✓ Action '{id}' saved");
        return 0;
    }
```

- [ ] **Step 2: Replace `AuraTxt.Cli/Program.cs`**

```csharp
using AuraTxt.Cli.Commands;
using AuraTxt.Cli.Menus;
using AuraTxt.Core.Services;

var configService = new ConfigService();

if (args.Length == 0)
{
    await new InteractiveMenu(configService).RunAsync();
    return 0;
}

return args[0] switch
{
    "provider" => await new ProviderCommand(configService).ExecuteAsync(args[1..]),
    "model"    => await new ProviderCommand(configService).ExecuteAsync(args[1..]), // compat alias
    "action"   => await new ActionCommand(configService).ExecuteAsync(args[1..]),
    "settings" => await new SettingsCommand(configService).ExecuteAsync(args[1..]),
    "doctor"   => new DoctorCommand(configService).Execute(),
    "restore"  => Restore(configService),
    _          => Help()
};

static int Restore(ConfigService svc)
{
    try
    {
        svc.Restore();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Config restored from config.json.bak");
        Console.ResetColor();
        return 0;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Restore failed: {ex.Message}");
        Console.ResetColor();
        return 3;
    }
}

static int Help()
{
    Console.WriteLine("auracfg — AuraTxt Config Tool");
    Console.WriteLine("Usage:");
    Console.WriteLine("  auracfg                        Interactive menu");
    Console.WriteLine("  auracfg provider  [options]    Manage model providers");
    Console.WriteLine("  auracfg action    [options]    Manage actions");
    Console.WriteLine("  auracfg settings  [options]    Manage UI settings");
    Console.WriteLine("  auracfg doctor                 Validate config.json");
    Console.WriteLine("  auracfg restore                Restore config from backup");
    return 1;
}
```

- [ ] **Step 3: Build CLI**

```powershell
dotnet build C:\Users\ldd\Documents\Works\AuraTxt\AuraTxt.Cli\AuraTxt.Cli.csproj
```
Expected: 0 errors (InteractiveMenu still has old code referencing ModelPlatform — fix in next task).

- [ ] **Step 4: Smoke-test doctor**

```powershell
dotnet run --project C:\Users\ldd\Documents\Works\AuraTxt\AuraTxt.Cli -- doctor
```
Expected: `✓ 0 errors, 0 warnings — config is healthy`

---

### Task 7: Rewrite InteractiveMenu.cs

**Files:**
- Modify: `AuraTxt.Cli/Menus/InteractiveMenu.cs` (complete rewrite)

- [ ] **Step 1: Replace `AuraTxt.Cli/Menus/InteractiveMenu.cs` entirely**

```csharp
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
            if (input == "X") { Environment.Exit(0); }
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
            var client = new AuraTxt.Core.Services.AiClient();
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

        var id   = Ask("Action ID (unique key, e.g. translate)");
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
        // Save current in-memory state to a temp file for doctor to check
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
```

- [ ] **Step 2: Build CLI**

```powershell
dotnet build C:\Users\ldd\Documents\Works\AuraTxt\AuraTxt.Cli\AuraTxt.Cli.csproj
```
Expected: 0 errors.

- [ ] **Step 3: Smoke-test interactive menu**

```powershell
dotnet run --project C:\Users\ldd\Documents\Works\AuraTxt\AuraTxt.Cli
```
Expected: English main menu appears with [1][2][3][D][X] options. Navigate with keyboard. X exits.

- [ ] **Step 4: Commit Phase 2**

```powershell
cd C:\Users\ldd\Documents\Works\AuraTxt
git add AuraTxt.Cli/
git commit -m "feat(cli): full auracfg rewrite — English menu, ProviderConfig, HotkeyCapture, doctor/restore"
```

---

## Phase 3 — WPF Adapter

### Task 8: Update ResultWindow + InteractiveWindow

**Files:**
- Modify: `AuraTxt/Windows/ResultWindow.xaml.cs`
- Modify: `AuraTxt/Windows/InteractiveWindow.xaml.cs`

- [ ] **Step 1: Replace `AuraTxt/Windows/ResultWindow.xaml.cs`**

```csharp
using System.Windows;
using System.Windows.Input;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using Clipboard = System.Windows.Clipboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AuraTxt.Windows;

public partial class ResultWindow : Window
{
    private readonly ActionItem   _action;
    private readonly string       _selectedText;
    private readonly ConfigRoot   _cfg;
    private string                _currentPrompt;

    public ResultWindow(ActionItem action, string selectedText, ConfigRoot cfg)
    {
        InitializeComponent();
        _action        = action;
        _selectedText  = selectedText;
        _cfg           = cfg;
        _currentPrompt = action.Prompt;

        TitleLabel.Text     = $"{action.Name} · {GetModelLabel(action, cfg)}";
        ResultText.FontSize = cfg.Settings.FontSize;
        Opacity             = cfg.Settings.ResultWindowOpacity;

        Loaded += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        ResultText.Text = "Processing…";
        try { ResultText.Text = await CallModelAsync(); }
        catch (Exception ex) { ResultText.Text = $"[Error] {ex.Message}"; }
    }

    private async Task<string> CallModelAsync()
    {
        var resolved = _cfg.ResolveModel(_action.ModelId);

        // Built-in services — pass selected text directly, no prompt needed
        if (resolved?.model.TargetModel == "Google_Translate")
            return await new GoogleTranslateClient().TranslateAsync(_selectedText);
        if (resolved?.model.TargetModel == "Youdao_Dict")
            return await new YoudaoClient().TranslateAsync(_selectedText);

        if (resolved is null)
            throw new InvalidOperationException($"Model not found: {_action.ModelId}");

        var prompt = _currentPrompt
            .Replace("{SelectedText}", _selectedText)
            .Replace("{UserInput}", "");

        return await new AiClient().CompleteAsync(resolved.Value.provider, resolved.Value.model, prompt);
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
    private async void RegenBtn_Click(object sender, RoutedEventArgs e) => await RunAsync();
    private void CopyBtn_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(ResultText.Text);

    private void EditBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PromptEditDialog(_currentPrompt);
        if (dlg.ShowDialog() == true) { _currentPrompt = dlg.Result; _ = RunAsync(); }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.P) EditBtn_Click(sender, new RoutedEventArgs());
        else if (e.Key == Key.R) RegenBtn_Click(sender, new RoutedEventArgs());
        else if (e.Key == Key.C) CopyBtn_Click(sender, new RoutedEventArgs());
    }

    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private static string GetModelLabel(ActionItem action, ConfigRoot cfg)
    {
        var resolved = cfg.ResolveModel(action.ModelId);
        if (resolved is null) return action.ModelId;
        return $"{resolved.Value.provider.DisplayName} / {resolved.Value.model.Alias}";
    }
}
```

- [ ] **Step 2: Replace `AuraTxt/Windows/InteractiveWindow.xaml.cs`**

```csharp
using System.Windows;
using System.Windows.Input;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using Clipboard = System.Windows.Clipboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AuraTxt.Windows;

public partial class InteractiveWindow : Window
{
    private readonly ActionItem _action;
    private readonly string     _selectedText;
    private readonly ConfigRoot _cfg;
    private string              _currentPrompt;
    private (ProviderConfig provider, ModelEntry model)? _activeModel;

    public InteractiveWindow(ActionItem action, string selectedText, ConfigRoot cfg)
    {
        InitializeComponent();
        _action        = action;
        _selectedText  = selectedText;
        _cfg           = cfg;
        _currentPrompt = action.Prompt;

        TitleLabel.Text     = action.Name;
        ResultText.FontSize = cfg.Settings.FontSize;
        Opacity             = cfg.Settings.ResultWindowOpacity;

        // Populate model picker with all model refs, excluding built-in default models
        var items = cfg.AllModelRefs()
            .Where(r => !r.Ref.StartsWith("default/"))
            .Select(r => new ModelPickerItem(r.Ref, r.Label))
            .ToList();
        ModelPicker.ItemsSource       = items;
        ModelPicker.DisplayMemberPath = "Label";
        ModelPicker.SelectedValuePath = "Id";

        var initial = cfg.ResolveModel(action.ModelId);
        if (initial is not null && !action.ModelId.StartsWith("default/"))
        {
            ModelPicker.SelectedValue = action.ModelId;
            _activeModel = initial;
        }
    }

    private void ModelPicker_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ModelPicker.SelectedValue is string id)
            _activeModel = _cfg.ResolveModel(id);
    }

    private async void SendBtn_Click(object sender, RoutedEventArgs e)  => await GenerateAsync();
    private async void RegenBtn_Click(object sender, RoutedEventArgs e) => await GenerateAsync();

    private async Task GenerateAsync()
    {
        if (_activeModel is null)
        {
            ResultText.Text = "[Error] Please select a model first.";
            return;
        }
        ResultText.Text = "Processing…";
        var prompt = _currentPrompt
            .Replace("{SelectedText}", _selectedText)
            .Replace("{UserInput}", UserInput.Text);
        try { ResultText.Text = await new AiClient().CompleteAsync(_activeModel.Value.provider, _activeModel.Value.model, prompt); }
        catch (Exception ex) { ResultText.Text = $"[Error] {ex.Message}"; }
    }

    private void CopyBtn_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(ResultText.Text);

    private void EditBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PromptEditDialog(_currentPrompt);
        if (dlg.ShowDialog() == true) _currentPrompt = dlg.Result;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.P) EditBtn_Click(sender, new RoutedEventArgs());
        else if (e.Key == Key.R) RegenBtn_Click(sender, new RoutedEventArgs());
        else if (e.Key == Key.C) CopyBtn_Click(sender, new RoutedEventArgs());
    }

    private void UserInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            _ = GenerateAsync();
        }
    }

    private record ModelPickerItem(string Id, string Label);
}
```

- [ ] **Step 3: Build full solution**

```powershell
dotnet build C:\Users\ldd\Documents\Works\AuraTxt\AuraTxt.sln
```
Expected: `Build succeeded. 0 Error(s)` (2 NU1701 warnings for MouseKeyHook are expected).

- [ ] **Step 4: Run Core tests**

```powershell
dotnet test C:\Users\ldd\Documents\Works\AuraTxt\AuraTxt.Core.Tests -v minimal
```
Expected: `Passed: 23`

- [ ] **Step 5: Commit Phase 3**

```powershell
cd C:\Users\ldd\Documents\Works\AuraTxt
git add AuraTxt/Windows/ResultWindow.xaml.cs AuraTxt/Windows/InteractiveWindow.xaml.cs
git commit -m "feat(wpf): adapt ResultWindow/InteractiveWindow to ProviderConfig/ModelEntry schema"
```

---

## End-to-End Verification

- [ ] **V1** `dotnet run --project AuraTxt.Cli` → English main menu [1][2][3][D][X]
- [ ] **V2** `[1]` → `[A]` add provider → add DeepSeek with URL + key + `deepseek-chat` model → `[X]` exit → "Save? Y" → config.json has "deepseek" provider with Models array
- [ ] **V3** `[2]` → `[A]` add action → select deepseek/deepseek-chat → set hotkey Alt+Q → action saved
- [ ] **V4** `[2]` → `[A]` add action → select same hotkey Alt+Q → error "conflicts with" shown, ESC skips
- [ ] **V5** `[D]` doctor → `✓ 0 errors, 0 warnings`
- [ ] **V6** `auracfg doctor` (param mode) → same output
- [ ] **V7** `auracfg restore` with no .bak → "Restore failed: No backup found"
- [ ] **V8** Provider detail `[3]` Disable Thinking toggle → off→on→off round-trip
- [ ] **V9** `[T]` test model → shows response or error within 10s
- [ ] **V10** Start WPF app → select text → action menu shows → click translate → Google result appears

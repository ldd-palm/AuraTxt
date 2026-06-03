# AuraTxt Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build AuraTxt — a Windows system-tray AI text-selection tool with a WPF popup menu, result windows, and a CLI config tool (auracfg.exe).

**Architecture:** One solution, three projects sharing AuraTxt.Core (Class Library). AuraTxt.Core owns all models, JSON persistence, AI clients, and hotkey validation. AuraTxt (WPF) and AuraTxt.Cli (Console) both reference Core and never depend on each other.

**Tech Stack:** C# 12 / .NET 8, WPF, System.Text.Json, xUnit, H.NotifyIcon.Wpf, MouseKeyHook, NHotkey.Wpf, SharpVectors.Wpf

**Spec:** `docs/superpowers/specs/2026-05-30-auratxt-design.md`

---

## Phase 1 — AuraTxt.Core (Foundation)

### Task 1: Scaffold Solution

**Files:**
- Create: `AuraTxt.sln`
- Create: `AuraTxt.Core/AuraTxt.Core.csproj`
- Create: `AuraTxt.Core.Tests/AuraTxt.Core.Tests.csproj`

- [ ] **Step 1: Create solution and projects**

```powershell
cd C:\Users\ldd\Documents\Works\AuraTxt
dotnet new sln -n AuraTxt
dotnet new classlib -n AuraTxt.Core -f net8.0-windows -o AuraTxt.Core
dotnet new xunit   -n AuraTxt.Core.Tests -f net8.0-windows -o AuraTxt.Core.Tests
dotnet sln add AuraTxt.Core/AuraTxt.Core.csproj
dotnet sln add AuraTxt.Core.Tests/AuraTxt.Core.Tests.csproj
dotnet add AuraTxt.Core.Tests/AuraTxt.Core.Tests.csproj reference AuraTxt.Core/AuraTxt.Core.csproj
```

- [ ] **Step 2: Add test infrastructure packages**

```powershell
cd AuraTxt.Core.Tests
dotnet add package Microsoft.NET.Test.Sdk
dotnet add package xunit.runner.visualstudio
```

- [ ] **Step 3: Delete placeholder files**

```powershell
Remove-Item AuraTxt.Core/Class1.cs
Remove-Item AuraTxt.Core.Tests/UnitTest1.cs
```

- [ ] **Step 4: Verify build**

```powershell
dotnet build AuraTxt.sln
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```powershell
git init
git add .
git commit -m "chore: scaffold solution with Core and Core.Tests projects"
```

---

### Task 2: Data Models

**Files:**
- Create: `AuraTxt.Core/Models/ConfigRoot.cs`
- Create: `AuraTxt.Core/Models/ModelPlatform.cs`
- Create: `AuraTxt.Core/Models/ActionItem.cs`
- Create: `AuraTxt.Core/Models/AppSettings.cs`

- [ ] **Step 1: Write failing test**

Create `AuraTxt.Core.Tests/Models/ModelTests.cs`:

```csharp
using AuraTxt.Core.Models;
using Xunit;

namespace AuraTxt.Core.Tests.Models;

public class ModelTests
{
    [Fact]
    public void ModelPlatform_Alias_CombinesProviderAndTargetModel()
    {
        var m = new ModelPlatform { Provider = "openai-compatible", TargetModel = "deepseek-chat" };
        Assert.Equal("openai-compatible/deepseek-chat", m.Alias);
    }

    [Fact]
    public void ActionItem_IsSystemModel_TrueWhenDollarPrefix()
    {
        var a = new ActionItem { ModelId = "$google-translate" };
        Assert.True(a.IsSystemModel);
    }

    [Fact]
    public void ActionItem_IsSystemModel_FalseForNormalId()
    {
        var a = new ActionItem { ModelId = "deepseek" };
        Assert.False(a.IsSystemModel);
    }

    [Fact]
    public void ConfigRoot_HasSystemDefaults()
    {
        var c = new ConfigRoot();
        Assert.Equal("google-translate", c.System.GoogleTranslate.Provider);
        Assert.Equal("youdao-dict",      c.System.YoudaoDict.Provider);
    }
}
```

- [ ] **Step 2: Run test — expect FAIL**

```powershell
dotnet test AuraTxt.Core.Tests --filter "ModelTests" -v minimal
```
Expected: compiler errors (types not defined).

- [ ] **Step 3: Create `AuraTxt.Core/Models/AppSettings.cs`**

```csharp
namespace AuraTxt.Core.Models;

public class AppSettings
{
    public int FontSize { get; set; } = 14;
    public double ResultWindowOpacity { get; set; } = 0.95;
    public int MenuTriggerDelayMs { get; set; } = 100;
}
```

- [ ] **Step 4: Create `AuraTxt.Core/Models/ModelPlatform.cs`**

```csharp
namespace AuraTxt.Core.Models;

public class ModelPlatform
{
    public string DisplayName  { get; set; } = "";
    public string Provider     { get; set; } = "openai-compatible";
    public string BaseUrl      { get; set; } = "";
    public string ApiKey       { get; set; } = "";
    public string TargetModel  { get; set; } = "";
    public string Alias        => $"{Provider}/{TargetModel}";
}
```

- [ ] **Step 5: Create `AuraTxt.Core/Models/ActionItem.cs`**

```csharp
namespace AuraTxt.Core.Models;

public class ActionItem
{
    public string Id            { get; set; } = "";
    public string Name          { get; set; } = "";
    public string Icon          { get; set; } = "";
    public string ModelId       { get; set; } = "";
    public bool   IsInteractive { get; set; }
    public string Hotkey        { get; set; } = "";
    public string Prompt        { get; set; } = "";

    public bool IsSystemModel => ModelId.StartsWith('$');
}
```

- [ ] **Step 6: Create `AuraTxt.Core/Models/ConfigRoot.cs`**

```csharp
namespace AuraTxt.Core.Models;

public class ConfigRoot
{
    public SystemConfig                    System   { get; set; } = new();
    public Dictionary<string, ModelPlatform> Models { get; set; } = new();
    public List<ActionItem>               Actions   { get; set; } = new();
    public AppSettings                    Settings  { get; set; } = new();
}

public class SystemConfig
{
    public SystemService GoogleTranslate { get; set; } = new()
        { Provider = "google-translate", DisplayName = "Google 翻译" };
    public SystemService YoudaoDict { get; set; } = new()
        { Provider = "youdao-dict", DisplayName = "有道词典" };
}

public class SystemService
{
    public string Provider    { get; set; } = "";
    public string DisplayName { get; set; } = "";
}
```

- [ ] **Step 7: Run test — expect PASS**

```powershell
dotnet test AuraTxt.Core.Tests --filter "ModelTests" -v minimal
```
Expected: `Passed: 4`

- [ ] **Step 8: Commit**

```powershell
git add AuraTxt.Core/Models/ AuraTxt.Core.Tests/Models/
git commit -m "feat(core): add config data models"
```

---

### Task 3: ConfigService

**Files:**
- Create: `AuraTxt.Core/Services/ConfigService.cs`
- Create: `AuraTxt.Core.Tests/Services/ConfigServiceTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class ConfigServiceTests : IDisposable
{
    private readonly string _tmpPath = Path.Combine(Path.GetTempPath(), $"auratxt_test_{Guid.NewGuid()}.json");
    private readonly ConfigService _svc;

    public ConfigServiceTests()
    {
        _svc = new ConfigService(_tmpPath);
    }

    [Fact]
    public void Load_ReturnDefault_WhenFileAbsent()
    {
        var cfg = _svc.Load();
        Assert.NotNull(cfg);
        Assert.Equal("google-translate", cfg.System.GoogleTranslate.Provider);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var cfg = _svc.Load();
        cfg.Models["test"] = new ModelPlatform { DisplayName = "Test", TargetModel = "gpt-4o" };
        _svc.Save(cfg);

        var loaded = _svc.Load();
        Assert.True(loaded.Models.ContainsKey("test"));
        Assert.Equal("Test", loaded.Models["test"].DisplayName);
    }

    [Fact]
    public void Save_IsAtomic_WritesToTempThenRenames()
    {
        // Verify no .tmp file remains after save
        _svc.Save(_svc.Load());
        Assert.False(File.Exists(_tmpPath + ".tmp"));
    }

    public void Dispose()
    {
        if (File.Exists(_tmpPath)) File.Delete(_tmpPath);
    }
}
```

- [ ] **Step 2: Run test — expect FAIL**

```powershell
dotnet test AuraTxt.Core.Tests --filter "ConfigServiceTests" -v minimal
```

- [ ] **Step 3: Create `AuraTxt.Core/Services/ConfigService.cs`**

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
        {
            var def = new ConfigRoot();
            Save(def);
            return def;
        }
        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<ConfigRoot>(json, JsonOpts) ?? new ConfigRoot();
    }

    public void Save(ConfigRoot config)
    {
        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, JsonOpts));
        File.Move(tmp, _path, overwrite: true);
    }
}
```

- [ ] **Step 4: Run test — expect PASS**

```powershell
dotnet test AuraTxt.Core.Tests --filter "ConfigServiceTests" -v minimal
```
Expected: `Passed: 3`

- [ ] **Step 5: Commit**

```powershell
git add AuraTxt.Core/Services/ConfigService.cs AuraTxt.Core.Tests/Services/
git commit -m "feat(core): add ConfigService with atomic save"
```

---

### Task 4: HotkeyValidator

**Files:**
- Create: `AuraTxt.Core/Constants/SystemKeys.cs`
- Create: `AuraTxt.Core/Services/HotkeyValidator.cs`
- Create: `AuraTxt.Core.Tests/Services/HotkeyValidatorTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class HotkeyValidatorTests
{
    private readonly HotkeyValidator _v = new();
    private readonly List<ActionItem> _none = new();

    [Theory]
    [InlineData("Alt+T",       true)]
    [InlineData("Ctrl+Shift+R",true)]
    [InlineData("Win+F1",      true)]
    [InlineData("T",           false)]   // no modifier
    [InlineData("Alt",         false)]   // key missing
    [InlineData("",            false)]
    [InlineData("Ctrl+",       false)]
    public void IsValidFormat(string hotkey, bool expected) =>
        Assert.Equal(expected, _v.IsValidFormat(hotkey));

    [Fact]
    public void Validate_ReturnsSystemReserved_ForWinL()
    {
        var (result, _) = _v.Validate("Win+L", _none);
        Assert.Equal(HotkeyValidationResult.SystemReserved, result);
    }

    [Fact]
    public void Validate_ReturnsConflict_WhenHotkeyUsed()
    {
        var actions = new List<ActionItem>
        {
            new() { Id = "translate", Name = "翻译", Hotkey = "Alt+T" }
        };
        var (result, name) = _v.Validate("Alt+T", actions);
        Assert.Equal(HotkeyValidationResult.Conflict, result);
        Assert.Equal("翻译", name);
    }

    [Fact]
    public void Validate_ExcludesOwnId_OnUpdate()
    {
        var actions = new List<ActionItem>
        {
            new() { Id = "translate", Name = "翻译", Hotkey = "Alt+T" }
        };
        var (result, _) = _v.Validate("Alt+T", actions, excludeId: "translate");
        Assert.Equal(HotkeyValidationResult.Valid, result);
    }

    [Fact]
    public void Validate_ReturnsValid_ForUnusedHotkey()
    {
        var (result, _) = _v.Validate("Alt+Q", _none);
        Assert.Equal(HotkeyValidationResult.Valid, result);
    }
}
```

- [ ] **Step 2: Run test — expect FAIL**

```powershell
dotnet test AuraTxt.Core.Tests --filter "HotkeyValidatorTests" -v minimal
```

- [ ] **Step 3: Create `AuraTxt.Core/Constants/SystemKeys.cs`**

```csharp
namespace AuraTxt.Core.Constants;

public static class SystemKeys
{
    public static readonly HashSet<string> Reserved =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Win+L","Win+D","Win+E","Win+R","Win+I","Win+S","Win+A","Win+X",
            "Win+P","Win+K","Win+H","Win+G","Win+M","Win+T","Win+U","Win+W",
            "Win+Tab","Win+Space","Win+Enter",
            "Win+Left","Win+Right","Win+Up","Win+Down",
            "Alt+F4","Alt+Tab","Alt+Esc",
            "Ctrl+Alt+Delete","PrintScreen","Win+PrintScreen","Alt+PrintScreen"
        };
}
```

- [ ] **Step 4: Create `AuraTxt.Core/Services/HotkeyValidator.cs`**

```csharp
using AuraTxt.Core.Constants;
using AuraTxt.Core.Models;

namespace AuraTxt.Core.Services;

public enum HotkeyValidationResult { Valid, InvalidFormat, SystemReserved, Conflict }

public class HotkeyValidator
{
    private static readonly HashSet<string> Modifiers =
        new(StringComparer.OrdinalIgnoreCase) { "Ctrl","Alt","Shift","Win" };

    private static readonly HashSet<string> Keys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "A","B","C","D","E","F","G","H","I","J","K","L","M",
            "N","O","P","Q","R","S","T","U","V","W","X","Y","Z",
            "F1","F2","F3","F4","F5","F6","F7","F8","F9","F10","F11","F12",
            "0","1","2","3","4","5","6","7","8","9",
            "Space","Tab","Enter","Escape","Delete","Insert",
            "Home","End","PageUp","PageDown","Left","Right","Up","Down"
        };

    public bool IsValidFormat(string hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey)) return false;
        var parts = hotkey.Split('+');
        if (parts.Length < 2) return false;
        var key  = parts[^1].Trim();
        var mods = parts[..^1].Select(p => p.Trim()).ToArray();
        return mods.Length > 0
            && mods.All(m => Modifiers.Contains(m))
            && Keys.Contains(key);
    }

    public (HotkeyValidationResult result, string? conflictName) Validate(
        string hotkey, IEnumerable<ActionItem> existing, string? excludeId = null)
    {
        if (!IsValidFormat(hotkey))
            return (HotkeyValidationResult.InvalidFormat, null);

        if (SystemKeys.Reserved.Contains(hotkey))
            return (HotkeyValidationResult.SystemReserved, null);

        var conflict = existing
            .Where(a => a.Id != excludeId)
            .FirstOrDefault(a =>
                string.Equals(a.Hotkey, hotkey, StringComparison.OrdinalIgnoreCase));

        return conflict is null
            ? (HotkeyValidationResult.Valid, null)
            : (HotkeyValidationResult.Conflict, conflict.Name);
    }
}
```

- [ ] **Step 5: Run test — expect PASS**

```powershell
dotnet test AuraTxt.Core.Tests --filter "HotkeyValidatorTests" -v minimal
```
Expected: `Passed: 8`

- [ ] **Step 6: Commit**

```powershell
git add AuraTxt.Core/Constants/ AuraTxt.Core/Services/HotkeyValidator.cs AuraTxt.Core.Tests/Services/HotkeyValidatorTests.cs
git commit -m "feat(core): add HotkeyValidator with system-reserved and conflict detection"
```

---

### Task 5: AiClient

**Files:**
- Create: `AuraTxt.Core/Services/AiClient.cs`

- [ ] **Step 1: Create `AuraTxt.Core/Services/AiClient.cs`**

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
        ModelPlatform model, string prompt, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"{model.BaseUrl.TrimEnd('/')}/chat/completions");

        req.Headers.Add("Authorization", $"Bearer {model.ApiKey}");
        req.Content = JsonContent.Create(new
        {
            model    = model.TargetModel,
            messages = new[] { new { role = "user", content = prompt } },
            stream   = false
        });

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

- [ ] **Step 2: Verify build**

```powershell
dotnet build AuraTxt.Core/AuraTxt.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```powershell
git add AuraTxt.Core/Services/AiClient.cs
git commit -m "feat(core): add OpenAI-compatible AiClient"
```

---

### Task 6: GoogleTranslateClient

**Files:**
- Create: `AuraTxt.Core/Services/GoogleTranslateClient.cs`
- Create: `AuraTxt.Core.Tests/Services/GoogleTranslateClientTests.cs`

- [ ] **Step 1: Write unit test for tk token algorithm**

```csharp
using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class GoogleTranslateClientTests
{
    [Theory]
    [InlineData("hello")]
    [InlineData("你好")]
    [InlineData("test 123")]
    public void GenerateTk_ProducesFormatNNdotNN(string text)
    {
        var tk = GoogleTranslateClient.GenerateTk(text);
        Assert.Matches(@"^\d+\.\d+$", tk);
    }
}
```

- [ ] **Step 2: Run test — expect FAIL**

```powershell
dotnet test AuraTxt.Core.Tests --filter "GoogleTranslateClientTests" -v minimal
```

- [ ] **Step 3: Create `AuraTxt.Core/Services/GoogleTranslateClient.cs`**

```csharp
using System.Net.Http;
using System.Text.Json;

namespace AuraTxt.Core.Services;

public class GoogleTranslateClient
{
    private readonly HttpClient _http;

    public GoogleTranslateClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<string> TranslateAsync(
        string text, string from = "auto", string to = "zh-CN", CancellationToken ct = default)
    {
        var tk  = GenerateTk(text);
        var url = $"https://translate.google.com/translate_a/single" +
                  $"?client=gtx&sl={from}&tl={to}&hl=zh-CN" +
                  $"&dt=bd&dt=t&ie=UTF-8&oe=UTF-8&tk={tk}&q={Uri.EscapeDataString(text)}";

        var json = await _http.GetStringAsync(url, ct);
        return ParseTranslation(json);
    }

    private static string ParseTranslation(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var sb = new System.Text.StringBuilder();
        foreach (var seg in doc.RootElement[0].EnumerateArray())
        {
            if (seg.GetArrayLength() > 0 && seg[0].ValueKind == JsonValueKind.String)
                sb.Append(seg[0].GetString());
        }
        return sb.ToString();
    }

    // Ported from google_translate.js — tk token generation
    public static string GenerateTk(string text, string tkk = "0.0")
    {
        var parts = tkk.Split('.');
        long h = long.Parse(parts[0]);
        var g = new List<int>();

        for (int i = 0; i < text.Length; i++)
        {
            int c = text[i];
            if (c < 128)
                g.Add(c);
            else if (c < 2048)
            {
                g.Add(c >> 6 | 192);
                g.Add(c & 63  | 128);
            }
            else if ((c & 0xFC00) == 0xD800
                && i + 1 < text.Length
                && (text[i + 1] & 0xFC00) == 0xDC00)
            {
                c = 0x10000 + ((c & 0x3FF) << 10) + (text[++i] & 0x3FF);
                g.Add(c >> 18 | 240);
                g.Add(c >> 12 & 63 | 128);
                g.Add(c >> 6  & 63 | 128);
                g.Add(c       & 63 | 128);
            }
            else
            {
                g.Add(c >> 12 | 224);
                g.Add(c >> 6  & 63 | 128);
                g.Add(c       & 63 | 128);
            }
        }

        long a = h;
        foreach (int x in g)
        {
            a += x;
            a = Xform(a, "+-a^+6");
        }
        a = Xform(a, "+-3^+b+-f");
        a ^= long.Parse(parts[1]);
        if (a < 0) a = (a & 0x7FFFFFFF) + 0x80000000L;
        a %= 1_000_000;
        return $"{a}.{a ^ h}";
    }

    private static long Xform(long a, string b)
    {
        for (int d = 0; d < b.Length - 2; d += 3)
        {
            long shift = b[d + 2] >= 'a' ? b[d + 2] - 87 : b[d + 2] - '0';
            long val   = b[d + 1] == '+' ? (long)((ulong)a >> (int)shift) : a << (int)shift;
            a = b[d] == '+' ? (a + val) & 0xFFFFFFFFL : a ^ val;
        }
        return a;
    }
}
```

- [ ] **Step 4: Run test — expect PASS**

```powershell
dotnet test AuraTxt.Core.Tests --filter "GoogleTranslateClientTests" -v minimal
```
Expected: `Passed: 3`

- [ ] **Step 5: Commit**

```powershell
git add AuraTxt.Core/Services/GoogleTranslateClient.cs AuraTxt.Core.Tests/Services/GoogleTranslateClientTests.cs
git commit -m "feat(core): port Google Translate tk-token client from JS"
```

---

### Task 7: YoudaoClient

**Files:**
- Create: `AuraTxt.Core/Services/YoudaoClient.cs`

- [ ] **Step 1: Create `AuraTxt.Core/Services/YoudaoClient.cs`**

```csharp
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AuraTxt.Core.Services;

public class YoudaoClient
{
    private readonly HttpClient _http;
    private const string AppKey = "fanyideskweb";
    private const string SignSuffix = "Y2FYu%TNSbMCxc3t2u^XT";

    public YoudaoClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _http.DefaultRequestHeaders.Add("Referer", "https://fanyi.youdao.com/");
        _http.DefaultRequestHeaders.Add("Cookie", "OUTFOX_SEARCH_USER_ID=1@100.1.1.1;");
    }

    public async Task<string> TranslateAsync(string text, CancellationToken ct = default)
    {
        var salt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var sign = Md5($"{AppKey}{text}{salt}{SignSuffix}");
        var body = $"i={Uri.EscapeDataString(text)}&from=AUTO&to=zh-CHS" +
                   $"&smartresult=dict&client={AppKey}&salt={salt}&sign={sign}" +
                   $"&doctype=json&version=2.1&keyfrom=fanyi.web";

        var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        var resp = await _http.PostAsync(
            "https://fanyi.youdao.com/translate_o?smartresult=dict&smartresult=rule",
            content, ct);
        resp.EnsureSuccessStatusCode();

        return ParseTranslation(await resp.Content.ReadAsStringAsync(ct));
    }

    public async Task<string> DictionaryAsync(string word, CancellationToken ct = default)
    {
        var url = $"https://dict.youdao.com/w/{Uri.EscapeDataString(word)}/";
        var html = await _http.GetStringAsync(url, ct);
        return ExtractDefinitions(html);
    }

    private static string ParseTranslation(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("translateResult", out var results))
            return "";

        var sb = new StringBuilder();
        foreach (var row in results.EnumerateArray())
        foreach (var seg in row.EnumerateArray())
            if (seg.TryGetProperty("tgt", out var tgt))
                sb.Append(tgt.GetString());

        if (doc.RootElement.TryGetProperty("smartResult", out var smart)
            && smart.TryGetProperty("entries", out var entries))
        {
            sb.AppendLine();
            foreach (var e in entries.EnumerateArray())
                sb.AppendLine(e.GetString());
        }
        return sb.ToString().Trim();
    }

    private static string ExtractDefinitions(string html)
    {
        // Extract text between <div class="trans-container"> tags
        const string start = "trans-container";
        var idx = html.IndexOf(start, StringComparison.Ordinal);
        if (idx < 0) return "";
        var open  = html.IndexOf('>', idx) + 1;
        var close = html.IndexOf("</div>", open, StringComparison.Ordinal);
        if (open < 0 || close < 0) return "";
        var inner = html[open..close];
        // Strip HTML tags
        return System.Text.RegularExpressions.Regex.Replace(inner, "<[^>]+>", "").Trim();
    }

    private static string Md5(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }
}
```

- [ ] **Step 2: Verify build**

```powershell
dotnet build AuraTxt.Core/AuraTxt.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```powershell
git add AuraTxt.Core/Services/YoudaoClient.cs
git commit -m "feat(core): port Youdao translate+dictionary client from JS"
```

---

## Phase 2 — auracfg.exe (CLI Config Tool)

### Task 8: CLI Project Setup

**Files:**
- Create: `AuraTxt.Cli/AuraTxt.Cli.csproj`
- Create: `AuraTxt.Cli/Program.cs`

- [ ] **Step 1: Create CLI project**

```powershell
cd C:\Users\ldd\Documents\Works\AuraTxt
dotnet new console -n AuraTxt.Cli -f net8.0-windows -o AuraTxt.Cli
dotnet sln add AuraTxt.Cli/AuraTxt.Cli.csproj
dotnet add AuraTxt.Cli/AuraTxt.Cli.csproj reference AuraTxt.Core/AuraTxt.Core.csproj
```

- [ ] **Step 2: Set output filename in `AuraTxt.Cli/AuraTxt.Cli.csproj`**

Replace the generated `.csproj` content with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <AssemblyName>auracfg</AssemblyName>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\AuraTxt.Core\AuraTxt.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create `AuraTxt.Cli/Program.cs`**

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
    "model"    => await new ModelCommand(configService).ExecuteAsync(args[1..]),
    "action"   => await new ActionCommand(configService).ExecuteAsync(args[1..]),
    "settings" => await new SettingsCommand(configService).ExecuteAsync(args[1..]),
    _          => Help()
};

static int Help()
{
    Console.WriteLine("auracfg — AuraTxt 配置工具");
    Console.WriteLine("用法:");
    Console.WriteLine("  auracfg                        交互式菜单");
    Console.WriteLine("  auracfg model    [--list|--set|--update|--delete]");
    Console.WriteLine("  auracfg action   [--list|--set|--update|--delete]");
    Console.WriteLine("  auracfg settings [--show|--set]");
    return 1;
}
```

- [ ] **Step 4: Create shared arg-parser `AuraTxt.Cli/Commands/ArgParser.cs`**

```csharp
namespace AuraTxt.Cli.Commands;

public static class ArgParser
{
    /// Converts ["--id","foo","--force"] into {"id":"foo","force":"true"}
    public static Dictionary<string, string> Parse(string[] args)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            var key = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                d[key] = args[++i];
            else
                d[key] = "true";
        }
        return d;
    }
}
```

- [ ] **Step 5: Verify build**

```powershell
dotnet build AuraTxt.Cli/AuraTxt.Cli.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```powershell
git add AuraTxt.Cli/
git commit -m "feat(cli): scaffold auracfg project with entry point and arg parser"
```

---

### Task 9: ModelCommand

**Files:**
- Create: `AuraTxt.Cli/Commands/ModelCommand.cs`

- [ ] **Step 1: Create `AuraTxt.Cli/Commands/ModelCommand.cs`**

```csharp
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
```

- [ ] **Step 2: Verify build and smoke-test**

```powershell
dotnet build AuraTxt.Cli/AuraTxt.Cli.csproj
dotnet run --project AuraTxt.Cli -- model --list
```
Expected: `（无配置平台）` or table of platforms.

- [ ] **Step 3: Commit**

```powershell
git add AuraTxt.Cli/Commands/ModelCommand.cs
git commit -m "feat(cli): implement model subcommand (list/set/update/delete)"
```

---

### Task 10: ActionCommand

**Files:**
- Create: `AuraTxt.Cli/Commands/ActionCommand.cs`

- [ ] **Step 1: Create `AuraTxt.Cli/Commands/ActionCommand.cs`**

```csharp
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
        if (!opts.TryGetValue("id", out var id)) return Err("缺少 --id");
        var cfg = config.Load();

        // Validate hotkey if provided
        if (opts.TryGetValue("hotkey", out var hk) && !string.IsNullOrEmpty(hk))
        {
            var existing = cfg.Actions.Where(a => a.Id != id);
            var (res, conflict) = _hv.Validate(hk, existing);
            if (res == HotkeyValidationResult.InvalidFormat)
                return Err($"快捷键格式无效：{hk}（示例：Alt+T）");
            if (res == HotkeyValidationResult.SystemReserved)
                return Err($"系统保留热键，无法注册：{hk}", 2);
            if (res == HotkeyValidationResult.Conflict)
                return Err($"快捷键 {hk} 已被「{conflict}」使用", 2);
        }

        var idx = cfg.Actions.FindIndex(a => a.Id == id);
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
        Console.WriteLine($"✓ 动作 '{id}' 已保存");
        return 0;
    }

    private int Update(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id", out var id)) return Err("缺少 --id");
        var cfg = config.Load();
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

        if (opts.TryGetValue("name",      out var n)) item.Name          = n;
        if (opts.TryGetValue("icon",      out var i)) item.Icon          = i;
        if (opts.TryGetValue("model-id",  out var m)) item.ModelId       = m;
        if (opts.TryGetValue("prompt",    out var p)) item.Prompt        = p;
        if (opts.TryGetValue("interactive", out var iv))
            item.IsInteractive = iv == "true";

        config.Save(cfg);
        Console.WriteLine($"✓ 动作 '{id}' 已更新");
        return 0;
    }

    private int Delete(Dictionary<string, string> opts)
    {
        if (!opts.TryGetValue("id", out var id)) return Err("缺少 --id");
        var cfg = config.Load();
        var removed = cfg.Actions.RemoveAll(a => a.Id == id);
        if (removed == 0) return Err($"未找到动作 '{id}'", 2);
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
        Console.WriteLine("auracfg action --list");
        Console.WriteLine("auracfg action --set    --id <id> --name <name> --icon <lucide> --model-id <id> --interactive <true|false> --prompt \"<text>\" [--hotkey <key>]");
        Console.WriteLine("auracfg action --update --id <id> [任意字段]");
        Console.WriteLine("auracfg action --delete --id <id>");
        return 1;
    }
}
```

- [ ] **Step 2: Smoke-test hotkey conflict detection**

```powershell
# First add an action with Alt+T
dotnet run --project AuraTxt.Cli -- action --set --id translate --name 翻译 --icon languages --model-id '$google-translate' --interactive false --prompt "{SelectedText}" --hotkey "Alt+T"
# Try to add another with Alt+T — should fail
dotnet run --project AuraTxt.Cli -- action --set --id polish --name 润色 --icon sparkles --model-id deepseek --interactive false --prompt "润色：{SelectedText}" --hotkey "Alt+T"
```
Expected second command: `快捷键 Alt+T 已被「翻译」使用`

- [ ] **Step 3: Commit**

```powershell
git add AuraTxt.Cli/Commands/ActionCommand.cs
git commit -m "feat(cli): implement action subcommand with hotkey validation"
```

---

### Task 11: SettingsCommand + InteractiveMenu

**Files:**
- Create: `AuraTxt.Cli/Commands/SettingsCommand.cs`
- Create: `AuraTxt.Cli/Menus/InteractiveMenu.cs`

- [ ] **Step 1: Create `AuraTxt.Cli/Commands/SettingsCommand.cs`**

```csharp
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
        Console.WriteLine($"font-size:  {s.FontSize}");
        Console.WriteLine($"opacity:    {s.ResultWindowOpacity}");
        Console.WriteLine($"delay-ms:   {s.MenuTriggerDelayMs}");
        return 0;
    }

    private int Set(Dictionary<string, string> opts)
    {
        var cfg = config.Load();
        var s   = cfg.Settings;
        if (opts.TryGetValue("font-size", out var fs) && int.TryParse(fs, out var fsi))
            s.FontSize = fsi;
        if (opts.TryGetValue("opacity",   out var op) && double.TryParse(op, out var opd))
            s.ResultWindowOpacity = Math.Clamp(opd, 0.1, 1.0);
        if (opts.TryGetValue("delay-ms",  out var dm) && int.TryParse(dm, out var dmi))
            s.MenuTriggerDelayMs = Math.Max(0, dmi);
        config.Save(cfg);
        Console.WriteLine("✓ 设置已保存");
        return 0;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("auracfg settings --show");
        Console.WriteLine("auracfg settings --set [--font-size <n>] [--opacity <0-1>] [--delay-ms <n>]");
        return 1;
    }
}
```

- [ ] **Step 2: Create `AuraTxt.Cli/Menus/InteractiveMenu.cs`**

```csharp
using AuraTxt.Cli.Commands;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Menus;

public class InteractiveMenu(ConfigService config)
{
    public async Task RunAsync()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("AuraTxt 配置工具 (auracfg)");
            Console.WriteLine(new string('─', 30));
            Console.WriteLine(" [1] 模型平台管理");
            Console.WriteLine(" [2] 功能动作管理");
            Console.WriteLine(" [3] 界面设置");
            Console.WriteLine(" [0] 退出");
            Console.Write("\n请选择：");

            var key = Console.ReadKey(true).KeyChar;
            if (key == '0') break;
            if (key == '1') await ModelMenuAsync();
            else if (key == '2') await ActionMenuAsync();
            else if (key == '3') SettingsMenu();
        }
    }

    private async Task ModelMenuAsync()
    {
        Console.Clear();
        Console.WriteLine("── 模型平台管理 ──");
        Console.WriteLine("[1] 查看所有 [2] 添加 [3] 修改 [4] 删除 [0] 返回");
        Console.Write("选择：");
        var key = Console.ReadKey(true).KeyChar;
        Console.WriteLine();
        var cmd = new ModelCommand(config);
        if (key == '1') await cmd.ExecuteAsync(["--list"]);
        else if (key == '2') await AddModelInteractive(cmd);
        else if (key == '3') await EditModelInteractive(cmd);
        else if (key == '4') await DeleteModelInteractive(cmd);
        if (key != '0') { Console.Write("\n按任意键继续…"); Console.ReadKey(true); }
    }

    private static async Task AddModelInteractive(ModelCommand cmd)
    {
        Console.Write("平台 ID: ");      var id   = Console.ReadLine()?.Trim() ?? "";
        Console.Write("显示名称: ");     var name = Console.ReadLine()?.Trim() ?? "";
        Console.Write("API Base URL: "); var url  = Console.ReadLine()?.Trim() ?? "";
        Console.Write("API Key: ");      var key  = Console.ReadLine()?.Trim() ?? "";
        Console.Write("模型名称: ");     var model= Console.ReadLine()?.Trim() ?? "";
        await cmd.ExecuteAsync(["--set","--id",id,"--display",name,"--url",url,"--key",key,"--model",model]);
    }

    private async Task EditModelInteractive(ModelCommand cmd)
    {
        await cmd.ExecuteAsync(["--list"]);
        Console.Write("\n输入要修改的 ID: ");
        var id = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrEmpty(id)) return;
        Console.WriteLine("（直接回车保持原值）");
        Console.Write("新显示名称: "); var name = Console.ReadLine()?.Trim();
        Console.Write("新 URL: ");     var url  = Console.ReadLine()?.Trim();
        Console.Write("新 Key: ");     var key  = Console.ReadLine()?.Trim();
        Console.Write("新模型名: ");   var mdl  = Console.ReadLine()?.Trim();
        var args = new List<string> { "--update", "--id", id };
        if (!string.IsNullOrEmpty(name)) { args.Add("--display"); args.Add(name); }
        if (!string.IsNullOrEmpty(url))  { args.Add("--url");     args.Add(url);  }
        if (!string.IsNullOrEmpty(key))  { args.Add("--key");     args.Add(key);  }
        if (!string.IsNullOrEmpty(mdl))  { args.Add("--model");   args.Add(mdl);  }
        await cmd.ExecuteAsync(args.ToArray());
    }

    private async Task DeleteModelInteractive(ModelCommand cmd)
    {
        await cmd.ExecuteAsync(["--list"]);
        Console.Write("\n输入要删除的 ID: ");
        var id = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrEmpty(id)) return;
        Console.Write($"确认删除 '{id}'？(y/N) ");
        if (Console.ReadLine()?.Trim().ToLower() == "y")
            await cmd.ExecuteAsync(["--delete", "--id", id]);
    }

    private async Task ActionMenuAsync()
    {
        Console.Clear();
        Console.WriteLine("── 功能动作管理 ──");
        Console.WriteLine("[1] 查看所有 [2] 添加 [3] 修改 [4] 删除 [0] 返回");
        Console.Write("选择：");
        var key = Console.ReadKey(true).KeyChar;
        Console.WriteLine();
        var cmd = new ActionCommand(config);
        if (key == '1') await cmd.ExecuteAsync(["--list"]);
        else if (key == '2') await AddActionInteractive(cmd);
        else if (key == '3') await EditActionInteractive(cmd);
        else if (key == '4') await DeleteActionInteractive(cmd);
        if (key != '0') { Console.Write("\n按任意键继续…"); Console.ReadKey(true); }
    }

    private async Task AddActionInteractive(ActionCommand cmd)
    {
        var hv  = new HotkeyValidator();
        var cfg = config.Load();
        Console.Write("动作 ID: ");        var id    = Console.ReadLine()?.Trim() ?? "";
        Console.Write("动作名称: ");       var name  = Console.ReadLine()?.Trim() ?? "";
        Console.Write("图标 (lucide): ");  var icon  = Console.ReadLine()?.Trim() ?? "";

        // Show available model aliases
        Console.WriteLine("可用模型：");
        Console.WriteLine("  $google-translate  $youdao-dict");
        foreach (var (mid, m) in cfg.Models) Console.WriteLine($"  {mid} ({m.Alias})");
        Console.Write("绑定模型 ID: "); var modelId = Console.ReadLine()?.Trim() ?? "";

        Console.Write("是否交互式 (y/N): ");
        var interactive = (Console.ReadLine()?.Trim().ToLower() == "y").ToString().ToLower();
        Console.Write("Prompt 内容: ");    var prompt = Console.ReadLine()?.Trim() ?? "";

        // Hotkey with validation
        string hotkey = "";
        while (true)
        {
            Console.Write("快捷键 (留空跳过, Esc取消): ");
            var line = ReadLineWithEsc();
            if (line is null) { Console.WriteLine("已取消"); return; }
            if (string.IsNullOrEmpty(line)) break;
            var (res, conflict) = hv.Validate(line, cfg.Actions);
            if (res == HotkeyValidationResult.InvalidFormat)
            { Console.WriteLine("格式无效，示例：Alt+T"); continue; }
            if (res == HotkeyValidationResult.SystemReserved)
            { Console.WriteLine("系统保留热键"); continue; }
            if (res == HotkeyValidationResult.Conflict)
            { Console.WriteLine($"已被「{conflict}」使用"); continue; }
            // Valid — confirm
            Console.Write($"设置快捷键为 {line}？(y/N) ");
            if (Console.ReadLine()?.Trim().ToLower() == "y") { hotkey = line; break; }
        }

        var args = new List<string>
            { "--set","--id",id,"--name",name,"--icon",icon,
              "--model-id",modelId,"--interactive",interactive,"--prompt",prompt };
        if (!string.IsNullOrEmpty(hotkey)) { args.Add("--hotkey"); args.Add(hotkey); }
        await cmd.ExecuteAsync(args.ToArray());
    }

    private async Task EditActionInteractive(ActionCommand cmd)
    {
        await cmd.ExecuteAsync(["--list"]);
        Console.Write("\n输入要修改的 ID: ");
        var id = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrEmpty(id)) return;
        Console.WriteLine("（直接回车保持原值）");
        Console.Write("新名称: ");    var name   = Console.ReadLine()?.Trim();
        Console.Write("新图标: ");    var icon   = Console.ReadLine()?.Trim();
        Console.Write("新 Prompt: "); var prompt = Console.ReadLine()?.Trim();

        var cfg = config.Load();
        var hv  = new HotkeyValidator();
        string hotkey = "";
        while (true)
        {
            Console.Write("新快捷键 (留空跳过, Esc取消): ");
            var line = ReadLineWithEsc();
            if (line is null) break;
            if (string.IsNullOrEmpty(line)) break;
            var (res, conflict) = hv.Validate(line, cfg.Actions, excludeId: id);
            if (res == HotkeyValidationResult.InvalidFormat)
            { Console.WriteLine("格式无效"); continue; }
            if (res == HotkeyValidationResult.SystemReserved)
            { Console.WriteLine("系统保留热键"); continue; }
            if (res == HotkeyValidationResult.Conflict)
            { Console.WriteLine($"已被「{conflict}」使用"); continue; }
            Console.Write($"设置快捷键为 {line}？(y/N) ");
            if (Console.ReadLine()?.Trim().ToLower() == "y") { hotkey = line; break; }
        }

        var args = new List<string> { "--update", "--id", id };
        if (!string.IsNullOrEmpty(name))   { args.Add("--name");   args.Add(name);   }
        if (!string.IsNullOrEmpty(icon))   { args.Add("--icon");   args.Add(icon);   }
        if (!string.IsNullOrEmpty(prompt)) { args.Add("--prompt"); args.Add(prompt); }
        if (!string.IsNullOrEmpty(hotkey)) { args.Add("--hotkey"); args.Add(hotkey); }
        await cmd.ExecuteAsync(args.ToArray());
    }

    private async Task DeleteActionInteractive(ActionCommand cmd)
    {
        await cmd.ExecuteAsync(["--list"]);
        Console.Write("\n输入要删除的 ID: ");
        var id = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrEmpty(id)) return;
        Console.Write($"确认删除 '{id}'？(y/N) ");
        if (Console.ReadLine()?.Trim().ToLower() == "y")
            await cmd.ExecuteAsync(["--delete", "--id", id]);
    }

    private void SettingsMenu()
    {
        Console.Clear();
        new SettingsCommand(config).ExecuteAsync(["--show"]).Wait();
        Console.WriteLine("\n修改设置（留空保持原值）：");
        Console.Write("字体大小: "); var fs = Console.ReadLine()?.Trim();
        Console.Write("透明度 (0-1): "); var op = Console.ReadLine()?.Trim();
        Console.Write("触发延迟(ms): "); var dm = Console.ReadLine()?.Trim();
        var args = new List<string> { "--set" };
        if (!string.IsNullOrEmpty(fs)) { args.Add("--font-size"); args.Add(fs); }
        if (!string.IsNullOrEmpty(op)) { args.Add("--opacity"); args.Add(op); }
        if (!string.IsNullOrEmpty(dm)) { args.Add("--delay-ms"); args.Add(dm); }
        if (args.Count > 1)
            new SettingsCommand(config).ExecuteAsync(args.ToArray()).Wait();
        Console.Write("按任意键继续…"); Console.ReadKey(true);
    }

    // Returns null on Esc, otherwise the line typed
    private static string? ReadLineWithEsc()
    {
        var sb = new System.Text.StringBuilder();
        while (true)
        {
            var k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.Escape) { Console.WriteLine(); return null; }
            if (k.Key == ConsoleKey.Enter)  { Console.WriteLine(); return sb.ToString(); }
            if (k.Key == ConsoleKey.Backspace && sb.Length > 0)
            { sb.Remove(sb.Length - 1, 1); Console.Write("\b \b"); continue; }
            if (k.KeyChar != '\0') { sb.Append(k.KeyChar); Console.Write(k.KeyChar); }
        }
    }
}
```

- [ ] **Step 3: Smoke-test interactive mode**

```powershell
dotnet run --project AuraTxt.Cli
```
Expected: menu appears, navigate works, Esc in hotkey field cancels.

- [ ] **Step 4: Commit**

```powershell
git add AuraTxt.Cli/Commands/SettingsCommand.cs AuraTxt.Cli/Menus/
git commit -m "feat(cli): add settings command and full interactive menu with hotkey validation"
```

---

## Phase 3 — AuraTxt WPF App

### Task 12: WPF Project Setup

**Files:**
- Create: `AuraTxt/AuraTxt.csproj`
- Modify: `AuraTxt/App.xaml` + `AuraTxt/App.xaml.cs`

- [ ] **Step 1: Create WPF project**

```powershell
cd C:\Users\ldd\Documents\Works\AuraTxt
dotnet new wpf -n AuraTxt -f net8.0-windows -o AuraTxt
dotnet sln add AuraTxt/AuraTxt.csproj
dotnet add AuraTxt/AuraTxt.csproj reference AuraTxt.Core/AuraTxt.Core.csproj
```

- [ ] **Step 2: Add NuGet packages**

```powershell
cd AuraTxt
dotnet add package H.NotifyIcon.Wpf --version 2.1.0
dotnet add package MouseKeyHook --version 5.6.0
dotnet add package NHotkey.Wpf --version 3.0.0
dotnet add package SharpVectors.Wpf --version 1.8.1
cd ..
```

- [ ] **Step 3: Update `AuraTxt/AuraTxt.csproj`** — enable Windows Forms for NotifyIcon fallback and set no main window

Replace content:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>AuraTxt</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\AuraTxt.Core\AuraTxt.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="H.NotifyIcon.Wpf" Version="2.1.0" />
    <PackageReference Include="MouseKeyHook" Version="5.6.0" />
    <PackageReference Include="NHotkey.Wpf" Version="3.0.0" />
    <PackageReference Include="SharpVectors.Wpf" Version="1.8.1" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Update `AuraTxt/App.xaml`** — no startup window, tray-only

```xml
<Application x:Class="AuraTxt.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
  <Application.Resources />
</Application>
```

- [ ] **Step 5: Update `AuraTxt/App.xaml.cs`**

```csharp
using System.Windows;
using AuraTxt.Services;
using AuraTxt.Core.Services;

namespace AuraTxt;

public partial class App : Application
{
    private TrayIconManager? _tray;
    private GlobalHookService? _hook;
    private HotkeyService? _hotkeys;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var config = new ConfigService();
        _tray    = new TrayIconManager(config, () => Shutdown());
        _hotkeys = new HotkeyService(config);
        _hook    = new GlobalHookService(config, _hotkeys);
        _hook.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Stop();
        _hotkeys?.UnregisterAll();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 6: Verify build**

```powershell
dotnet build AuraTxt/AuraTxt.csproj
```
Expected: `Build succeeded.` (will have missing type errors — resolve in next tasks)

- [ ] **Step 7: Commit**

```powershell
git add AuraTxt/
git commit -m "feat(app): scaffold WPF project with tray-only shutdown mode"
```

---

### Task 13: AppState + TrayIconManager

**Files:**
- Create: `AuraTxt/Services/AppState.cs`
- Create: `AuraTxt/Services/TrayIconManager.cs`
- Create: `AuraTxt/Resources/tray.ico` (placeholder 16x16 ICO)

- [ ] **Step 1: Create `AuraTxt/Services/AppState.cs`** (shared static flags, avoids cross-task circular reference)

```csharp
namespace AuraTxt.Services;

public static class AppState
{
    public static bool IsMonitoringPaused { get; set; }
}
```

- [ ] **Step 2: Create a placeholder tray icon**

```powershell
# Download a simple placeholder icon (16x16 white circle)
# Or copy any .ico file as AuraTxt/Resources/tray.ico
# For now create a minimal valid ICO via PowerShell:
$bytes = [Convert]::FromBase64String("AAABAAEAEBAAAAEAIABoBAAAFgAAACgAAAAQAAAAIAAAAAEAIAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
[IO.File]::WriteAllBytes("AuraTxt/Resources/tray.ico", $bytes)
```

- [ ] **Step 2: Mark ICO as embedded resource in csproj** — add to `AuraTxt/AuraTxt.csproj`:

```xml
<ItemGroup>
  <Resource Include="Resources\tray.ico" />
</ItemGroup>
```

- [ ] **Step 3: Create `AuraTxt/Services/TrayIconManager.cs`**

```csharp
using System.Windows;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using AuraTxt.Core.Services;

namespace AuraTxt.Services;

public class TrayIconManager : IDisposable
{
    private readonly TaskbarIcon _icon;

    public TrayIconManager(ConfigService config, Action onExit)
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = "AuraTxt",
            IconSource  = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Resources/tray.ico"))
        };

        var menu = new System.Windows.Controls.ContextMenu();

        var toggle = new System.Windows.Controls.MenuItem { Header = "暂停监听" };
        toggle.Click += (_, _) =>
        {
            toggle.Header = toggle.Header.ToString() == "暂停监听" ? "恢复监听" : "暂停监听";
            AppState.IsMonitoringPaused = !AppState.IsMonitoringPaused;
        };

        var settings = new System.Windows.Controls.MenuItem { Header = "配置 (auracfg)" };
        settings.Click += (_, _) =>
        {
            var auracfg = System.IO.Path.Combine(
                AppContext.BaseDirectory, "auracfg.exe");
            System.Diagnostics.Process.Start(auracfg);
        };

        var exit = new System.Windows.Controls.MenuItem { Header = "退出" };
        exit.Click += (_, _) => onExit();

        menu.Items.Add(toggle);
        menu.Items.Add(settings);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(exit);

        _icon.ContextMenu = menu;
        _icon.ForceCreate();
    }

    public void Dispose() => _icon.Dispose();
}
```

- [ ] **Step 4: Verify build**

```powershell
dotnet build AuraTxt/AuraTxt.csproj
```

- [ ] **Step 5: Commit**

```powershell
git add AuraTxt/Services/TrayIconManager.cs AuraTxt/Resources/tray.ico AuraTxt/AuraTxt.csproj
git commit -m "feat(app): add system tray icon with context menu"
```

---

### Task 14: ClipboardService + GlobalHookService

**Files:**
- Create: `AuraTxt/Services/ClipboardService.cs`
- Create: `AuraTxt/Services/GlobalHookService.cs`

- [ ] **Step 1: Create `AuraTxt/Services/ClipboardService.cs`**

```csharp
using System.Windows;

namespace AuraTxt.Services;

public static class ClipboardService
{
    /// Temporarily sends Ctrl+C to get selected text, then restores clipboard.
    /// Returns empty string if no text is selected or clipboard op fails.
    public static async Task<string> GetSelectedTextAsync(int delayMs = 100)
    {
        await Task.Delay(delayMs);
        string previous = "";
        string selected = "";

        try
        {
            // Save current clipboard
            if (Clipboard.ContainsText())
                previous = Clipboard.GetText();

            Clipboard.Clear();

            // Send Ctrl+C to active window
            System.Windows.Forms.SendKeys.SendWait("^c");
            await Task.Delay(80);

            if (Clipboard.ContainsText())
                selected = Clipboard.GetText();
        }
        catch { /* clipboard unavailable — return empty */ }
        finally
        {
            try
            {
                if (!string.IsNullOrEmpty(previous))
                    Clipboard.SetText(previous);
                else
                    Clipboard.Clear();
            }
            catch { }
        }
        return selected;
    }
}
```

- [ ] **Step 2: Create `AuraTxt/Services/GlobalHookService.cs`**

```csharp
using System.Windows;
using Gma.System.MouseKeyHook;
using AuraTxt.Core.Services;
using AuraTxt.Windows;

namespace AuraTxt.Services;

public class GlobalHookService
{
    private readonly ConfigService _config;
    private readonly HotkeyService _hotkeys;
    private IKeyboardMouseEvents? _hook;

    public GlobalHookService(ConfigService config, HotkeyService hotkeys)
    {
        _config  = config;
        _hotkeys = hotkeys;
    }

    public void Start()
    {
        _hook = Hook.GlobalEvents();
        _hook.MouseUpExt += OnMouseUp;
        _hotkeys.RegisterAll(_config.Load());
    }

    public void Stop()
    {
        if (_hook is null) return;
        _hook.MouseUpExt -= OnMouseUp;
        _hook.Dispose();
        _hook = null;
    }

    private void OnMouseUp(object? sender, MouseEventExtArgs e)
    {
        if (AppState.IsMonitoringPaused || e.Button != System.Windows.Forms.MouseButtons.Left) return;
        var pos = new System.Drawing.Point(e.X, e.Y);
        // Fire-and-forget: get selected text then show menu
        _ = TryShowMenuAsync(pos);
    }

    private async Task TryShowMenuAsync(System.Drawing.Point cursorPos)
    {
        var text = await ClipboardService.GetSelectedTextAsync(
            _config.Load().Settings.MenuTriggerDelayMs);
        if (string.IsNullOrWhiteSpace(text)) return;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var cfg  = _config.Load();
            var menu = new ActionMenuWindow(cfg, text, cursorPos);
            menu.Show();
        });
    }
}
```

- [ ] **Step 3: Add UseWindowsForms to AuraTxt.csproj** for `SendKeys`

In `AuraTxt/AuraTxt.csproj` `<PropertyGroup>`:

```xml
<UseWindowsForms>true</UseWindowsForms>
```

- [ ] **Step 4: Verify build** (ActionMenuWindow not yet defined — OK for now)

```powershell
dotnet build AuraTxt/AuraTxt.csproj 2>&1 | Select-String -Pattern "error|warning" | Select-Object -First 20
```

- [ ] **Step 5: Commit**

```powershell
git add AuraTxt/Services/ClipboardService.cs AuraTxt/Services/GlobalHookService.cs AuraTxt/AuraTxt.csproj
git commit -m "feat(app): add mouse hook and clipboard-based text selection detection"
```

---

### Task 15: HotkeyService

**Files:**
- Create: `AuraTxt/Services/HotkeyService.cs`

- [ ] **Step 1: Create `AuraTxt/Services/HotkeyService.cs`**

```csharp
using System.Windows;
using System.Windows.Input;
using NHotkey;
using NHotkey.Wpf;
using AuraTxt.Core.Models;
using AuraTxt.Windows;
using AuraTxt.Core.Services;

namespace AuraTxt.Services;

public class HotkeyService
{
    private readonly ConfigService _config;
    private readonly List<string> _registered = new();

    public HotkeyService(ConfigService config) => _config = config;

    public void RegisterAll(ConfigRoot cfg)
    {
        UnregisterAll();
        foreach (var action in cfg.Actions.Where(a => !string.IsNullOrEmpty(a.Hotkey)))
        {
            if (!TryParseHotkey(action.Hotkey, out var key, out var mods)) continue;
            try
            {
                var captured = action; // closure capture
                HotkeyManager.Current.AddOrReplace(action.Id, key, mods, (_, _) =>
                    _ = FireActionAsync(captured));
                _registered.Add(action.Id);
            }
            catch
            {
                // Key already registered by another app — tray notification handled in App
            }
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _registered)
            try { HotkeyManager.Current.Remove(id); } catch { }
        _registered.Clear();
    }

    private async Task FireActionAsync(ActionItem action)
    {
        var text = await ClipboardService.GetSelectedTextAsync(50);
        if (string.IsNullOrWhiteSpace(text)) return;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var cfg = _config.Load();
            ShowResultFor(action, text, cfg);
        });
    }

    public static void ShowResultFor(ActionItem action, string selectedText, ConfigRoot cfg)
    {
        if (action.IsInteractive)
        {
            var win = new InteractiveWindow(action, selectedText, cfg);
            win.Show();
        }
        else
        {
            var win = new ResultWindow(action, selectedText, cfg);
            win.Show();
        }
    }

    private static bool TryParseHotkey(string hotkey, out Key key, out ModifierKeys mods)
    {
        key  = Key.None;
        mods = ModifierKeys.None;
        var parts = hotkey.Split('+');
        if (parts.Length < 2) return false;

        foreach (var mod in parts[..^1])
        {
            mods |= mod.Trim().ToLower() switch
            {
                "ctrl"  => ModifierKeys.Control,
                "alt"   => ModifierKeys.Alt,
                "shift" => ModifierKeys.Shift,
                "win"   => ModifierKeys.Windows,
                _       => ModifierKeys.None
            };
        }

        return Enum.TryParse(parts[^1].Trim(), true, out key) && key != Key.None;
    }
}
```

- [ ] **Step 2: Verify build**

```powershell
dotnet build AuraTxt/AuraTxt.csproj 2>&1 | Select-String "^.*error" | Select-Object -First 10
```

- [ ] **Step 3: Commit**

```powershell
git add AuraTxt/Services/HotkeyService.cs
git commit -m "feat(app): add global hotkey registration service"
```

---

### Task 16: ActionMenuWindow

**Files:**
- Create: `AuraTxt/Windows/ActionMenuWindow.xaml`
- Create: `AuraTxt/Windows/ActionMenuWindow.xaml.cs`
- Create: `AuraTxt/Services/IconCacheService.cs`

- [ ] **Step 1: Create `AuraTxt/Services/IconCacheService.cs`**

```csharp
using System.IO;
using System.Net.Http;
using System.Windows.Media;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace AuraTxt.Services;

public class IconCacheService
{
    private static readonly string CacheDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "AuraTxt", "icons");
    private static readonly HttpClient Http = new();
    private static readonly WpfDrawingSettings SvgSettings = new() { IncludeRuntime = true };

    public static async Task<DrawingImage?> GetIconAsync(string lucideName)
    {
        Directory.CreateDirectory(CacheDir);
        var path = Path.Combine(CacheDir, $"{lucideName}.svg");

        if (!File.Exists(path))
        {
            try
            {
                var url = $"https://raw.githubusercontent.com/lucide-icons/lucide/main/icons/{lucideName}.svg";
                var svg = await Http.GetStringAsync(url);
                await File.WriteAllTextAsync(path, svg);
            }
            catch { return null; }
        }

        try
        {
            using var converter = new FileSvgConverter(SvgSettings);
            var drawing = converter.Convert(path);
            return drawing is null ? null : new DrawingImage(drawing);
        }
        catch { return null; }
    }
}
```

- [ ] **Step 2: Create `AuraTxt/Windows/ActionMenuWindow.xaml`**

```xml
<Window x:Class="AuraTxt.Windows.ActionMenuWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        Topmost="True" ShowInTaskbar="False" SizeToContent="WidthAndHeight"
        ResizeMode="NoResize">
  <Border Background="White" CornerRadius="999"
          Padding="6,4" Margin="0"
          Effect="{StaticResource {x:Static SystemParameters.DropShadowKey}}">
    <Border.Effect>
      <DropShadowEffect BlurRadius="12" ShadowDepth="2" Opacity="0.25" Color="Black"/>
    </Border.Effect>
    <StackPanel x:Name="IconPanel" Orientation="Horizontal" VerticalAlignment="Center"/>
  </Border>
</Window>
```

- [ ] **Step 3: Create `AuraTxt/Windows/ActionMenuWindow.xaml.cs`**

```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using AuraTxt.Services;

namespace AuraTxt.Windows;

public partial class ActionMenuWindow : Window
{
    private readonly ConfigRoot _cfg;
    private readonly string _selectedText;

    public ActionMenuWindow(ConfigRoot cfg, string selectedText, System.Drawing.Point cursor)
    {
        InitializeComponent();
        _cfg          = cfg;
        _selectedText = selectedText;

        // Position near cursor
        Left = cursor.X + 4;
        Top  = cursor.Y - 40;

        Loaded += async (_, _) => await BuildMenuAsync();
        Deactivated += (_, _) => Close();
    }

    private async Task BuildMenuAsync()
    {
        // Fixed left: Copy
        IconPanel.Children.Add(MakeButton("📋", "复制", () =>
        {
            Clipboard.SetText(_selectedText);
            Close();
        }));
        IconPanel.Children.Add(MakeSeparator());

        // Dynamic actions
        foreach (var action in _cfg.Actions)
        {
            var a   = action;
            var img = await IconCacheService.GetIconAsync(a.Icon);
            var btn = MakeButton(img, $"{a.Name}{(string.IsNullOrEmpty(a.Hotkey) ? "" : $" ({a.Hotkey})")}", () =>
            {
                Close();
                HotkeyService.ShowResultFor(a, _selectedText, _cfg);
            });
            IconPanel.Children.Add(btn);
        }

        IconPanel.Children.Add(MakeSeparator());

        // Fixed right: Settings
        IconPanel.Children.Add(MakeButton("⚙️", "设置 (auracfg)", () =>
        {
            var exe = System.IO.Path.Combine(AppContext.BaseDirectory, "auracfg.exe");
            System.Diagnostics.Process.Start(exe);
            Close();
        }));
    }

    private static Button MakeButton(object iconContent, string tooltip, Action onClick)
    {
        var content = iconContent is string emoji
            ? (UIElement)new TextBlock { Text = emoji, FontSize = 14, VerticalAlignment = VerticalAlignment.Center }
            : (UIElement)new System.Windows.Controls.Image
              {
                  Source = (ImageSource)iconContent,
                  Width = 14, Height = 14
              };

        var btn = new Button
        {
            Content             = content,
            Width               = 28, Height = 28,
            Background          = System.Windows.Media.Brushes.Transparent,
            BorderBrush         = System.Windows.Media.Brushes.Transparent,
            Cursor              = Cursors.Hand,
            ToolTip             = tooltip,
            ToolTipService      = { InitialShowDelay = 300 }
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button MakeButton(DrawingImage? img, string tooltip, Action onClick)
    {
        if (img is null) return MakeButton("?", tooltip, onClick);
        return MakeButton((object)img, tooltip, onClick);
    }

    private static Separator MakeSeparator() => new()
    {
        Width = 1, Height = 18,
        Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 224, 224)),
        Margin = new Thickness(3, 0, 3, 0)
    };
}
```

- [ ] **Step 4: Verify build**

```powershell
dotnet build AuraTxt/AuraTxt.csproj
```

- [ ] **Step 5: Commit**

```powershell
git add AuraTxt/Windows/ActionMenuWindow.* AuraTxt/Services/IconCacheService.cs
git commit -m "feat(app): add ActionMenuWindow capsule with dynamic icon loading"
```

---

### Task 17: ResultWindow

**Files:**
- Create: `AuraTxt/Windows/ResultWindow.xaml`
- Create: `AuraTxt/Windows/ResultWindow.xaml.cs`

- [ ] **Step 1: Create `AuraTxt/Windows/ResultWindow.xaml`**

```xml
<Window x:Class="AuraTxt.Windows.ResultWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None" AllowsTransparency="True"
        Background="Transparent" Topmost="True" ShowInTaskbar="False"
        Width="480" MinHeight="120" ResizeMode="CanResize"
        KeyDown="Window_KeyDown">
  <Border Background="#1E1E1E" CornerRadius="8" Margin="8">
    <Border.Effect>
      <DropShadowEffect BlurRadius="16" ShadowDepth="4" Opacity="0.4" Color="Black"/>
    </Border.Effect>
    <Grid>
      <Grid.RowDefinitions>
        <RowDefinition Height="36"/>
        <RowDefinition Height="*"/>
      </Grid.RowDefinitions>

      <!-- Title bar -->
      <Border Grid.Row="0" Background="#2A2A2A" CornerRadius="8,8,0,0"
              MouseLeftButtonDown="TitleBar_MouseDown">
        <Grid Margin="10,0">
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
          </Grid.ColumnDefinitions>
          <!-- Close button -->
          <Button Grid.Column="0" x:Name="CloseBtn" Width="12" Height="12"
                  Background="#FF5F57" BorderBrush="Transparent"
                  Template="{StaticResource CircleButtonTemplate}"
                  Click="CloseBtn_Click"/>
          <!-- Action + model label -->
          <TextBlock Grid.Column="1" x:Name="TitleLabel"
                     Foreground="#888" FontSize="11" VerticalAlignment="Center"
                     Margin="8,0" TextTrimming="CharacterEllipsis"/>
          <!-- Icon buttons -->
          <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center" Margin="0,0,4,0">
            <Button x:Name="EditBtn"  Width="26" Height="26" ToolTip="编辑 Prompt (P)"  Click="EditBtn_Click"  Style="{StaticResource IconBtnStyle}"/>
            <Button x:Name="RegenBtn" Width="26" Height="26" ToolTip="重新生成 (R)"      Click="RegenBtn_Click" Style="{StaticResource IconBtnStyle}" Margin="4,0,0,0"/>
            <Button x:Name="CopyBtn"  Width="26" Height="26" ToolTip="复制全部 (C)"      Click="CopyBtn_Click"  Background="#6366F1" BorderBrush="Transparent" Margin="4,0,0,0"
                    Foreground="White" FontSize="11" Content="C" Width="26" Height="26"/>
          </StackPanel>
        </Grid>
      </Border>

      <!-- Content -->
      <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto" Margin="4">
        <TextBox x:Name="ResultText"
                 Background="Transparent" Foreground="#E2E8F0"
                 FontSize="{Binding FontSize}" FontFamily="Segoe UI"
                 IsReadOnly="True" TextWrapping="Wrap" BorderThickness="0"
                 Padding="12" LineHeight="24" AcceptsReturn="True"/>
      </ScrollViewer>
    </Grid>
  </Border>
</Window>
```

- [ ] **Step 2: Create `AuraTxt/Windows/ResultWindow.xaml.cs`**

```csharp
using System.Windows;
using System.Windows.Input;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Windows;

public partial class ResultWindow : Window
{
    private readonly ActionItem _action;
    private readonly string _selectedText;
    private readonly ConfigRoot _cfg;
    private string _currentPrompt;

    public ResultWindow(ActionItem action, string selectedText, ConfigRoot cfg)
    {
        InitializeComponent();
        _action       = action;
        _selectedText = selectedText;
        _cfg          = cfg;
        _currentPrompt = BuildPrompt(action.Prompt, selectedText, "");

        TitleLabel.Text = $"{action.Name} · {GetModelDisplayName(action, cfg)}";
        ResultText.FontSize = cfg.Settings.FontSize;
        Opacity = cfg.Settings.ResultWindowOpacity;

        Loaded += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        ResultText.Text = "正在处理…";
        try
        {
            var result = await CallModelAsync(_currentPrompt);
            ResultText.Text = result;
        }
        catch (Exception ex)
        {
            ResultText.Text = $"[错误] {ex.Message}";
        }
    }

    private async Task<string> CallModelAsync(string prompt)
    {
        if (_action.ModelId == "$google-translate")
            return await new GoogleTranslateClient().TranslateAsync(_selectedText);
        if (_action.ModelId == "$youdao-dict")
            return await new YoudaoClient().TranslateAsync(_selectedText);

        if (!_cfg.Models.TryGetValue(_action.ModelId, out var model))
            throw new InvalidOperationException($"未找到模型：{_action.ModelId}");

        return await new AiClient().CompleteAsync(model, prompt);
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    private async void RegenBtn_Click(object sender, RoutedEventArgs e) => await RunAsync();

    private void CopyBtn_Click(object sender, RoutedEventArgs e) =>
        Clipboard.SetText(ResultText.Text);

    private void EditBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PromptEditDialog(_currentPrompt);
        if (dlg.ShowDialog() == true)
        {
            _currentPrompt = dlg.Result;
            _ = RunAsync();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.P) EditBtn_Click(sender, new RoutedEventArgs());
        else if (e.Key == Key.R) RegenBtn_Click(sender, new RoutedEventArgs());
        else if (e.Key == Key.C) CopyBtn_Click(sender, new RoutedEventArgs());
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private static string BuildPrompt(string template, string selected, string userInput) =>
        template.Replace("{SelectedText}", selected).Replace("{UserInput}", userInput);

    private static string GetModelDisplayName(ActionItem action, ConfigRoot cfg)
    {
        if (action.ModelId == "$google-translate") return "Google 翻译";
        if (action.ModelId == "$youdao-dict")      return "有道词典";
        return cfg.Models.TryGetValue(action.ModelId, out var m) ? m.DisplayName : action.ModelId;
    }
}
```

- [ ] **Step 3: Create `AuraTxt/Windows/PromptEditDialog.xaml`**

```xml
<Window x:Class="AuraTxt.Windows.PromptEditDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="编辑 Prompt" Width="480" Height="280"
        WindowStartupLocation="CenterScreen" Background="#1E1E1E" Foreground="#E2E8F0">
  <Grid Margin="12">
    <Grid.RowDefinitions>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>
    <TextBox x:Name="PromptBox" Grid.Row="0" Background="#0F172A" Foreground="#E2E8F0"
             BorderBrush="#334155" AcceptsReturn="True" TextWrapping="Wrap"
             FontFamily="Consolas" FontSize="13" Padding="8" VerticalScrollBarVisibility="Auto"/>
    <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,8,0,0">
      <Button Content="取消" Width="70" Height="28" Margin="0,0,8,0" Click="Cancel_Click"/>
      <Button Content="确认生成" Width="80" Height="28" Background="#6366F1"
              Foreground="White" BorderBrush="Transparent" Click="OK_Click"/>
    </StackPanel>
  </Grid>
</Window>
```

- [ ] **Step 4: Create `AuraTxt/Windows/PromptEditDialog.xaml.cs`**

```csharp
using System.Windows;

namespace AuraTxt.Windows;

public partial class PromptEditDialog : Window
{
    public string Result { get; private set; } = "";

    public PromptEditDialog(string currentPrompt)
    {
        InitializeComponent();
        PromptBox.Text = currentPrompt;
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        Result = PromptBox.Text;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
```

- [ ] **Step 5: Verify build**

```powershell
dotnet build AuraTxt/AuraTxt.csproj
```

- [ ] **Step 6: Commit**

```powershell
git add AuraTxt/Windows/ResultWindow.* AuraTxt/Windows/PromptEditDialog.*
git commit -m "feat(app): add ResultWindow with P/R/C icon buttons and prompt editing"
```

---

### Task 18: InteractiveWindow

**Files:**
- Create: `AuraTxt/Windows/InteractiveWindow.xaml`
- Create: `AuraTxt/Windows/InteractiveWindow.xaml.cs`

- [ ] **Step 1: Create `AuraTxt/Windows/InteractiveWindow.xaml`**

```xml
<Window x:Class="AuraTxt.Windows.InteractiveWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None" AllowsTransparency="True"
        Background="Transparent" Topmost="True" ShowInTaskbar="False"
        Width="520" Height="400" ResizeMode="CanResize"
        KeyDown="Window_KeyDown">
  <Border Background="#1E1E1E" CornerRadius="8" Margin="8">
    <Border.Effect>
      <DropShadowEffect BlurRadius="16" ShadowDepth="4" Opacity="0.4" Color="Black"/>
    </Border.Effect>
    <Grid>
      <Grid.RowDefinitions>
        <RowDefinition Height="36"/>
        <RowDefinition Height="120"/>
        <RowDefinition Height="*"/>
      </Grid.RowDefinitions>

      <!-- Title bar -->
      <Border Grid.Row="0" Background="#2A2A2A" CornerRadius="8,8,0,0"
              MouseLeftButtonDown="TitleBar_MouseDown">
        <Grid Margin="10,0">
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
          </Grid.ColumnDefinitions>
          <Button Grid.Column="0" Width="12" Height="12" Background="#FF5F57"
                  BorderBrush="Transparent" Template="{StaticResource CircleButtonTemplate}"
                  Click="CloseBtn_Click"/>
          <TextBlock Grid.Column="1" x:Name="TitleLabel" Foreground="#888"
                     FontSize="11" VerticalAlignment="Center" Margin="8,0"/>
          <ComboBox Grid.Column="2" x:Name="ModelPicker" Margin="4,0" Height="22"
                    Background="#1A2744" Foreground="#93C5FD" FontSize="10"
                    BorderBrush="#1E3A8A" VerticalContentAlignment="Center"
                    SelectionChanged="ModelPicker_SelectionChanged"/>
          <!-- Icon buttons: Send | Edit | Regen | Copy -->
          <StackPanel Grid.Column="3" Orientation="Horizontal" VerticalAlignment="Center" Margin="0,0,4,0">
            <Button x:Name="SendBtn"  Width="26" Height="26" Background="#22C55E"
                    BorderBrush="Transparent" ToolTip="发送生成 (Enter)" Click="SendBtn_Click" Margin="0,0,4,0"/>
            <Separator Width="1" Height="16" Background="#3D3D3D"/>
            <Button x:Name="EditBtn"  Width="26" Height="26" Style="{StaticResource IconBtnStyle}" ToolTip="编辑 Prompt (P)" Click="EditBtn_Click" Margin="4,0,0,0"/>
            <Button x:Name="RegenBtn" Width="26" Height="26" Style="{StaticResource IconBtnStyle}" ToolTip="重新生成 (R)"    Click="RegenBtn_Click" Margin="4,0,0,0"/>
            <Button x:Name="CopyBtn"  Width="26" Height="26" Background="#6366F1"
                    BorderBrush="Transparent" Foreground="White" ToolTip="复制全部 (C)"
                    Click="CopyBtn_Click" Margin="4,0,0,0"/>
          </StackPanel>
        </Grid>
      </Border>

      <!-- User input -->
      <Border Grid.Row="1" Background="#181818" BorderBrush="#2D2D2D" BorderThickness="0,0,0,1">
        <Grid Margin="12,8">
          <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
          </Grid.RowDefinitions>
          <TextBlock Text="回复意图" Foreground="#64748B" FontSize="10" Margin="0,0,0,4"/>
          <TextBox x:Name="UserInput" Grid.Row="1" Background="#0F172A" Foreground="#CBD5E1"
                   BorderBrush="#334155" AcceptsReturn="True" TextWrapping="Wrap"
                   FontSize="13" Padding="8" PreviewKeyDown="UserInput_KeyDown"/>
        </Grid>
      </Border>

      <!-- AI result -->
      <ScrollViewer Grid.Row="2" VerticalScrollBarVisibility="Auto">
        <Grid Margin="12,8">
          <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
          </Grid.RowDefinitions>
          <TextBlock Text="生成结果" Foreground="#64748B" FontSize="10" Margin="0,0,0,4"/>
          <TextBox x:Name="ResultText" Grid.Row="1" Background="Transparent"
                   Foreground="#E2E8F0" IsReadOnly="True" TextWrapping="Wrap"
                   BorderThickness="0" FontSize="13" LineHeight="24"/>
        </Grid>
      </ScrollViewer>
    </Grid>
  </Border>
</Window>
```

- [ ] **Step 2: Create `AuraTxt/Windows/InteractiveWindow.xaml.cs`**

```csharp
using System.Windows;
using System.Windows.Input;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Windows;

public partial class InteractiveWindow : Window
{
    private readonly ActionItem _action;
    private readonly string _selectedText;
    private ConfigRoot _cfg;
    private string _currentPrompt;
    private ModelPlatform? _activeModel;

    public InteractiveWindow(ActionItem action, string selectedText, ConfigRoot cfg)
    {
        InitializeComponent();
        _action       = action;
        _selectedText = selectedText;
        _cfg          = cfg;
        _currentPrompt = action.Prompt;

        TitleLabel.Text = action.Name;
        ResultText.FontSize = cfg.Settings.FontSize;
        Opacity = cfg.Settings.ResultWindowOpacity;

        // Populate model picker with all user models
        ModelPicker.ItemsSource = cfg.Models
            .Select(kvp => new { Id = kvp.Key, Label = kvp.Value.Alias })
            .ToList();
        ModelPicker.DisplayMemberPath = "Label";
        ModelPicker.SelectedValuePath = "Id";

        // Pre-select the action's bound model if it's a user model
        if (cfg.Models.ContainsKey(action.ModelId))
        {
            ModelPicker.SelectedValue = action.ModelId;
            _activeModel = cfg.Models[action.ModelId];
        }
    }

    private void ModelPicker_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ModelPicker.SelectedValue is string id && _cfg.Models.TryGetValue(id, out var m))
            _activeModel = m;
    }

    private async void SendBtn_Click(object sender, RoutedEventArgs e) => await GenerateAsync();

    private async void RegenBtn_Click(object sender, RoutedEventArgs e) => await GenerateAsync();

    private async Task GenerateAsync()
    {
        if (_activeModel is null && !_action.IsSystemModel) 
        {
            ResultText.Text = "[错误] 请先选择模型";
            return;
        }
        ResultText.Text = "正在处理…";
        var prompt = _currentPrompt
            .Replace("{SelectedText}", _selectedText)
            .Replace("{UserInput}", UserInput.Text);
        try
        {
            ResultText.Text = await new AiClient().CompleteAsync(_activeModel!, prompt);
        }
        catch (Exception ex)
        {
            ResultText.Text = $"[错误] {ex.Message}";
        }
    }

    private void CopyBtn_Click(object sender, RoutedEventArgs e) =>
        Clipboard.SetText(ResultText.Text);

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
}
```

- [ ] **Step 3: Verify build**

```powershell
dotnet build AuraTxt/AuraTxt.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```powershell
git add AuraTxt/Windows/InteractiveWindow.*
git commit -m "feat(app): add InteractiveWindow with user-input, model picker, and icon buttons"
```

---

### Task 19: WPF Shared Resources (Styles)

**Files:**
- Create: `AuraTxt/Resources/Styles.xaml`
- Modify: `AuraTxt/App.xaml`

- [ ] **Step 1: Create `AuraTxt/Resources/Styles.xaml`**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- Circular button template (for close dot) -->
  <ControlTemplate x:Key="CircleButtonTemplate" TargetType="Button">
    <Ellipse Fill="{TemplateBinding Background}"
             Width="{TemplateBinding Width}" Height="{TemplateBinding Height}"/>
  </ControlTemplate>

  <!-- Dark icon button style -->
  <Style x:Key="IconBtnStyle" TargetType="Button">
    <Setter Property="Background"   Value="#2D2D2D"/>
    <Setter Property="BorderBrush"  Value="#3D3D3D"/>
    <Setter Property="Foreground"   Value="#94A3B8"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Cursor"       Value="Hand"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border Background="{TemplateBinding Background}"
                  BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}"
                  CornerRadius="5">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
    <Style.Triggers>
      <Trigger Property="IsMouseOver" Value="True">
        <Setter Property="Background" Value="#3D3D3D"/>
      </Trigger>
    </Style.Triggers>
  </Style>

</ResourceDictionary>
```

- [ ] **Step 2: Update `AuraTxt/App.xaml`** to merge the dictionary

```xml
<Application x:Class="AuraTxt.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="Resources/Styles.xaml"/>
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

- [ ] **Step 3: Final build**

```powershell
dotnet build AuraTxt.sln
```
Expected: `Build succeeded.  0 Error(s)`

- [ ] **Step 4: Commit**

```powershell
git add AuraTxt/Resources/Styles.xaml AuraTxt/App.xaml
git commit -m "feat(app): add shared WPF styles for icon buttons and close dot"
```

---

## End-to-End Manual Verification

After all tasks complete, verify these scenarios in order:

- [ ] **V1** Run `dotnet run --project AuraTxt.Cli -- model --set --id ds --display DeepSeek --url https://api.deepseek.com --key sk-xxx --model deepseek-chat` → config.json updated
- [ ] **V2** Run `dotnet run --project AuraTxt.Cli -- action --set --id translate --name 翻译 --icon languages --model-id '$google-translate' --interactive false --prompt "{SelectedText}" --hotkey Alt+T` → action added
- [ ] **V3** Run `dotnet run --project AuraTxt.Cli -- action --set --id reply --name 智能回复 --icon mail --model-id ds --interactive true --prompt "原文：{SelectedText} 意图：{UserInput} 请润色回信" --hotkey Alt+R`
- [ ] **V4** Run `dotnet run --project AuraTxt -- ` → tray icon appears, no main window
- [ ] **V5** Open Notepad, type text, select it → action menu capsule appears near cursor
- [ ] **V6** Click translate icon → ResultWindow shows Google Translate result
- [ ] **V7** Press `Alt+T` with text selected → same result without clicking menu
- [ ] **V8** Click reply icon → InteractiveWindow opens, type intent, press Enter → AI result appears
- [ ] **V9** Press P in ResultWindow → PromptEditDialog opens, edit and confirm → new result shown
- [ ] **V10** Run `dotnet run --project AuraTxt.Cli -- action --set --id foo --hotkey Alt+T` → conflict error shown

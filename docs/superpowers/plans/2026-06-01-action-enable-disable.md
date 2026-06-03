# Action Enable/Disable & System Actions — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add enable/disable per action, two undeletable system actions (copy/speech), multi-line prompt input in auracfg CLI, and WPF system-action routing.

**Architecture:** Two new bools on `ActionItem` (`Enabled`, `IsSystem`). CLI shows color-coded status, blocks delete of system actions, supports multi-line prompt paste with Ctrl+D. WPF filters disabled actions and routes empty-ModelId system actions to hardcoded handlers (copy→clipboard, speech→TTS).

**Tech Stack:** C# 12, .NET 8 (WPF) / net10.0 (CLI), System.Speech.Synthesis (TTS)

---

## File Map

| File | Responsibility |
|------|---------------|
| `AuraTxt.Core/Models/ActionItem.cs` | Data model — new `Enabled`, `IsSystem` |
| `AuraTxt.Core/Services/ConfigService.cs` | `CreateDefault()` seeds system actions |
| `AuraTxt.Cli/Commands/ActionCommand.cs` | Batch CLI — `--enabled`, delete guard |
| `AuraTxt.Cli/Menus/InteractiveMenu.cs` | TUI — list colors, detail menus, multi-line prompt, delete skip, create ask |
| `AuraTxt/Windows/ActionMenuWindow.xaml.cs` | WPF — filter disabled, route system actions, remove hardcoded copy |

---

### Task 1: Add `Enabled` and `IsSystem` to `ActionItem`

**Files:**
- Modify: `AuraTxt.Core/Models/ActionItem.cs`

- [ ] **Step 1: Add the two properties**

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
    public bool   Enabled       { get; set; } = true;   // NEW
    public bool   IsSystem      { get; set; }            // NEW

    public bool IsSystemModel => ModelId.StartsWith("default/", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Build to verify it compiles**

```powershell
dotnet build AuraTxt.Core/AuraTxt.Core.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add AuraTxt.Core/Models/ActionItem.cs
git commit -m "feat: add Enabled and IsSystem properties to ActionItem"
```

---

### Task 2: Seed system actions in `ConfigService.CreateDefault()`

**Files:**
- Modify: `AuraTxt.Core/Services/ConfigService.cs`

- [ ] **Step 1: Add copy and speech actions after default models**

The `CreateDefault()` method currently creates `Models["default"]` then calls `Save(cfg)`. Add two system actions right before the `Save` call:

```csharp
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

    // NEW: system actions
    cfg.Actions.Add(new ActionItem
    {
        Id       = "copy",
        Name     = "Copy",
        Icon     = "clipboard-copy",
        Hotkey   = "Ctrl+C",
        Enabled  = true,
        IsSystem = true
    });
    cfg.Actions.Add(new ActionItem
    {
        Id       = "speech",
        Name     = "Speech",
        Icon     = "speech",
        Hotkey   = "Ctrl+E",
        Enabled  = true,
        IsSystem = true
    });

    Save(cfg);
    return cfg;
}
```

- [ ] **Step 2: Build to verify**

```powershell
dotnet build AuraTxt.Core/AuraTxt.Core.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add AuraTxt.Core/Services/ConfigService.cs
git commit -m "feat: seed copy and speech system actions in default config"
```

---

### Task 3: Batch CLI — `--enabled` param and delete guard

**Files:**
- Modify: `AuraTxt.Cli/Commands/ActionCommand.cs`

- [ ] **Step 1: Add `--enabled` support to `--set`**

In the `Set` method, after the `IsInteractive` line, add `Enabled`:

```csharp
var item = new ActionItem
{
    Id            = id,
    Name          = opts.GetValueOrDefault("name", ""),
    Icon          = opts.GetValueOrDefault("icon", ""),
    ModelId       = opts.GetValueOrDefault("model-id", ""),
    IsInteractive = opts.GetValueOrDefault("interactive", "false") == "true",
    Hotkey        = opts.GetValueOrDefault("hotkey", ""),
    Prompt        = opts.GetValueOrDefault("prompt", ""),
    Enabled       = opts.GetValueOrDefault("enabled", "true") == "true"   // NEW
};
```

- [ ] **Step 2: Add `--enabled` support to `--update`**

In the `Update` method, after the `IsInteractive` parsing line, add:

```csharp
if (opts.TryGetValue("enabled", out var en)) item.Enabled = en == "true";
```

- [ ] **Step 3: Add delete guard for system actions**

In the `Delete` method, after finding the item but before removing it, add the guard:

```csharp
private int Delete(Dictionary<string, string> opts)
{
    if (!opts.TryGetValue("id", out var id)) return Err("Missing --id");
    var cfg     = config.Load();
    var item    = cfg.Actions.FirstOrDefault(a => a.Id == id);
    if (item is null) return Err($"Action '{id}' not found", 2);

    // NEW: guard system actions
    if (item.IsSystem) return Err($"Cannot delete system action '{id}'", 2);

    var removed = cfg.Actions.RemoveAll(a => a.Id == id);
    config.Save(cfg);
    Console.WriteLine("✓ Deleted");
    return 0;
}
```

- [ ] **Step 4: Update help text**

Replace the `PrintHelp` method:

```csharp
private static int PrintHelp()
{
    Console.WriteLine("auracfg action --list");
    Console.WriteLine("auracfg action --set    --id <id> --name <name> --icon <lucide> --model-id <id> --interactive <true|false> --prompt \"<text>\" [--hotkey <key>] [--enabled <true|false>]");
    Console.WriteLine("auracfg action --update --id <id> [any field including --enabled]");
    Console.WriteLine("auracfg action --delete --id <id>");
    return 1;
}
```

- [ ] **Step 5: Build to verify**

```powershell
dotnet build AuraTxt.Cli/AuraTxt.Cli.csproj
```

Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add AuraTxt.Cli/Commands/ActionCommand.cs
git commit -m "feat: add --enabled param to action CLI, guard system action deletion"
```

---

### Task 4: Multi-line prompt input in `InteractiveMenu`

**Files:**
- Modify: `AuraTxt.Cli/Menus/InteractiveMenu.cs`

- [ ] **Step 1: Replace `AskPrompt` with multi-line version**

Replace the existing `AskPrompt` method (lines 574–584) with:

```csharp
/// Reads multi-line prompt text. Ctrl+D (EOF) to finish, Esc to cancel before entering text.
private static string AskPrompt(bool interactive)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  Prompt text (type or paste, Ctrl+D to finish, Esc to cancel):");
    Console.WriteLine("  Placeholders: {SelectedText} = highlighted text" +
                      (interactive ? "   {UserInput} = text you type in the popup" : ""));
    Console.WriteLine(interactive
        ? "  Example: Based on \"{SelectedText}\", write a reply that: {UserInput}"
        : "  Example: Translate {SelectedText} into Chinese.");
    Console.ResetColor();

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("  > ");
    Console.ResetColor();

    var sb   = new System.Text.StringBuilder();
    var line = Console.ReadLine();

    // If the first line is null (Ctrl+D immediately), treat as cancel
    if (line is null) return "";

    sb.Append(line);
    while (true)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("  > ");
        Console.ResetColor();
        var next = Console.ReadLine();
        if (next is null) break;  // Ctrl+D / EOF → done
        sb.Append('\n').Append(next);
    }

    var result = sb.ToString().TrimEnd('\n', '\r');
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"  ({result.Split('\n').Length} line(s) saved)");
    Console.ResetColor();
    return result;
}
```

Note: `Esc` in the middle of `Console.ReadLine()` cannot be detected (the method blocks until Enter). If the user types nothing and presses Ctrl+D on the first line, they get an empty prompt. If they've already typed content, Ctrl+D finishes and returns what was typed.

- [ ] **Step 2: Build to verify**

```powershell
dotnet build AuraTxt.Cli/AuraTxt.Cli.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add AuraTxt.Cli/Menus/InteractiveMenu.cs
git commit -m "feat: support multi-line prompt input with Ctrl+D to finish"
```

---

### Task 5: Action list — color-coded status and delete protection

**Files:**
- Modify: `AuraTxt.Cli/Menus/InteractiveMenu.cs`

- [ ] **Step 1: Update `ActionFeaturesMenu` list display**

Replace the loop inside `ActionFeaturesMenu` (lines 312–317):

```csharp
for (int i = 0; i < _cfg.Actions.Count; i++)
{
    var a  = _cfg.Actions[i];
    var hk = string.IsNullOrEmpty(a.Hotkey) ? "—" : a.Hotkey;
    var model = string.IsNullOrEmpty(a.ModelId) ? "—" : ModelLabel(a.ModelId);

    Console.Write($"  [{(i + 1).ToString().PadLeft(1)}] ");
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
    Console.WriteLine(")");
}
```

Note: The `PadLeft(1)` is a simplification. For two-digit indices the alignment will shift slightly — acceptable for a TUI. If alignment matters, use `$"[{(i + 1),-2}]"`.

- [ ] **Step 2: Update `DeleteActionFlow` to skip system actions**

Replace the `DeleteActionFlow` method (lines 386–398):

```csharp
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
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build AuraTxt.Cli/AuraTxt.Cli.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add AuraTxt.Cli/Menus/InteractiveMenu.cs
git commit -m "feat: color-coded enabled/disabled status in action list, skip system actions on delete"
```

---

### Task 6: Action detail menu — Status toggle and system action limited view

**Files:**
- Modify: `AuraTxt.Cli/Menus/InteractiveMenu.cs`

- [ ] **Step 1: Rewrite `ActionDetailMenu` to branch on `IsSystem`**

Replace the entire `ActionDetailMenu` method (lines 400–461):

```csharp
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
            // System action: only icon, hotkey, status
            Item("1", "Icon  ", $": {action.Icon}");
            Item("2", "Hotkey", $": {hk}");
            Console.Write("  [3] Status  : ");
            PrintStatus(action.Enabled);
            Console.WriteLine();
        }
        else
        {
            var isBuiltin = action.ModelId.StartsWith("default/");
            Item("1", "Icon       ", $": {action.Icon}");
            Item("2", "Model      ", $": {ModelLabel(action.ModelId)}");
            Item("3", "Interactive", $": {(isBuiltin ? "(n/a — built-in)" : action.IsInteractive.ToString())}");
            Item("4", "Prompt     ", $": {(isBuiltin ? "(n/a — built-in)" : Truncate(action.Prompt, 50))}");
            Item("5", "Hotkey     ", $": {hk}");
            Console.Write("  [6] Status     : ");
            PrintStatus(action.Enabled);
            Console.WriteLine();
        }

        Sep();
        Item("0", "Back");
        Item("X", "Exit");
        Prompt();

        var input = Console.ReadLine()?.Trim() ?? "";
        if (input == "0") return;
        if (input.ToUpper() == "X") ExitFlow();

        if (isSystem)
        {
            switch (input)
            {
                case "1":
                    Console.WriteLine("  Find icons at https://lucide.dev/icons/");
                    var ic = Ask($"New icon [{action.Icon}]");
                    if (!string.IsNullOrWhiteSpace(ic)) { action.Icon = ic; _dirty = true; }
                    break;
                case "2":
                    var newHk = HotkeyCapture.Capture(_cfg.Actions, excludeId: action.Id);
                    action.Hotkey = newHk;
                    _dirty = true;
                    if (!string.IsNullOrEmpty(newHk)) WriteSuccess($"Hotkey set to {newHk}.");
                    Pause();
                    break;
                case "3":
                    action.Enabled = !action.Enabled;
                    _dirty = true;
                    WriteSuccess($"Status → {(action.Enabled ? "enabled" : "disabled")}");
                    Pause();
                    break;
            }
        }
        else
        {
            switch (input)
            {
                case "1":
                    Console.WriteLine("  Find icons at https://lucide.dev/icons/");
                    var ic = Ask($"New icon [{action.Icon}]");
                    if (!string.IsNullOrWhiteSpace(ic)) { action.Icon = ic; _dirty = true; }
                    break;
                case "2":
                    var mid = SelectModel();
                    if (mid is not null)
                    {
                        action.ModelId = mid;
                        if (mid.StartsWith("default/")) { action.IsInteractive = false; action.Prompt = ""; }
                        _dirty = true;
                    }
                    break;
                case "3":
                    if (action.ModelId.StartsWith("default/"))
                    { WriteGray("  Built-in service: interaction not applicable."); Pause(); break; }
                    action.IsInteractive = !action.IsInteractive;
                    _dirty = true;
                    WriteSuccess($"Interactive → {action.IsInteractive}");
                    Pause();
                    break;
                case "4":
                    if (action.ModelId.StartsWith("default/"))
                    { WriteGray("  Built-in service: no prompt needed."); Pause(); break; }
                    Console.WriteLine();
                    var pr = AskPrompt(action.IsInteractive);
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
                    action.Enabled = !action.Enabled;
                    _dirty = true;
                    WriteSuccess($"Status → {(action.Enabled ? "enabled" : "disabled")}");
                    Pause();
                    break;
            }
        }
    }
}
```

- [ ] **Step 2: Add the `PrintStatus` helper method**

Add this new method alongside the other console helpers (near `Truncate`):

```csharp
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
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build AuraTxt.Cli/AuraTxt.Cli.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add AuraTxt.Cli/Menus/InteractiveMenu.cs
git commit -m "feat: add status toggle to action detail, limited view for system actions"
```

---

### Task 7: Add action flow — ask for enabled/disabled

**Files:**
- Modify: `AuraTxt.Cli/Menus/InteractiveMenu.cs`

- [ ] **Step 1: Add enabled prompt in `AddActionFlow`**

In `AddActionFlow`, after the hotkey capture and before creating the `ActionItem`, add:

Find this block (around line 369):
```csharp
var hotkey = HotkeyCapture.Capture(_cfg.Actions);
```

Replace with:
```csharp
var hotkey = HotkeyCapture.Capture(_cfg.Actions);

Console.Write("  Enable this action? (Y/n): ");
var enableAns = char.ToLower(ReadKey());
Console.WriteLine();
var enabled = enableAns != 'n';
```

Then update the `ActionItem` constructor to include `Enabled`:

```csharp
_cfg.Actions.Add(new ActionItem
{
    Id            = id,
    Name          = id,
    Icon          = icon,
    ModelId       = modelId,
    IsInteractive = isInteractive,
    Prompt        = prompt,
    Hotkey        = hotkey,
    Enabled       = enabled      // NEW
});
```

- [ ] **Step 2: Build to verify**

```powershell
dotnet build AuraTxt.Cli/AuraTxt.Cli.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add AuraTxt.Cli/Menus/InteractiveMenu.cs
git commit -m "feat: ask for enabled/disabled when creating an action"
```

---

### Task 8: WPF `ActionMenuWindow` — filter disabled, route system actions, remove hardcoded copy

**Files:**
- Modify: `AuraTxt/Windows/ActionMenuWindow.xaml.cs`

- [ ] **Step 1: Rewrite `BuildMenuAsync`**

Replace the entire `BuildMenuAsync` method (lines 57–97):

```csharp
private async Task BuildMenuAsync()
{
    // Dynamic: enabled actions from config (includes system actions like copy, speech)
    foreach (var action in _cfg.Actions.Where(a => a.Enabled))
    {
        var a   = action;
        var img = await IconCacheService.GetIconAsync(a.Icon);
        var tip = $"{a.Name}{(string.IsNullOrEmpty(a.Hotkey) ? "" : $" ({a.Hotkey})")}";

        Button btn;
        if (string.IsNullOrEmpty(a.ModelId))
        {
            // System action — route by ID
            btn = img is not null
                ? MakeImageButton(img, tip, () => ExecuteSystemAction(a.Id))
                : MakeEmojiButton("?", tip, () => ExecuteSystemAction(a.Id));
        }
        else
        {
            // Normal action
            btn = img is not null
                ? MakeImageButton(img, tip, () => { SafeClose(); HotkeyService.ShowResultFor(a, _selectedText, _cfg); })
                : MakeEmojiButton("?", tip, () => { SafeClose(); HotkeyService.ShowResultFor(a, _selectedText, _cfg); });
        }
        IconPanel.Children.Add(btn);
    }

    IconPanel.Children.Add(MakeSeparator());

    // Fixed right: Settings (Lucide: settings)
    var settingsImg = await IconCacheService.GetIconAsync("settings");
    var settingsBtn = settingsImg is not null
        ? MakeImageButton(settingsImg, "Settings (auracfg)", () =>
          {
              SafeClose();
              var exe = System.IO.Path.Combine(AppContext.BaseDirectory, "auracfg.exe");
              if (System.IO.File.Exists(exe)) System.Diagnostics.Process.Start(exe);
          })
        : MakeEmojiButton("⚙️", "Settings (auracfg)", () =>
          {
              SafeClose();
              var exe = System.IO.Path.Combine(AppContext.BaseDirectory, "auracfg.exe");
              if (System.IO.File.Exists(exe)) System.Diagnostics.Process.Start(exe);
          });
    IconPanel.Children.Add(settingsBtn);
}
```

- [ ] **Step 2: Add `ExecuteSystemAction` method**

Add this new method to the class (e.g., after `BuildMenuAsync`):

```csharp
private void ExecuteSystemAction(string id)
{
    switch (id)
    {
        case "copy":
            Clipboard.SetText(_selectedText);
            break;
        case "speech":
            var synthesizer = new System.Speech.Synthesis.SpeechSynthesizer();
            synthesizer.SpeakAsync(_selectedText);
            break;
    }
    SafeClose();
}
```

- [ ] **Step 3: Build the full solution**

```powershell
dotnet build
```

Expected: Build succeeded. If `System.Speech` is not referenced, add it:

```powershell
dotnet add AuraTxt/AuraTxt.csproj package System.Speech
```

- [ ] **Step 4: Commit**

```bash
git add AuraTxt/Windows/ActionMenuWindow.xaml.cs
git commit -m "feat: filter disabled actions, route system actions (copy/speech), remove hardcoded copy button"
```

---

### Task 9: Smoke test and verify

- [ ] **Step 1: Run auracfg CLI interactive mode**

```powershell
dotnet run --project AuraTxt.Cli/AuraTxt.Cli.csproj
```

Verify:
- Menu shows copy and speech actions at top of list
- Enabled/disabled shown in green/gray
- Delete menu skips system actions
- Detail menu for copy shows only Icon/Hotkey/Status
- Detail menu for speech shows only Icon/Hotkey/Status
- Adding a new action asks for enabled
- Status toggle flips enabled ↔ disabled
- Multi-line prompt accepts pasted text, Ctrl+D finishes

- [ ] **Step 2: Verify batch CLI `--enabled`**

```powershell
# Test --enabled param
dotnet run --project AuraTxt.Cli/AuraTxt.Cli.csproj -- action --list
dotnet run --project AuraTxt.Cli/AuraTxt.Cli.csproj -- action --update --id copy --enabled false
dotnet run --project AuraTxt.Cli/AuraTxt.Cli.csproj -- action --list

# Verify delete is blocked
dotnet run --project AuraTxt.Cli/AuraTxt.Cli.csproj -- action --delete --id copy
```

Expected: Update works, list shows disabled, delete prints error.

- [ ] **Step 3: Build final and commit**

```bash
git add -A
git commit -m "chore: final verification of action enable/disable feature"
```

---

## Execution Order

Tasks are sequential — each depends on the previous:
1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9

Tasks 3-7 all modify different sections of `InteractiveMenu.cs` and could theoretically be combined, but keeping them separate makes each commit revertible and reviewable.

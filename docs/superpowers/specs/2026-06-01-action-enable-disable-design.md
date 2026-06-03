# Action Enable/Disable & System Actions — Design Spec

**Date**: 2026-06-01
**Status**: draft

## Overview

Add enable/disable per action, two undeletable system actions (copy, speech), multi-line prompt input, and consistent status display in the CLI.

---

## 1. Data Model Changes

### `ActionItem` (AuraTxt.Core/Models/ActionItem.cs)

Two new properties:

```csharp
public bool Enabled  { get; set; } = true;   // menu visibility + hotkey activation
public bool IsSystem { get; set; }            // undeletable, ModelId empty, limited edit
```

`Enabled = true` default ensures backward compatibility — existing configs load with all actions enabled.

`IsSystem` follows the same explicit-marking philosophy as `Models["default"]` for system models.

### `ConfigService.CreateDefault()` (AuraTxt.Core/Services/ConfigService.cs)

Add two system actions after creating default models:

```csharp
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
```

`ModelId` stays empty (`""`) — the WPF layer detects this and routes to system handlers.

---

## 2. CLI Interactive Menu Changes

### 2.1 Action List Display (`ActionFeaturesMenu`)

Format: `(hotkey | model | status)`

- **enabled** → `ConsoleColor.Green`
- **disabled** → `ConsoleColor.DarkGray`

```
=== Action Features ===
  [1] copy               (Ctrl+C | — | enabled)
  [2] speech             (Ctrl+E | — | enabled)
  [3] translate          (Alt+T | OpenAI / GPT-4o | enabled)
  [4] summarize          (Ctrl+Shift+S | OpenAI / GPT-4o | disabled)
```

System actions show `—` for model (no model bound).

### 2.2 Delete Protection (`DeleteActionFlow`)

- System actions (`IsSystem == true`) are excluded from the deletion list
- The displayed list shows only user actions; numbering skips system actions implicitly
- Batch `--delete` in `ActionCommand` returns an error for system actions

### 2.3 Action Detail Menu (`ActionDetailMenu`)

**Regular action:**
```
--- Action: translate ---
  [1] Icon        : languages
  [2] Model       : OpenAI / GPT-4o
  [3] Interactive : False
  [4] Prompt      : Translate {SelectedText} into Chinese.
  [5] Hotkey      : Alt+T
  [6] Status      : enabled
  ─────────────────────
  [0] Back  [X] Exit
```

`[6]` toggles `Enabled` in place with color feedback.

**System action (copy / speech):**
```
--- Action: copy (system) ---
  [1] Icon    : clipboard-copy
  [2] Hotkey  : Ctrl+C
  [3] Status  : enabled
  ─────────────────────
  [0] Back  [X] Exit
```

Model, Interactive, Prompt are hidden. Only Icon, Hotkey, Status are editable.

### 2.4 Add Action Flow (`AddActionFlow`)

After prompt input, ask:

```
  Enable this action? (Y/n):
```

Default `Y` (enabled).

### 2.5 Multi-line Prompt Input (`AskPrompt`)

```
  Prompt text (type or paste, Ctrl+D to finish, Esc to cancel):
  Placeholders: {SelectedText} = highlighted text   {UserInput} = text you type in the popup
  Example: Translate {SelectedText} into Chinese.
  >
```

- Read lines in a loop via `Console.ReadLine()`
- `null` (EOF / Ctrl+D) → return accumulated text with trailing newlines trimmed
- `Esc` key → discard and return `""`
- Each non-null line is appended with `\n` to a `StringBuilder`
- Works for pasting multi-line prompts from clipboard

---

## 3. Batch CLI Changes (`ActionCommand`)

### `--set` and `--update`

New optional parameter:

```
--enabled true|false
```

Maps to `item.Enabled = value == "true"`.

### `--delete`

Rejects system actions:

```
if (item.IsSystem) return Err($"Cannot delete system action '{id}'", 2);
```

### Help text

```
auracfg action --set    --id <id> --name <name> --icon <lucide> --model-id <id> --interactive <true|false> --prompt "<text>" [--hotkey <key>] [--enabled <true|false>]
auracfg action --update --id <id> [any field including --enabled]
```

---

## 4. WPF Changes (`ActionMenuWindow`)

### 4.1 Filter disabled actions

```csharp
foreach (var action in _cfg.Actions.Where(a => a.Enabled))
```

### 4.2 System action routing

Actions with empty `ModelId` are detected and routed:

```csharp
if (string.IsNullOrEmpty(a.ModelId))
{
    switch (a.Id)
    {
        case "copy":
            // Clipboard.SetText(_selectedText); SafeClose(); break;
        case "speech":
            // TTS: new SpeechSynthesizer().SpeakAsync(_selectedText); SafeClose(); break;
    }
    return;
}
// else: normal HotkeyService.ShowResultFor(...)
```

### 4.3 Remove hardcoded Copy button

The current hardcoded copy button (lines 60-64) is removed — the config-based `copy` action replaces it. The settings button (⚙️) stays hardcoded (it's not an action, it's app UI).

---

## 5. Files Touched

| File | Change |
|------|--------|
| `AuraTxt.Core/Models/ActionItem.cs` | Add `Enabled`, `IsSystem` |
| `AuraTxt.Core/Services/ConfigService.cs` | `CreateDefault()` → add copy + speech actions |
| `AuraTxt.Cli/Menus/InteractiveMenu.cs` | List colors, detail menu routing, multi-line prompt, delete skip, create asks enabled |
| `AuraTxt.Cli/Commands/ActionCommand.cs` | `--enabled` param, delete protection for system actions |
| `AuraTxt/Windows/ActionMenuWindow.xaml.cs` | Filter disabled, route system actions, remove hardcoded copy |

---

## 6. Edge Cases

- **Existing configs** (no `Enabled` field): JSON deserialization defaults to `true` — all actions work.
- **Existing configs** (no `IsSystem` field): defaults to `false` — all existing actions are normal.
- **System action hotkey conflict**: HotkeyCapture allows system actions to use Ctrl+C/Ctrl+E — the global hook already filters these before dispatch.
- **Disabling a system action**: Allowed — it just won't show in the menu and its hotkey won't fire.
- **Empty prompt after Esc**: Treated as "no changes" in edit flow, or "action created with empty prompt" in create flow.

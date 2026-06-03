# CLAUDE.md

## Guidelines

Tradeoff: These guidelines bias toward caution over speed. For trivial tasks, use judgment.

1. Think Before Coding
   Don't assume. Don't hide confusion. Surface tradeoffs.

Before implementing:

State your assumptions explicitly. If uncertain, ask.
If multiple interpretations exist, present them - don't pick silently.
If a simpler approach exists, say so. Push back when warranted.
If something is unclear, stop. Name what's confusing. Ask.

2. Simplicity First
   Minimum code that solves the problem. Nothing speculative.

No features beyond what was asked.
No abstractions for single-use code.
No "flexibility" or "configurability" that wasn't requested.
No error handling for impossible scenarios.
If you write 200 lines and it could be 50, rewrite it.
Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

3. Surgical Changes
   Touch only what you must. Clean up only your own mess.

When editing existing code:

Don't "improve" adjacent code, comments, or formatting.
Don't refactor things that aren't broken.
Match existing style, even if you'd do it differently.
If you notice unrelated dead code, mention it - don't delete it.
When your changes create orphans:

Remove imports/variables/functions that YOUR changes made unused.
Don't remove pre-existing dead code unless asked.
The test: Every changed line should trace directly to the user's request.

4. Goal-Driven Execution
   Define success criteria. Loop until verified.

Transform tasks into verifiable goals:

"Add validation" → "Write tests for invalid inputs, then make them pass"
"Fix the bug" → "Write a test that reproduces it, then make it pass"
"Refactor X" → "Ensure tests pass before and after"
For multi-step tasks, state a brief plan:

1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
   Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test

```sh
# Build entire solution
dotnet build

# Build a specific project
dotnet build AuraTxt/AuraTxt.csproj

# Run tests (xunit + coverlet)
dotnet test
```

## Project Layout

```
AuraTxt.sln
├── AuraTxt/              # WPF tray app (WinExe, net8.0-windows)
├── AuraTxt.Core/          # Shared models & services (classlib, no WPF dependency)
├── AuraTxt.Cli/           # auracfg.exe — CLI config tool (Console app)
└── AuraTxt.Core.Tests/    # xunit tests for Core project
```

## Architecture

**The app is a tray-only WPF application** — no main window, no `MainWindow.xaml`. Startup in `App.xaml.cs` initializes three services then sits in the tray.

### Core data flow

1. User selects text anywhere → **global mouse hook** (`GlobalHookService`, via `MouseKeyHook`) fires.
2. `ClipboardService.GetSelectedTextAsync()` simulates Ctrl+C, reads clipboard, restores previous content.
3. On the WPF dispatcher: `ActionMenuWindow` appears near the cursor showing configured actions.
4. User clicks an action (or presses its global hotkey) → `HotkeyService.ShowResultFor()`:
   - Non-interactive → `ResultWindow` (single pane, AI output directly)
   - Interactive → `InteractiveWindow` (dual pane: user input + AI output)
5. `AiClient.CompleteAsync()` calls the model's OpenAI-compatible `/chat/completions` endpoint.

### Two special built-in models (live in `Models["default"]`)

- `Google_Translate` → `GoogleTranslateClient` (web scraping `translate.google.com`)
- `Youdao_Dict` → `YoudaoClient` (web scraping `fanyi.youdao.com` + `dict.youdao.com`)

These are resolved in `ResultWindow.CallModelAsync()` with early returns **before** the generic `AiClient` path. They are not user-editable via the CLI.

### Config system

`ConfigService` reads/writes `%APPDATA%/AuraTxt/config.json`. Always use `ConfigService.Load()` rather than reading the file directly — it creates a sensible default with built-in models on first run.

**Model ref format**: `"providerId/TargetModel"` (e.g. `"openai/gpt-4o"`). `ConfigRoot.ResolveModel(ref)` splits on `/` and returns `(ProviderConfig, ModelEntry)?`.

**Prompt placeholders**: `{SelectedText}` (the highlighted text) and `{UserInput}` (only for interactive actions).

### Global state — `AppState` (static class)

| Flag | Purpose |
|------|---------|
| `IsMonitoringPaused` | Suppress mouse hook; tray icon switches to `aruatxt_paused.ico` |
| `IsMenuHidden` | Suppress popup menu; actions still work via hotkeys |
| `MenuSuppressUntil` | `DateTime` cooldown — prevents menu re-trigger after an action fires |

### Key WPF patterns

- **Keyboard shortcuts**: All result windows use `PreviewKeyDown` (tunneling event), **not** `KeyDown` (bubbling). This is necessary because `TextBox` captures and swallows key events before they bubble. Set `e.Handled = true` after handling.
- **DPI conversion**: The global mouse hook returns physical screen pixels. Convert to WPF device-independent pixels (DIPs) with `VisualTreeHelper.GetDpi(this)` before setting `Window.Left`/`Top`. Without this, the menu appears at wildly wrong positions on scaled displays.
- **Window closing race**: `ActionMenuWindow` uses a `_closing` guard flag. The `Deactivated` event fires when a result window opens, racing with the button-click `Close()`. Always check `_closing` before calling `Close()`.
- **Tray icons**: `H.NotifyIcon.Wpf` (`TaskbarIcon`). Icons are `Resources/aruatxt_active.ico` and `Resources/aruatxt_paused.ico`.

### auracfg CLI tool

Built from `AuraTxt.Cli/` → `auracfg.exe`. Two modes:
- **Interactive** (no args): menu-driven TUI for humans
- **Batch** (with args): `auracfg provider|action|settings [options]` for AI/script automation

Always builds to the same output directory as the main app so `ActionMenuWindow` can launch it via `AppContext.BaseDirectory/auracfg.exe`.

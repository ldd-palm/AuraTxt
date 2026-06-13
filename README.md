# AuraTxt

> AI-powered floating text actions — select any text, instantly translate or process with AI.

A **tray-only WPF app** for Windows. Highlight text with a drag or double-click, and a floating action menu appears near your cursor. Pick an action to translate, summarize, rewrite, or run any custom prompt against an OpenAI-compatible LLM — results stream in live.

![AuraTxt](images/menu.png)

## Features

- **Floating action menu** — pops up at the cursor after drag-select or double-click, never steals focus from the app you're working in
- **Streaming AI** — any OpenAI-compatible API (OpenAI, DeepSeek, local LLMs); responses stream token-by-token into a lightweight result window
- **Profile system** — 13 built-in model profiles (DeepSeek V4, Qwen3, Gemini, Gemma, GLM-5, Kimi K2, MiniMax, Llama…) auto-matched by model name; each profile controls adapter type, thinking-mode payloads, and `<think>` tag stripping; user-extensible via `profiles/*.json`
- **Thinking control** — per-action `ThinkingMode` (`disable` / `enable_high`) maps to the correct vendor payload automatically via the matched profile
- **Built-in translation** — Google Translate & Youdao Dictionary, no API key required
- **Interactive mode** — dual-pane window: type follow-up instructions, regenerate with refined input
- **Global hotkeys** — trigger any action directly from any app, menu not needed
- **Text-to-speech** — read the selection aloud (SAPI5, configurable voice)
- **External prompts** — actions reference `.md` prompt files edited live without restart; `{SelectedText}` / `{UserInput}` placeholders; injection-resistant system prompt
- **Themes** — Win11 Fluent light/dark + custom themes as plain JSON (34 semantic tokens), hot-swap via tray reload
- **Portable** — everything lives next to the exe; no installer, no registry
- **Config CLI** — `auracfg.exe`: interactive TUI for humans, batch commands for scripts/AI

## Project Structure

```
AuraTxt.sln
├── AuraTxt/              WPF tray app (WinExe, no main window)
│   ├── Services/         Global mouse/keyboard hook, clipboard capture, hotkeys, tray icon
│   └── Windows/          ActionMenuWindow, ResultWindow, InteractiveWindow
├── AuraTxt.Core/         Shared library (no WPF dependency)
│   ├── Models/           ConfigRoot, ProviderConfig, ModelEntry, ActionItem, AppSettings, ProfileFile
│   ├── Adapters/         IAdapter, OpenAICompatibleAdapter, GeminiNativeAdapter, AdapterRegistry
│   ├── Profiles/         13 embedded profile JSONs (auto-seeded to profiles/ on first run)
│   ├── Util/             GlobMatcher, JsonPathSetter, TagStripFilter
│   └── Services/         AiClient, ConfigService, ProfileService, PromptService, ThemeService,
│                         translation clients, TTS
├── AuraTxt.Cli/          auracfg.exe — config tool (TUI + batch commands)
└── AuraTxt.Core.Tests/   xunit tests
```

Data files (created on first run, all next to the exe):

| Path | Content |
|------|---------|
| `config.json` | Providers, models, actions, settings |
| `prompts/*.md` | System prompt + per-action prompt files |
| `themes/*.json` | Color themes (user-editable) |
| `profiles/*.json` | Model profiles (seeded from built-ins; add custom ones here) |
| `icons/` | Lucide icon cache |
| `auratxt.log` | Request/response log (only with `--log`) |

A full reproduction-grade specification lives in [SPEC.md](SPEC.md).

## Getting Started

1. Download `AuraTxt.exe` + `auracfg.exe` from [Releases](https://github.com/ldd-palm/AuraTxt/releases) into one folder (self-contained — no runtime install needed)
2. Run `AuraTxt.exe` — an icon appears in the system tray
3. Right-click tray icon → **Settings (auracfg)** to add an AI provider and actions
4. Select text in any app → floating menu appears → click an action

### Configuring a provider

In auracfg: **Model Platform → [A] Add** — enter a provider id, Base URL (e.g. `https://api.deepseek.com/v1`), API key, and model name. The adapter type defaults to `openai_compatible`; set `AdapterType = "gemini_native"` for Google Gemini endpoints. Use **[T] Test Connection** to verify.

Or in one batch command:

```sh
auracfg provider --set --id deepseek --display DeepSeek --url https://api.deepseek.com/v1 --key sk-... --model deepseek-chat --alias ds
```

### Configuring an action

In auracfg: **Action Features → Add** — pick a name, a [Lucide](https://lucide.dev) icon, a model, and a prompt. Prompts are `.md` files under `prompts/`; use `{SelectedText}` for the highlighted text and `{UserInput}` for interactive-mode input. Optionally assign a global hotkey (e.g. `Alt+T`).

```sh
auracfg action --set --id translate --name Translate --icon languages --model-id deepseek/deepseek-chat --interactive false --prompt prompts/translate.md --hotkey Alt+T
```

### Result window keys

| Key | Function |
|-----|----------|
| `Esc` | Close |
| `R` | Regenerate |
| `P` | Edit prompt and rerun |
| `C` | Copy all output |
| `T` | Pin (disable click-outside dismiss) |
| `Ctrl+C` | Copy selected portion of the output |

The model dropdown in the title bar switches models on the fly and persists the choice.

### Tray menu

| Item | Function |
|------|----------|
| Service: Pause/Resume | Suspend text monitoring and global hotkeys |
| Hide/Show Menu | Suppress the popup menu (hotkeys still work) |
| Reload Settings | Re-read config.json, re-apply theme and hotkeys |
| Settings (auracfg) | Open the config tool |
| About | Project page on GitHub |
| Exit | Quit |

### Troubleshooting

- `AuraTxt.exe --log` writes all requests and streamed responses to `auratxt.log`
- `auracfg doctor` validates the config and reports problems
- `auracfg restore` rolls back to `config.json.bak`

## Build

```sh
dotnet build
dotnet test

# Single-file framework-dependent → publish/release/ (requires .NET 8 on the target machine)
dotnet publish AuraTxt/AuraTxt.csproj     -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/release
dotnet publish AuraTxt.Cli/AuraTxt.Cli.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/release
```

## License

MIT

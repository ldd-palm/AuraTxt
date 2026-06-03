# AuraTxt

> AI-powered floating text actions — select any text, instantly translate or process with AI.

A **tray-only WPF app** for Windows. Highlight text with a drag or double-click, and a floating action menu appears near your cursor. Pick an action to translate, summarize, rewrite, or call any OpenAI-compatible LLM.

![AuraTxt](images/menu.png)

## Features

- **Floating action menu** — appears at cursor after drag-select or double-click
- **AI models** — any OpenAI-compatible API (OpenAI, DeepSeek, Anthropic, local LLMs)
- **Built-in translation** — Google Translate & Youdao Dict (no API key needed)
- **Interactive mode** — chat with AI, refine results with follow-up input
- **Global hotkeys** — trigger actions directly from any app
- **Win11 Fluent Design** — light/dark themes, user-editable JSON theme files
- **Portable** — single-folder deployment, no installation required
- **Config CLI** — `auracfg.exe` for interactive or batch configuration

## Quick Start

1. Download `AuraTxt.exe` + `auracfg.exe` from [Releases](https://github.com/ldd-palm/AuraTxt/releases)
2. Requires [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
3. Run `AuraTxt.exe` — icon appears in system tray
4. Right-click tray icon → **Config (auracfg)** to set up providers and actions
5. Select text anywhere → floating menu → pick an action

## Build

```sh
dotnet build
dotnet test
dotnet publish AuraTxt/AuraTxt.csproj -c Release -o publish -p:PublishSingleFile=true -p:SelfContained=false
dotnet publish AuraTxt.Cli/AuraTxt.Cli.csproj -c Release -o publish -p:PublishSingleFile=true -p:SelfContained=false
```

## Project Layout

```
AuraTxt.sln
├── AuraTxt/              WPF tray app (WinExe)
├── AuraTxt.Core/         Shared models & services
├── AuraTxt.Cli/          auracfg.exe — CLI config tool
└── AuraTxt.Core.Tests/   xunit tests
```

## Theme System

31 semantic color tokens, stored as user-editable JSON in `themes/`. Supports light, dark, and custom themes with real-time hot-swap via tray reload. See [Theme_Design.md](Theme_Design.md) for the token reference.

## License

MIT

# AI_PROJECT — 技术栈与架构

> 最后更新：2026-06-03。本文件描述项目的稳定结构，供 AI/开发者快速建立全局认知。
> 与 `CLAUDE.md`（协作规范）互补：CLAUDE.md 讲"怎么改"，本文件讲"是什么"。

## 一句话定位

AuraTxt 是一个 **纯托盘 WPF 应用**：鼠标划词/双击后在光标旁弹出浮动动作菜单，点击动作即对选中文本调用 AI（或内置翻译服务）并显示结果。配套 `auracfg.exe` CLI 管理配置。

## Solution 结构（.NET 8 / net8.0-windows）

```
AuraTxt.sln
├── AuraTxt/              WPF 托盘应用 (WinExe)。无主窗口，启动即驻留托盘
├── AuraTxt.Core/        共享类库：数据模型 + 服务（无 WPF 依赖）
├── AuraTxt.Cli/         auracfg.exe — 配置工具（交互菜单 + 批量命令）
└── AuraTxt.Core.Tests/  xunit 测试
```

依赖方向单向：`AuraTxt` / `AuraTxt.Cli` → `AuraTxt.Core`。

## 关键依赖（NuGet）

| 包 | 用途 |
|----|------|
| H.NotifyIcon.Wpf | 托盘图标 |
| MouseKeyHook | 全局鼠标/键盘钩子（划词 + 双击检测） |
| NHotkey.Wpf | 全局热键注册 |
| SharpVectors.Wpf | SVG 图标渲染 |
| System.Speech | 朗读（speech action） |
| System.Windows.Automation | UI Automation 读取选中文本（剪贴板兜底前的首选） |

## 核心数据流

```
鼠标操作 → GlobalHookService
           ├─ 拖拽划词（MouseDown→MouseUp 位移≥5px）
           └─ 双击选词（MouseDoubleClick，本次新增）
           ↓
         ClipboardService.GetSelectedTextAsync()
           ① UI Automation ② keybd_event Ctrl+C
           ↓
         ActionMenuWindow（光标旁浮动菜单，Win11 Fluent 双层阴影）
           ↓  点击动作
           ├─ 内置模型(Google_Translate/Youdao_Dict) → 直接调用，不带 AI prompt
           └─ AI 模型 → ResultWindow / InteractiveWindow
                         → PromptService.Resolve(路径→文件内容)
                         → AiClient.CompleteAsync(system + user 两条 message)
                         → /chat/completions
```

## 关键服务（AuraTxt.Core/Services）

| 服务 | 职责 |
|------|------|
| `ConfigService` | 读写 `{exe目录}/config.json`（始终用 `Load()`，首次自动生成默认；加载时向后兼容旧 model Enabled 字段） |
| `PromptService` | **Prompt 文件外挂**：`Resolve(路径→读文件/内联兼容)`、`ListPrompts`、`EnsureScaffold`(生成默认 system.md/template.md)、`IsFileRef` |
| `ThemeService` | **JSON 主题系统**：`EnsureScaffold`(生成 themes/ + 内置 light.json/dark.json)、`ListThemes`(扫描)、`LoadTheme`(读 JSON + 缺 key 补全) |
| `AiClient` | OpenAI 兼容 `/chat/completions`；system+user 双消息 |
| `GoogleTranslateClient` / `YoudaoClient` | 内置免 key 翻译（网页抓取）；目标语言可配置 |
| `HotkeyValidator` | 热键格式/保留键/冲突校验 |
| `IconDownloadService` | 下载 Lucide 图标到本地缓存（CLI 与 WPF 共用） |
| `LogService` | 条件日志：`--log` 开启，写入 `auratxt.log`（exe 同级） |

## 主程序服务（AuraTxt/Services）

| 服务 | 职责 |
|------|------|
| `GlobalHookService` | 鼠标钩子；MouseDown 记录位置 + 延迟 light-dismiss，MouseUp 划词判定 + 弹菜单，**MouseDoubleClick 双击选词** |
| `ClipboardService` | 取选中文本：① UI Automation（无副作用）② keybd_event 模拟 Ctrl+C 兜底 |
| `HotkeyService` | 注册全局热键、`ShowResultFor` 路由到结果窗口 |
| `IconCacheService` | 图标加载（内置资源 + 本地缓存 + 后台下载，复用 IconDownloadService） |
| `TrayIconManager` | 托盘菜单（Service Pause/Resume, Hide/Show Menu, Reload Settings, Config, Exit），Win11 Fluent 无图标列样式 |
| `AppState`(static) | 全局状态：`IsMonitoringPaused`/`IsMenuHidden`/`MenuSuppressUntil`/`IsResultWindowOpen`/`LastProcessedText`/`ActiveMenu`/`IsMenuUpdating` |

## 主题系统

- **31 个语义令牌**（`SurfaceFill`、`Accent`、`TextPrimary` 等），Light/Dark 各一套值
- 令牌定义在 `{exe}/themes/*.json`，用户可编辑、可自建
- WPF 侧全部通过 `{DynamicResource ...}` 引用，运行时热切换
- `ThemeService` 负责脚手架（自动生成 light.json/dark.json）、列表、加载（缺失 key 用内置 fallback 补全）
- 内置三套主题：Light（冷蓝 Fluent）、Dark（蓝灰暗色）、用户自建（blue.json / notion.json）
- ActionMenu 和 ComboBox 下拉列表在 Dark 模式下仍保持浅色（图标可读性）
- Light 模式采用 `#F3F6F9` 冷蓝灰底 + `#CCE0F5` 边框 + `#111111` 高对比文字

## Model 状态管理（本次新增）

- `ModelEntry.Enabled` (bool, 默认 `true`)：控制 model 是否在 action 配置中可选
- auracfg 中 enabled model 绿色显示，disabled 灰色显示
- disable model 前检查是否有 action 引用（同 delete 守卫）
- 为 action 配置/修改 model 时只显示 enabled model
- Test Model 显示全部 model，enabled 绿色优先
- 旧 config.json 加载时自动 backfill `Enabled = true`
- 内置 model (Google_Translate/Youdao_Dict) 始终在列表中，`Enabled = true`

## 配置与数据文件（便携版 — 全部在 exe 同级目录）

- `config.json` — 主配置。Action 的 `Prompt`、`Settings.SystemPrompt` **存的是 .md 文件路径**（运行时实时读取）。
- `Prompts/` — `system.md`、`template.md`、各 action 的 `{id}.md`。
- `themes/` — `light.json`、`dark.json`、用户自定义 JSON 主题文件。
- `icons/` — 图标缓存（首次从 lucide.dev 下载，常用图标内置于 `Resources/icons/`）。
- `auratxt.log` — AI 请求/响应日志（`--log` 模式下）。
- 模型引用格式：`providerId/TargetModel`（如 `deepseek/deepseek-v4-flash`）。
- 占位符：`{SelectedText}`（划选文本）、`{UserInput}`（交互式输入）。
- **便携版**：`ConfigService`/`PromptService`/`ThemeService`/`IconDownloadService`/`IconCacheService` 均使用 `AppContext.BaseDirectory`。
- `AuraTxt.exe --log` 启用日志（写入 exe 同级 `auratxt.log`）。

## 发布

```sh
dotnet publish AuraTxt/AuraTxt.csproj -c Release -o publish -p:PublishSingleFile=true -p:SelfContained=false
dotnet publish AuraTxt.Cli/AuraTxt.Cli.csproj -c Release -o publish -p:PublishSingleFile=true -p:SelfContained=false
```

> 生成两个单文件 exe（需 .NET 8 Desktop Runtime）。拷贝到任意目录即可运行。auracfg.exe 携带 `aruatxt_paused.ico` 图标。

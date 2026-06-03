# AI_HANDOFF — 交接快照

> 生成时间：2026-06-03。供下一个 AI/开发者无缝接手。

## 一、当前停在哪里

**便携版发布就绪。** 所有功能完整，编译通过，测试全绿（26/26）。

本轮新增/改动的能力：
- **双击选词触发菜单**：`GlobalHookService.OnMouseDoubleClick` → 延迟关闭（`DeferredClose` 500ms + CTS）+ `UpdateMenu()` 原地更新
- **Win11 Fluent 主题系统**：31 个令牌 JSON 外挂（`themes/`），`DynamicResource` 全窗口引用，Light（冷蓝 `#F3F6F9`）/ Dark 双模式 + 用户自建主题
- **ActionMenu 双层阴影**：环境光 BlurRadius 24 + 方向光 BlurRadius 10
- **Model Enable/Disable**：`ModelEntry.Enabled` 状态管理，auracfg 列表绿色/灰色区分，disable 前 action 引用检查，SelectModel 过滤 disabled
- **窗口缩放**：ResultWindow / InteractiveWindow `WindowChrome.ResizeBorderThickness=6`
- **模型选择持久化**：切换 model 即时写入 config.json
- **托盘菜单 Win11 Fluent**：ContextMenu + MenuItem 完全自定义 template，无图标列全宽通栏
- **内置模型 Edit Prompt 只读提示**：英文说明，不可编辑
- **auracfg 改进**：`[S] Save Config` 主菜单、`[B] Back` 大小写兼容、携带图标、`--theme` CLI 参数
- **UI 全英文化**：所有标签、tooltip、按钮文字
- **Bug 修复**：Edit Prompt 闪退（`_editing` guard）、`DisplayMemberPath`+`ItemTemplate` 冲突、auracfg 闪退（重试）、Theme JSON 大小写兼容、Reload 主题切换、逗号显示等

已从上一轮延续下来的基础设施：
- Prompt 文件外挂（config.json 存路径，运行时实时读文件）
- DATA BOUNDARY 防注入
- 浮动菜单状态管理（三道防线 + 物理位移判定）
- 图标内置与本地缓存
- 便携版数据目录

## 二、有没有 Bug 没修完

**没有。** 编译 0 Error，测试 26/26 全绿。`auracfg doctor` 输出 clean。

唯一"非 bug 的已知取舍"：无。

## 三、数据状态

所有数据在 exe 同级目录（便携版）：
- `config.json` — 首次运行自动生成
- `Prompts/` — `system.md`、`template.md`、各 action 的 `.md`
- `themes/` — `light.json`、`dark.json`、用户自定义 `.json`
- `icons/` — 首次使用时从 lucide.dev 下载
- `auratxt.log` — 仅 `--log` 模式

## 四、编译与发布

```sh
dotnet build                           # 编译
dotnet test                            # 测试（26 pass）
dotnet publish AuraTxt/AuraTxt.csproj -c Release -o publish \
    -p:PublishSingleFile=true -p:SelfContained=false
dotnet publish AuraTxt.Cli/AuraTxt.Cli.csproj -c Release -o publish \
    -p:PublishSingleFile=true -p:SelfContained=false
```

> 生成 `publish/AuraTxt.exe`（~4.2 MB）+ `publish/auracfg.exe`（~0.3 MB），需 .NET 8 Desktop Runtime。
> **发布前先 `Stop-Process AuraTxt,auracfg`，否则 MSB4018 锁文件。**

## 五、接手须知

- 编译前先 `Stop-Process AuraTxt,auracfg`，否则 MSB3027 锁文件。
- 改 prompt **内容**只需改 `Prompts/*.md` 或 config 路径，**无需重编译**（运行时实时读）。改 prompt **组装逻辑**才动代码。
- 改主题颜色编辑 `{exe}/themes/*.json` 或 auracfg 中切换，`Reload Settings` 即时生效（`DynamicResource` 自动刷新）。
- 新增主题文件放入 `themes/`，JSON 格式（大小写兼容），必须包含全部 31 个 key，缺失 key 自动用内置 fallback 补全。
- `AuraTxt.exe --log` 启用日志，调试看 `auratxt.log`（完整请求体 + 原始响应）。
- `auracfg doctor` 快速诊断 config 健康状态。
- `AppContext.BaseDirectory` 即 exe 所在目录，所有数据路径以此为根。
- ActionMenu 背景/ComboBox 在 Dark 下强制浅色（`MenuSurfaceFill`/`PickerBgFill` 令牌），因为菜单图标是黑色。
- 双击选词的延迟关闭时间为 500ms（`DeferredClose` 中 `Task.Delay(500, ct)`），如需调整改 `ActionMenuWindow.xaml.cs`。
- `IsMenuUpdating` flag 防止 UpdateMenu 期间 Deactivated 误关窗口。
- Model disable 检查 action 引用逻辑在 `ModelDetailMenu` case "4"，与 delete 守卫一致。
- 详细踩坑史见 `docs/floating-menu-devlog.md`；prompt 写法约定见 `docs/prompt-conventions.md`；决策理由见 `AI_DECISIONS.md`。

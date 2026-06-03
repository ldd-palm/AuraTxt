# AI_TASKS — 待办清单

> 最后更新：2026-06-03。已完成项已清理，仅保留待办与潜在改进。

## 🟢 潜在改进（非必须）

- [ ] **`thinking:{type:disabled}` 参数**：非 OpenAI 标准（Anthropic 风格），DeepSeek 网关目前忽略它（HTTP 200 正常）。如换 provider 报错可移除。
- [ ] **Prompt/Mode 库丰富**：预置更多场景的 prompt 模板（代码审查、邮件润色、技术文档翻译等）。
- [ ] **Model 库丰富**：预置更多主流 provider 的默认 model 列表（如 OpenAI o-series、Claude 4.x 系列等）。
- [ ] **Windows 11 Mica/Acrylic 背景**：目前 ActionMenu 用双层阴影模拟深度感。如需真正的半透明毛玻璃效果，需 P/Invoke `SetWindowCompositionAttribute`（WPF 原生不支持）。

## ✅ 本轮已完成（归档，勿重复）

浮动菜单基础功能修复（DPI 定位、状态三道防线、light-dismiss、物理位移判定、图标内置+缓存）、菜单 UI 改造、System Prompt 分 role、翻译 echo 修复、DATA BOUNDARY 防注入、Prompt 文件外挂改造、Action Order 显示位置、结果窗口 light-dismiss、Action Name 可编辑、Target Language 设置、LogService + --log、托盘菜单英文+图标、便携版目录切换、Copy action hotkey 锁定、ComboBox 浅色主题、应用图标、UTF-8 控制台编码、SystemPrompt 内容预览。

### 双击选词触发菜单
`GlobalHookService` 订阅 `MouseDoubleClick`，双击单词自动获取文本并在光标位置弹出菜单。已显示菜单时双击另一个单词 → 平滑移动更新内容（`UpdateMenu`），不先关后开。延迟关闭机制（`DeferredClose` 500ms + `CancellationTokenSource`）避免与 light-dismiss 冲突。

### Win11 Fluent 主题系统
31 个语义颜色令牌，JSON 文件外挂（`themes/*.json`），`DynamicResource` 全窗口引用。Light 冷蓝调配色（`#F3F6F9`/`#E6EEF7`/`#CCE0F5`/`#111111`），Dark 保留深色结果窗 + ActionMenu/ComboBox 强制浅色。ActionMenu 双层阴影（外环境光 BlurRadius 24 + 内方向光 BlurRadius 10）。托盘菜单 Win11 Fluent 无图标列全宽通栏样式。auracfg → General Settings → Theme 切换。

### Model Enable/Disable 管理
`ModelEntry.Enabled` 属性，默认 `true`。auracfg 列表绿色/灰色区分，disable 前检查 action 引用。SelectModel 只显示 enabled model。旧 config 向后兼容自动 fix。内置 model 始终在列表中。

### 窗口缩放
ResultWindow / InteractiveWindow 通过 `WindowChrome.ResizeBorderThickness=6` 支持拖拽边角缩放，`MinWidth=320 MinHeight=200`。

### 模型选择持久化
ResultWindow / InteractiveWindow 切换 model 后即时写入 `config.json`，重启/重载后保留选择。

### 内置模型 Edit Prompt 只读提示
ResultWindow 中选择内置 model 后点击 Edit Prompt → 只读英文提示对话框，说明不支持自定义 prompt，显示当前目标语言。

### Bug 修复
- Edit Prompt 闪退（`_editing` 守卫防止 Deactivated 误关）
- `DisplayMemberPath` + `ItemTemplate` 冲突 → 移除 `DisplayMemberPath`
- auracfg 独立启动闪退（`ConfigService.Load` 增加重试逻辑）
- Theme JSON 大小写兼容（`PropertyNameCaseInsensitive`）
- Reload Settings 实时切换主题（`ApplyTheme` 加入 `ReloadConfig`）
- Provider 仅剩 disabled model 时逗号显示异常

### UI 英文化
全部 UI 文字：ResultWindow / InteractiveWindow 标签与 tooltip、PromptEditDialog 按钮、ActionMenu logo tooltip。

### auracfg 改进
- 主菜单新增 `[S] Save Config`
- `[B] Back` 统一大小写兼容
- 携带 `aruatxt_paused.ico` 图标
- `--theme <id>` CLI 参数

详见 `AI_DECISIONS.md` 与 `docs/floating-menu-devlog.md`。

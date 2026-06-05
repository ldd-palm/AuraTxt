# CLI 字符菜单设计与实现规范

> 版本：1.0 · 2026-06-04
> 基于 AuraTxt / auracfg 项目实践总结，适用于 .NET 8 + Spectre.Console 的交互式 TUI 程序。

---

## 一、核心设计原则

### 1. 文本第一，可扫描

- **齐头截断**：长文本（描述、路径、日志）超过终端宽度前必须优雅截断（`…`），绝对不允许自动换行打破垂直对齐线。
- **视觉对齐**：状态、单选/复选框靠左对齐；数值、动态指标靠右对齐。
- **固定列宽**：菜单项的 Label 列使用 `PadRight(N)` 固定宽度，Value 列对齐于同一位置。

### 2. 渐进式呈现

- 不要把所有信息堆到一个屏幕：**主菜单 → 子菜单 → 详情编辑** 三级分层。
- 向导式多步操作（Add Provider/Add Action）提取为独立 `Flow` 类，不嵌入页面循环。
- 底部 Footer **常驻**显示当前可用的快捷键提示。

### 3. 优雅降级与环境自适应

- 在输出颜色和特殊字符前检测终端能力：
  ```csharp
  bool HasColor => AnsiConsole.Profile.Capabilities.ColorSystem != ColorSystem.NoColors;
  ```
- 设置 `NO_COLOR=1` 时自动降级为无色模式。
- 符号降级表：

  | 功能 | 支持 Unicode | ASCII 降级 |
  |------|-------------|-----------|
  | 已选中 | `(●)` | `(*)` |
  | 未选中 | `( )` | `( )` |
  | 已勾选 | `[■]` | `[X]` |
  | 未勾选 | `[ ]` | `[ ]` |
  | 分隔线 | `─────────────────────` | `---------------------` |
  | 成功 | `✓` | `OK` |
  | 失败 | `✗` | `X` |

---

## 二、标准四区布局

```
╭──────────────────────────────────────────────────────────╮
│  AuraCfg › Model Platform › OpenAI                       │  ← 1. Header（面包屑）
╰──────────────────────────────────────────────────────────╯

  › [1] Edit Base URL      https://api.openai.com            ← 2. Main（方向键 + 数字直达）
    [2] Edit API Key       sk-ab●●●●●●●●
    [3] gpt-4o             (●) active
    [4] gpt-4o-mini        ( ) inactive
  ─────────────────────
    [S] Save Config

  ✓ API Key updated successfully                            ← 3. Notice（1 行，下次按键后消失）

  ↑↓ Navigate  │  [Enter] Select  │  [Esc] Back  │  [Q] Quit  ← 4. Footer（常驻）
```

| 区域 | 内容 | 高度 |
|------|------|------|
| **Header** | 当前导航路径（面包屑），Rounded 边框 Panel | 固定 3 行 |
| **Main** | 菜单项列表，支持滚动；光标行高亮 | 动态 |
| **Notice** | 操作结果（成功/警告/错误），下次 DrawFrame 自动清空 | 0–1 行 |
| **Footer** | 上下文相关的快捷键提示 | 固定 1 行 |

---

### 进度条样式

​                                                      

✻ Compacting conversation… (58s)                                                                                        
  ▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱ 48%   



## 三、键盘交互规范

| 按键 | 动作 |
|------|------|
| `↑` / `↓` | 移动光标（跳过分隔符）|
| `Enter` | 激活当前高亮项 |
| `1`–`9` | 直达对应编号的菜单项并立即激活 |
| `A`/`D`/`S`/`B` 等字母 | 直达对应快捷操作 |
| `Esc` / `Backspace` | 返回上一级 |
| `B` | 返回上一级（字母 Back） |
| `Q` / `q` | 退出（弹出保存提示） |
| `Ctrl+C` | 强制退出（OS 信号，无需处理） |

**规则**：
- 数字直达的同时更新光标位置，保持视觉一致。
- 光标到达边界时循环（从最后一项跳到第一项，反之亦然）。
- 分隔符（Separator）不参与光标计数，跳过。

---

## 四、颜色语义

| 语义 | Spectre.Console 颜色 | 适用场景 |
|------|---------------------|---------|
| **Primary（选中）** | `bold cyan` | 当前光标行、激活的页签 |
| **Success（成功/启用）** | `green` | `active`、`enabled`、操作成功 |
| **Warning（警告/空闲）** | `yellow` | `warning`、`idle`、`pending` |
| **Danger（危险/禁用）** | `red` | `inactive`、`disabled`、`failed` |
| **Muted（次要）** | `grey` | 未选中项、注释、快捷键提示、Value 列默认色 |
| **Header/Footer 边框** | `grey35` | Panel 边框、分隔线、Footer 文字 |

---

## 五、状态符号规范

```csharp
// 单选状态
"(●) active"    // 启用
"( ) inactive"  // 禁用

// 复选状态
"[■] enabled"
"[ ] disabled"
```

`ValueStyle` 枚举控制 Value 列颜色：

```csharp
public enum ItemValueStyle { Muted, Success, Danger, Warning }
```

---

## 六、.NET 实现架构（Spectre.Console）

### 推荐目录结构

```
YourCli/
├── Program.cs                    # 入口：无参数 → TuiApp；有参数 → 批量命令
├── Commands/                     # 批量命令（不依赖 TUI）
│   └── ...
└── Tui/
    ├── TuiTypes.cs               # MenuItem, MenuKey, NoticeKind, ItemValueStyle
    ├── NavStack.cs               # 面包屑导航栈
    ├── TuiRenderer.cs            # 所有 Spectre.Console 调用的唯一出口
    ├── TuiApp.cs                 # 驱动循环 + 共享业务逻辑
    ├── Pages/
    │   ├── IMenuPage.cs          # interface + PageResult + PageResultKind
    │   ├── PageBase.cs           # 光标跟踪基类（BuildCursorState, MoveUp/Down, JumpTo）
    │   ├── MainMenuPage.cs
    │   └── ...                   # 各功能页面
    └── Flows/                    # 多步向导（不是完整页面）
        └── ...
```

### 核心抽象

```csharp
// 页面接口
public interface IMenuPage
{
    string Title { get; }
    Task<PageResult> RunAsync(TuiApp app, CancellationToken ct);
}

// 导航结果
public enum PageResultKind { Back, Exit, Push }
public sealed record PageResult(PageResultKind Kind, IMenuPage? Next = null)
{
    public static PageResult Back()               => new(PageResultKind.Back);
    public static PageResult Exit()               => new(PageResultKind.Exit);
    public static PageResult Push(IMenuPage next) => new(PageResultKind.Push, next);
}

// 菜单项
public sealed record MenuItem(
    string Key,             // "1", "A", "B", "D" 等
    string Label,
    string? Value      = null,
    ItemValueStyle ValueStyle = ItemValueStyle.Muted,
    bool   IsSeparator  = false)
{
    public static MenuItem Sep() => new("", "", IsSeparator: true);
}

// 按键判别联合体
public abstract record MenuKey
{
    public sealed record Arrow(bool Up) : MenuKey;
    public sealed record Number(int N)  : MenuKey;
    public sealed record Letter(char C) : MenuKey;
    public sealed record Confirm        : MenuKey;
    public sealed record Escape         : MenuKey;
    public sealed record Quit           : MenuKey;
    public sealed record Unknown        : MenuKey;
}
```

### TuiApp 驱动循环

```csharp
_nav.Push(new MainMenuPage());
while (!_nav.IsEmpty)
{
    var result = await _nav.Peek()!.RunAsync(this, ct);
    switch (result.Kind)
    {
        case PageResultKind.Back:  _nav.Pop();             break;
        case PageResultKind.Push:  _nav.Push(result.Next!); break;
        case PageResultKind.Exit:  await HandleExitAsync(); return;
    }
}
```

**规则**：
- 页面内部**绝不**调用 `Environment.Exit()`，统一由 `TuiApp.HandleExitAsync()` 处理。
- 所有页面返回 `PageResult`，TuiApp 负责导航决策。

### 页面循环模板

```csharp
public override Task<PageResult> RunAsync(TuiApp app, CancellationToken ct)
{
    while (true)
    {
        var items = BuildItems(app);               // 每帧重新构建（支持动态列表）
        var (cursor, sel) = BuildCursorState(items);
        app.Renderer.DrawFrame(app.GetBreadcrumb(), items, cursor, FooterWith());

        var key = app.Renderer.ReadMenuKey();
        switch (key)
        {
            case MenuKey.Arrow a:
                if (a.Up) MoveUp(sel.Count); else MoveDown(sel.Count);
                break;
            case MenuKey.Confirm:
                var r = Activate(items[cursor].Key, app);
                if (r != null) return Task.FromResult(r);
                break;
            case MenuKey.Number n:
                JumpTo(sel, items, n.N.ToString());
                r = Activate(n.N.ToString(), app);
                if (r != null) return Task.FromResult(r);
                break;
            case MenuKey.Letter l:
                JumpTo(sel, items, l.C.ToString());
                r = Activate(l.C.ToString(), app);
                if (r != null) return Task.FromResult(r);
                break;
            case MenuKey.Escape: return Task.FromResult(PageResult.Back());
            case MenuKey.Quit:   return Task.FromResult(PageResult.Exit());
        }
    }
}
```

### 渲染管线（每次按键后执行）

```
1. BuildItems()          → 从当前 config 状态构建 List<MenuItem>
2. BuildCursorState()    → 计算光标在 items 中的实际 index（跳过 separator）
3. DrawFrame()
   ├─ AnsiConsole.Clear()
   ├─ Panel(面包屑).Expand().Border(Rounded)   → Header
   ├─ foreach item → RenderItem(item, isSelected)  → Main
   ├─ if _notice → MarkupLine(colored notice); _notice = ""  → Notice（一次性）
   └─ MarkupLine(footer hints)                → Footer
4. ReadMenuKey()         → Console.ReadKey(intercept: true) → MenuKey
5. 页面 dispatch → 执行动作 / 移动光标 → 回到 1
```

---

## 七、Spectre.Console 关键陷阱

### 1. `[[` 转义规则（最常见错误）

Spectre.Console markup 中，`[` 和 `]` 是标签定界符：

| 写法 | 输出 |
|------|------|
| `[[` | `[`（字面量） |
| `]]` | `]`（字面量） |
| `[[1]]` | `[1]`（字面量方括号包围的数字） |
| `[[[1]]]` | ❌ 错误！中间的 `[1]` 被识别为 markup 标签，导致"Unbalanced markup stack" |

**规则**：永远使用 `Markup.Escape(userContent)` 处理用户数据，对字面量括号使用 `[[` / `]]`。

```csharp
// 正确：显示 "[1] General Settings"
AnsiConsole.MarkupLine($"[[{key}]] {Markup.Escape(label)}");

// 错误：三层括号
AnsiConsole.MarkupLine($"[[[{key}]]] {label}");  // ← 崩溃
```

### 2. Windows Forms 命名冲突

项目若同时启用 `<UseWindowsForms>true</UseWindowsForms>`（因为依赖 `SendKeys` 等），会与 Spectre.Console 产生类型冲突：

| 冲突类型 | 解决方法 |
|---------|---------|
| `Panel` | 使用全限定名 `Spectre.Console.Panel` |
| `Color` | 使用全限定名 `Spectre.Console.Color` |

### 3. `SelectionPrompt` 没有 `.Select()` 预选方法

0.49+ 版本的 `SelectionPrompt<T>` **没有**设置默认选中项的实例方法。如需预选，只能调整 `choices` 列表顺序（将默认项放第一位）。

### 4. `StatusContext.Spinner` 是属性，不是方法

```csharp
// 错误
ctx.Spinner(Spinner.Known.Dots);   // ← 编译错误

// 正确
ctx.Spinner = Spinner.Known.Dots;
```

### 5. Spectre.Console 需要真实 TTY

`AnsiConsole.Clear()` 在 stdin/stdout 被重定向的非交互环境（CI、管道）中会抛出 `IOException: The handle is invalid`。这是正常现象，不影响真实终端使用。批量命令（无 TUI 路径）不应调用任何 Spectre Console API。

### 6. `TextPrompt.DefaultValue()` 替代 SendKeys

老式 `SendKeys.SendWait()` 用于向输入框预填内容，在 Spectre.Console 中用 `TextPrompt<string>.DefaultValue(v)` 代替：

```csharp
// 旧写法（需要 UseWindowsForms）
SendKeys.SendWait(escaped);
Console.ReadLine();

// 新写法（纯 Spectre）
AnsiConsole.Prompt(new TextPrompt<string>("Label:").DefaultValue(currentValue).AllowEmpty());
```

---

## 八、输入组件选型

| 场景 | 组件 | 说明 |
|------|------|------|
| 菜单导航 | 自定义 `ReadMenuKey()` | 支持方向键 + 数字直达 + 字母，`Console.ReadKey(intercept:true)` |
| 文本输入 | `TextPrompt<string>` | `.AllowEmpty()` + `.DefaultValue()` |
| 密码/Key | `TextPrompt<string>.Secret('•')` | 字符掩码 |
| 单选列表 | `SelectionPrompt<string>` | 方向键选择，无数字直达 |
| 确认 | `AnsiConsole.Confirm()` | Y/n 问答 |
| 长操作 | `AnsiConsole.Status().Start(...)` | 旋转 spinner |

**规则**：`SelectionPrompt` 仅用于**辅助选择场景**（选主题、选语音、选语言），主菜单循环用自定义 `ReadMenuKey()` 以保持数字直达能力。

---

## 九、Notice 机制

Notice 区是一次性消息——显示在 Footer 上方，下次 `DrawFrame` 调用时自动清空：

```csharp
// 设置（在页面的动作处理中调用）
app.Renderer.SetNotice("API Key updated successfully.");
app.Renderer.SetNotice("File not found.", NoticeKind.Error);

// 实现（TuiRenderer 内部）
if (!string.IsNullOrEmpty(_notice))
{
    AnsiConsole.MarkupLine($"  [{color}]{sym} {Markup.Escape(_notice)}[/]");
    _notice = "";   // 消费后立即清空
}
```

颜色映射：

| NoticeKind | 颜色 | 符号 |
|-----------|------|------|
| Success | green | ✓ |
| Warning | yellow | ! |
| Error | red | ✗ |
| Info | grey | i |

**不要使用 `Pause()`（按任意键继续）**，Notice 机制替代了它——用户的下一次按键自然推进页面，Notice 在此时消失。

---

## 十、Flow（多步向导）

当一个操作需要多个输入步骤（如"添加 Provider"需要 ID → URL → API Key → 第一个 Model），将其提取为独立的静态 `Flow` 类：

```csharp
// 调用方（页面内）
AddProviderFlow.Run(app);

// Flow 实现
public static class AddProviderFlow
{
    public static void Run(TuiApp app)
    {
        var id  = app.Renderer.Ask("Provider ID");
        var url = app.Renderer.Ask("Base URL");
        var key = app.Renderer.AskSecret("API Key");
        // ... 验证、写入 cfg、SetNotice
    }
}
```

Flow 不返回 `PageResult`，不操作导航栈，只修改数据并设置 Notice。调用它的页面负责在下一帧重新渲染。

---

## 十一、脏标记与保存

```csharp
// 任何修改后
app.MarkDirty();
app.Renderer.SetNotice("...");

// 统一由 TuiApp.HandleExitAsync() 处理退出时询问
if (Dirty && renderer.Confirm("Changes detected. Save before exit?"))
    configService.SaveWithBackup(cfg);
```

**规则**：
- 页面内部只调用 `app.MarkDirty()`，**不自行调用 Save**。
- 唯一调用 `SaveWithBackup` 的地方：`[S] Save Config` 动作 和 `HandleExitAsync()`。

---

## 十二、验证清单

每次发布前确认：

- [ ] `dotnet build` 0 error
- [ ] `dotnet test` 全部通过
- [ ] 批量命令（无 TUI 路径）正常工作
- [ ] ↑↓ 移动光标并高亮，到边界时循环
- [ ] 数字 1-9 直达对应项并激活
- [ ] Enter 进入子页面；Esc 返回；Q 弹出保存提示
- [ ] Notice 区显示操作结果，下次按键后消失
- [ ] 面包屑随导航深度实时更新
- [ ] 长值截断为 `…`，不换行
- [ ] API Key / Secret 显示为 `sk-ab●●●●●●`
- [ ] `NO_COLOR=1` 环境下纯 ASCII 降级
- [ ] 在 Windows Terminal 和 cmd.exe 各测一遍

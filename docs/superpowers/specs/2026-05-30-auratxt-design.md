# AuraTxt 设计规范

**日期：** 2026-05-30  
**状态：** 已确认，待实现

---

## 一、产品定位

AuraTxt 是一款常驻 Windows 系统托盘的全局 AI 划词效率工具。用户在任意应用中用鼠标选中文本后，光标旁自动弹出动作菜单，点击或按快捷键触发 AI 处理，结果在浮窗中展示。

---

## 二、技术选型

| 项目 | 选型 |
|---|---|
| UI 框架 | WPF (.NET 8) |
| 目标平台 | Windows 10/11 x64 |
| 语言 | C# 12 |
| 配置格式 | JSON (`%AppData%\AuraTxt\config.json`) |

---

## 三、Solution 结构

```
AuraTxt.sln
├── AuraTxt.Core/          # Class Library — 共享数据层
├── AuraTxt/               # WPF App — 主程序
└── AuraTxt.Cli/           # Console App — 输出 auracfg.exe
```

依赖方向：`AuraTxt` → `AuraTxt.Core` ← `AuraTxt.Cli`（单向，Core 不依赖任何上层）

---

## 四、AuraTxt.Core 职责

```
AuraTxt.Core/
├── Models/
│   ├── ConfigRoot.cs          # 根配置对象（System + Models + Actions + Settings）
│   ├── ModelPlatform.cs       # 用户自定义 AI 平台
│   ├── ActionItem.cs          # 功能动作定义
│   └── AppSettings.cs        # 界面设置
├── Services/
│   ├── ConfigService.cs       # 读写 config.json（加文件锁防并发写入）
│   ├── AiClient.cs            # OpenAI-compatible HTTP 客户端（async，非流式）
│   ├── GoogleTranslateClient.cs  # 移植 google_translate.js（tk token 算法）
│   ├── YoudaoClient.cs        # 移植 youdao.js（MD5 签名，翻译 + 词典双接口）
│   └── HotkeyValidator.cs     # 快捷键验证（格式 + 系统保留 + 内部冲突）
└── Constants/
    └── SystemKeys.cs          # Windows 系统保留热键白名单
```

---

## 五、config.json 数据模型

```json
{
  "System": {
    "GoogleTranslate": {
      "Provider": "google-translate",
      "DisplayName": "Google 翻译"
    },
    "YoudaoDict": {
      "Provider": "youdao-dict",
      "DisplayName": "有道词典"
    }
  },
  "Models": {
    "<id>": {
      "DisplayName": "string",
      "Provider": "openai-compatible",
      "BaseUrl": "string",
      "ApiKey": "string",
      "TargetModel": "string",
      "Alias": "<Provider>/<TargetModel>"
    }
  },
  "Actions": [
    {
      "Id": "string",
      "Name": "string",
      "Icon": "string (lucide icon name)",
      "ModelId": "string  // 普通 ID 或 $google-translate / $youdao-dict",
      "IsInteractive": false,
      "Hotkey": "string (可空)",
      "Prompt": "string  // 支持 {SelectedText} 和 {UserInput} 占位符"
    }
  ],
  "Settings": {
    "FontSize": 14,
    "ResultWindowOpacity": 0.95,
    "MenuTriggerDelayMs": 100
  }
}
```

**System 区块为只读**，任何工具（GUI、auracfg）均不可修改或删除。  
`ModelId` 使用 `$` 前缀引用 System 服务，防止与用户自定义 Model ID 冲突。

---

## 六、AuraTxt WPF 主程序

### 启动流程

1. 初始化系统托盘图标（右键菜单：开启/关闭监听 · 退出）
2. 读取 `%AppData%\AuraTxt\config.json`，注册所有 Action 的全局快捷键
3. 安装全局鼠标钩子（`WH_MOUSE_LL`）

### 文字选择检测

```
WH_MOUSE_LL 监听 WM_LBUTTONUP
    ↓ 等待 100ms（MenuTriggerDelayMs，可配置）
    ↓ 保存当前剪贴板内容
    ↓ 发送 Ctrl+C 到活动窗口
    ↓ 读取剪贴板新内容
    ↓ 还原旧剪贴板内容（用户无感知）
    ↓ 若选中内容非空 → 在光标位置显示 ActionMenuWindow
```

### 三个核心窗口

#### ActionMenuWindow（动作菜单）

- 无边框圆角胶囊窗口，`Topmost = true`，点击窗口外自动关闭
- 布局：`[📋 复制] | [动作图标×N（来自 Actions 列表）] | [⚙️ 设置]`
- 每个图标 28×28px，悬停 300ms 后显示 Tooltip（动作名 + 快捷键）
- 图标来源：Lucide Icons（`Icon` 字段对应 lucide icon name，本地缓存 SVG）
- 点击 ⚙️ → 打开 auracfg 交互式菜单（`auracfg.exe` 启动新控制台窗口）

#### ResultWindow（结果大框）

- 无边框深色窗口，`Topmost = true`，切换应用不消失
- 标题栏左：❌ 关闭按钮 + 动作名 + 模型名
- 标题栏右（图标按钮）：
  - ✏️ 编辑 Prompt（快捷键 P）
  - 🔄 重新生成（快捷键 R）
  - 📋 复制全部（快捷键 C）
- 内容区：一次性展示text文本（非流式）

#### InteractiveWindow（交互式窗口）

- 同 ResultWindow 风格，分上下两栏
- 标题栏右（图标按钮，从左到右）：
  - ▶ 发送生成（快捷键 Enter）
  - ✏️ 编辑 Prompt（快捷键 P）
  - 🔄 重新生成（快捷键 R）
  - 📋 复制全部（快捷键 C）
  - 模型切换下拉（显示已配置模型的 Alias）
- 上栏：用户输入意图（TextBox，多行）
- 下栏：AI 生成结果（text文本，一次性展示）

---

## 七、auracfg.exe CLI 设计

### 运行模式

```
auracfg                  # 无参数 → 交互式菜单模式
auracfg <cmd> <args>     # 静默参数模式（Claude Code / Agent 调用）
```

### 命令结构

```
auracfg model  --list
               --set    --id <id> --display <name> --url <url> --key <key> --model <model>
               --update --id <id> [--display|--url|--key|--model 任意组合]
               --delete --id <id> [--force]

auracfg action --list
               --set    --id <id> --name <name> --icon <lucide-name>
                        --model-id <id> --interactive <true|false>
                        --prompt "<text>" [--hotkey <key>]
               --update --id <id> [任意字段]
               --delete --id <id>

auracfg settings --show
                 --set   [--font-size <n>] [--opacity <0-1>] [--delay-ms <n>]
```

### 快捷键验证流程

适用于 `auracfg action --set/--update --hotkey` 及交互式菜单输入快捷键时：

```
① 格式验证：必须为 <修饰键>+<键名>（如 Alt+T, Ctrl+Shift+R）
       ↓ 非法格式 → 报错，重新输入 / 返回错误码 1
② 系统保留检测：对照 SystemKeys.cs 白名单
   （包含 Win+L, Win+D, Win+E, Alt+F4, PrintScreen 等）
       ↓ 系统保留 → 提示 "系统保留热键，无法注册"，重新输入 / 错误码 2
③ 内部冲突检测：遍历 config.json 所有 Action.Hotkey
       ↓ 冲突 → 提示 "已被 [动作名] 使用"，重新输入 / 错误码 2
④ 全部通过：
   - **交互式菜单模式**：显示输入值，等待确认
     "设置快捷键为 Alt+T？(y/N)" — ESC 或 N → 取消，不写入
   - **静默参数模式**（`--hotkey` 直接传入）：跳过确认，验证通过即写入
```

`HotkeyValidator` 封装在 `AuraTxt.Core`，GUI 的 Prompt 编辑弹窗调同一个验证器。

### 退出码

| 码 | 含义 |
|---|---|
| 0 | 成功 |
| 1 | 参数格式错误 |
| 2 | 业务验证失败（冲突/保留键/ID不存在） |
| 3 | 文件 IO 错误 |

### 级联保护规则

- `auracfg model --delete --id X`：若有 Action 绑定 X，拒绝执行并列出绑定的 Action，加 `--force` 则连同 Action 一并删除
- `auracfg model/action --delete --id $google-translate`（System 服务）：直接拒绝，报错退出

---

## 八、内置免费服务

### Google Translate（移植自 google_translate.js）

- 接口：`https://translate.google.com/translate_a/single?client=gtx&...`
- 认证：`tk` token 算法（纯本地计算，无需 API Key）
- 用于：翻译类 Action 绑定 `$google-translate` 时

### 有道（移植自 youdao.js）

- 翻译接口：`https://fanyi.youdao.com/translate_o`（MD5 签名）
- 词典接口：`https://dict.youdao.com/w/<word>/`（HTML 解析）
- 用于：绑定 `$youdao-dict` 时；词典接口适合单词查询

两者均无需 API Key，移植时保留原签名/token 算法，在 `AuraTxt.Core` 中以 C# 实现。

---

## 九、错误处理

| 场景 | 处理方式 |
|---|---|
| AI 请求超时/失败 | ResultWindow 显示错误信息，提供 R 重试 |
| config.json 格式损坏 | 启动时提示，提供重置为默认值选项 |
| 快捷键注册失败（被其他程序占用） | 托盘气泡通知，该动作仅可通过菜单触发 |
| 剪贴板操作失败 | 静默忽略，不弹出菜单 |

---

## 十、验证方式

1. 运行 `AuraTxt.exe`，在 Chrome / Word / VS Code 中划选文字 → 菜单出现在光标旁
2. 点击翻译图标 → ResultWindow 弹出并显示 Google Translate 结果
3. 按 `Alt+T` 快捷键（无需点菜单）→ 同上
4. 点击智能回复 → InteractiveWindow 弹出 → 填写意图 → 点 ▶ → 结果出现
5. 运行 `auracfg model --set --id test --display "Test" --url ... --key ... --model ...` → `config.json` 更新
6. 运行 `auracfg action --set --id foo --hotkey "Alt+T"` → 提示冲突，拒绝写入
7. 运行 `auracfg model --delete --id test --force` → 关联 Action 连同删除

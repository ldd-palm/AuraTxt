# Theme Design Guide

## 令牌表（31 枚）

### 面板 / 表面

| 令牌 | 用途 | 建议 |
|------|------|------|
| `SurfaceFill` | 浮动面板、结果窗主体背景 | 极浅中性色或微冷/暖调，与系统底色拉开 5-8% |
| `MenuSurfaceFill` | **ActionMenu 专用背景** | 同 `SurfaceFill`；Dark 下也必须保持浅色（图标为黑色） |
| `SurfaceElevated` | ResultWindow / InteractiveWindow 主体 | 同 `SurfaceFill` |
| `TitleBarFill` | 结果窗标题栏背景 | **应明显深于 `SurfaceElevated`**（约深 5-8%），制造头部区域层级感 |
| `SurfaceStroke` | 所有面板 1px 外边框 | 中性灰或微着色，不宜过深 |

### 文字

| 令牌 | 用途 | 建议 |
|------|------|------|
| `TextPrimary` | 主文字（结果内容、输入框） | 高对比度，Light 下接近纯黑（`#111`） |
| `TextSecondary` | 副文字（标题栏标签） | 中灰色 |
| `TextTertiary` | 弱文字（占位符、提示标签） | 浅灰色 |

### 按钮 / 控件

| 令牌 | 用途 | 建议 |
|------|------|------|
| `BtnFill` | 按钮默认背景 | 同面板底色或略深 |
| `BtnFillHover` | 按钮悬停背景 | 同 `TitleBarFill` 保持统一 |
| `BtnFillPressed` | 按钮按下背景 | 深于 hover |
| `BtnStroke` | 按钮边框 | 同 `SurfaceStroke` |
| `IconBtnFill` | 工具栏图标按钮默认背景 | 同 `BtnFill` |
| `IconBtnFillHover` | 工具栏图标按钮悬停背景 | 同 `BtnFillHover` |
| `IconBtnStroke` | 工具栏图标按钮边框 | 同 `BtnStroke` |
| `MenuBtnFill` | ActionMenu 按钮默认背景 | **始终透明**（`#00FFFFFF`） |
| `MenuBtnFillHover` | ActionMenu 按钮悬停背景 | 同 `TitleBarFill` |

### 输入区域

| 令牌 | 用途 | 建议 |
|------|------|------|
| `InputFill` | 文本输入框背景 | 同 `SurfaceFill` |
| `InputStroke` | 文本输入框边框 | 同 `SurfaceStroke` |
| `UserInputFill` | 交互窗用户输入区背景 | 略深于主体（形成嵌入感）或同 `SurfaceFill` |
| `UserInputStroke` | 用户输入区分隔边框 | 同 `SurfaceStroke` |

### 下拉选择器

| 令牌 | 用途 | 建议 |
|------|------|------|
| `CmbFill` | ComboBoxItem 默认背景 | 始终白色 `#FFFFFF`（可读性） |
| `CmbStroke` | ComboBox 边框 | 同 `SurfaceStroke` |
| `CmbHighlight` | ComboBoxItem 悬停/选中高亮背景 | 淡蓝（`#DBEAFE`） |
| `CmbHighlightText` | ComboBoxItem 高亮时文字色 | 深蓝（`#1E40AF`） |
| `PickerBgFill` | ComboBox 本体 + 下拉菜单背景 | 始终白色 `#FFFFFF` |
| `PickerFgFill` | ComboBox 选中项 / 下拉项文字色 | 始终深色（`#111` 左右） |

### 语义色

| 令牌 | 用途 | 建议 |
|------|------|------|
| `Accent` | 强调色（内置 model 高亮、主操作按钮） | Indigo `#6366F1`，跨主题可微调亮度但保持色相 |
| `AccentHover` | 强调色悬停 | 深于 `Accent` |
| `CloseBtn` | 关闭按钮（macOS 风格红点） | 始终 `#FF5F57` |
| `CopyBtn` | 复制按钮 | 同 `Accent` |
| `SendBtn` | 发送/生成按钮 | 始终 `#22C55E` |

### 其它

| 令牌 | 用途 | 建议 |
|------|------|------|
| `Divider` | 分隔线 | 同 `SurfaceStroke` |
| `ShadowOpacity` | DropShadowEffect 不透明度 | `0.06`-`0.15`（Light）/ `0.2`-`0.4`（Dark） |

---

## 视觉层级设计原则

```
┌─────────────────────── TitleBarFill（最深处，标题区）───────────────────────┐
├───────────────── SurfaceElevated（主体背景，中浅色）────────────────────────┤
│                                                                              │
│  ┌── BtnFill（控件底色，与主体同色或略深）──┐                                 │
│  │    ┌── InputFill（输入框，与主体同色）──┐  │                                │
│  │    │                                    │  │                                │
│  │    └── InputStroke（边框，浅灰）────────┘  │                                │
│  └── BtnStroke ──────────────────────────────┘                                │
│                                                                              │
└── SurfaceStroke（面板外边框）────────────────────────────────────────────────┘
                              ↓ 双层投影
             环境光（BlurRadius=24, Depth=0, 低不透明度）
             方向光（BlurRadius=10, Depth=2, 中不透明度）
```

### 核心规则

1. **上深下浅**：TitleBar 最深 → 主体中浅 → 输入框/按钮与主体同色或略深
2. **边框统一**：所有边框用同一个 `SurfaceStroke` 色调
3. **悬停统一**：所有悬停背景用同一个 `TitleBarFill` 色调
4. **Dark 模式例外**：ActionMenu、ComboBox 在 Dark 下保持浅色（图标可读 + 下拉可读）

---

## 如何创建新主题

### 1. 拷贝模板

```sh
cp themes/blue.json themes/my-theme.json
```

### 2. 修改 JSON 中的 `name` 和 `description`

### 3. 调整颜色值

- **冷色倾向**：底色用 `#F3F6F9` 系列（RGB 中 B 略高）
- **暖色倾向**：底色用 `#F7F7F5` 系列（RGB 中 R 略高）
- **高对比**：`TextPrimary` 用 `#111111`，`TitleBarFill` 用更深的中间色
- **低对比/柔和**：`TextPrimary` 用 `#333333`，面板底色与边框更接近

### 4. 放入 `{exe}/themes/` 目录

### 5. 在 auracfg → General Settings → Theme 中选择

### 6. 托盘 Reload Settings 即时生效

---

## 内置主题参考

### Blue（冷蓝 Fluent）

```
底色 #F3F6F9  标题栏 #E6EEF7  边框 #CCE0F5  文字 #111111
特点：极浅冷灰底，1px 蓝调边框，明显蓝色倾向
```

### Notion（暖灰）

```
底色 #F7F7F5  标题栏 #EDEDE8  边框 #DBDBD3  文字 #1A1A1A
特点：极浅暖沙底，1px 暖灰边框，接近 Notion 白板底色
```

### Dark（暗色）

```
底色 #1E1E1E  标题栏 #2A2A2A  边框 #383838  文字 #E2E8F0
例外：MenuSurfaceFill / PickerBgFill 仍为浅色
```

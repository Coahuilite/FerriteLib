# FerriteLib UiKit 变化报告 — 0.7.0 早期 → 当前 HEAD

> 面向消费者 **NivariansGrandStructure (NGS)**，用于判断是否需要重写其 UI。
> 取证时间：2026-09-25（本机）。取证方式：对工作区 `ferritelib/` 的**只读**检视（git 历史 / 源码 / 文档 / 已有构建产物哈希）。
> **本报告未运行任何构建、测试、打包或变异脚本**；凡源文档本身标注 UNRUN 或本机未取证的项，均显式标注「未取证」。

---

## 0. 首要结论（先读这一段）

1. **不存在「0.7.0 首个 rc」。** 本仓库本地 tag 只有 `v0.2.0-rc1`、`v0.3.0-rc1`、`v0.4.0-rc1`；0.7 线**从未发布、无 tag、无 Release asset**（`ferritelib/docs/consumers/consume-from-0.7.0.md:21-24`；本地 `git tag` 输出）。因此本报告的「0.7.0 早期」基线取 **0.7 线开线提交 `8fe3026`（2026-09-17，三轴开到 0.7.0）**（`ferritelib/docs/development/0.7/README.md:14-15`），并给出 2026-09-21 批次这一中间锚点。
2. 当前契约版本是 **`0.7.0`**，消费者区间 **`[0.7.0,0.8.0)`**。
3. 0.7.x 处于**临时版本豁免**期：**公开新增不抬 minor**，到期条件 = 首个 tag/release 或跨库锁步结束。对 pin 了 `[0.7.0,0.8.0)` 的消费者意味着：**数字不变，但 surface 在同一区间内持续增长**，不能把「minor 相同」当作二进制兼容。
4. 从开线到 HEAD，公开 surface 的主要增量是：**1 个新 kind（`input/text-field`）+ 1 个新公开类型（`UiOption`）+ 1 个身份化 `UiNative.TextField` 重载 + 一大批 manifest 词汇（placement/help/payload/selected/width/match-content 等）+ 2 套命名 palette 与样式文档词汇**；**破坏性变更 4 条**（构造函数语义、容器间距缺省、tone 词汇、HoverPoint 删除）。
5. **HEAD 与已构建产物不同步**：根交付 DLL（`1.6/Assemblies/`）内嵌提交 `9938121`，且缺 `07f3c40` 的 Source 改动；`dist/build/*` 内嵌 `e7f2215`（代码与 HEAD 相同，但未内嵌 HEAD）；`dist/dev/` 更旧（`07f3c40`）。详见 §6。
   **交付前复核（2026-09-25 22:26，只读）：** `dist/build/*` 已于 22:15 重建、现内嵌 `ebb1a769`（新哈希见 §6.1），HEAD 已到 `41ad08d`；**根 carrier 与 `dist/dev` 未变**。本条的结论不变、且更成立——源码 HEAD 仍不等于任何已构建 DLL 的内嵌提交。

---

## 1. 版本号、版本轴与消费者兼容区间

| 轴 | 值 | 出处 |
| --- | --- | --- |
| **契约轴（contract）** | `FerriteLibVersion.Api = 0.7.0` | `ferritelib/Source/FerriteLib.UiKit/Kernel/FerriteLibVersion.cs:72` |
| **发布轴（release）** | `About/About.xml <modVersion> = 0.7.0` | `ferritelib/About/About.xml`（`<modVersion>0.7.0</modVersion>`） |
| **构建轴（build）** | `VersionPrefix = 0.7.0`，`VersionSuffix = dev`；`AssemblyVersion/FileVersion = 0.7.0.0` | `ferritelib/Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj:15-18` |
| **消费者断言区间** | `[0.7.0, 0.8.0)` | `ferritelib/docs/development/0.7/05-api-contract.md:5`；`ferritelib/docs/consumers/consume-from-0.7.0.md:50-53` |
| **`AssemblyInformationalVersion`** | `0.7.0-dev+<commit-sha>`（**不是兼容值**，仅内嵌提交） | `FerriteLibVersion.cs:20-27`；实测 DLL ProductVersion 见 §6 |

- 断言方式：消费者在自己的 Mod 构造函数里调用
  `FerriteLibVersion.Require(new Version(0,7,0), new Version(0,8,0));`
  （`consume-from-0.7.0.md:48-51`）。注意 **RimWorld 的 `ModRequirement` 无法表达版本**，所以每个消费者都必须自己断言（`ferritelib/AGENTS.md`「The game cannot express a prerequisite version」）。
- **临时豁免（关键）**：`0.7.x` 线上「公开新增不抬 minor」（维护者裁定 2026-09-22；`AGENTS.md`「Version axes」；`05-api-contract.md:12-26`）。到期 = 首个 tag/release **或** 跨库锁步结束；到期后恢复「pre-1.0 任何公开变化都抬 minor」。因此本区间内 `Api` 始终是 `0.7.0`，**不能据数字判断 surface 是否移动**。
- 版本登记与分级：`docs/api-tiers.md` 是兼容承诺的唯一 home；三层为 **Stable / public-unstable / internalize-candidate**（`ferritelib/docs/api-tiers.md:5-15`）。harness lane `FerriteLibApiTierTests` 强制每个导出类型恰好归类一次，并 pin 住 stable 列表（`api-tiers.md:17-21`）。

---

## 2. 自 0.7.0 早期起公开 surface 增加了什么

### 2.1 完整 widget kind 清单

**注册式 kind（`UiWidgetCatalog.Snapshot()` 返回的 `(scope,kind)`，scope 均为 `core`）：共 17 个。**
来源：`ferritelib/Source/FerriteLib.UiKit/Kernel/KernelCoreWidgetRegistrar.cs:6-30`；`api-tiers.md:55-58` 记「seventeen core kinds」（`input/text-field` 到达前是 16）。

| # | kind 字符串 | 用途（一句话） | 声明位置 |
| --- | --- | --- | --- |
| 1 | `input/button` | 一个标题+一条命令的点击叶子，自绘 hover/armed；支持 `ActionBind`、`PayloadKey`、`Chrome="none"` | `Kernel/Widgets/ButtonWidget.cs:25` |
| 2 | `input/checkbox` | 一个 bool 值绑定的两态勾选，自持 hover/armed 外观与命中规则 | `Kernel/Widgets/CheckboxWidget.cs:41` |
| 3 | `input/slider` | 绑定单个 float 的裸滑条，轨道=标签测宽后的剩余宽度 | `Kernel/Widgets/SliderWidget.cs:21` |
| 4 | `input/stepper-slider` | 步进器+滑条的复合控件，会话态保存拖拽连续性 | `Kernel/Widgets/StepperSliderWidget.cs:14` |
| 5 | `input/number-field` | 一个 float 以文本编辑、按提交规则写回（会话草稿） | `Kernel/Widgets/NumberFieldWidget.cs:18` |
| 6 | **`input/text-field`（0.7 新增）** | 单行字符串编辑；focus/草稿/提交/占位符归元素自身 | `Kernel/Widgets/TextFieldWidget.cs:26` |
| 7 | `input/dropdown` | 下拉选择：字符串值绑定 + 动态 `OptionsBind` / 静态 `OptionN`+`ValueN` | `Kernel/Widgets/DropdownWidget.cs:18` |
| 8 | `input/mode-row` | 响应式模式选择行（1..8 项；宽/中/窄三态），支持本地化 `TitleKeyN` 与 per-option `HoverHelpKey` | `Kernel/Widgets/InputModeRowWidget.cs:33` |
| 9 | `text/wrapped` | 一个按排定宽度自动换行并自测高的字符串叶子 | `Kernel/Widgets/WrappedTextWidget.cs:28` |
| 10 | `chrome/banner` | 主题化文本带，高度随文本；采用 `Tone`/`Emphasis` 角色对（G5） | `Kernel/Widgets/ChromeBannerWidget.cs:22` |
| 11 | `chrome/rule` | 水平细线分隔；自然尺寸=hairline，永不接收输入 | `Kernel/Widgets/RuleWidget.cs:28` |
| 12 | `section/header` | 主题标题 + 可选底部 1px 分隔线（`Chrome="none"` 关闭，0.7.x 新增） | `Kernel/Widgets/SectionHeaderWidget.cs:20` |
| 13 | `state/empty` | 空状态占位文本带（`Text`/`TextKey`，无 Bind） | `Kernel/Widgets/EmptyStateWidget.cs:18` |
| 14 | `display/progress` | 只读占比条（float/Max），值变化只重绘不改 rect | `Kernel/Widgets/ProgressWidget.cs:30` |
| 15 | `container/tree` | 数据驱动层级展示（`IReadOnlyList<UiTreeRow>`）；只做层级，不做排序/图遍历 | `Kernel/Widgets/TreeWidget.cs:35` |
| 16 | `chart/line` | 折线图（`IReadOnlyList<Vector2>` 或 `Points`）；`Editable`+`ActionBind` 时控制点可拖并发出 `UiChartPointChange` | `Kernel/Widgets/LineChartWidget.cs:22` |
| 17 | `Repeat` | 集合元素：按 `<Templates>` 里的模板为每个 item key 物化一行（引擎接管，非绘制控件） | `Kernel/Widgets/RepeatTemplateWidget.cs:28` |

**结构性容器 kind（不是注册式 kind，不在 catalog 里；由引擎按元素名识别）：9 个。**
`Stack`、`Row`、`Column`、`Wrap`、`Overlay`、`Section`、`Surface`、`Scroll`、`Clip`
来源：`UiHost.cs:1267-1278`（`IsContainerKind`）；`UiLayoutEngine.cs:2273-2321`（引擎镜像）、`:2872-2879`。

| 容器 | 用途 |
| --- | --- |
| `Stack` / `Column` | 垂直流（内容高 = 子元素之和） |
| `Row` | 水平流（内容高 = 子元素最大值；`Narrow="Column"/"Stack"` 可换向） |
| `Wrap` | 网格换行；**只有它接受 `Cols`/`NarrowCols`**（A3） |
| `Overlay` | 层叠/放置容器（placement 的宿主，内容高 = 最大值） |
| `Section` / `Surface` | 带表面绘制的垂直容器 |
| `Scroll` | 垂直滚动视口（滚动条内贴在右缘；快照可读 viewport/content extent） |
| `Clip` | 裁剪容器 |

**根元素 `<UiPage>`**（必须 `Schema="2"` + `Source`）；**通用 widget 元素 `<Widget Kind="...">`**（`Kind` 必填，不可嵌套子元素）；**`<Repeat>`**、**`<Styles>`**、**`<Templates>`**。
来源：`ferritelib/Source/FerriteLib.UiKit/Kernel/UiLayoutManifest.cs:54-57`（`SupportedElementNames`）、`:161-183`、`:469-500`（`ReadElement` 的 kind 推导）。

> 对照基线：开线提交 `8fe3026`（2026-09-17）时注册 kind 为 **16 个**（无 `input/text-field`），kind 常量清单见 `git show 8fe3026:Source/FerriteLib.UiKit/Kernel/Widgets/*.cs`。即 **0.7 线只新增了 1 个 kind**。

### 2.2 新增 kind / 类型 / 成员

| 类别 | 内容 | 出处 |
| --- | --- | --- |
| **新 kind** | `input/text-field`（B7，2026-09-20） | `05-api-contract.md:207-245`；`consume-from-0.7.0.md:59-65,113` |
| **新公开类型（public-unstable）** | `UiOption`（display+value 对，FL-16，2026-09-21） | `Kernel/UiOption.cs`；`05-api-contract.md:508-527`；`api-tiers.md:72-74` |
| **新公开成员** | `UiNative.TextField(Rect, string, UiSession, string, out bool)`（身份化重载） | `05-api-contract.md:222-225`；`api-tiers.md:119-125` |
| **新公开成员** | `UiNative.IsMouseOver(Rect, UiWidgetContext)`（带 context 的悬停判定；**不**应用 disabled 规则，只应用更高层遮挡规则） | `05-api-contract.md:274-278,306-311` |
| **新公开静态工厂** | `UiTheme.Vanilla`（与既有 `DarkGold` 成为命名 peer） | `Kernel/UiTheme.cs:356`；`05-api-contract.md:28-29` |
| **新公开成员** | `UiTheme.AccentHover`（只读派生，替换被删除的 `HoverPoint`） | `Kernel/UiTheme.cs:159-168`；`05-api-contract.md:163-172` |
| **新公开成员** | `UiStyleIssue.ElementPath`（未知 scope 名归属到声明元素） | `Kernel/UiStyleDocument.cs:31-38`；`05-api-contract.md:589-622` |
| **新公开成员** | `UiDiagnosticSubscription.GeometryEnabled / .GeometryOverlay / .DumpGeometry()`（仅 Dev 构建；Release 下 enable 会 throw） | `05-api-contract.md:652-711`；`consume-from-0.7.0.md:129` |

**未新增公开类型**：Batch 1 的 placement、G2/G3、B2/B5、SelectedKey、G1/G4、MatchContent、section/header 的 `Chrome` 等全部是 **manifest 属性字符串**，不改 API-tier 类型表（`05-api-contract.md:124-128,583-584`）。

### 2.3 新增 XML / manifest 词汇

**引擎级 widget 属性（`UiHost.CommonWidgetAttributes`，`UiHost.cs:889-907`）当前集合：**
`Id, Kind, Hidden, Tab, Width, WidthKey, MinWidth, MaxWidth, NarrowHidden, WideHidden, SelectedKey, Scheme, Density, Visible, VisibleKey, HelpKey, AlignX, OffsetX, AlignY, OffsetY`

**容器属性（`UiHost.ContainerAttributes`，`UiHost.cs:909-920`）：**
`Id, Kind, Gap, Padding, Height, Title, TitleKey, Hidden, Tab, Width, WidthKey, Fill, MinWidth, MaxWidth, Breakpoint, Narrow, Cols, NarrowCols, NarrowHidden, WideHidden, Scheme, Density, Visible, VisibleKey, AlignX, OffsetX, AlignY, OffsetY`

逐个新增项（相对 0.7.0 开线 `8fe3026`；均已用 `git show 8fe3026:...UiHost.cs` 对照确认当时**不存在**）：

| 词汇 | 语义 / 值域 | 出处 |
| --- | --- | --- |
| `AlignX / OffsetX / AlignY / OffsetY` | placement：`origin = parentOrigin + fraction*(parentSpan-selfSpan) + offset`；`AlignX∈{Left,Center,Right,Stretch}`，`AlignY∈{Top,Middle,Bottom,Stretch}`；`Stretch` 为缺省；`Offset` 可为像素或 `N%`；仅在 `Overlay` 子级（双向+百分比）或 flow 容器的交叉轴（仅像素）合法 | `Kernel/UiPlacement.cs:1-90,358-416`；`05-api-contract.md:124-136` |
| `HelpKey` | 元素级帮助身份（**引擎级**，opaque literal，库不翻译/不解析）；指针悬停时引擎 claim；**容器与模板根上被拒绝** | `UiHost.cs:894-901`；`05-api-contract.md:287-339` |
| `HoverHelpKey` | per-option 帮助身份绑定键；`input/mode-row` 发布 `DescriptionN`（无则 `ValueN`），`input/dropdown` 发布所悬停行的 **value**；仅在答案变化时写一次 | `05-api-contract.md:247-285,313-319`；`consume-from-0.7.0.md:114,117` |
| `TitleKey1..8` | `input/mode-row` 选项标题的翻译键（key 胜 literal；进入 label set 供 `Width="Auto"` 测量） | `05-api-contract.md:321-329`；`consume-from-0.7.0.md:116` |
| `WideHidden` | `NarrowHidden` 的镜像：仅宽态参与布局；父级无 `Breakpoint` 时创建期拒绝 | `05-api-contract.md:387-391` |
| `WidthKey` | float 值绑定回答声明宽度；显式 `Width` 胜出；announce 该键会重排；不可答则记录一条 appearance note 并保持 unsized | `05-api-contract.md:392-396` |
| `SelectedKey` | bool 绑定为真时把元素角色解析为 **Active** 态；**在 `<Repeat>` 模板内为 item-scoped**（B1）；Active 文本取 `TextOnGold` 且**忽略 `Emphasis`** | `05-api-contract.md:397-406,448-492` |
| `PayloadKey` | `input/button`：把绑定载荷交给命令；有它绑 `BindAction<string>`，无它仍 `BindCommand`；模板内 item-scoped | `05-api-contract.md:427-434` |
| `Chrome`（`input/button`） | 仅接受缺省或 `none`；`none` = 不画表面但仍命中，`Height="Auto"` 时高度取内容带；其他值创建期拒绝 | `05-api-contract.md:435-438` |
| `Chrome`（`section/header`） | 仅接受缺省或 `none`；`none` 抑制 1px 分隔线、保留标题；其他值创建期拒绝 | `05-api-contract.md:624-650` |
| `Height="MatchContent"` | 元素高度 = 父内容解析出的高度，自身不参与该计算；仅在 `Row`/`Overlay` 的子级且至少一个兄弟不声明时合法；垂直容器/`Wrap`/root/全部子级都声明 → 创建期拒绝并说明原因；`Narrow="Column"` 时退化为自身内容高 | `Kernel/UiPlacement.cs:24-40`；`05-api-contract.md:529-587` |
| `Tab` on containers | 容器现在可声明 `Tab`（引擎本就读它）；隐藏时带走整棵子树，但保留 node/state | `05-api-contract.md:341-380` |
| `Tone` 值域收缩 + `Emphasis` | `Tone∈{Neutral,Success,Warning,Danger}`；`Active`/`Disabled` 本 minor 内仍重定向到状态并各记一条去重 note，下个 minor 边界拒绝 | `05-api-contract.md:145-162`；`consume-from-0.7.0.md:111` |

**item-scope 绑定角色**（模板内解析为 `<Items>.<itemKey>.<declaredKey>`）：
`Bind、ActionBind、OptionsBind、VisibleKey、PayloadKey、SelectedKey`（`SelectedKey` 由 B1 加入；`Tab` 故意保持 page-level）。来源：`05-api-contract.md:448-492,519-520`；`api-tiers.md:514-531`。

### 2.4 新增样式 / 主题能力

- **两套命名 palette，构造器是空 bag**：`UiTheme.Vanilla`（中性表面+保留黄）与 `UiTheme.DarkGold`（暖金/近黑平面），互为 peer，均无默认；`new UiTheme()` 变成**未着色的 token bag**。来源：`Kernel/UiTheme.cs:105-168,347-419`；`05-api-contract.md:88-113`。
- **accent 只有一个存储色**：`AccentGold` + 只读派生 `AccentHover`（每通道向白走剩余距离的 30%，alpha 不变）+ `AccentWith(alpha)`；`HoverPoint` 不再存在。来源：`Kernel/UiTheme.cs:146-174`；`05-api-contract.md:163-172`。
- **样式文档词汇（`<Styles Schema="1">`，两种文本来源：独立 `.styles` 文件 或 manifest 内 `<Styles>` 节）**：
  - `<Scheme Name="...">` → `<Color Token="..." Value="#RRGGBB[AA] | r,g,b[,a]">`（22 个颜色 token：`Base/Panel/Raised/Hover/Selected/Success/Danger/WorkspacePlane/SectionBand/TextPrimary/TextSecondary/TextOnGold/TextOnDanger/TextDisabled/AccentGold/Border/BorderStrong/Divider/*Border`，**不含 HoverPoint**）；`<Font Token="DefaultFont" Value="Tiny|Small|Medium">`。
  - `<Density Name="...">` → `<Metric Token="..." Value="非负数">`（`Padding/Spacing/Gap/RowHeight/Hairline`）。
  - 页面级缺省：`<Styles Scheme="..." Density="...">`。
  - 失败策略：**appearance-class 全部 fail-soft**，逐条丢弃并记入 `UiStyleDocument.Issues`（上限 `MaxIssues=32`）；只有整份文档不可读/root 或 Schema 不对才 structural invalid。
  来源：`Kernel/UiStyleDocument.cs:102-249,312-538`；`05-api-contract.md:174-183`；`consume-from-0.7.0.md:130`。
- **未知 scope 名定位**：元素写 `Scheme`/`Density` 指向文档未定义的名字时，finding 现归属到**声明它的元素**（`UiStyleIssue.ElementPath`），而不是文档路径；仍 fail-soft。来源：`05-api-contract.md:589-622`。
- **Density 穿透容器**：容器 `Padding`/`Gap` 缺省改为取 `UiTheme.Geometry.Padding`/`Gap`（两套内置 palette 都是 `6`）——这同时是破坏性变更（见 §3）。`Spacing/RowHeight/Hairline` 仍是 widget 内部，不成为容器缺省。来源：`05-api-contract.md:141-144`；`consume-from-0.7.0.md:110`。

### 2.5 新增布局能力

- placement 词汇（`AlignX/OffsetX/AlignY/OffsetY`，CP-1/CP-2）与 `Height="MatchContent"`（CP-3 的一部分）。
- `WidthKey`（宽度轴第一次可被绑定驱动）、`WideHidden`（宽态可见性）。
- `Tab` on containers（一致性修复）。
- Density 到达容器间距（CP-0）。

---

## 3. 破坏性变更（对 pin `[0.7.0,0.8.0)` 的消费者意味着什么）

> 0.7 线**从未交付**，因此所有破坏都发生在「还没人编译过」的窗口里（`05-api-contract.md:31-40`），对 NGS 而言：只要你**重建/重编译**，就有迁移成本；不重建则可能悄悄跑在旧 surface 上。

| # | 变更 | 类型 | 迁移 | 出处 |
| --- | --- | --- | --- | --- |
| B-1 | `new UiTheme()` 由「带推荐皮肤的默认」变为**未着色空 bag**；`UiTheme.Vanilla` 成为新工厂 | 行为破坏（无签名变化） | 在必填 theme 参数处显式传 `UiTheme.Vanilla` 或 `UiTheme.DarkGold`，再覆写 | `05-api-contract.md:31-40,88-106`；`consume-from-0.7.0.md:69-72` |
| B-2 | 容器 `Padding` **和** `Gap` 缺省从 **0 → 主题 geometry（内置=6）** | 视觉破坏 | 需要旧结果的地方显式写 `Padding="0"`/`Gap="0"`；已声明的属性仍胜出 | `05-api-contract.md:141-144`；`consume-from-0.7.0.md:110` |
| B-3 | 作者 `Tone` 词汇收缩为 `Neutral/Success/Warning/Danger`；`Active`/`Disabled` 本 minor 内仍生效并各记 1 条去重 appearance note，**下个 minor（0.8）边界拒绝** | 词汇破坏（延迟） | 用「状态」表达：disabled 用只读值绑定，selected 用 `SelectedKey`；不要继续写 `Tone="Active"` | `05-api-contract.md:145-162`；`consume-from-0.7.0.md:111` |
| B-4 | `UiTheme.HoverPoint` **删除**，由只读派生 `AccentHover` 取代（该类型 public-unstable） | 成员移除 | 改读 `AccentHover`；样式文档写 `HoverPoint` 会记 1 条 unknown token，其余 scheme 仍生效 | `05-api-contract.md:163-172`；`consume-from-0.7.0.md:112` |
| B-5 | A1：Row 中 `Width="Auto"` 且标签不可测的子级不再塌成 1-unit stub，而按 unsized 分配 | 行为破坏 | 依赖 stub 的折叠填充需显式固定 `Width`；但注意「同一元素在宽/窄两形态表现不同」不能用固定 Width 复现，官方路线是两份互斥呈现 + `VisibleKey` | `05-api-contract.md:44-52`；`consume-from-0.7.0.md:77-92` |
| B-6 | A2：`Height` 创建期校验（非法值从 arrange 期 `FormatException` 变为创建期可定位 `UiContractException`）；A3：`Cols`/`NarrowCols` 仅在 `Wrap` 合法；A4：dropdown 精确 value 优先于显示文本回退；A5：wrong-kind options 注册会指名 `BindOptions<T>` | 校验收紧 / 行为修复 | 修正非法值；非 `Wrap` 容器移除 inert 的 `Cols`/`NarrowCols` | `05-api-contract.md:54-86`；`consume-from-0.7.0.md:73-88` |
| B-7 | B8：`input/mode-row` 的 `Width="Auto"` 不再把 `DescriptionN` 计宽（label set 只含 `TitleN`+`TitleKeyN`） | 布局破坏 | 若原来靠长 Description 撑宽，显式写 `Width` 或 `MinWidth` | `05-api-contract.md:185-205`；`consume-from-0.7.0.md:89-92` |
| B-8 | B1：`SelectedKey` 加入 item-scope 表——模板里声明 `SelectedKey` 且寄托 page-level 同名绑定的页面，语义改变（按行解析） | 语义破坏 | 在模板里绑 `items.<key>.selected`；这是修 bug 的方向 | `05-api-contract.md:448-492`；`consume-from-0.7.0.md:124` |
| B-9 | §4c：从定义中**移除元素会释放其全部状态**（子节点、state slot、滚动位、dirty/recovery、hit layer、widget 实例）；重新加入得到**全新状态**。这是 **0.4→0.5** 引入的语义，贯穿 0.6/0.7，**不是 0.7 新增** | 编译期不可见的行为 | 需要保状态：用 `Visible`/`VisibleKey`/`Tab` 隐藏而非删除；必须删除时把状态放在**自己的 model**（业务键），重加时 re-seed。检测：`session.GetNodeByElementId("your-id")`（声明期非 null，移除后 null） | `consume-from-0.7.0.md:144-181`；`api-tiers.md`（0.5 节） |
| B-10 | `UiSession.ClaimHover(string claim)` 参数名由 `elementId` 改为 `claim`（public-unstable，仅影响具名参数调用者） | 源码级 | 改用位置参数或更新实参名 | `api-tiers.md:90-95` |

**没有任何 public 类型 / kind 被删除，没有任何既有成员签名变化，`UiStatusTone` 的成员（含 `Active`/`Disabled`）未被移除。** 来源：`consume-from-0.7.0.md:132-134`；`05-api-contract.md:150-152`。

**对 pin `[0.7.0,0.8.0)` 的 NGS 的直接含义：**
- `Require(0.7.0, 0.8.0)` 在当前 HEAD 仍返回 true（`Api == 0.7.0`）。
- 但因为**临时豁免**，同一个数字内 surface 已显著增长；若 NGS 只在早期 0.7 编译过，**不会**收到编译错误提示，只会在运行/行为上遇到差异（尤其 B-1..B-4）。
- 所以：**要么升到当前 HEAD 重新编译并吸收 §4b 的迁移表，要么钉死在一个具体产物哈希**（见 §6），不要只按数字判断。

---

## 4. 测试 / 门禁，与消费者需跟进的迁移

### 4.1 自开线以来新增

- **门禁从若干条升到正式的 10 道 fail-fast 链**（`scripts/verify-local.ps1:16-41`），其中 **Gate 10（`scripts/verify-dev-instrument.ps1`）是 0.7 新门禁**：只在 Dev 配置跑 harness，要求出现 7 个 dev-only 断言名、断言数下限 12、dump 行下限 5、不得出现 release-only 名字，并对自身检查器做 4 组对照夹具（空 / 仅 release / 绿退出但缺名 / 完整样本不得被拒）。`05-api-contract.md:696-708`；`verify-local.ps1:278-289`。
- **新增脚本**（相对 `8fe3026`）：`scripts/export-carrier.ps1`、`scripts/verify-carrier-export.ps1`、`scripts/verify-dev-instrument.ps1`、`scripts/verify-license.ps1`，以及整个 `tools/mutation/`（自测变异电池：`Invoke-Mutation.ps1`、`mutation-check.ps1`、`batches/t2-2026-09-24.ps1`、`Invoke-AnchoredEdits.ps1`）。来源：`git diff --name-status --diff-filter=A 8fe3026..HEAD -- scripts tools`。
- **新增 14 个 lane 测试文件**（同样相对 `8fe3026`）：`KernelArchitectureProbeTests`、`KernelBatch1VerificationTests`、`KernelContainerTabTests`、`KernelContentHeightTests`、`KernelDevGeometryTests`、`KernelElementHelpTests`、`KernelModeRowHelpTests`、`KernelModeRowLocalizationTests`、`KernelOptionHelpTests`、`KernelOrdinarySettingsRecipeTests`、`KernelPlacementTests`、`KernelSectionHeaderTests`、`KernelTextFieldTests`、`KernelToneVocabularyTests`。
- 断言规模参考（来自仓库内文档/日志，**非本次运行**）：batch 2 记为 `ALL PASS, 2594 ok`（`ferritelib/docs/development/0.7/60-capability-dispositions.md:61-68`）；2026-09-24 的 gate-10 复原记录含 `2660 'ok:' line(s)`（`ferritelib/MEMORY.md:145-157`）。**当前 HEAD 的精确断言数未取证**（本次不跑构建）。
- **最近一次完整 10 门链的已记录结果**：全部 OK + `compatibility carrier unchanged (hash and mtime)`（`ferritelib/dist/dev-work/t2-C4-verify-local.log`，mtime 2026-09-24 15:26）。

### 4.2 消费者需要跟进的迁移步骤

1. **§4c（删除元素释放状态）** —— 这是「不会编译失败」的那条；重写时必须决定「隐藏 vs 删除」，并把需存活的状态放进自己的 model（`consume-from-0.7.0.md:144-181`）。
2. **§4b 的逐条表**：`Padding`/`Gap` 缺省（B-2）、`Tone` 值域（B-3）、`HoverPoint`→`AccentHover`（B-4）、`input/text-field`、`HoverHelpKey`、`HelpKey`、`TitleKey1..8`、`Tab` on containers、`UiOption`、`PayloadKey`、`Chrome="none"`+`Height="Auto"`、`WideHidden`、`WidthKey`、`SelectedKey`、banner 角色对、placement 四属性、`Height="MatchContent"`、`section/header` `Chrome="none"`、几何 instrument、`UiStyleIssue.ElementPath`（`consume-from-0.7.0.md:102-134`）。
3. **版本断言**保持在构造函数里（`Require(new Version(0,7,0), new Version(0,8,0))`）。
4. **不要复制 DLL**：用 `<HintPath>` + `<Private>false</Private>`，只有 `coahuilite.ferritelib` 可以随包分发（`consume-from-0.7.0.md:36-38`）。
5. **绑定纪律**：注册时声明 `UiInvalidation`（Paint/Measure/Structure），模型变化调用 `NotifyChanged(key)`；`Set` 不广播（`api-tiers.md:490-513`）。
6. **一条已知陷阱**：`UiTheme.SelectedSurface.Border` 的 fallback 是 `AccentGold`；显式传入扁平 scheme 的 `SelectedBorder` 会把 fallback 别名掉。想要 accent 就直接取 `theme.AccentGold` 或 `UiThemeDraw.AccentRail(rect, theme, active, width)`（`consume-from-0.7.0.md:136-142`）。

---

## 5. 外部 XML 驱动 UI（文档路径）的完整能力边界

### 5.1 结构

- 根：`<UiPage Schema="2" Source="...">`；`Schema` 与 `Source` 均为必填，Schema 只接受 `"2"`（`UiLayoutManifest.cs:161-183`）。最大深度 16、节点数 512（`:44-45`）。
- 根下：一个可选 `<Styles>` 节（≤32768 字符）、一个可选 `<Templates>` 节、若干容器 / `<Widget>` / `<Repeat>` 元素（`UiLayoutManifest.cs:224-256`）。
- **Widget 只能写成 `<Widget Kind="...">`**，且不可含子元素；容器的 kind 由**元素名**决定；`<Repeat>` 不能有子元素（`UiLayoutManifest.cs:486-543`；`Repeat` 由 `Template=` 指模板）。

### 5.2 支持的 kind

- **容器 9 个**：`Stack`、`Row`、`Column`、`Wrap`、`Overlay`、`Section`、`Surface`、`Scroll`、`Clip`（`UiHost.cs:1267-1278`）。
- **注册式 widget 17 个**（§2.1 的 kind 字符串，写在 `<Widget Kind="...">`）。
- **`Repeat`** 与 `<Templates>`：可物化行；模板元素只能是容器或 `<Widget>`；模板内**禁止嵌套 `<Repeat>`**；模板 Id/元素 Id 不得含 `/` 或保留分隔符 `#`（`UiLayoutManifest.cs:304-413`）。
- 模板内的绑定键不对 page bindings 校验（item-scoped），由引擎的模板校验器负责（`UiLayoutManifest.cs:31-39,107-118`）。

### 5.3 支持的属性

见 §2.3 的两张属性表 + 各 kind 的注册 schema。要点：
- **引擎级**属性不写进 kind schema 也合法（`UiHost.cs:1255-1256`）。
- **未知属性名 fail-closed**：创建期 `UiContractException`（`UiHost.cs:1258-1263`）。
- **fail-soft 的是值**：未知 `Tone` 值、不可解析的 `VisibleKey`/`WidthKey` 会回退并记一条去重 appearance note，不炸页面（`05-api-contract.md:136-138,392-396`）。
- placement 的拒绝矩阵、`MatchContent` 的拒绝矩阵均在创建期，且每条拒绝说明**原因**（`UiPlacement.cs`；`05-api-contract.md:544-557`）。

### 5.4 style / theme 文档

- **支持**。两种来源：独立 `<Styles Schema="1">` 文件（推荐做纯外观作者）或 manifest 内 `<Styles>` 节；同一个 parser、同一套词汇（`UiStyleDocument.cs:11-101`）。
- 词汇 = `Scheme`（22 色 token + `DefaultFont`）、`Density`（5 个 metric token）、页面级 `Scheme`/`Density`（`UiStyleDocument.cs:480-538`）。
- 两种来源同时给：**传入的 document 胜出**，manifest 节被作为 displaced 报告（不静默择一）（`UiHost.cs:101-109`；`api-tiers.md:75-85`）。
- **不支持**：文档级「选择内置 palette」（`Theme="Vanilla"` 之类）——disposition 为 DEFER，无引证（`60-capability-dispositions.md:39`）。`Color` 目前只覆盖 token 覆写，不能定义 base palette 本体。

### 5.5 repeat / template 与绑定

- **支持 Repeat/Templates**，item-scope 绑定键见 §2.3。`Items` 必须是 `IReadOnlyList<string>` 值绑定，`Template` 必须指向同一 manifest 的模板；行 key 空白/重复/含保留字符会被**有界报告拒绝**，不会 reconcil 到别的行状态（`api-tiers.md:514-531`；`RepeatTemplateWidget.cs`）。
- **绑定类型是 C# 强类型的 `IUiBindings`**（`BindValue/BindReadOnly/BindOptions/BindAction/BindCommand`），**XML 里没有表达式、没有 Def 引用、没有热重载 promise 之外的 codegen**（`AGENTS.md`「Purpose and non-goals」）。
- **文档热重载**：`UiDocumentService`（0.5 起）在**主线程 Pump** 时提交；watcher 线程只做 `Signal`；按文档原子批提交、last-known-good、有界表（`UiDocumentService.cs:10-50`）。`UiDocumentSource.Kind` 区分 `Layout`（`Schema="2"`）与 `Style`（`Schema="1"`）；失败是「按文档」而非全局（`UiDocumentSource.cs:6-66`）。Dev 构建默认开启自动 watch，Release 默认关闭（`UiDocumentService.cs:55-59`）。
- **窗口壳**：`UiPageWindow` 可直接托管一个 XML 页面，不需要消费者写 `Verse.Window` 子类；构造参数 = key / manifest / bindings / theme / translation / title / closeText / noticeText / metrics(可选) / document(可选)（`Kernel/UiPageWindow.cs:24-63`）；`PageHost` 暴露只读 `UiHost?` 供诊断与订阅（`:65-84`）。
- **已知边界（明确未覆盖）**：模板子树的绑定键不受 Host 创建期 walk 校验（由引擎模板校验器覆盖，属已知 0.5 gap，见 `05-api-contract.md:778-781`）；`Height="MatchContent"` 在模板子树/程序化 spec/R`Narrow="Column"` 时退化为 Auto（`05-api-contract.md:554-557`）。

---

## 6. HEAD、日期，与已发布/已构建 DLL 的字节与 SHA-256

- **HEAD**：`1c05b0f3d95a285dada7d113cffc6344e1de71a6`，日期 **2026-09-24 15:35:44 +0800**，标题
  `docs(memory): 'derivable' means rebuildable, and only three of eleven cases have it by another name (0.7.x, task-19)`；工作树干净（`git status --porcelain` 为空）。

| 产物 | 字节 | mtime | SHA-256 | 内嵌 ProductVersion | 与 HEAD 同步？ |
| --- | --- | --- | --- | --- | --- |
| `ferritelib/1.6/Assemblies/FerriteLib.UiKit.dll`（根交付 carrier，Release） | 253,440 | 2026-09-23 01:24:37 | `416BE3C1BDA44055BAF10EB0C5722E61533DBC30DEA616F9529894B9D84ECC79` | `0.7.0-dev+9938121f8b36...` | **否**：内嵌 `9938121`，且缺 `07f3c40` 对 `UiDevGeometryProbe/UiHost/UiNative/csproj` 的 Source 改动（`git diff --stat 9938121..HEAD -- Source` 非空） |
| `ferritelib/dist/build/Release/FerriteLib.UiKit.dll` | 253,440 | 2026-09-24 15:26:26 | `8A1A98ADB253D6A7381FE529F136504CC31401972BFFEE8E47C8639C2479D037` | `0.7.0-dev+e7f221521592...` | **代码同步、提交未同步**：`git diff --stat e7f2215..HEAD -- Source` 为空，但内嵌提交是 `e7f2215`（HEAD 之后只有 docs/tools 提交）；要内嵌 HEAD 需重建 |
| `ferritelib/dist/build/Dev/FerriteLib.UiKit.dll` | 281,088 | 2026-09-24 15:26:32 | `0F50D58969D9109274737CFCD943C0E7276DFA37A9C1C762AF346E46245BA8BE` | `0.7.0-dev+e7f2215...` | 同上（Dev 配置，含 `FER_DEV` 诊断） |
| `ferritelib/dist/dev/FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll`（Dev 包） | 281,088 | 2026-09-24 11:53:24 | `4729E2758929BBA52906828110E5F7980A0C77AC19CCF965AE3AC65E83B4956A` | `0.7.0-dev+07f3c401e3b9...` | **否**：内嵌 `07f3c40`，比 HEAD 旧；其 `version.txt` 自述 `commit=07f3c401e3b9 / payload-sha256=4729E2...`；`About/About.xml` mtime 2026-09-17 |

- 根 carrier 是「已发布」路径（消费者 `<HintPath>` 的常见目标），**当前是 stale 的**；`docs/consumers/consume-from-0.7.0.md:18-19` 里引用的哈希 `271128299A9CFF…FC8F82F` 与以上任何一份都**不匹配**，文档自己也注明那是「一次工作树的 rehearsal identity，作为历史引用」。
- 说明：本次**没有**运行任何构建，所以无法给出「在 HEAD 重建后」的新哈希；上表是**现有磁盘产物**的事实。
- 结论：**源码 HEAD ≠ 任何已构建 DLL 的内嵌提交**；`dist/build/*` 的**代码**与 HEAD 一致（仅 docs/tools 提交在其后），根 carrier 与 `dist/dev` 的**代码也落后**。消费者若要用根 carrier，必须重新构建/重新导出。

### 6.1 身份复核（2026-09-25 22:26，只读、同一方法）

§6 是**取证时刻（21:40）**的磁盘事实；此后工作区又动了三个提交（T29：`312f9b7` 22:12:35 / `ebb1a76` 22:15:35 / `41ad08d` 22:18:33）并做了一次构建（22:15:36/43）。交付前按同一只读方法复测——`Get-FileHash` + PE 版本资源 `FileVersionInfo.ProductVersion`，**不加载程序集**（与 `scripts/export-carrier.ps1` 打印身份的方式一致）：

| 产物 | 字节 | mtime | SHA-256 | 内嵌 ProductVersion | 相对 §6 |
| --- | --- | --- | --- | --- | --- |
| `1.6/Assemblies/FerriteLib.UiKit.dll`（根交付 carrier，Release） | 253,440 | 2026-09-23 01:24:37 | `416BE3C1BDA44055BAF10EB0C5722E61533DBC30DEA616F9529894B9D84ECC79` | `0.7.0-dev+9938121f8b36…` | **未变** |
| `dist/build/Release/FerriteLib.UiKit.dll` | 253,440 | 2026-09-25 22:15:36 | `1738EC4AC45457212CF853E6C1134F027F0108145B13CED7C3241C0E9F30420A` | `0.7.0-dev+ebb1a7699a60…` | **已变**（原 `8A1A98AD…` / `e7f2215`） |
| `dist/build/Dev/FerriteLib.UiKit.dll` | 281,088 | 2026-09-25 22:15:43 | `F7A1BBFA0D504885234E0940DB4BBA0814B064E6F5BD465D507D4A964ACF6889` | `0.7.0-dev+ebb1a7699a60…` | **已变**（原 `0F50D589…` / `e7f2215`） |
| `dist/dev/FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll`（Dev 包） | 281,088 | 2026-09-24 11:53:24 | `4729E2758929BBA52906828110E5F7980A0C77AC19CCF965AE3AC65E83B4956A` | `0.7.0-dev+07f3c401e3b9…` | **未变**（`version.txt` 自述 `commit=07f3c401e3b9`） |

- **HEAD** 现为 `41ad08d`（2026-09-25 22:18:33）。`git diff --stat e7f2215..HEAD -- Source` 仍为**空**，`git diff --stat 9938121..HEAD -- Source` 仍为**非空（4 文件）** ⇒ §6 的两条判断（`dist/build/*` 的代码与 HEAD 一致；根 carrier 缺 `07f3c40` 的 Source 改动）**都仍然成立**。
- §6 那句「工作树干净」在交付时**已不成立**：本文件当时未被跟踪（即本报告本身）。

**证据类别（照台账 §14.21 的三类）**：本报告**没有任何运行时测量**——它没有构建、测试、打包或变异。
- 字节 / SHA-256 / mtime 是**交付时刻磁盘上的事实，事后直接读取**；本报告**不主张**这些字节与任何构建/运行时刻相同（那需要 before/after 之类的单调量，本报告没有）。
- 全部 `px` 数字（如 `section/header` 的 1px 分隔线、placement 的像素微调）都是**源码 / 契约里声明的值（约定）**，**不是实测布局几何**；行高类数字若出现，其约定值以实测校准表为准。
- 凡源文档标注 UNRUN、或本机未取证的项，一律按 UNRUN / 未取证照写，不升级。

---

## 7. 如果 NGS 决定抛弃旧 UI 重写，最值得用的 5 项新能力

1. **裸命中带：`input/button` 的 `Chrome="none"` + `Height="MatchContent"`（G3 + MatchContent）。**
   命中面积可以与相邻文本**等高**，替代手量矩形；官方引用页的覆盖从 69.9%/53.3% 提升到 ≥99.5%（`05-api-contract.md:749-800`；`consume-from-0.7.0.md:121,127`）。这是重写「整行可点」类 UI 最省事的一条。
2. **集合行自述身份与选中态：`PayloadKey`（G2）+ item-scoped `SelectedKey`（B1）。**
   一行能把自己的 key 交给命令，并能声明「我是被选中的那行」，不必在 page 级重造选择态（`05-api-contract.md:427-434,448-492`；`consume-from-0.7.0.md:120,124`）。
3. **`input/text-field` + `HelpKey` / `HoverHelpKey`（B7 / G1 / G4）。**
   单行文本框的 focus、草稿、提交规则、禁用拒绝、恢复槽与 fit 审计全归元素；帮助身份由引擎 claim，消费方只读 `Session.HoverClaim`/`HoverClaimElement`（`05-api-contract.md:207-339`；`consume-from-0.7.0.md:113-117`）。
4. **样式/密度数据化：`UiTheme.Vanilla`/`DarkGold` + `<Styles Schema="1">`（`Scheme`+`Density`）+ 引擎级 `Scheme`/`Density` 属性。**
   皮肤与密度成为文档数据而不是重编译；未知名 fail-soft 且定位到声明元素（`Kernel/UiTheme.cs:347-419`；`UiStyleDocument.cs:480-538`；`consume-from-0.7.0.md:69-72,130`）。
5. **布局/放置词汇：`AlignX/OffsetX/AlignY/OffsetY` + `WidthKey` + `WideHidden` + 容器可声明 `Tab` + density 穿透容器。**
   让「居中/贴边/像素微调/可绑定宽度/宽窄两态/按 tab 分区」落在数据里，而不是 C# 里（`UiPlacement.cs:1-90`；`05-api-contract.md:115-183,341-406`；`consume-from-0.7.0.md:110,118,122-126`）。

> 附：开发期最值得用的诊断工具是 **dev-only 几何 instrument**（`host.Diagnostics.GeometryEnabled=true` → `DumpGeometry()` 给出每个节点的 arranged/draw/window 矩形、坐标空间原点、高度模式与解析高度，以及每次采样的 `hit/miss/covered/disabled` 判定）。它在 Release 构建里不存在，enable 会 throw（`05-api-contract.md:652-711`；`consume-from-0.7.0.md:129`）。

---

## 8. 未取证 / 明确未知

- **0.7.0 的 rc / tag / Release asset**：本仓库本地不存在；远程标签未取证（无网络动作）。
- **当前 HEAD 自身的 10 门链与 harness 结果**：本次只读，未运行构建/测试；最近可查记录是 `dist/dev-work/t2-C4-verify-local.log`（2026-09-24 15:26，十门 OK）。当前精确断言数未取证。
- **真实游戏 E2E 与消费者编译验收**：仓库文档明确记为 **未完成/未主张**（`docs/development/0.7/README.md:77-91`；`next-stage-guide-zh.md:37-38`）。Packs 行点击失效根因未确认。
- **`GeometryEnabled` 在真实多模组共存下的行为**：`UiFitAudit.Enabled` 全局开关被记为已知风险，非已复现故障（`AGENTS.md`；`next-stage-guide-zh.md:81-82`）。
- **远程 origin/0.7.x 的实际 tip**：本地 `remotes/origin/0.7.x` 存在但未核对与本地 HEAD 的差异（未取证）。
- `docs/api-tiers.md` 的 `LineChartWidget` 分层理由已失效，内部化移动**待裁**（属下一个破坏性窗口），本轮未改（`60-capability-dispositions.md:80-197`）。

---

### 引用文件清单（相对 workspace 根）

- `ferritelib/Source/FerriteLib.UiKit/Kernel/FerriteLibVersion.cs`
- `ferritelib/Source/FerriteLib.UiKit/Kernel/KernelCoreWidgetRegistrar.cs`
- `ferritelib/Source/FerriteLib.UiKit/Kernel/UiHost.cs`
- `ferritelib/Source/FerriteLib.UiKit/Kernel/UiLayoutManifest.cs`
- `ferritelib/Source/FerriteLib.UiKit/Kernel/UiLayoutEngine.cs`
- `ferritelib/Source/FerriteLib.UiKit/Kernel/UiPlacement.cs`
- `ferritelib/Source/FerriteLib.UiKit/Kernel/UiTheme.cs`
- `ferritelib/Source/FerriteLib.UiKit/Kernel/UiStyleDocument.cs`
- `ferritelib/Source/FerriteLib.UiKit/Kernel/UiDocumentSource.cs`
- `ferritelib/Source/FerriteLib.UiKit/Kernel/UiDocumentService.cs`
- `ferritelib/Source/FerriteLib.UiKit/Kernel/UiPageWindow.cs`
- `ferritelib/Source/FerriteLib.UiKit/Kernel/Widgets/*.cs`（17 个 kind）
- `ferritelib/docs/development/0.7/05-api-contract.md`
- `ferritelib/docs/development/0.7/60-capability-dispositions.md`
- `ferritelib/docs/development/0.7/next-stage-guide-zh.md`
- `ferritelib/docs/development/0.7/README.md`
- `ferritelib/docs/consumers/consume-from-0.7.0.md`
- `ferritelib/docs/api-tiers.md`
- `ferritelib/AGENTS.md`
- `ferritelib/MEMORY.md`
- `ferritelib/scripts/verify-local.ps1`

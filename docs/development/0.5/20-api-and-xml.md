# 20 公共 API 与 XML 使用说明（0.5.x）

> **状态：设计草案。** 本文件是本轮的接口交接合同；`0.5.0` 冻结的是"窗口区间"，
> 具体类型名与签名在实现落地前仍是占位。落地后由对应提交把最终形态写进本节，
> 并在 `docs/api-tiers.md` 登记分级。**任何伪代码都不是已实现证据。**

## 1. 页面作者（XML）会看到什么

一份布局 XML 由元素构成，元素要么是**结构容器**，要么是**已注册 kind**：

```xml
<Ui SchemaVersion="2" Source="example/page">
  <Window Id="main" TitleKey="example.page.title" Width="640" Height="420" CloseOnCancel="true">
    <Scroll Id="body" Height="Fill">
      <Section Id="members" TitleKey="example.page.members">
        <Repeat Id="member-list" Items="members" Template="member-row" />
      </Section>
      <Checkbox Id="flag-ammo" LabelKey="example.page.flag.ammo" Label="Ammo"/>
      <Button Id="apply" TextKey="example.page.apply" Command="apply"/>
    </Scroll>
  </Window>
</Ui>
```

要点：

- **结构、取值、外观分离。** XML 只声明结构、绑定键与外观角色（`Tone`/`Emphasis`/`Scheme`/`Density`），
  **不写表达式、条件、循环或代码**（library 的非目标）。
- **每 kind 自带属性 schema**：未登记的属性在创建期被拒绝（`UiContractException`），不是运行期静默忽略。
  弃用属性走"接受并重定向一个 minor"的通道。
- **绑定键是名字**，没有一个消费者能"顺手读到另一个窗口的选中态"：命令携带自己的上下文键。
- **条件显隐（P2 落地）**：`Visible="true|false"` 是静态写法，引擎级（容器与 kind 都接受，默认 true）；
  `VisibleKey="bindingKey"` 是动态写法，按 `IUiBindings.TryGetBool` 解析 bool 值绑定——键缺失或类型不符
  **不致命**，元素保持可见，并记一条按 (元素, kind, 属性, 键) 去重的 appearance 回退（fail-soft 不静默）。
  既有 `Hidden="true|1"` 是静态旧写法，语义不变、继续可用，新页面用 `Visible`/`VisibleKey`。
- **失效分类由绑定声明（P2 落地）**：`BindValue`/`BindReadOnly`/`BindOptions`/`BindAction` 的
  `invalidates` 参数取 `UiInvalidation` 的 `Paint`/`Measure`/`Structure`；`NotifyChanged(key)` 在帧边界
  合并提交：`Paint` 只影响下一次绘制、不触发重排测量，`Measure`/`Structure` 才让声明该键的元素重测。
  动态可执行性用 `BindCommand(key, action, canExecute)` + `IUiBindings.CanExecute`；禁用元素不执行、
  不捕获 hot control/pointer，禁用外观走既有的 writability 漏斗（`writable: false` → `UiStatusTone.Disabled`）。

## 2. 业务作者（C#）会看到什么

```csharp
// 注册一个 kind（扩展面）；普通页面不需要 C#
UiWidgetRegistry.Register("example", "badge", () => new BadgeWidget(),
    attributeSchema: new[] { "Id", "Tone", "Emphasis", "Label", "Visible" },
    labelAttributes: new[] { "Label" });

// 绑定与命令：值、只读值、选项、命令（含可执行性）
bindings.BindValue<string>("page.title", () => model.Title, v => model.Title = v);
bindings.BindReadOnly<int>("page.count", () => model.Count);
bindings.BindCommand("apply", () => model.Apply(), canExecute: () => model.CanApply);
```

## 3. 本轮要落地的公共面（按包）

| 包 | 公共类型（草案名） | 语义 |
| --- | --- | --- |
| P1 | `UiWindowKey` | `(consumer, windowKind, contextKey)` 三元身份 |
| P1 | `UiWindowCatalog` | 注册/打开/关闭/查询；同 key 重开 = 激活既有实例 |
| P1 | `UiWindowOptions` | 每窗策略：是否允许同型多开、暂停、相机、输入吸收、拖拽/缩放、初始尺寸 |
| P1 | `UiFocusPolicy` | 活动目标与键盘跟随：点击即激活；失活控件不接收输入，草稿保留 |
| P2 | `IUiBindings.GetRevision(key)` | 每个键一个修订号，替代全局计数器 |
| P2 | `IUiBindings.NotifyChanged(key/keys)` | 消费者在模型变更后发通知；库在帧边界合并提交 |
| P2 | `UiInvalidation`（flags） | `Paint` / `Measure` / `Structure`：失效分类，固定尺寸读数不触发整页测量 |
| P2 | `BindCommand(actionId, Action, Func<bool> canExecute)` | 动态可执行状态 → 统一禁用交互（不执行、不捕获、禁用外观） |
| P2 | `Visible`/`VisibleKey` 属性 | 条件显隐；受支持的结构更新只重建受影响子树 |
| P3 | `Repeat` + 模板 | keyed repeater：按 item key 复用节点、删除即清理状态、item 局部绑定作用域 |
| P3 | `input/checkbox`、`display/progress`、`container/tree` | 三个公共控件；树只表达层级，不承担调度 |
| P4 | `UiDocumentSource` | `{ Id, Kind: Layout\|Style, Path }` |
| P4 | `UiDocumentService` | 有界、可释放；依赖追踪；候选校验；原子批次提交；LKG 回退；开发模式自动监听可配置 |
| P4 | `UiReloadReport` | 每个文档/窗口的接受或拒绝及原因（文件、元素、原因），失败按版本去重 |
| P5 | `UiDiagnosticHub`、`UiDiagnosticEvent` | 每 host/session 订阅与有界事件；reload/fit/recovery + 计时 |
| P2（落地补充） | `IUiBindings.GetRevision(key)` / `NotifyChanged(key...)` / `GetInvalidation(key)` / `CanExecute(actionId)` / `TryGetBool(key, out bool)` | 每键修订与通知；失效分类读回；命令可执行性；可见性查询要能把"键不可解析"当**答案**而不是异常（`TryGet<T>` 对类型不符是抛出的） |

## 4. 热重载合同（P4 落地时必须逐条对照）

1. 窗口声明引用布局与样式文件；库记录依赖。改独立布局只更新该窗口；改共享样式更新**所有实际依赖**的窗口。
2. 文件监听只提交变化信号；**不在工作线程访问 Verse/Unity/业务对象**。合并重复通知、稳定读取。
3. 在安全主线程边界完整解析并校验候选；**当前 GUI pass 不更换树**；以内容标识避免重复提交。
4. **批次原子性**：同一批先验证全部受影响窗口，任一失败则整批保留原有效版本，不做半批更新。
   无关文档的失败不阻塞其他批次。
5. 首次加载前没有外部文件时用内嵌资源；后续失败保留 **last-known-good**。所有者是有界、可释放的文档服务，
   **不能依赖已关闭的窗口 session，也不新增无界进程级静态缓存**。
6. 稳定 ID/key 与兼容 kind 保留滚动、选择、展开与输入草稿；删除或换 kind 清理旧状态。
   重载不提交输入草稿、不重放业务命令、不重置业务模型。
7. 活跃拖拽 / IME / 文本编辑：安全暂停提交或取消 UI 捕获并保留兼容草稿；**不得留下全局 hotControl**。
   失败显示文件/元素/原因，按失败版本去重；修好可恢复。
8. 颜色更新无需测量；字号/间距/结构变化触发布局。发布包保留外部 XML 与内嵌回退；
   自动监听开发模式默认开启，生产开关可配置。

**不在范围**：运行中的 C# kind 增删改。

## 5. 多窗口与焦点合同（P1 落地时必须逐条对照）

- 每窗独立 session：各自保留选择、滚动、展开与输入草稿；业务模型不被 session 持有。
- 同一时刻只有一个活动目标接收键盘输入；**键盘跟随点击**；切换窗口不丢各自状态。
- 面板外可正常操作地图；面板内输入不穿透；与原版模态窗口正确共存。
- 关闭一窗不得取消另一窗的业务任务或释放其订阅。
- 实例 key = `consumer + window-kind + context-key`；每座目标每类最多一个，重复打开激活既有实例。
- **暂停是独立策略**：库提供配置，不替消费者决定产品默认值。
- 退出当前游戏、载入另一存档、目标销毁时关闭或进入明确失效状态，取消订阅，不携带旧引用跨局。

## 6. 示例位置

- 中立夹具页 / 规格：`tools/FerriteLib.UiKit.Tests`（**库内示例，不是消费证据**）。
- 消费者侧示例：由消费者团队在自身仓库落地；本仓只提供 `30-consumer-handoff.md` 的接入步骤。

## 3.1 已落地接口（第一个开发包：P1 + P2 + P4）

以下签名取自当前 `0.5.x` 树，是**已实现并自动化验证**的部分。P3（集合与控件）与 P5（诊断隔离）仍在开发，见 §3。

### 窗口（P1）

```csharp
// identity
public readonly struct UiWindowKey { string Consumer; string WindowKind; string ContextKey; }

// per-window policy; a null option means "leave the game's own behaviour alone"
public sealed class UiWindowOptions {
    bool AllowMultipleInstances = true;   // false => the vanilla exact-C#-type rule applies
    bool? ForcePause, PreventCameraMotion, AbsorbInputAroundWindow;
    bool? Draggable, Resizeable, CloseOnAccept, CloseOnCancel, CloseOnClickedOutside;
    bool SetFocusOnActivate = true;
}

public enum UiFocusPolicy { ... }          // how an activated target is focused

public sealed class UiWindowCatalog {
    UiFocusPolicy FocusPolicy { get; }
    IReadOnlyList<UiWindowHost> Instances { get; }
    UiWindowHost? ActiveWindow { get; }
    void Register(...);                    // kind -> factory + options
    bool Open(UiWindowKey key);            // same key reopens = activates the existing instance
    bool Close(UiWindowKey key);
    int  CloseAll();
    bool TryGet(UiWindowKey key, out UiWindowHost host);
    bool IsActive(UiWindowKey key);
    void Activate(UiWindowKey key);
}

// a concrete shell for an ordinary XML page: no bespoke C# Window subclass needed
public sealed class UiPageWindow : UiWindowHost { ... }
```

```xml
<Window Id="main" Title="Your title" CloseText="Close">
  <Scroll Id="body"><Checkbox Id="flag" Label="Ammo"/></Scroll>
</Window>
```

### 绑定、失效与命令态（P2）

```csharp
// every binding declares what its change invalidates
void BindValue<T>(string id, Func<T> get, Action<T> set, UiInvalidation invalidates = UiInvalidation.Everything);
void BindReadOnly<T>(string id, Func<T> get, UiInvalidation invalidates = ...);
void BindCommand(string actionId, Action action, Func<bool> canExecute, UiInvalidation invalidates = ...);

[Flags] public enum UiInvalidation { Paint, Measure, Structure, Everything }

void NotifyChanged(params string[] keys);   // coalesced to one commit per frame boundary
long GetRevision(string key);
UiInvalidation GetInvalidation(string key);
bool CanExecute(string actionId);
bool TryGetBool(string key, out bool value);
```

Rules a page author relies on:

- **Paint** reuses the arrangement (a fixed-size readout does not re-measure the page).
  **Measure**/**Structure** mark the declaring nodes and move the single arrangement clock.
- A disabled command is not executed, does not capture the pointer/hot control, and takes the disabled
  treatment from the one writability funnel. There is no per-widget disabled branch to copy.

条件显隐（XML）：

```xml
<Text Id="note" Visible="false" ... />        <!-- static -->
<Text Id="note" VisibleKey="hasWarning" ... /> <!-- dynamic: bool binding key -->
```

- 不可解析的 `VisibleKey` **不**让页面创建失败：元素保持可见，并记录一条去重的 fail-soft 报告。
- `Hidden` 仍按原样工作，是旧的静态形式，不废弃、不重载。
- 显隐不改变未命名兄弟的声明序号（稳定身份），隐藏元素保留其节点与状态。

### 文档与热重载（P4）

```csharp
public enum UiDocumentKind { Layout, Style }
public readonly struct UiDocumentSource { string Id; UiDocumentKind Kind; string Path; }
public sealed class UiReloadReport { DocumentId, Kind, Path, Version, Accepted, Skipped, Duplicate,
                                     Rejected, Element, Reason, HostsAffected, HostsCommitted }

public sealed class UiDocumentService : IDisposable {
    static bool DefaultAutoWatch { get; }          // ON in dev builds, OFF in release unless configured
    bool Add(UiDocumentSource source, string embeddedFallbackXml = "");
    bool Attach(UiHost host, string? layoutId, string? styleId = null);
    void Detach(UiHost host);
    bool Pump();                                   // main-thread commit boundary (called from UiHost.BeginFrame)
    UiReloadReport? Reload(string documentId);      // manual entry point, always available
    IReadOnlyList<UiReloadReport> ReloadAll();
    bool Signal(string documentId);
}
```

- 一个批次先验证**全部**受影响 host（layout 走创建期契约，style 走 `TryPrepareStyleCandidate`），
  任一失败则整批保留原有效版本；提交期失败会把已提交的 host 回滚。
- 首次无外部文件时用内嵌回退；后续失败保留 last-known-good；失败按版本去重报告。
- 工作线程只投递信号（BCL only）；提交发生在主线程帧边界，当前 GUI pass 不换树。
- 稳定 Id/key 保留滚动/选择/展开/草稿；删除或换 kind 清理旧状态；拖拽/编辑中重载会释放 hot control 并保留草稿。
- **不在范围**：运行中的 C# kind 增删改。
## 7. P3 落地形态：keyed repeater 与三个公共控件（已实现，证据类 1/2）

> 本节由落地提交追加，形态与实现同源。命名与语义均以本节为准；§1 的示例仍是设计草案。
> 代码位置：`Source/FerriteLib.UiKit/Kernel/Widgets/{CheckboxWidget,ProgressWidget,TreeWidget,RepeatTemplateWidget,UiTreeRow}.cs`、
> `Kernel/UiLayoutEngine.cs`、`Kernel/UiLayoutManifest.cs`；lane：`tools/FerriteLib.UiKit.Tests/KernelRepeatTests.cs`、
> `KernelControlKindTests.cs`、`KernelFixturePageTests.cs`。

### 7.1 XML 形态

```xml
<UiPage Schema="2" Source="example/page">
  <Templates>
    <!-- 模板：一次声明，按 item key 物化；每个模板元素必须有 Id（模板内部 Id 不得含 '#'） -->
    <Row Id="entry" Gap="6" Padding="2">
      <Widget Id="done"    Kind="input/checkbox"   Bind="done"    LabelKey="example.entry.done" />
      <Widget Id="caption" Kind="text/wrapped"     Bind="caption" />
      <Widget Id="amount"  Kind="display/progress" Bind="amount"  Max="1" Height="8" />
    </Row>
  </Templates>

  <Column Id="body" Gap="6" Padding="6">
    <Widget Id="total"   Kind="display/progress" Bind="total"   Max="1" Height="10" />
    <Repeat Id="entries" Items="entries" Template="entry" Gap="2" />
    <Widget Id="outline" Kind="container/tree"  Bind="outline" ActionBind="focus" RowHeight="18" />
  </Column>
</UiPage>
```

- `<Templates>`（可选，最多一个，depth 1）：每个直接子元素是一份模板，其 `Id` 即模板名；模板元素**不在**
  `Roots` 里，因此 Host 的创建期遍历看不到它们。模板子树内不得再出现 `<Repeat>`（不做二层协调）。
- `<Repeat>`（无子元素）：`Items` 命名一个 **`IReadOnlyList<string>` 值绑定**（模型按显示顺序投影出的稳定业务 key），
  `Template` 命名一份模板；`Padding`/`Gap`/`Height` 与容器同义。`Items` 未绑定、类型不符、`Template` 悬空、
  `Repeat` 带子元素或属性越界，都是**创建期拒绝**（`UiContractException`/`FormatException`），不是空列表。
- 行身份 = `<declaredId>#<itemKey>`，行内绑定 key = `<Items>.<itemKey>.<declaredKey>`（`Bind`/`ActionBind`/
  `OptionsBind`/`VisibleKey` 四项被作用域化；`Tab` 保持页面级，因为 tab 不是某一行的答案）。
- item key 契约：空白、重复、含保留字符（`/`、`#`、身份编码的两个标记字符）的 key **拒绝该行** —— 不产生元素、
  不产生节点、不产生状态，经有界通道（`UiFitAudit`，按 路径|kind|属性|值 去重）记录一条；**不做静默协调**。
  同 key 重排复用同一节点与状态；删除 key 由 session 既有 `PruneNodesExcept` 释放该行的节点、状态槽、子节点与命中层。
  **隐藏不等于移除**：`Visible`/`VisibleKey`/`Tab` 隐藏的 `<Repeat>` 会保留上一次物化的行身份与状态（隐藏元素保留节点这条 P2 规则同样适用于行），
  只有定义/绑定不再提供的 key 才被释放；重新可见时复用同一节点。
- `input/checkbox`：`Bind`（bool 值绑定，必填）+ 可选 `ActionBind`（命令，声明后该元素进入 P2 的禁用漏斗）+
  `Label`/`LabelKey`/`Height` + `Tone`/`Emphasis`。
- `display/progress`：`Bind`（**float**，必填）+ 可选 `Max`（正数，默认 1）+ `Height` + `Tone`（无文本，故无 `Emphasis`）。
  值按 `value/Max` 取 [0,1] 并夹紧；不取输入。
- `container/tree`：`Bind`（**`IReadOnlyList<UiTreeRow>`**，必填）+ 可选 `ActionBind`（`BindAction<string>`，
  命中时以该行 key 为载荷）+ 可选 `RowHeight`（正数，默认 density 行高）+ `Tone`/`Emphasis`。

### 7.2 每个新 kind 对 "What earns a kind" 的论证（`AGENTS.md`）

| kind | 拥有的、manifest 无法表达的东西 | 不拥有/不复制 |
| --- | --- | --- |
| `input/checkbox` | 每元素交互态（hover/armed 三态）与自己的命中规则；两态回写（写读到的反值）；盒内几何 | 禁用外观不是私有分支：可写性走 `IsWritable`→resolved table，命令态读回引擎发布的 `UiNode.IsDisabled`，两者都落到**唯一**的 Disabled 行；输入仍走 `UiNative.Button(rect, ctx)` 这一个漏斗 |
| `display/progress` | 值契约（float、`Max`、夹紧）与自身内容的测量契约（固定 band ⇒ 值变化是 `Paint` 类）+ 填充几何 | 不取输入（与 `chrome/rule` 同为"负规则"）；不新增第二套失效路径 |
| `container/tree` | 每行 band 测量（`RowHeight`/density）、层级缩进几何、逐行命中规则 | 只表达层级：不排序、不调度、不评估任务图；行集合与展开态都归模型，库按 `UiTreeRow.Key` 读取，从不按下标保存状态 |
| `Repeat`（元素，非绘制控件） | 集合协调本身：按业务 key 物化行、复用节点/状态、删除释放；item 局部绑定作用域 | 不是第二种身份方案：行身份由既有 `UiNodeId` 组合、节点表与 prune 生命周期承载；不是第二个刷新通道（`Items` 作为声明 key 进入 P2 的按 key 通知） |

### 7.3 失效分类（与 P2 组合，不新增路径）

- `Items` 绑定应按 `UiInvalidation.Structure` 注册：插入/删除/重排都改变行集合，需要重新排列。
- 每行每个字段按语义注册：checkbox 的 bool 与其后的命令、progress 的 float 都是 `Paint`（固定 band，值不改几何）；
  行内文本标注 `Measure`。
- 引擎把 `Items` 与每个已排列行元素的 `Bind`/`ActionBind`/`OptionsBind`/`VisibleKey`（作用域化后的 key）
  记入声明的 key→node 表，因此 `NotifyChanged` 只标记声明它的元素，Paint 类公告不移动排列时钟。

### 7.4 模板结构校验的边界（写清，不留给读者推断）

- **覆盖**：模板元素必须是已注册 kind 或容器；属性必须落在该 kind 的 schema（+ 全元素通用名）或容器词表内；
  未知 kind / 未知属性以 `UiContractException` 在**创建期**拒绝，消息含模板名与模板内路径
  （`Template 'row' is invalid at '<Templates>/row/flag': ...`）。这一层校验在 `UiLayoutEngine` 构造时执行，
  两个词表与 `UiHost` 私有词表的相等性由 lane 反射钉住（防止复制漂移）。
- **不覆盖**：`UiHost` 对页面元素执行的数值/窄态语法（`Width`/`Padding`/`Gap`/`Height`/`Breakpoint`/…）不在此层重复验证，
  原因是被校验的子树带着 item 作用域、且 `UiHost` 的词表是私有的。后果与"程序化构造的 spec"一致：模板内写坏的数值按
  引擎既有的"未声明/退化"路径处理，而不是创建期拒绝。这是**已知缺口**，不是"已覆盖"。
- 模板内元素的 `Validate`（绑定存在性）**不在创建期执行**：其 key 是 item 作用域的，创建期无法存在。缺失的 item 局部 key
  是 fail-soft：新控件（checkbox/progress/tree）用 `TryGet` 读，取默认值并记录一条去重报告后照常绘制；既有 atom
  （如 `text/wrapped`）保持自身契约（`Get` 抛出→由树的 recovery 记录一次恢复带，页面继续绘制）。
- 热重载：模板表随文件内容标识一起变化，重载候选的解析/预检路径与页面一致；模板结构错误在 commit 阶段抛出时由文档服务
  回滚整批并保留 last-known-good（`UiDocumentService` 既有规则），不是半批更新。

### 7.5 中立夹具页（库内示例，**不是**消费证据）

`tools/FerriteLib.UiKit.Tests/KernelFixturePageTests.cs` 是本轮的形态证据：一个数据驱动行集 + 三个新公共控件，
页面半边只有一份 XML 与键绑定，没有手写矩形、没有手动刷新、没有手写集合身份、没有手写输入规则。

- 该 lane 钉住的事实：行 band 高 = density 行高；行内控件点击写回的是该行自己的 key；模型变化（值）下一帧即被读取，
  而结构变化必须经由**一次** `NotifyChanged`；Paint 类公告复用同一排列快照；树按层级缩进并以被点击行的 key 回报。
- **证据等级**：这是本仓自己的库内示例（证据类 1/2），`AGENTS.md` "Our own demo is not consumption" 适用：
  它证明这些 kind 在桩度量下能画、能测、能恢复，**不**抬高"已验证公共面"计数，也**不是**晋升门的 provenance。
### 7.6 修正说明（最终独立验证 F8）

§1 的示例曾写 `<Repeat Items="members" ItemKey="id" Template="member-row"/>`。**`ItemKey` 不是真实属性**：
落地形态的 `<Repeat>` 只有 `Items`（items 绑定键）与 `Template`（`<Templates>` 中的模板 Id），
行身份由**items 绑定所提供的 item 键**决定（身份形如 `<declaredId>#<itemKey>`）。写 `ItemKey=` 会在创建期被拒绝。
本节的示例已按真实形态修正，§1 的其余部分是设计草案，一律以 §7 为准。

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
        <Repeat Id="member-list" Items="members" ItemKey="id" Template="member-row" />
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

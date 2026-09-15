# 30 消费者团队接入说明（0.5.x）

本节面向消费者团队。**库侧提供接口与开发包；消费者的页面迁移与真实页面验收由消费者团队完成。**
库内示例与夹具不构成消费者验证。

## 1. 版本区间

| 项 | 值 |
| --- | --- |
| 本轮契约轴 | `0.5.0`（`FerriteLibVersion.Api`） |
| 建议消费者 pin | `[0.5.0, 0.6.0)` |
| 发布通道 | 消费者经已发布的 GitHub Release 资产集成；同目录兄弟文件夹的 `Private=false` 是开发便利，不是契约 |
| 单 DLL carrier | 只有本 Mod 交付 `FerriteLib.UiKit.dll`；消费者不得自带副本 |

游戏无法表达前置版本区间（`ModRequirement` 只解析 `packageId`/`alternativePackageIds`/`displayName`），
所以**每个消费者必须在自己构造函数里断言区间**：

```csharp
FerriteLibVersion.Require(new Version(0, 5, 0), new Version(0, 6, 0), "your.packageId", out string diagnostic);
```

不通过时 `diagnostic` 会给出 `MISMATCH`、载入的 `Api` 与消费者编译时的下界——
这是可读的前置错误，而不是首次绘制时的 `TypeLoadException`。

## 2. 接入步骤

1. 引用 `1.6/Assemblies/FerriteLib.UiKit.dll`（`<Private>false</Private>`）。
2. `About.xml` 声明 `modDependencies` + `loadAfter`（版本区间只能在代码里断言）。
3. 构造函数里 `Require(...)`，失败则禁用自身 UI 并给出可读提示。
4. 页面：布局 XML + typed bindings；自定义组合件经 `UiWidgetRegistry.Register`。
5. 热重载：开发模式下由库的文档服务监听；生产默认关闭自动监听（可显式开启），手动重载始终可用。

## 3. 从旧线迁移（0.3/0.4 → 0.5）

按顺序检查，每条都对应 `docs/api-tiers.md` 的对应章节：

1. **session 状态按 node 键**（0.4 窗口内变更）：`GetScrollPosition`/`SetScrollPosition` 收 `UiNode`。
   用 `Session.GetNodeByElementId("your-id")` 做桥接。`TrippedNodes` 取代 `TrippedComponentIds`。
2. **窗口壳**：旧代码若自己管理"同类窗口去重"，迁移到 `UiWindowCatalog`（P1），否则同型多实例会被原版
   `RemoveWindowsOfType` 互相关闭。
3. **刷新**：若你在每次绘制里重建树或手动 bump 全局修订，迁移到按 key 通知（P2）。
   固定尺寸读数不应引起整页测量。
4. **禁用**：手写的"不可用就不画按钮"迁移到 `canExecute`（P2），禁用态由库统一处理，且不接收输入。
5. **集合**：手写的行集合迁移到 `Repeat`（P3）；提供稳定业务 key，否则排序会串状态。
6. **热重载**：内嵌 manifest 可继续使用；要获得"改文件即生效"，把布局/样式改为外部文件并交给文档服务（P4）。

## 4. 已知限制与边界（诚实清单）

- **C# kind 不热重载。** kind 是编译期词汇；改 kind 行为必须重新编译并由消费者重新发布。
- **原版职责不重复。** 跨窗口调度、层级、焦点、生命周期归 `Verse.Window`/`WindowStack`；
  库只补实例身份、页面生命周期、动态结构、绑定通知与诊断隔离。
- **暂停/相机是全局语义。** `WindowsForcePause`/`WindowsPreventCameraMotion` 对**全部**窗口取任一为真，
  不是窗口局部开关；多窗组合必须由消费者按产品策略选择，库只暴露配置。
- **地图层渲染不在库内**（永久非目标）。
- **实机行为未由本仓证明。** 激活/层级/输入穿透/模态共存等组合行为是 IL 级事实推导 + 自动化断言，
  必须由消费者团队在真实游戏里验收（清单见 `../../in-game-walkthrough.md` 与 [40-verification.md](40-verification.md)）。

- **重载会重置文档可设置的主题令牌。** 热重载提交前会把注入 `UiTheme` 的、可由样式文档设置的令牌恢复为 host 构造时的值，否则被删除的覆盖会继续生效。**后果**：若消费者在 host 构造之后重新调过这些令牌之一，下一次重载提交会丢掉这次调整。要保留就请在重载后重新应用，或把该调整放进样式文档。此行为由 P4 的独立验证实测（`docs/development/0.5/verification/p4-adversarial.md`），尚未决定是否在 0.5 内修正。

- **同型多开需要显式选择。** `UiWindowOptions.AllowMultipleInstances = false` 走的是原版规则：`WindowStack.Add` 按**精确 C# 类型**（配合既有窗口的 `onlyOneOfTypeAllowed`）驱逐同型窗口。因此两个不同 window-kind 若共用同一个 `Window` 派生类，会互相关闭；要同型共存必须显式允许，或者让每个 kind 有自己的壳类型。`UiPageWindow` 作为通用壳时请特别注意这一点（它建议每个 kind 用不同的实例 key，而不是不同的 C# 类型）。
- **`UiWindowOptions.NormalSize` 是暂定语义。** 它是"在 `PreOpen` 时重新施加的静止尺寸"，目前**没有消费者证据**，是本轮最弱的一条 P1 声明；把它当成可用的默认值之前请先在自己的窗口上验证。

- **失效类别在"注册绑定"时声明，不能按元素覆盖。** `BindValue/BindReadOnly/BindOptions/BindAction/BindCommand` 上的 `UiInvalidation` 属于**该 key**，同一 key 的所有元素共用同一个类别，不声明即 `Everything`。一个页面若同一个 key 需要两种类别，请拆成两个 key。
- **`Paint` ≠ 元素级重测。** 排布缓存是整页的：`Paint` 复用已排布快照（不重排），`Measure`/`Structure` 触发整页重排。按 key 的是**通知的定向**，不是元素级增量测量。
- **在元素存在之前发出的通知不会补发。** `NotifyChanged` 对一个尚未排布过的 key 只记录、不生效；该元素首次排布时会直接读当前模型值，所以不会丢更新，但语义是"元素存在后再通知"。
- **自持 kind 里的裸 IMGUI 拖拽不受 `canExecute` 约束。** 漏斗的禁用守卫在 `UiNative` 的带 ctx 入口上；一个手写拖拽（如库内 `chart/line` 的做法）不走漏斗，因此禁用态不会阻止它的拖拽。公共拖拽型控件应走库入口。
- **`UiSession.ContentRevision` 是排布缓存时钟，不是失效 API。** 消费者用它做失效判断会失去按 key 的定向能力；请用 `NotifyChanged` + `UiInvalidation`。

- **一个无法解析的「页面级」默认值，在【重载】时会拒绝整份候选版本。** 候选样式文档的 `DefaultScheme`/`DefaultDensity` 必须在该文档自身声明过
  （`SchemeNames`/`DensityNames`）；否则该**版本被拒绝**，last-known-good 继续生效，失败按版本去重报告。
  **首次加载不走这条规则**（实测）：结构合法但页面级名字无法解析的样式文件在首次加载时会被**直接采纳**，页面级名字静默不生效，
  文档其余部分照常生效。同一个文件因此在「首次加载」与「重载」下语义不同——这不是笔误，是本轮已登记的剩余关闭项。
  这是本库失败阶梯的一处**刻意收窄**：逐元素的未知 `Tone`/`Emphasis` 仍然是 fail-soft（回退到默认表现并记录），
  但页面级默认值的作用域是**整页**，fail-soft 会让页面以"作者没有写过的外观"提交成功。
  **代价要写清**：该文档里其余合法的 scheme/density 在该版本里**也不会生效**，改对拼写后一起生效。
  如果你只想要"这一个名字无效、其余照常生效"，请在该文档里声明这个名字，或不要把它放在页面级默认值上。
- **一旦上一份生效的文档声明过页面级 Scheme/Density，切到任何其他样式文档都会把"文档可设置的主题令牌"整体恢复为 host 构造时的值**
  ——不只恢复那份页面级默认值真正声明过的令牌。因此：host 构造之后对主题做的重新着色（包括任何文档都没声明过的令牌），
  在这次重载中会被丢弃；而当前文档没有声明页面级默认值时不会发生这种重置。
  **恢复方式**：重载后重新施加该着色，或把该值写进文档而不是直接改主题。
  （该行为由 P4 独立验证实测并有 lane 固定；未在 0.5 内做逐令牌收窄，因为令牌到主题属性的映射目前只在 `UiStyleResolver` 内部存在一份。）

- **禁用守卫管的是「交互」，不是「外观」。** `UiNative` 没有主题，所以命令绑定为不可执行状态的元素，其**禁用外观**仍由该 kind 自己绘制——与 `input/button` 的做法一致。库负责的是：不执行、不接收输入、不捕获指针/hot control、不给焦点。
- **`UiNative` 的禁用守卫覆盖哪些入口。** `Button(rect, ctx)`、`DropdownButton`、`Slider`、`NumberField`（后两者通过 `elementId` 解析节点身份；解析不到节点时保持原有行为，即「任意 state key」用法不受影响）。
  **`TextField(Rect, string)` 是例外**：它既没有 session 也没有 key，无法查询禁用态——这是已登记的空缺；需要禁用文本输入时请用带 session 的形式或自持 kind。

## 5. 交接节奏

| 阶段 | 交付物 | 消费者可开始做什么 |
| --- | --- | --- |
| 第一个开发包 | P1+P2+P4 合并后的 `dist/dev/FerriteLib` + 本文档 | 多窗口壳、通知/命令态、外部 XML 热重载接入 |
| 第二个开发包 | 加上 P3+P5 | 集合与公共控件迁移、多窗诊断接入 |
| 最终 | `40-verification.md` 的证据表 + 实机清单 | 真实页面验收与反馈回交 |

消费者发现的缺口：**在消费者树内先组合实现**，把 `owner/repo@sha:path:line` 证据回交本仓，
由本团队评估是否晋升为公共能力；**跨仓修改由所属团队完成**，本团队不代改消费者。
## 6. 当前可用面（第一个开发包）

| 能力 | 状态 | 消费者现在能做什么 |
| --- | --- | --- |
| keyed 窗口实例 + 活动目标 + 暂停/相机策略 | 已实现 + 自动化验证（7 处突变） | 每个 kind 按 `UiWindowKey` 打开；同 key 重开即激活；同型多开需显式允许 |
| 通用 XML 页壳（无需 C# Window 子类） | 已实现 + 自动化验证 | 普通页面用 `UiPageWindow` 直接承载 |
| 按 key 通知 + 失效分类 + 命令可执行态 + 条件显隐 | 已实现 + 自动化验证（6 处突变） | 模型变更调 `NotifyChanged`；禁用交给 `canExecute`；显隐用 `Visible`/`VisibleKey` |
| 外部布局/样式文件 + 自动/手动热重载 + LKG | 已实现 + 自动化验证（P4 三处 + P4b 四处突变） | 把 XML 交给 `UiDocumentService`；开发模式自动监听 |
| keyed repeater、checkbox、进度条、树 | **尚不可用**（P3 开发中） | 暂用现有 atom/自持 kind 组合 |
| 多 host/session 诊断隔离 | **尚不可用**（P5 开发中） | 暂用现有 `UiFitAudit` |

**接口冻结程度**：以上均为 `0.5.0` 窗口内的**公共面**，其中 P1/P2 新增类型目前按 `public-unstable` 登记
（见 `docs/api-tiers.md`）。区间 `[0.5.0,0.6.0)` 承诺的是"签名在区间内不删不改"，不是"形状已定稿"。
消费者开始接入是安全的；把“已接入”写进验收结论要等**消费者自己的树**真的编译并运行。
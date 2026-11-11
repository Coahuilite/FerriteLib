# Design：按事件分层执行

## Context

一个数字键在 1.6 上依次产生 KeyDown、字符 KeyDown、KeyUp，Unity 在每个之前补一个 Layout；加上帧末的 Layout 和 Repaint，一帧里有 `2 + 2 × 输入事件` 个 pass。原生 1.6 实测在 1050 个打字帧上全部满足这个关系。每个 pass 都执行完整的 `DrawFrame`：`MeasureAndArrange` 走 `TryIncremental`，对 retained entry 逐项建立上下文，校验 LayoutStamp，验证叶测量并捕获字体环境；`Draw` 先 `CollectPointerLayers`，再 `DrawEntries`，两次遍历都要解析上下文与禁用状态，然后调度每个 widget。

**IMGUI 实际要求的：** 同一帧里各事件的控件调用顺序必须一致（control ID、焦点、hotControl、原生编辑框），可交互控件必须在每个事件上运行，像素只在 Repaint 产生。其余的都是内核自己的安排。原版文本框在同样的事件流下保持 60 FPS，是这个判断的对照。

## Decisions

### D1 周期边界只由 Repaint 推出（用户决定）
`UiFrame.Phase` 已经携带事件阶段。周期在 Repaint pass 结束时关闭，下一个 pass 开启新周期。不读 `Time.frameCount`，不改 containment 白名单，也不新增公开 C# 签名。一个 Host 收不到 Repaint 时周期不前进，此时已宣告变化仍会触发同步，而隐藏窗口本来就不产生像素。

### D2 三层的归属
| 工作 | 层 | 理由 |
| --- | --- | --- |
| `DismissPopupOnOutsidePress`、`TakePopupWheelIfCovering`、`BeginHitPass`／`EndHitPass`、`PushHitLayer` | T1 | 命中层按 pass 交接，弹层与滚轮按事件消费 |
| `ResolveDisabled`、Cancel 发布、HelpRef 声明 | T1 | 资格是即时的（FL08-U4）；帮助按 pass 计（FL08-HELP-GRACE） |
| widget operate、原生编辑、请求、capture | T1 | control ID 一致性与输入合同 |
| `TryIncremental`、`PipelineContext`、环境捕获 | T2 | FL08-U3 只要求合法呈现机会；RU2-B 的旧 Measure 在合法同步时验证 |
| `CollectPointerLayers`（内容命中几何） | T2 | 几何只随同步改变；同一 pass 内的滚动仍立即重新收集（现有行为） |
| 内置 `text/wrapped`、`input/button`、`input/checkbox` 的展示段，以及 fit | T3 | 只有 Repaint 产生像素；先例是 0.8.x 已采用的离屏延期 |

`DrawContextForEntry` 的结果在 T2 同步点之间复用；它已经有基于修订号的缓存，这一层只是让它在同一周期里不必重新判定。

### D3 已宣告变化立即同步
同步点由“周期首个 pass、Repaint、已宣告变化后的下一个 pass”构成。已宣告变化的列表就是现有 `TryIncremental` 拒绝复用时检查的那些条件，加上本周期内完成的业务请求。因此 F07 的 Live 改值链（Set → Bump → Invalidate）在下一个 pass 就重新同步，即时性与现在相同。

### D4 不读普通 getter 的展示默认省略（深模块）
判定由库对内置 wrapper 逐一做出，不交给作者：静态 TextKey 或字面文本、tracked 展示值、按钮与复选框的纯绘制部分，不调用普通 getter，在非 Repaint pass 上省略不会改变任何作者可观察的结果；剩下的差别只是诊断出现的时点，这属于观察边界。前提是后端声明展示隔离能力（FL16-DISPLAY-ISOLATION），用来消除共享 GUIStyle 的副作用。

这与此前被撤回的“全局非 Repaint 文本早退”不同：那次没有区分普通读取，也没有隔离能力，存在共享样式与故障时点的反例。本次范围只限不读普通 getter 的展示段，operate 保留。翻译接口 `IUiTranslation.Translate` 的调用次数会减少；该接口被要求对同一 key 稳定返回，作者不应依赖调用次数，这一点写入 consumer guide。

### D5 版本化默认值，而不是弃用周期
FL08-COMPAT 保护消费者声明范围内的 XML 含义与合法条件。如果在 0.9 直接把所有页面的默认改成分层，就会改变 Schema 2 页面的普通 getter 执行条件，属于破坏性变更，需要先弃用并再保留两个 minor。

改用 Schema 区分：Schema 2 页面的含义冻结，0.8.x 的写法在 0.9 原样成立；Schema 3 页面从一开始就以分层为默认。新作者不需要了解这一层，旧作者不受影响，这正是深模块“接口小、默认对”的要求，同时验证了兼容线的能力。代价是解析器要接受两个 Schema 值，并在文档中说明两者的唯一差别。

被否决的方案：
- 按消费者 `Require` 范围推断默认值：隐式，且同一 Host 的身份不一定能追到消费者。
- 只靠 `TieredEvents` 默认关闭：作者需要了解事件模型才能拿到收益，不符合深模块。

### D6 `DeferClippedDisplay` 并入
在分层页面上，完全裁剪的展示在 Repaint 也延期，相当于所有 Overlay 默认启用；显式 `false` 仍可关闭子树。Schema 2 且未启用分层的页面保持 0.8.x 语义。这个属性不弃用；以后如果要弃用，走 FL08-COMPAT 的流程。

### D7 自定义 widget
自定义 widget 在任何页面上都保持逐 pass Draw，与现在相同。0.8.x 的延期显示能力接口（`IUiDeferredDisplay`、`IUiIsolatedPaint`）是 internal，外部作者无法实现。本变更只在库内部使用它们，不公开。把 T3 开放给自定义 widget 需要新增公开能力和 API tier 决定，属于后续变更，不在本次范围。

## Counterexamples and how they hold

- **Cancel 阶梯**（`KernelCancelLadderTests` 中隐藏已获取的 subject）：隐藏前先 NotifyChanged，是已宣告变化，下一个 pass 同步，CancelTargetNode 按原时序清除。
- **原生 control ID 与 IME：** T1 不变，原生编辑调用照常每 pass 执行。
- **帮助 grace：** 按 pass 计数不变。
- **retained fault paint：** T3 的省略不被当作成功，fault 与最后成功的展示都保留。
- **BeforeDraw 每 pass 同步：** 在 DrawFrame 之外，不受影响。

## Risks / Trade-offs

- **周期内静默的普通几何变化：** 输入分派最多使用到本周期的同步结果。现有合同下分派本来就读取上一个 pass 的命中层；像素在 Repaint 时是新的。
- **普通 getter 调用次数下降：** 对有副作用的 getter 可以观察到，所以放在执行方式里，Schema 2 默认不变。
- **多个 Host 各自维护周期：** 不共享，也不代替彼此确认（FL08-ORDINARY）。
- **预期收益（推断）：** 非 Repaint pass 只剩 T1。实测稳定页上 Arrange 约 0.65 ms，`CollectPointerLayers` 本体约 0.27 ms，加上展示段，非 Repaint pass 预计降到零点几毫秒，断崖从约 90 键/s 提高数倍，分配与 GC 频率随之下降。以原生扫描为准。

## 0.8.x 兼容线

0.8.x 只回补两项：FL09-TIER-SAFE 的无感默认，以及 Schema 2 上显式写 `TieredEvents="true"`。0.8.x 不接受 Schema 3；0.8.x 已有的 `DeferClippedDisplay` 保持原样。这样在 0.8.x 上编写的 manifest 在 0.9 中含义相同。

回补需要单独授权；授权后在 0.8.x 的 `docs/openspec/changes/` 下提交对应的 ADDED，现行规则引用本变更，不复制两份正文。US 0.6.x 在其 Schema 2 manifest 根上声明 `TieredEvents="true"`。

## Verification

- 用户已授权的新测试，只覆盖本变更的新语义：周期边界、T1／T2／T3 分界、已宣告变化的重新同步、Repaint 鲜读、Schema 2 与 3 的默认值、`TieredEvents`／`DeferClippedDisplay` 的兼容，以及隔离能力缺失时的回退。
- 现有相关套件与 `--readonly-api` 照常运行。
- 原生扫描：稳定页 30/65/85/110 键/s、改值成对 15 键/s、静置 GC；对比逐 pass 与分层执行，并做用户实体键盘复测。文档场景不能代替这些检查。

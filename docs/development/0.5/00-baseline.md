# 00 基线、取代关系与 API 窗口

日期：2026-09-15 起。分支 `0.5.x`。

## 1. 基线（本轮开始时的树是什么）

| 项 | 值 | 来源 |
| --- | --- | --- |
| 基线 revision | `c53bd37` | `git rev-parse 0.5.x`；`0.4.x` 与 `origin/0.4.x` 同点 |
| 契约轴 `FerriteLibVersion.Api` | `0.4.0` | `Source/FerriteLib.UiKit/Kernel/FerriteLibVersion.cs` |
| 已发布 | `v0.4.0-rc1`（prerelease） | `MEMORY.md` 发布状态；`/releases/latest` 仍 404 |
| 门禁 | `scripts/verify-local.ps1` 10 项，基线全绿（含 restore setup） | 本轮复跑，见 [40-verification.md](40-verification.md) |
| harness | `tools/FerriteLib.UiKit.Tests`，Release，`ALL PASS` | 同上 |

三条版本轴（契约 `Api` / 发布 `About/About.xml <modVersion>` / 构建 `csproj <VersionPrefix>`）由 harness
同一条断言钉住，本轮一起移动到 0.5.0。

## 2. 已实现 vs 计划：先纠正四处过时说法

**不要沿用下列旧口径。** 每一条都写清旧说法、新事实、证据。

### 2.1 "FL 尚无 node / hit stack" —— 旧说法，node 层已落地

- **新事实**：元素身份层已在 0.4.x 落地：`UiNodeId`（`Key` 规范身份 + `Path` 展示路径，U+001F/U+001E 分隔符使身份单射）、
  `UiNode`（身份 + 每元素命名状态槽 + `IsDirty` + 子节点链）、`UiSession` 以 node 为键持有状态
  （`GetNode`/`GetOrCreateNode`/`GetNodeByElementId`/`SetScrollPosition(UiNode,…)`/`TrippedNodes`）。
- **hit stack 是"有第一层，不是没有"**：`UiHitLayer`（元素 + Host 空间矩形 + 是否弹层）存在，
  `UiSession.BeginHitPass/PushHitLayer/IsPointerOverHigherLayer` 已能把"最上层不接收点击"这条规则集中表达；
  `UiPopup.RectFor` 是唯一的弹层矩形规则。
- **仍然欠的**：内容层之间不互相裁决，`UiNative.YieldsToCoveringPopup` 仍是下拉触发器的私有分支；
  完整 z 序调度（topmost-first dispatch + 元素级 yield 全部来自栈）是本轮 **P2/P3** 的目标。
- 证据：`Source/FerriteLib.UiKit/Kernel/UiHitLayer.cs`、`UiNode.cs`、`UiNodeId.cs`、`UiSession.cs`；
  退役条件写在 `docs/api-tiers.md` 的 `UiHitLayer` 条目。

### 2.2 "仅重开生效就是完整热重载目标" —— 被取代

- 旧口径：布局/样式文件改动"重开窗口后生效"即达标，运行中不更新。
- **本轮取代**：开发模式下保存 XML 后，**已打开且受影响的窗口**必须更新；同时提供手动重载与 last-known-good 回退。
  C# kind 的增删改**明确不在**热重载范围（kind 是编译期词汇）。见 [20-api-and-xml.md](20-api-and-xml.md) §热重载。
- 依据：主计划的"热重载合同"（本轮任务规划 §4），以及 FL 自身"文件来源 + 一个解析器 + 一个校验入口"的既有裁定。

### 2.3 "0.5 只围绕 18 个消费者 kind / 第二消费者不排期" —— 被取代

- 本轮 0.5 的目标是**公共基础能力 + 多窗口**；消费者自持 kind 的"清零"不是目标。
  专用组合件允许保留，但其身份、状态、失效、恢复、诊断必须走库公共机制。
- 因此本轮的"完成判据"不是 kind 数量，而是：普通页面不再手写矩形、刷新、集合身份与输入规则。

### 2.4 生态协议的"消费者先手写、一版后晋升" —— 适用范围收窄（不是废除）

- 旧规则：库只吸收"消费者已手写且形状通用"的能力，并要求满一版才晋升。
- **取代关系**：`AGENTS.md` "What earns a kind" 的**晋升门**仍然约束**新增专门 kind**（provenance 可引用、
  中立、无新进程级可变静态、有 harness lane）。
  但**通用公共基础能力**（多窗口壳、绑定通知、集合协调、文档热重载、诊断隔离等）由已确认的真实需求**直接驱动实现**，
  不再要求消费者先重复造轮子。区分标准：该能力是否有"页面无法表达的通用语义"，
  而不是"是否已有人在别处手写过"。
- 本条的适用区别同步进 `AGENTS.md`；不继续用旧规则阻断已确认的公共基础能力。

## 3. 原版边界（已核验事实 + 本轮决策）

事实来源：主计划 §1 对本机 `Assembly-CSharp.dll` 的 `Verse.Window` / `Verse.WindowStack` 反编译
（ilspycmd，退出 0，DLL SHA-256 `5CF1B5BE…356A`）。**这是 IL 级事实，不是实机行为**。

| 能力 | 原版事实 | FL 决策 |
| --- | --- | --- |
| 绘制与层级 | `Window.WindowOnGUI` 用 `GUI.Window`；`WindowStack` 按 layer 排序 | 继续合成原版，不建第二套跨窗 z-order |
| 打开/关闭 | `Add` → `PreOpen/PostOpen`；`TryRemove` → `OnCloseRequest/PreClose/PostClose` | 库在这些钩子挂注册/订阅/释放；保留"可拒绝关闭"语义 |
| 同类窗口 | `Add` 调 `RemoveWindowsOfType`，按既有窗口的 `onlyOneOfTypeAllowed` + 精确 C# 类型判断 | 通用壳必须**允许同型多实例**，再由库按 `consumer + window-kind + context-key` 去重 |
| 焦点与激活 | `Notify_ManuallySetFocus` 设焦点但不改列表层级；点击路径会重排 | 不把"设焦点"当"带到前台"；激活组合需实机验证 |
| 输入与取消 | `GetsInput` 受上层 `absorbInputAroundWindow` 影响 | 跨窗遵守原版；库只补窗口内节点命中、弹层、控件焦点与捕获清理 |
| 暂停/相机 | `WindowsForcePause`/`WindowsPreventCameraMotion` 对**全部**窗口取任一为真 | 暂停是独立策略，库只提供配置，不替消费者定产品默认值 |
| 位置/拖拽/缩放 | `windowRect`/`draggable`/`resizeable`/`WindowResizer` 已有 | 优先复用；标题栏限定等产品行为由消费者选 |
| Dialog 打开副作用 | `PreOpen` 在 Dialog 层通知选择器并清理相关状态 | 消费者层级与地图选取模式必须自行先原型确认 |

原版没有提供 XML 页面、集合协调与绑定依赖机制——这三块是 FL 的职责，也是本轮 P2/P3/P4 的主体。

## 4. API 窗口与兼容策略

- 契约轴本轮 = **0.5.0**。pre-1.0 下 minor 即破坏信号；**任何公共面新增也升 minor**。
- 消费者编译并钉住区间，例如 `[0.5.0,0.6.0)`，并在自身构造函数里断言（游戏无法表达前置版本区间）。
- 词汇退役规则不变：弃用属性**接受并重定向**，至少保留一个 minor，只在 minor 边界移除。
- 0.5 窗口内计划中的破坏性变更集中为**一次** minor（不是每项一次），清单见
  [30-consumer-handoff.md](30-consumer-handoff.md) §迁移。
- 单 DLL carrier 不变：只有本 Mod 交付 `FerriteLib.UiKit.dll`；`Require` 的 `DUPLICATE CARRIER` 是唯一检测器。
- 本仓文档不得出现消费者业务词汇与个人绝对路径；外部需求证据按 `MEMORY.md` 转录规则落账。

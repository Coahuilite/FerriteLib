# FL09 按事件分层执行：候选变更

状态：**候选，2026-11-11 起草，待用户采用。** 用户已决定方向与以下取舍：页面 manifest 根属性为开关；展示周期只由 Repaint 推出；本变更的新语义可加入新测试；US 随变更启用；库默认处理，作者仅在需要时改动（深模块）；新合同写入 0.9.x，0.8.x 作为兼容线。采用前，本目录不是现行规格，也不授权实施、构建或运行。

## Why

已证事实（1.6 原生，私有证据 `opus-20261111`，不随仓库分发）：

- 每个 IMGUI 事件完整执行一次 `UiHost.DrawFrame`：整页布局验证，加上整页绘制与调度遍历。Unity 在每个输入事件前补一次 Layout，一个数字键（KeyDown、字符、KeyUp）因此对应 6 个 Host pass。
- 稳定页面上，Host 每个 pass 约 1.7–2 ms（Arrange 约 0.65、Draw 约 1.1），以 4 KiB 块计至少分配约 0.2 MB。在 30 到 110 键/s 之间，单个 pass 的成本基本不变。
- Unity 在一帧内处理完全部排队事件，形成反馈断崖。当时载体在 75–85 键/s 出现断崖，110 键/s 时 2.8 FPS、积压 204 键、尾部 2.0 s。同一 sender 下原版文本框保持 60 FPS。
- 用户实体键盘复测，最高 19.8 键/s，中英文输入法均测：107–110 FPS，无积压。剩下可感的顿挫来自分配引起的 GC：打字时 GC 频率从每分钟 12.9 次升到 18.2 次。

IMGUI 只规定事件数量，以及各事件的控件调用顺序。每个事件都付出整页成本，是 FL 内核的执行安排。规格只要求在“合法呈现机会”同步（FL08-U3），没有要求每个 IMGUI pass 都重新验证与重做展示。

## What Changes

- **ADDED 分层执行合同**（[presentation-execution](specs/contracts/presentation-execution/spec.md)）：
  - 以 Repaint 为界划分展示周期；
  - 每个 pass 分为三层：T1 每事件、T2 每同步点、T3 仅 Repaint；
  - 已宣告的变化在下一个 pass 立即重新同步；
  - Repaint 总是鲜读、鲜验证，玩家看到的像素与逐 pass 执行等价。
- **ADDED 无感默认子集：** 不读取普通 getter 的展示工作，在非 Repaint 事件上一律由库省略，无需任何开关。
- **ADDED 离屏展示延期：** 把 0.8.x 已采用的 `DeferClippedDisplay` 带入 0.9，作为兼容 XML；分层页面自动包含它。
- **MODIFIED FL08-ORDINARY**（[binding-reactivity](specs/core/binding-reactivity/spec.md)）：分层执行生效时，普通展示读取在 Repaint 执行，普通布局输入在同步点执行，这是合法的执行条件；未生效的页面保持逐 pass 执行。
- **ADDED 版本化默认值**（[structure](specs/core/structure/spec.md)）：
  - 新增 `UiPage Schema="3"`，默认分层，可用 `TieredEvents="false"` 退出；
  - `Schema="2"` 保持逐 pass，可用 `TieredEvents="true"` 显式启用，与 0.8.x 同义。
- **ADDED 适配器能力**（[rimworld16](specs/adapters/rimworld16/spec.md)）：执行 owner 声明展示隔离能力；没有该能力的后端，T3 退回原有逐 pass 执行。

## Not changed

- 不绕开 IMGUI，不自绘，不接管输入。T1 内容保持逐事件执行：控件调用与原生 control ID 顺序、operate、请求、capture、焦点、IME、命中层、帮助声明（仍按 pass 计）、Cancel 发布、执行资格。
- 业务 Set／Invoke／请求、即时资格（FL08-IMMEDIATE-REQUEST、FL08-U4）、已宣告变化的同步时点、编辑草稿、保存防抖等消费者合同，均不改变。
- Schema 2 页面在不声明属性时，行为与 0.8.x 相同。FL08-COMPAT 不修改，由版本化默认值满足。
- 自定义 widget 和编辑器保持原有逐 pass Draw。延期显示能力在 0.8.x 是 internal；把它开放给外部作者属于后续变更。
- **改值仍走 full。** 改值不退回整页事务（F07）需要按原次序校验和 `us/mode-row` 迁移，另立变更。稳定状态零分配属于纯实现优化，不写 delta。
- 不改 1.7 adapter 的支持声明。不建新 scheduler、线程或业务时钟。GUI pass 和 Repaint 不成为业务时钟（FL08-U5）。

## Capabilities

- Modified capabilities：contracts/presentation-execution（ADDED）、core/binding-reactivity（MODIFIED FL08-ORDINARY）、core/structure（ADDED）、adapters/rimworld16（ADDED）。
- 只作引用、规则不变：core/layout（RU2-B／RU2-C）、contracts/help-output（FL08-HELP-GRACE）、core/interaction-editing、policies/public-authoring（FL08-COMPAT）。

## Impact

- **FL 实现：** Host 帧入口与周期状态、`TryIncremental` 的同步点复用、内容命中层复用、内置展示 wrapper 的 T3 拆分、manifest 解析（Schema 3 与 `TieredEvents`）、1.6 执行的展示隔离能力。只读 API 层级：Schema 和属性是 XML 词汇扩展，不新增 C# 公开签名；如确需新增，采用前另行列出。
- **测试：** 用户授权为本变更的新语义加入新测试，范围只限 T1／T2／T3 分界、周期边界、已宣告变化的重新同步、Repaint 鲜读、Schema 2 与 3 的默认值，以及 `TieredEvents`、`DeferClippedDisplay` 的兼容。其他工作的既有测试限制不变。
- **0.8.x 兼容线：** 只回补“无感默认子集 + Schema 2 显式 `TieredEvents="true"`”，见 design 的兼容一节。在 0.8.x 落地需要单独授权，并在 0.8.x 现行规格提交对应 ADDED。
- **US：** 0.6.x（FL 0.8.x）在 Schema 2 manifest 上声明 `TieredEvents="true"`；0.9 线迁到 Schema 3 时取得默认值。这属于 US 自己的变更。
- **证据：** 采用后以原生扫描（30–110 键/s、改值成对、静置）和用户实体键盘复测验收。文档场景不等于测试通过。

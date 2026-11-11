# FerriteLib 0.9.x 现行规格

0.9开发线采用[开发工具连接](openspec/specs/contracts/developer-tools/spec.md)：可选Tools、agent语义访问、同源headless、作者文档、prepare、动态布局输入和原window-cost接缝；签名与用法在[连接指南](development/0.9/tools.md)。正常单一载体含休眠工具能力，不要求第二份Dev库。既有Stable、部分采用、原观察和独立合法入口保持。

新原生开发统一面向 **RimWorld 1.7**，跟随当前命名空间／API，不锁小构建号；0.8／1.6另行维护。独立Tools承担脱机图形宿主、游戏内工具和虚拟舞台，FL只提供中立接缝。中立窗壳、选择注记控制、同源坐标命中及可选非提交预算已进入本线；完整窗的隔离Native17消费、Game17构建和未验按[实现记录](openspec/implementation.md#stage-evidence)分列。原page／provider／MeasureAndArrange及纯读条件保各自原适用范围；准确1.6、物理输入／IME与真实游戏宿主不由隔离舞台代签。
规格表达应有保证；源码是否全部满足、已知偏差及证据身份在[开发映射](openspec/implementation.md)。
[Trace／legacy-fit 两项新保证](openspec/specs/contracts/observation-boundary/spec.md)于2026-10-29获准采用，
[变更记录](openspec/changes/isolate-observation/proposal.md)保存采用来源；实现证据与固定产物仍分别记录，不替代原生接受。

2026-11-11采用的[事件分层变更](openspec/changes/fl09-event-tiering/proposal.md)已并入现行的展示执行、结构、绑定及保留1.6 adapter能力页；实现与验证进度由该change清单及开发映射分别记录。

## 当前保证与已采用目标分开阅读

2026-11-08的[基础能力change](openspec/changes/fl09-basic-ui-capabilities/proposal.md)已采用、未实施：
显式双轴Scroll、有限正比例缩放／平移画布与随能力交付的输入／取消；原生编辑先留视口外。
通用详情先复用inline，真正交互popup/modal、完整K1及OS桥为后续ADDED。新目标只在change持正文，不从采用推成当前API可用。
旧普通冗余／同值／离页键通知条件保持；当前依赖／提交／观察收敛的[R1修复](openspec/implementation.md#fl09-reliability)
已落源码／隔离构建／既有检查，并有原宽通知caller的准确新载体after-success，待CO／PM接受；旧现场具体pending键仍未唯一定位。
后续顺序见[0.9计划](development/0.9/extension-plan.md)，shader不进基础补齐。

## 组合图与唯一规范归属

    业务模型／独立服务（值、资格、接受、保存）
           ↓ 普通通知或显式 tracked 来源
    绑定与响应 ──[展示执行连接]──→ 同轮展示 → 测量／排列 → paint / hit
           ↑ 一次业务请求                ↑               ↓
           └──────── 交互／编辑 ← 有效几何／实际原生资格
    结构／稳定身份 ─→ 所有能力       属性来源 ─→ 测量输入与外观
                             UI寿命／恢复 ──[更新连接]──→ stage / seal
                             实际主题 ──[帮助连接]──→ 消费者面板
                             观察事实 ──[观察连接]──→ 可选诊断接收者
    共享 Verse adapter 源 → 显式 RimWorld17 目标：事件、窗口资格、文字、坐标与资源
    保留的 RimWorld16 合同／证据：仅按原1.6维护目标使用，不替代1.7接受

Structure／Layout／Semantics／Appearance 是四项平行职责，不是新建四棵树或四个 manager。
同一规则只有下面的一个正文归属；调用图、符号、证据和偏差不另立规则。

| 需要找到的保证 | 唯一正文 |
|---|---|
| 声明、成员、稳定项与 UI／业务身份 | [结构](openspec/specs/core/structure/spec.md) |
| 普通与 tracked、依赖、即时读、批次、Retry／Resume | [绑定与响应](openspec/specs/core/binding-reactivity/spec.md) |
| 当前资格、wheel、capture、请求、共享编辑、精确重叠 | [交互与编辑](openspec/specs/core/interaction-editing/spec.md) |
| 分配、测量、scroll、anchors、共享列、局部与增量发布／full 回退 | [布局](openspec/specs/core/layout/spec.md) |
| 逐属性继承／撤销、活视图、颜色／文字／图像／背景 | [外观](openspec/specs/core/appearance/spec.md) |
| hide／retarget／remove／Close、保留故障展示、逐项释放 | [寿命与恢复](openspec/specs/core/lifetime-recovery/spec.md) |
| bounded diagnostics、R9 facts／budgets、fit | [观察能力](openspec/specs/core/observation/spec.md) |
| 可选Tools、agent语义、同源headless、文档／prepare／cost与虚拟视口接缝 | [开发工具连接](openspec/specs/contracts/developer-tools/spec.md) |
| reader/owner → 一致展示 → 当前执行 | [展示执行连接](openspec/specs/contracts/presentation-execution/spec.md) |
| definition/style producer → prepare/stage/seal → 所有 Host | [更新连接](openspec/specs/contracts/definition-update/spec.md) |
| opaque topic／Session grace／owned endpoint → 消费者帮助 | [帮助连接](openspec/specs/contracts/help-output/spec.md) |
| callback 分类、实际结果权威、窄观察异常隔离 | [观察连接](openspec/specs/contracts/observation-boundary/spec.md) |
| 原生目标／logical UI、观看分离与借用执行 | [1.7目标与工具接缝](openspec/specs/contracts/developer-tools/spec.md)；[保留1.6 adapter合同](openspec/specs/adapters/rimworld16/spec.md)仅按原目标适用 |
| E1–E5／N1–N3／F1–F3、完整默认用途、K1延后、window默认 | [默认包装政策](openspec/specs/policies/default-wrappers/spec.md) |
| S1–S7、合法采用、版本／兼容、模块／新核心正向路线 | [公开作者政策](openspec/specs/policies/public-authoring/spec.md) |

公开等级唯一权威仍是 [API tiers](api-tiers.md)，签名／constants／optional defaults 仍在
[原位清单](development/0.8/stable-api.txt)。本次没有升降等级或提升 Session。
首次公开基线直接教当前用法；旧内部词汇迁移只作历史参照，不是新作者的升级前置步骤。

## 按读者任务进入

- **普通页面作者：** [settings recipe](consumers/ordinary-settings.md) → S1/S2/S6 → 默认包装。
- **自定义／部分采用：** S3/S4/S5 → 所选能力及其直接连接；本线native用[1.7连接／目标指南](development/0.9/tools.md)，保留1.6 adapter正文不自动承诺1.7行为。
- **修复或扩展维护者：** [来源覆盖](openspec/source-coverage.md) → 对应现行要求 →
  [实现与开放项](openspec/implementation.md) → [手工变更方法](openspec/README.md)。
- **窗口和实际 fit：** [窗口指南](consumers/rimworld-window-layout.md)；应用 breakpoint、筛选／分页和可读尺寸不变成 FL 默认。
- **接包与支持：** [0.9连接／目标指南](development/0.9/tools.md) → [构建／诊断](build-and-debug.md)。
  [0.8消费指南](consumers/consume-from-0.8.0-dev.md)与[0.8发布说明](release-notes/v0.8.0-rc2.md)保维护线身份，不是0.9安装或发布入口。

被替代正文保内容位于[归档](archive/specs-before-20261029/INDEX.md)，不是另一套 current。
仍有阅读价值的 counter 导读、真实接入证据和旧版本记录保原身份；旧人验不签当前新行为。

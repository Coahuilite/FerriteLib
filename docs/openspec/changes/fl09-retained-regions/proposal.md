# FL09 显式区域保留：候选变更

状态：**proposed，未采用、未实施**。SPEC 来源为 WO-FL09-RETAINED-DISPLAY-20261111 r1；
基准 `d07cdc04f9985bb6818a34f5f90ba20c345f0206`。本目录不覆盖 current；采用前产品工作停止。

## Why

F09 的瓶颈已经不只是布局计算。历史 RW1.6 A 展开稳态 Host P50 为 9.970 ms/帧，
Arrange／Draw 为 3.976／5.979 ms；另场归因仍有每帧 2 次增量同步、42 次 sparse guard。
C-r2 暖开把 Core／Plan／Visible 调用从 658／239／237 降到 26／15／9，但仍进入
658 次 MeasureElement，展开 Host P50 9.962 ms/帧、Draw 5.981 ms；135/135 完整帧超预算。
这支持进一步减少区域重复验证、派生和调度，不支持把已有叶缓存再命名后宣布完成。

viewport 场的 sparse Draw 42 次中40次不与实际 clip 相交；包围成本还含原生编辑、
草稿、帮助及绑定，不能跳过整个控件。完整表示也不等于业务 view：真实
TryCommitActionField 解析失败可改变 row-result 的文字／可见性／高度而不 bump view；
未提交稿也能改变错误标记与高度。

目标：作者明确选择后，FL 在区域边界核对完整表示及自身环境／状态，复用同一不可变
区域结果和调度片段，不逐项重复 Capture／Measure／布局派生。变化只重建相应表示分支并传播
所需几何；原生输入照常，可见内容在实际 Repaint 提交。整帧目标仍是 WO 的165 FPS／
6.060606 ms，合同不保证机器无关的 FPS 或全部 frame/buffer 都不画。

## Preferred direction

**新的显式表示端口 + 原 Host 内的区域事务 + 可选公开 participant。**
源 owner 每合法边界鲜取不可变表示；FL 不用旧 view/Bump token 猜新鲜度。
区域内只有明确映射的新表示消费才可跳过派生；普通读取／未知 widget 是保持原调用的洞。
自定义 sparse 可把共享 FieldLayout、静态文字与 pointer facts 交给新纯 Build，
Operate 保原 editor／endpoint，Paint 每可见 Repaint 消费同一结果。
这比整页可信 token 小，也比只公开 internal deferred-display 更完整。

## What changes / 拟采用到哪里

| current 能力 | 真正 delta | owner／公开接缝 |
| --- | --- | --- |
| core/binding-reactivity | MODIFIED FL08-ORDINARY；ADDED FL09-REGION-SOURCE | 明确新表示端口，不重注册／拦截旧 Get；源捕获负责完整业务表示、含失败结果 |
| core/layout | MODIFIED RU2-B、RU2-D、FL08-FULL-FALLBACK；ADDED FL09-REGION-UPDATE | 完整区域证明可代替逐叶验证；成员变化可在同一 journal 局部更新并传播 Scroll／共享依赖 |
| core/structure | ADDED FL09-REGION-ENROLL | Host 上显式 attachment；不改 Schema／TieredEvents 默认、不新建节点身份树 |
| contracts/presentation-execution | MODIFIED U3、SPLIT-PHASE、TIERED、TIER-SAFE、CLIPPED-DISPLAY；ADDED FL09-REGION-PARTICIPANT | 外部作者可采用纯 Build／Operate／Paint；源、几何、输入、真实提交分开 |
| core/lifetime-recovery | ADDED FL09-REGION-LIFETIME | 候选成功才发布；退订、失败、重入、关闭沿原 owner |
| adapters/rimworld16 | ADDED FL16-REGION-PAINT | 保留后端的实际 Repaint／样式隔离；不推断 GPU 保留或1.7能力 |
| contracts/developer-tools | MODIFIED DEV-04；ADDED DEV-09-REGION | 新纯表示可用于非提交预算；同源有界观察，不为查询构建或刷新 |
| policies/public-authoring | ADDED FL09-REGION-AUTHOR | 最小独立作者路线及 Public-unstable 建议；旧签名／调用／语义不退休 |

appearance、interaction-editing、help-output、observation 的现行状态／权威继续适用，
不以本 change 改 palette、编辑政策、help grace、请求接受或观察预算。
具体 API 草案、例子、拒收反例与输入责任表在 [design](design.md)；各 MODIFIED 保完整原要求／场景。

## Real caller → consumer

US 1.6 消费 owner 在独立0.9分支把真实 `b-actions`（Scroll 内的 Repeat）登记到新的表示源。
源从当前 BuildView 的实际检查、目录／policy、完整已发现 key 向量、展开状态和 rowResults
构造分项共享的不可变表示；FL 自持当前约束、属性和 editor 状态。
原 BindValue／命令、persist-first TryCommitActionField、独立公开 source 入口继续存在。
未映射的其他页、未知贡献源和未迁移 widget 沿原读取／Draw，不要求整页切 reactive。
标头／字段／footer key 不因屏外或折叠而被删减以缩小分母。

## Compatibility and decision

推荐本组**新增接缝 Public-unstable**：源分组、表示／操作分界与适配器消费需真实 F09 验证，
现在冻结 Stable 会把尚未验证的组合形状长期固定。仍提供完整可采用合同和明确迁移，
不把“实验”当作允许丢请求／改旧语义。若 PM 选择 Stable，须在采用时把完整依赖闭包一并
列入 API tiers／stable inventory，承担同目标的签名与行为保护；不能只稳定 Attach 名称。
本候选不修改当前 API 分类、版本轴、Stable 清单或任何已发布状态。

不实施0.8 A/B/C-r2移植，不退役旧 API，不增加“外部调用者必须 Notify/Bump”的条件。
同源中性目标和各 adapter 分别交付；RW1.6 是此次事实输入，1.7 未获本轮实机授权。

## Evidence and gate

私有来源以记录名引用，不分发绝对路径、日志、DLL 或私有数据：
`opus-20261111/f09-chain/NATIVE-A.md`、`NATIVE-C.md`、`VIEWPORT.md`、
`delivery/SCOPE-GATE.md`。源码核对：`UniversalSqueaker@1ee2ae0` 的
`UI/UsKernelSettingsSource.cs:94–115`、`UI/UsKernelSettingsHost.Behavior.cs:309–329`、
`UI/Kernel/UsSparseFieldWidget.cs` 及 `.Layout.cs`，相对根为 `Source/UniversalSqueaker/`。
新0.9必须用固定同代开／关或功能等价基线测量；上述历史A/C不是新基线。

PM 核定 delta 与 API 等级后，由原 owner 把所选条款采用到唯一 current，再开始产品。
本次只作文档／源码事实核对；不加测试／probe、不构建、不运行游戏。后续验证继续使用既有
检查和获授权的真实 UI 脚本；方案场景不表示执行通过。任务分期见 [tasks](tasks.md)。

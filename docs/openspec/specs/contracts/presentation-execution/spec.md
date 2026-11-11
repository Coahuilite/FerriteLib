# 模型输入到展示与执行的连接

## Purpose
明确 registry／reader → presentation → measure/layout → draw/hit → business endpoint 的双方义务。模型持即时值，Host 持一轮展示，layout 持有效几何；没有第二个业务快照权威或自动异步派发器。

## Requirements

### Requirement: FL08-BINDING-OWNER registry 与访问器连接一致
注册、NotifyChanged、sealed endpoint 和可选 tracked 能力 SHALL 归真实 registry；Host 仍通过 IUiBindingAccess reader 读／请求。forwarding reader SHALL 明确 BindingOwner 为当前 minting registry 并一致转发 Value/Choice endpoints；不做 reflection/type probing。缺 owner 或声明 endpoint 型别／writability 不符 SHALL 定位到 widget／root／container／materialized item。普通 reader 不因声明 owner 而被 enroll tracking。

#### Scenario: 合法代理在两轮间换 backing
- **WHEN** Host 验证新 backing 成功
- **THEN** 才换 per-view projection、失效相关输入／布局并释放旧需求
- **AND** retained endpoint 仍属于 minting registry，作者需从新 proxy 重新取得；换 backing 不自动等于新业务目标

#### Scenario: 新 backing 验证失败
- **WHEN** 新声明能力不匹配
- **THEN** 保留旧 backing 的有效投影和连接，不把候选泄漏给当前页

### Requirement: FL08-U3 同轮输入一致，下一合法点采用新值
Host SHALL 在合法呈现机会先同步自身需求，再测量／排列、绘制；该轮普通展示消费同一输入，包括稳定 idle 回调。默认包装持自己的完整 demand；自定义 tracked 消费者通过 schema roles 或可选 demand 明确实际 key，不 prepare-everything。处理中业务新值 SHALL 到下一合法点采用，不递归重排，不固定 tick、Repaint 或必然下一帧；hover／pressed／capture 即时反馈不一概冻结。

#### Scenario: 较早 Button 改变模型
- **WHEN** 同回调后续控件读取已同步展示输入
- **THEN** 它继续本轮一致输入，下一合法同步再采用新值
- **AND** 不因 stable fast path 跳过本轮必要需求准备

### Requirement: FL08-IMMEDIATE-REQUEST 业务执行暂离展示读视图
Set/Invoke/typed request 以及 CanExecute／执行资格 SHALL 读取当前业务值而非旧展示；成功或失败后都恢复展示 scope。资格读取本身 SHALL 不开关业务批次、drain 或发布新展示。直接 source／computed 读取仍是业务即时读。业务拒绝权不由缓存投影替代。

#### Scenario: 当前资格谓词调用 bindings.Get
- **WHEN** 模型已变而展示尚未同步
- **THEN** 谓词看最新业务值并决定当前资格
- **AND** 随后普通展示仍用原本完整轮次输入

### Requirement: FL08-PRESENTATION-VALIDITY 当前无效不能冒称旧成功
presentation SHALL 分 key 保留 current validity、Fault 与可选 last value，独立完整输入可继续更新。当前失败 Get SHALL 抛，只有专用 display 路径可消费 retained paint。必要 layout 输入失败 SHALL 进入完整几何回退／暂停输入，不以旧宽度或空 items 当新成功。显式 Retry 只重试相关纯工作，不重做业务。

#### Scenario: 一项展示失败，另一项完整成功
- **WHEN** 同步得到这两种结果
- **THEN** 分别报告其状态，不让局部成功伪造全页有效
- **AND** 可靠独立消费可继续，不发布不一致共享几何

### Requirement: FL08-SPLIT-PHASE 手动分阶段须配对
合法手动 MeasureAndArrange 与 Draw SHALL 消费同一对应结果，中间不插另一轮 arrangement；frame 和 execution 显式供给。DrawFrame SHALL 自持一次 frame、measure、draw、hit、popup 及失败清理，不要求普通作者重写 Begin/End。回调 Close 后停止剩余工作。

#### Scenario: 嵌入已有原生窗口
- **WHEN** owner 将实际 content rect、一次捕获的 frame 与 execution 交给 Host
- **THEN** Host 完成同一连接管线，native owner 仍持资格和最终 execution 释放

### Requirement: FL09-TIERED 分层执行按展示周期同步、按事件交互
分层执行生效的 Host SHALL 以 Repaint 为界划分展示周期：从上一个 Repaint pass 结束起，到下一个 Repaint pass 结束为一个周期。周期边界只由该 Host 收到的事件阶段推出，不依赖额外的帧时钟。周期内每个 pass 分三层执行：

- **T1，每 pass：** 控件调用及原生 control ID 顺序、可交互部件的 operate、请求、capture、焦点／编辑／IME、Cancel 发布、帮助声明（仍按 pass 计）、命中层发布、执行资格，SHALL 与逐 pass 执行相同。
- **T2，每同步点：** 布局验证、上下文与主题解析、内容命中几何 SHALL 只在同步点执行。同步点为：周期的首个 pass、每个 Repaint pass、出现已宣告变化之后的下一个 pass。非同步点的 pass SHALL 使用本周期最近一次成功同步的结果。
- **T3，仅 Repaint：** 内置展示 wrapper 的纯展示部分，即普通展示读取、格式化、fit 审计与绘制，SHALL 只在 Repaint pass 执行。

已宣告变化包括：
- NotifyChanged、Host 失效（ContentRevision）、节点 MarkDirty、待处理的输入验证；
- 定义、翻译、主题布局或属性修订的改变；
- 可用尺寸或 viewport 的改变；
- bindings、metrics、theme、translation 身份的改变；
- 本周期内业务请求完成。

Repaint pass SHALL 先同步再绘制，所以逐 pass 执行与分层执行产生同一像素。T3 的省略不是成功绘制，SHALL 不清除 presentation fault，也不替换最后成功的 retained paint。分层执行不改变 FL08-U3 的同轮一致输入，也不让 GUI pass 或 Repaint 成为业务时钟（FL08-U5）。

#### Scenario: 同一帧内多个按键，模型未宣告变化
- **GIVEN** 分层执行生效，页面稳定
- **WHEN** 一帧内依次到达 Layout、KeyDown、Layout、Character、Layout、KeyUp、Layout、Repaint
- **THEN** 首个 pass 与 Repaint 执行 T2 同步，其余 pass 复用本周期的同步结果
- **AND** 每个 pass 的控件调用、control ID 顺序、焦点与命中层发布与逐 pass 执行相同
- **AND** 只有 Repaint 执行内置展示 wrapper 的普通展示读取与绘制

#### Scenario: Live 编辑请求在周期中途改变模型
- **WHEN** KeyDown 中原生编辑产生 Live 请求，消费者随后使 Host 失效
- **THEN** 下一个 pass 是同步点，按新输入验证布局
- **AND** 本周期后续事件与 Repaint 使用新同步结果，不等到下一个周期

#### Scenario: 普通 getter 在周期中途静默改变
- **WHEN** 一个未登记通知的普通值在同一帧的非 Repaint 事件之间改变
- **THEN** 本周期剩余的非 Repaint pass 可以继续使用已同步的几何
- **AND** Repaint 同步时读取当前值并按其绘制
- **AND** 该变化在被同步点读到之前，不被报告为已采用

#### Scenario: Host 长时间收不到 Repaint
- **WHEN** 窗口不显示，只收到非 Repaint 事件
- **THEN** 周期不前进，已宣告变化仍在下一个 pass 同步
- **AND** 复用结果不被当作成功展示，再次显示时第一个 Repaint 鲜读

### Requirement: FL09-TIER-SAFE 不读普通 getter 的展示默认只在 Repaint
不论页面是否启用分层执行，内置展示 wrapper 中不调用普通 getter 的展示工作 SHALL 在声明了展示隔离能力的执行后端上只在 Repaint 执行。这类工作包括：静态 TextKey 或字面文本、tracked 展示值、按钮与复选框的纯绘制部分。operate、控件调用、请求与资格 SHALL 仍然逐 pass 执行。这些展示的 fit 结论与样式回退诊断在 Repaint 报告。缺少该能力的后端、自定义 widget 与编辑器 SHALL 保持原逐 pass 执行。这个默认不需要作者声明，也不改变任何普通 getter 的调用。

#### Scenario: Layout 事件到达只有静态标签和按钮的页面
- **WHEN** 非 Repaint pass 执行
- **THEN** 不为静态标签量字或绘制，按钮仍执行 operate 与控件调用
- **AND** 随后的 Repaint 产生与原行为相同的像素

#### Scenario: 后端不声明展示隔离
- **WHEN** 执行 owner 没有该能力
- **THEN** 所有展示保持原逐 pass 执行，不降低结果

### Requirement: FL09-CLIPPED-DISPLAY 完全裁剪的展示可以延期
`Overlay` 上的 `DeferClippedDisplay` SHALL 默认关闭，只接受 `true`／`false`，只在 `Overlay` 上合法。后代 Overlay 省略时继承；显式 `false` 对该子树关闭，更深的显式 `true` 可以重新开启。它不改变排列、裁剪、命中层或行的存在。只有当有效 clip 完全为空时，内置 `text/wrapped`、`input/button`、`input/checkbox` 才可以把纯展示的普通读取、格式化、fit 与绘制延期；内容变为可见时，该 pass SHALL 读取当前展示值，即使普通 getter 没有发出通知。输入与业务操作，包括控件身份、资格、请求、capture、取消，SHALL 仍按原顺序逐 pass 执行。延期不是成功绘制：不清除 presentation fault，也不替换最后成功的 retained paint。不具备展示隔离能力的后端、未选择的 Overlay、自定义 widget 和编辑器保持原 Draw。分层执行生效的页面，SHALL 视为所有 Overlay 都已启用该延期，除非子树显式写 `false`。

本要求把 0.8.x 已采用的同名 XML 词汇及语义带入 0.9。以 0.8.x 编写的 manifest 在 0.9 中含义不变。

#### Scenario: 行滚出视口后又露出
- **WHEN** 行所在 Overlay 的有效 clip 先为空，后在一次滚动后可见
- **THEN** 不可见期间只执行输入与业务部分；可见后的首个 pass 读取当前值并绘制
- **AND** 不可见期间累积的展示错误在可见时按当前状态报告

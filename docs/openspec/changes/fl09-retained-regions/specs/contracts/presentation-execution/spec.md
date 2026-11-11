# Spec Delta — 候选，未采用

基准：d07cdc04f9985bb6818a34f5f90ba20c345f0206 的唯一 current。MODIFIED 保留完整原要求与全部仍适用场景；新增的显式区域义务仅适用于本 change 的主动采用。

## MODIFIED Requirements

### Requirement: FL08-U3 同轮输入一致，下一合法点采用新值
Host SHALL 在合法呈现机会先同步自身需求，再测量／排列、绘制；该轮普通展示消费同一输入，包括稳定 idle 回调。默认包装持自己的完整 demand；自定义 tracked 消费者通过 schema roles 或可选 demand 明确实际 key，不 prepare-everything。处理中业务新值 SHALL 到下一合法点采用，不递归重排，不固定 tick、Repaint 或必然下一帧；hover／pressed／capture 即时反馈不一概冻结。

显式区域表示是本轮输入的一部分，SHALL 在原合法同步边界取得；相同完整表示可共享区域纯结果而不逐后代重读。业务／执行资格仍从即时通道读，原操作顺序不变；操作中产生的新业务值不能使后续 sibling 混入半份新表示。编辑 owner 的当前 draft／caret／composition 保原即时展示，已在 Repaint 前发生的局部状态变化 SHALL 在该次同步采用。

#### Scenario: 较早 Button 改变模型
- **WHEN** 同回调后续控件读取已同步展示输入
- **THEN** 它继续本轮一致输入，下一合法同步再采用新值
- **AND** 不因 stable fast path 跳过本轮必要需求准备

#### Scenario: 边界之前发生了不带业务 bump 的错误结果
- **WHEN** source 的当前 row result 已改变而业务 view 未变
- **THEN** 第一个合法区域捕获取到当前错误，并让该次完整几何／展示采用它
- **AND** 不靠旧 view identity 跳读，也不让原调用者补 Notify 才可见

### Requirement: FL08-SPLIT-PHASE 手动分阶段须配对
合法手动 MeasureAndArrange 与 Draw SHALL 消费同一对应结果，中间不插另一轮 arrangement；frame 和 execution 显式供给。DrawFrame SHALL 自持一次 frame、measure、draw、hit、popup 及失败清理，不要求普通作者重写 Begin/End。回调 Close 后停止剩余工作。

显式区域的手动配对 SHALL 保留同一已提交输入／几何版本，间隙新失效进入下一合法配对；不得为新表示私插 arrangement 或用新成员配旧几何。无区域的原手动入口及 BeforeDraw／prepare 条件不改变；新附件不是另一个 advance owner。

#### Scenario: 嵌入已有原生窗口
- **WHEN** owner 将实际 content rect、一次捕获的 frame 与 execution 交给 Host
- **THEN** Host 完成同一连接管线，native owner 仍持资格和最终 execution 释放

#### Scenario: 手动 measure 与 draw 间来源改变
- **WHEN** 原 owner 已取得完整排列，随后出现新表示义务
- **THEN** 当前 Draw 只消费其对应的完整版本，新义务保持待下一合法配对
- **AND** 不在 Draw 中递归测排、不吞失效、不复活已关闭 Host

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

明确采用 FL09-REGION-UPDATE 时，T2 同步 SHALL 鲜验区域边界，但可复用已证明相等的整个区域纯工作／调度片段，而非重复遍历纯后代。原同步时点及 T1 次序不变；未采用消费继续上文普通鲜读条件。新 participant 的纯展示可按其独立合同执行，不把旧 custom Draw 自动移到 T3，也不以保留结果跳过实际需要的 Repaint 提交。

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

#### Scenario: 两个同步点之间区域没有变化
- **WHEN** 首 pass 与 Repaint 分别鲜取同一完整表示且环境／局部状态有效
- **THEN** 两个边界均成立，第二个不重复派生／验证未变纯后代
- **AND** interactive／unknown 操作保持，Repaint 实际画出可见内容

### Requirement: FL09-TIER-SAFE 不读普通 getter 的展示默认只在 Repaint
不论页面是否启用分层执行，内置展示 wrapper 中不调用普通 getter 的展示工作 SHALL 在声明了展示隔离能力的执行后端上只在 Repaint 执行。这类工作包括：静态 TextKey 或字面文本、tracked 展示值、按钮与复选框的纯绘制部分。operate、控件调用、请求与资格 SHALL 仍然逐 pass 执行。这些展示的 fit 结论与样式回退诊断在 Repaint 报告。缺少该能力的后端、自定义 widget 与编辑器 SHALL 保持原逐 pass 执行。这个默认不需要作者声明，也不改变任何普通 getter 的调用。

自定义 widget／编辑器的上文默认 SHALL 保持；只有作者另外实现并在区域明确采用 FL09-REGION-PARTICIPANT 时，才使用其新 Operate／Paint 分界。单有接口或 source 登记不改变旧 Draw；新纯 Paint 不得包含普通业务读取、控件调用或请求。

#### Scenario: Layout 事件到达只有静态标签和按钮的页面
- **WHEN** 非 Repaint pass 执行
- **THEN** 不为静态标签量字或绘制，按钮仍执行 operate 与控件调用
- **AND** 随后的 Repaint 产生与原行为相同的像素

#### Scenario: 后端不声明展示隔离
- **WHEN** 执行 owner 没有该能力
- **THEN** 所有展示保持原逐 pass 执行，不降低结果

#### Scenario: 新 participant 未被任何区域采用
- **WHEN** 原页正常调用该 IUiWidget
- **THEN** 仍运行原 Draw／Measure／PointerRegions，不因新增接口隐式启用保留模式

### Requirement: FL09-CLIPPED-DISPLAY 完全裁剪的展示可以延期
`Overlay` 上的 `DeferClippedDisplay` SHALL 默认关闭，只接受 `true`／`false`，只在 `Overlay` 上合法。后代 Overlay 省略时继承；显式 `false` 对该子树关闭，更深的显式 `true` 可以重新开启。它不改变排列、裁剪、命中层或行的存在。只有当有效 clip 完全为空时，内置 `text/wrapped`、`input/button`、`input/checkbox` 才可以把纯展示的普通读取、格式化、fit 与绘制延期；内容变为可见时，该 pass SHALL 读取当前展示值，即使普通 getter 没有发出通知。输入与业务操作，包括控件身份、资格、请求、capture、取消，SHALL 仍按原顺序逐 pass 执行。延期不是成功绘制：不清除 presentation fault，也不替换最后成功的 retained paint。不具备展示隔离能力的后端、未选择的 Overlay、自定义 widget 和编辑器保持原 Draw。分层执行生效的页面，SHALL 视为所有 Overlay 都已启用该延期，除非子树显式写 `false`。

本要求把 0.8.x 已采用的同名 XML 词汇及语义带入 0.9。以 0.8.x 编写的 manifest 在 0.9 中含义不变。

以上 XML 的合法位置／默认／旧 custom 条件 SHALL 保持。明确采用区域 participant 的纯 Paint 另按其新合同可在有效 clip 为空时延期，不把该权限授予旧 Draw，也不扩张 DeferClippedDisplay 的 XML 合法位置。其 Operate、help、编辑及 capture 结束仍逐事件执行，滚入后第一个适用 Repaint 消费当前表示并实画。

#### Scenario: 行滚出视口后又露出
- **WHEN** 行所在 Overlay 的有效 clip 先为空，后在一次滚动后可见
- **THEN** 不可见期间只执行输入与业务部分；可见后的首个 pass 读取当前值并绘制
- **AND** 不可见期间累积的展示错误在可见时按当前状态报告

#### Scenario: 区域 sparse 的纯展示在屏外
- **WHEN** 它的有效 clip 为空，但仍属于当前可靠输入域
- **THEN** 可延期其纯 Paint／纯展示计算，原 native 控件与生命周期操作继续原次序
- **AND** 滚入后首个适用 Repaint 同步并实际提交，不拿旧像素代替

## ADDED Requirements

### Requirement: FL09-REGION-PARTICIPANT 纯表示共享不取代操作
外部作者 SHALL 可用可选新 participant 在已登记区域提供完整纯 Build、原次序 Operate 与纯 Paint，
不需要 internal 接口、默认包装继承、私有 state table 或 Tools。Build SHALL 只消费明确表示、
当前完整环境及已有局部 UI 状态，产出不可变自然尺寸／label贡献／局部 pointer 和 paint 资料；
不得延迟普通业务读取到复用结果、创建 editor、发请求／帮助或保存借用 context。
Measure、pointer、paint SHALL 使用同一完整结果与当前有效位置，unknown 子段保持原回调。

Operate SHALL 保原每事件 native 控件顺序、资格、编辑／IME、capture／结束、Cancel、help 和 endpoint；
cached display 不授权业务操作。FL edit owner SHALL 使相关 draft／validity／conflict／focus／composition
改变参与局部表示失效；其他 custom local state 由其实际 mutation owner Invalidate 或保旧路径。
Paint SHALL 纯消费显示事实，不包含普通模型读／控件／命令。原 IUiWidget 与独立入口默认不变，
仅实现新接口或注册源不会隐式切换。

具有对应隔离／绘制能力的后端 SHALL 可省略非 Repaint／完全裁剪的纯展示，
但在每个实际需要的 Repaint 提交当前可见内容；未知后端明确回退原调用。
几何所需的 Build／首次测量 SHALL 不因屏外延期，图像供应者的当前资源与寿命仍归原 owner。
滚入、重显、尺寸／字体／语言改变后的首个适用 Repaint SHALL 使用当前完整表示。
复用只证明少派生／少调度，不证明 framebuffer、GPU 或显示器保留像素。

#### Scenario: native 编辑产生未提交稿
- **WHEN** 原 editor 改变 draft／validity 而业务表示不变
- **THEN** 原 editor 当次显示当前稿，相关纯反馈／高度按原合法边界采用当前 UI 状态
- **AND** 不等待业务 Bump，不重播提交，不把当前稿当已接受模型

#### Scenario: 暖开后未变 sparse
- **WHEN** 区域表示、约束和 editor 状态相等
- **THEN** 其布局／pointer／静态画面资料共用同一结果，仍按原顺序调用 Operate
- **AND** Repaint 通过实际后端画出当前可见资料，不能仅跳过 Draw 而留旧像素

#### Scenario: operate 关闭 Host
- **WHEN** 原业务操作或 native 回调令 Host 逻辑关闭
- **THEN** 不继续正常 Paint／其他节点调度或发布候选
- **AND** Close 的清理义务仍由原 owner 完成

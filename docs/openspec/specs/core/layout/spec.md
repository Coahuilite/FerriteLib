# 测量、有效几何与增量同步

## Purpose
用相同真实输入得到可供 paint、hit、clip、scroll 和 popup 消费的一份完整有效几何。复用是少做未变计算与候选发布，不是新的树、线程、业务时钟或保留旧像素。

0.9 区域保留已按 WO-FL09-RETAINED-DISPLAY-20261111 r2 采用、未实施；本页新增区域条款及
相关显式例外仅适用于主动采用的新能力，不表示当前 API 已可调用。行为唯一正文归本页，
采用来源见 [fl09-retained-regions](../../../changes/fl09-retained-regions/proposal.md)。

## Requirements

### Requirement: FL08-LAYOUT 父级分配而内容报告自然需要
父级 SHALL 持分配外框，内容／包装使用实际宽度、有效字体、翻译和度量报告需要。Stack、Column、Section、Surface、Clip 走纵向 flow；Row 用实际横向分配／跨轴最大值；Wrap 用各真实行；Overlay 用同一子尺寸；Scroll／Repeat 用纵向内容。Height 数值固定，Auto／缺省报告自然需要，合法 MatchContent 使用其父／同层独立参考；显式固定 Height 不得被内容反撑；Width、Min/Max、权重、Auto、Fill 与 MatchContent 须按真实约束生效。Fill 是独立 Fill=true，不是 Height="Fill"。

#### Scenario: 长标签遇到固定高度
- **WHEN** 真实测量的自然高度超过作者固定 Height
- **THEN** 分配仍受该 Height 约束并可定位不足
- **AND** 不用渲染时悄悄撑大父级替代真实布局

### Requirement: L1 不足保持约束且可诊断
自然内容 SHALL 默认增长，固定尺寸约束增长；不为塞入父框而破坏有效 MinWidth。不足 SHALL 给定位 WidthInsufficient、HeightOverflow、ChildOverflow 等事实，不自动缩字、ellipsis、滚动或裁切；Scroll／Clip 必须显式。布局诊断最多128条，超出带 Truncated。1逻辑单位正值安全下限不是可读最小尺寸，内部 label stacking 阈值也不是硬最小宽。

#### Scenario: 80宽的无标签 StepperSlider 使用默认部件
- **WHEN** minus24、range下限1、field64、plus24 与三个6间隙需要131宽
- **THEN** outer 仍80，报告短缺51及实际内部溢出
- **AND** 不自动改部件、加 scroll 或宣称131是人验可用下限

### Requirement: FL08-PLACEMENT 有限同父关系
Overlay SHALL 允许直接可见兄弟／parent 的有限轴边或中心依赖与自身 pivot，offset 用逻辑单位／父内比例。前向引用可解析；循环、缺失、歧义、错轴边以及同轴 Stretch+anchor SHALL 定位拒绝。Overlay 自然高度只取真实子尺寸加 chrome／padding，不因 offset 递归膨胀；重叠不挤动兄弟。任意跨树／跨 Repeat 项关系不提供。

#### Scenario: 第二个子项锚在先前声明后的兄弟右边
- **WHEN** 有向关系无环且目标是同父可见兄弟
- **THEN** 按真实尺寸依赖顺序求位置
- **AND** 隐藏或缺失目标导致定位错误而不套用旧坐标

### Requirement: FL08-SHARED-LABEL 标签贡献与消费者分开
SharedLabels 显式启用的组 SHALL 收直接参与 field 的真实 label 最大需求，受本地／翻译文本、字体、密度影响；五类 field 为 Slider、NumberField、TextField、StepperSlider、Dropdown。窄分配可把标签移上方并报告组合高度，空 label 无 band，嵌套组各自启用。父仍持外框，固定 Height 不膨胀；未启用组的 Dropdown 保留80逻辑单位列。

#### Scenario: 组中已有90宽贡献
- **WHEN** 另一项60变70，随后变120
- **THEN** 首次最大值仍90，后次120传播到真实共享列消费者
- **AND** 嵌套 UpdateScope 不屏蔽该贡献

### Requirement: FL08-SCROLL 内容范围不同于外框
Session SHALL 是滚动位置唯一 owner；自然内容、viewport、padding／title 与 scrollbar 保留宽度共同决定范围。复用同外框不能忽略内容范围变化。滚动／clip／当前偏移后的有效位置 SHALL 同供 draw、hit、popup anchor 与 scroll target。responsive Row 的 Breakpoint 必须正且 Narrow=Column；Wrap 的 Cols/NarrowCols 不构成全局两轴媒体查询。

#### Scenario: 固定100高视口内容80变140
- **WHEN** 外框不变而自然内容增长
- **THEN** 最大滚动从0变40，保留宽度参与真实再测量
- **AND** 不因父尺寸相同而保留过时滚动范围

### Requirement: RU2-A 范围持记录而不持调度
root SHALL 有 update scope；默认 Section／Surface／Scroll／Clip／Repeat 参与，作者可声明并嵌套 UpdateScope=true，false 不声明，非法布尔定位失败。普通 Row／Column／Stack 不被强制各成岛。scope SHALL 持阶段记录而非新 scheduler；父约束、Scroll 和共享列仍跨 scope 传播，不改变业务／节点身份。

#### Scenario: 行模板新增显式 scope
- **WHEN** 作者把相容 Repeat 模板根声明为范围
- **THEN** 原 key／editor／capture 身份不变
- **AND** 该范围不能阻止真实父约束进入

### Requirement: RU2-B 完整实际测量输入决定复用
默认包装 SHALL 自持真实输入。新可选 measure participant 的 capture SHALL 无副作用、完整包含宽度／文本／有效属性／共享结果／局部状态／服务环境，Calculate 只用已捕获事实。通知到达后先同步来源，再按实际相等输入复用；源被标 Paint 也不能覆盖其真实尺寸依赖。旧 Measure SHALL 在原合法同步验证，不推断跨同步 pure；相同同步同约束可复用已验证结果。null capture／非有限输入或高度 SHALL 走定位恢复。

普通成功同步 SHALL 将当前完整布局实际依赖和已验证版本对齐；过去访问过不等于永久当前依赖。
重新进入布局的成员须验证当前来源；同key多owner／多Host各保其真实义务。提交／测量期间的新公告不能被较早验证结果吞掉；
失败保持原完整结果及真实失效，不以清通知或改状态伪造成功。

明确选用 FL09-REGION-UPDATE 的表示消费 SHALL 可由一次完整区域输入／环境证明替代逐后代 capture／验证；只有原声明已切到新端口的库内消费或新纯 participant 适用。未接入的新旧 Measure、普通输入及未知度量 SHALL 保持上文原义务。区域相等证明必须覆盖真实尺寸依赖、source 和局部 UI 状态，不仅是业务 view 或旧版本号；来源读取失败不能当作相等。

#### Scenario: 通知后输出尺寸未变
- **WHEN** 来源真实变化但测得相同宽高
- **THEN** 该输入被验证，父 plan 可保留
- **AND** 不因复用旧高度而漏读应执行的普通 getter／旧 Measure

#### Scenario: 相同完整区域表示再次同步
- **WHEN** 当前边界鲜取源表示、布局环境和 UI 状态均与完整已提交记录相等
- **THEN** 已明确采用的区域可跳过其后代纯 capture／Measure／Build
- **AND** 其内普通读取洞与未知 Measure 仍按实际控制域执行，不把整页一律宣布 pure

### Requirement: RU2-C 度量环境必须真实
ITextMetrics SHALL 保持原两成员。可选环境能力 SHALL 按有效 font 提供 detached stamp，相等意味着同文本／字体／宽度结果相同，null 撤销复用；服务实例及 Host 也区分。自定义其他字体／服务应声明真实环境或保留 legacy 入口。未知老服务 SHALL 仅保原 idle snapshot 条件，新同步正常重验证；不追加字体 pure 条件。

#### Scenario: 颜色变而度量环境未变
- **WHEN** 只有 tint／hover 变化
- **THEN** 不重测文字；真实字体／语言／缩放或度量环境变更则使相应输入失效
- **AND** 不能用一个“全帧缓存”掩盖环境改变

### Requirement: RU2-D 局部 plan 与有效位置分离
局部 plan SHALL 验证其实际约束下全部子结果及直接身份／定义；相等子尺寸可保留 plan，尺寸变则运行真实排列。plan 不持可变页面空间 entry；当前子结果经 slot 与 offset 生成有效位置。移动子树 SHALL 更新实际受影响后续兄弟、后代、clip、hit、popup，而不强制重测未变文字。所有阶段候选 SHALL 与完整排列一起提交或放弃。

已采用区域的全部子结果有效性 SHALL 可由完整不可变分项表示和真实边界约束证明，不要求为证明而重走每个子项。成员／高度变化 SHALL 在原几何 owner 的同一事务中重建实际变化闭包，并把尺寸、位置、共享列、Scroll 内容／预留宽度／clamp 和 popup／hit 依赖传播到区域外；不能因有作用域而截断父约束。

#### Scenario: 前部文字增加一行
- **WHEN** 后区自身输入未变
- **THEN** 后区使用新位置但可保留其文字测量
- **AND** 不保留旧点击区域或半份新旧排列

#### Scenario: Repeat 内一项展开，其他分项表示未变
- **WHEN** 完整区域输入证明未变分支，真实成员或高度只在一项变化
- **THEN** 只重建变化分支及必要祖先／兄弟依赖，Scroll 与有效位置一并成功发布
- **AND** 未变分支不再进入逐项纯派生；无法证明的传播仍走原完整事务

### Requirement: RU2-E 稳定域候选不污染旧结果
生产共享 Release 路径 SHALL 在完整可判定稳定定义／成员域复用已提交结果；只对实际变更候选写入和受影响几何发布做准备，避免先全树复制再宣称增量。失败 SHALL 保留上一完整快照和缓存，丢弃候选写入，不回滚业务与 runtime declarations。旧 snapshot 在后续 full 更新／Close 后仍只读可用，不能指向候选 patch 容器。

#### Scenario: 未变稳定页重复同步
- **WHEN** 所有适用实际输入和环境仍有效
- **THEN** 可保留几何而不全树捕获／复制／重组发布
- **AND** 仍验证当前成员／修订并执行所需 Draw、hit 和 Repaint，不承诺总成本 O(变更节点)

### Requirement: FL08-FULL-FALLBACK 不完整依赖必须完整回退
在没有下述显式区域完整证明的路径，新定义／roots／reader或owner／度量翻译服务／viewport／布局属性generation、普通 getter 新同步、旧 Measure、未知字体、visibility/width/tab/Repeat成员变化、非叶 dirty、显式 content invalidation、新同步时的 shared-label/Auto-width、变化 MatchContent、高度变化穿过 Fill 或不支持祖先、变化域已有诊断、动态 demand／layout诊断回调 SHALL 保留正确 full path。回退不缩窄合法用法；同同步可复用已验证 enrolled 工作。

明确采用的新区域可用完整输入／环境／成员证据及同一 journal 的完整局部传播代替其中已证明闭包的 full，不以触发条件名称一律拒绝区域更新。未映射／未知输入、未证明的约束传播和失败仍回原完整路径；该新选择不缩窄任何旧合法用法，不允许父条件变化后探读旧后代。

#### Scenario: 多根页前根增高、后根子树含 Fill
- **WHEN** 后续根原可用高度依赖 viewport 减先前根高度，前根发生任一高度变化
- **THEN** 采用完整传播／完整事务，而不是只平移后根 Y 保留旧高
- **AND** 缩小也同样正确；输出未变／idle 不被无条件拒绝复用

#### Scenario: idle MatchContent
- **WHEN** 独立参考已提交且无 pending demand、dirty 或新测量
- **THEN** 可保留 idle 分配
- **AND** 参考有真实变化时在发布前走完整路径

#### Scenario: 已登记区域中的可见成员变化
- **WHEN** 新完整表示改变成员，未变分支及受影响祖先的传播均可证明
- **THEN** 采用完整局部事务，不仅因发生 visibility／Repeat／content 变化就重建整个 Host
- **AND** 若响应式宽度／Fill／共享列等传播无法证明，按原 full 完成正确结果

### Requirement: FL08-LAYOUT-FAIL 候选失败不损坏已发布结果
布局失败 SHALL 遵[寿命恢复](../lifetime-recovery/spec.md)的完整几何与输入暂停合同；候选共享容器只读，实际写时取得隔离存储。不删失败事务换取快路；ThemeFor 定位诊断、活解析视图及 mutable Theme 的正常条件 SHALL 保留。

#### Scenario: sparse 候选后转 full 又失败
- **WHEN** 候选已做部分验证或局部写入
- **THEN** 完整旧结果仍可读，下一次合法成功可恢复输入
- **AND** 不把部分 work 计作成功 publication

## 0.9 explicit owner constraints

A Host may provide a finite nonnegative runtime Height and/or responsive Narrow decision for a named
structural container. Leaf appearance/editor sizing is unchanged; Narrow selects an already declared Breakpoint variant. Null restores the declaration/automatic policy. Changed inputs take effect at the next
complete geometry transaction, without replacing XML, definition or Session; normal compatible draft,
focus and scroll identity remains. Runtime height outranks the declaration's sizing mode and row budget.
The initial implementation deliberately takes the conservative full fallback. Removing or incompatibly
replacing a declaration removes its constraint. Existing calls without runtime input retain their meaning.
Public usage: [owner layout input](../../../../development/0.9/tools.md#owner-layout-budgets-and-edit-completion).

## 已采用的后续能力

[双轴Scroll与有限画布ADDED](../../../changes/fl09-basic-ui-capabilities/specs/core/layout/spec.md)已采用、未实施，
由该change持唯一目标正文；旧纵Scroll／普通测量和布局条件继续由本文持有。实现差距见[实现账](../../../implementation.md#fl09-reliability)。

## 0.9 区域保留：已采用、未实施

本节仅约束主动采用的新能力；其余原条件保持。新增闭包已定 Public-unstable；接口／数据层先行导出，完整执行仍实施中，支持状态以实现账为准。

### Requirement: FL09-REGION-UPDATE 未变区域在边界停止重复工作
已显式采用区域 SHALL 在原 Host／几何 owner 内保留完整不可变纯结果与工作片段。
当前完整源、定义／reader owner、边界约束、有效字体／样式／语言／服务环境和局部 UI 状态相等时，
SHALL 在区域边界停止后代纯 Build／Capture／Measure、上下文解析及局部 pointer 几何再生成，
复用该区域调度；不能进入全部叶命中各自缓存后称为区域保留。
也不得把同样逐字段投影／格式化／几何工作移入 CaptureCurrent；有完整不变证据的生产源应在边界
核对来源域，只投影实际变化分项，源捕获成本仍计入更新工作。
真实 interactive／unknown 操作与当次有效位置／clip 变换继续，不能把总成本承诺为任意图 O(变化节点)。

部分表示变化 SHALL 利用真实未变分项／分支记录保留其纯结果，只重建实际变化闭包；
成员、顺序、自然尺寸、后续兄弟、共享列、Scroll 内容／滚动条预留／clamp 和 popup／hit SHALL 共同传播并成功发布。
unknown 需求按实际父控制域执行；未支持／不完整传播 SHALL 定位回原 full，而非默用旧坐标。
首次发现／materialize SHALL 完整测量、验证并实际参与后续绘制，不缩减 key／字段／场景掩盖成本。

#### Scenario: 稳态两次同步
- **WHEN** 两个合法同步边界均证明区域表示与环境未变
- **THEN** 各自完成边界鲜验，区域纯后代不再逐项派生／验证
- **AND** 所需交互和未知调用仍执行，复用记录不是旧像素承诺

#### Scenario: 行错误令固定 Scroll 内容变高
- **WHEN** view 未变而 row result 或 invalid draft 改变自然高度
- **THEN** 新完整布局采用当前错误，更新内容范围／位置／clamp／hit／popup
- **AND** 不以外框和业务 token 未变保留旧高度

#### Scenario: 字体或滚动条预留改变
- **WHEN** 相同源表示遇到新的宽度、字体／语言／度量、共享列或响应式读取域
- **THEN** 重新验证并构造实际受影响结果，未知环境保原鲜算
- **AND** 不为了获得复用要求旧尺规提供新环境能力

# Spec Delta — 采用来源（非 current）

0.9 已采用、未实施；来源为 bb7da49 及 WO-FL09-RETAINED-DISPLAY-20261111 r2 的 choices／paint证明与源捕获补充。
本文保留采用时完整 delta，不作为第二份 current；唯一正文见 [对应能力](../../../../../specs/core/layout/spec.md)。

## MODIFIED Requirements

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

## ADDED Requirements

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

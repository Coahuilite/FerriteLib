# Spec Delta — 采用来源（非 current）

以下为0.9 r2采用时的 delta；r3 源码与证据见实现账。来源为 bb7da49 及 WO-FL09-RETAINED-DISPLAY-20261111 r2 的 choices／paint证明与源捕获补充。
本文保留采用时完整 delta，不作为第二份 current；唯一正文见 [对应能力](../../../../../specs/core/binding-reactivity/spec.md)。

## MODIFIED Requirements

### Requirement: FL08-ORDINARY 普通读取保持原采用条件
普通值／只读／options 注册、手写访问器和旧自定义 Measure SHALL 保持原通知及合法执行条件；Set 请求业务 mutation，不等于存储成功，也不自动 NotifyChanged。显式通知默认 Everything；None=0、Paint=1、Measure=2、Structure=4，Everything 为后三者并集。公告须到达真实消费者声明的 binding；尺寸／可见性等依赖不能被误归为纯 paint。未登记 getter 不因快路获得 pure、订阅或创建线程前提。

普通公告 SHALL 容纳已注册键的同值、冗余、批内重复及非当前布局键，不要求作者过滤当前可见项；
这不把tracked equality的抑制政策加给普通通知。每Host按其实际完整依赖验证，不由共享bindings的一次公告／另一Host提交代确认。
待布局义务属于该 engine 最后成功 publication 的当前依赖域；只有成功 publication 才确认其读取前
采样的 revision，并退休已退出该域的键。隐藏不清模型通知，复显读取当前值；读取或测量期间的
新公告、失败候选均不能冒充已成功覆盖的 revision。

“合法执行条件”取决于页面的执行方式。逐 pass 执行时，普通展示读取与旧 Measure 按每个 pass 的同步执行。[分层执行](../../contracts/presentation-execution/spec.md)生效时：普通布局输入与旧 Measure SHALL 在同步点执行；内置展示 wrapper 的普通展示读取 SHALL 只在 Repaint 执行；业务请求、执行资格与 operate 中的读取仍逐 pass 执行。在这两种方式下，普通 getter 都 SHALL 在被同步点读到之后才被视为已采用，且 SHALL 不因复用而被推断为 pure。普通 getter 的调用次数与时点在两种方式之间可以不同；依赖每 pass 调用次数的作者 SHALL 选择逐 pass 执行。

显式采用 FL09-REGION-SOURCE 时，作者可另外选择一个不可变表示端口供列明的结构／纯展示角色消费；该端口的当前值 SHALL 在每个原合法同步边界鲜取，并作为新能力的采用事实。它不重注册、拦截或改变原 IUiBindingAccess.Get／Choices／endpoint，不能以登记同名 key 改写旧读取；operate、资格、请求、未映射及未迁移 custom 的普通读 SHALL 继续原条件。只有明确切换到新端口的消费才允许依据完整相同表示跳过纯派生，不因普通 getter 的存在推断其 pure，也不要求旧 source 调用者追加通知。

#### Scenario: 普通字段在 UI 外改变
- **WHEN** 业务改变影响文字测量的普通字段并按原注册 key 通知
- **THEN** 真实依赖在下次合法同步验证
- **AND** 不通过扫描任意对象字段或暗中启用 tracking 发现变化

#### Scenario: 曾显示的键离开当前布局后仍收到公告
- **WHEN** 同一Host隐藏祖先后，作者合法公告其后代旧键，包括未变值或重复键
- **THEN** 当前无布局依赖的历史键不永久阻塞其他有效布局；仍存的同键依赖继续同步
- **AND** 复显时验证最新值，不要求换定义／重建Host或只公告可见键

#### Scenario: 分层页面的普通展示值在帧内改变
- **GIVEN** 页面启用分层执行，一个 `text/wrapped` 绑定普通 getter
- **WHEN** 该 getter 的值在同一帧的两个非 Repaint 事件之间改变，且没有通知
- **THEN** 非 Repaint pass 不调用该 getter
- **AND** Repaint 调用它并绘制当前值，与逐 pass 执行的像素一致

#### Scenario: 新表示与旧业务读取并存
- **GIVEN** 区域明确采用表示端口，旧 BindValue／命令仍注册
- **WHEN** 表示仍是上次完整版本而操作检查当前业务值
- **THEN** 普通 Get 与请求看到原即时值，不被表示端口拦截
- **AND** 未登记页面和旧 custom 回调的读取条件完全不变

## ADDED Requirements

### Requirement: FL09-REGION-SOURCE 完整表示必须在真实边界鲜取
显式区域源 SHALL 在每个原合法同步边界真实捕获当前不可变表示；相同实例仅可代表全部实际表示输入仍相同。
源 owner SHALL 覆盖业务数据、目标、展开／成员、配置／目录／policy、相关错误／拒绝结果及贡献来源，
或将无法证明的需求留在原读取路径。不能只以 view、revision、Bump、同一对象或未通知证明无变化。
源 SHALL 不取得编辑状态／业务写入／线程派发权；FL 自持 UI 状态与环境证据。
普通外部 source 入口的现有合法调用不增加 Notify／Host／订阅前提，源自身必须涵盖这些入口的表示结果。

新端口 SHALL 与原绑定／endpoint 分开；明确 map 的 Value／Choices 与 item source/item/field 按真实类型连接。
未明确映射是原消费，映射后缺项／错型是定位失败，Unknown 是原路径而非空成功。
snapshot 容器 SHALL detached 且只读，实际被消费的 payload 不可变；不保存延迟读取可变业务的闭包。
独立 Host SHALL 各自确认其成功版本；中途新变化和失败不得被较早捕获吞掉。

Value／Choices SHALL 在 builder、read map 和 root／item snapshot 各为独立通道，同名 key 不冲突。
snapshot SHALL 公开 Choices<T>(key)，返回 defensive immutable 的 IReadOnlyList<UiChoice<T>>，
保留实际 UiChoice<T> 的 Text／Value／HelpRef／TextKey；Get<T> 只读 Value，不跨通道补读。
缺通道／key／item SHALL 抛 KeyNotFoundException；精确声明 T 不匹配 SHALL 抛 InvalidOperationException；
同通道重复登记 SHALL 抛 ArgumentException，空 choices 为合法值、null 外层 SHALL 抛 ArgumentNullException。纯 Build 不得借普通 reader 填补缺项。
Operate 的即时资格与原 endpoint 不因快照 choices 改变；它不是当前可执行集合的业务权威。

生产源 owner 能提供覆盖全部实际来源／写边／有效期的不变证据时，未变边界捕获 SHALL 核这些
源域证据并复用原 immutable 包，仅对变化分项重做投影；不得把逐字段 Get／派生／格式化／几何
计算移入 CaptureCurrent 再称省略。证据必须含失败结果、选择／展开及真实贡献，不用旧 view／
Bump 替代；未知来源仍保原路径，不要求旧调用者增加公告。

#### Scenario: 失败只改变行错误
- **WHEN** 原公开 TryCommitActionField 失败写 rowResults，而 view／configuration／Bump 未改变
- **THEN** 下一合法捕获包含错误文字／存在性，使相应展示与高度更新
- **AND** 不要求原调用者再 Notify，原业务副作用／失败结果不回滚

#### Scenario: 未知贡献来源与已封存目录并存
- **WHEN** 某贡献只有普通读取，而其他项由真实 immutable catalog／packet 证明
- **THEN** 未知需求保持原实际调用条件，已证明区域仍可局部复用
- **AND** 不把未知当静态，也不以可能存在扩展否定全部已封存来源

#### Scenario: 捕获返回 Unknown 或抛错
- **WHEN** 源明确无法证明当前表示，或实际捕获失败
- **THEN** 前者走原消费并报告 Unknown，后者保当前 Failed 和原恢复原因
- **AND** 不把失败改成 Unknown／空值，也不使旧读取看到过时表示

#### Scenario: root与item的value和choices同名
- **WHEN** 同一包在两个独立通道登记 mode 的 string value 与 UiChoice<string> 列表
- **THEN** Get<string> 与 Choices<string> 分别读回本通道的只读事实，不互相覆盖
- **AND** 删除 choices 或读错 T 明确失败，不从 value 列表或普通业务读补齐

#### Scenario: 未变生产捕获不重做字段工作
- **WHEN** 完整源域证据仍有效且无变化
- **THEN** CaptureCurrent 返回原 immutable 表示，不逐字段重建业务投影／展示准备
- **AND** 边界捕获与后代 Build 成本一并观察，不能转移成本后声称复用

# 结构、声明与稳定身份

## Purpose
定义页面中什么存在、它属于谁，以及相容更新时保留哪些身份。定义不持运行状态，业务目标不由视觉顺序推断。此能力的直接消费者是布局、交互及[定义更新连接](../../contracts/definition-update/spec.md)。

0.9 区域保留已按 WO-FL09-RETAINED-DISPLAY-20261111 r2 采用、r3 放行实施；本页新增区域条款及
相关显式例外仅适用于主动采用的新能力。接口及执行源码已提供，支持／证据见实现账；行为唯一正文归本页，
采用来源见 [fl09-retained-regions](../../../changes/fl09-retained-regions/proposal.md)。

## Requirements

### Requirement: FL08-STRUCTURE 四项职责共用事实而不互相代管
FL SHALL 平行区分 Structure、Layout、Semantics、Appearance；它们分别决定成员身份、有效几何、合格操作与状态转换、呈现及其真实度量输入。解析和寿命是贯穿职责，不是四棵树、四条线程或四个新运行时。业务值、接受、持久化 SHALL 仍由消费者模型决定；草稿与请求均不是已接受值。

#### Scenario: 自定义呈现替换默认外观
- **WHEN** 作者为同一行为选择另一内部排列或只消费行为事实
- **THEN** 状态转换仍来自所选共用能力，布局使用实际度量
- **AND** 外观不复制一套激活／编辑状态机，也不因无默认绘制而失去行为

### Requirement: FL08-DECLARATION XML 与程序定义同义
XML 与 C# SHALL 使用同一不可变定义和编译／验证规则。构造输入的属性、子项、choices SHALL 被复制为只读定义；实例状态不写回定义。XML SHALL 不执行表达式、反射或任意 C#，并拒绝 DTD／外部实体／非允许文字内容。页面 Schema 接受2与3，执行默认值见 FL09-PAGE-EXECUTION；定义深度上限16、节点数512、inline Styles 文本32768字符。未知 kind、属性、矛盾布局、无效模板／引用及非法子项 SHALL 给可定位错误。

#### Scenario: 构造后修改原集合
- **WHEN** 作者修改用于构造定义的字典或子项列表
- **THEN** 已构造定义及另一个实例不改变
- **AND** 程序 Create 不绕过实际 Host 的绑定类型与布局验证

### Requirement: FL08-LITERALS 属性只解释一次
数字、布尔、padding 与 offset SHALL 由共同属性模型解析后供消费者使用，不各自另设语法。布尔接受去空白的 true/false/1/0；padding 接受一、二或四个有限数；offset 接受逻辑单位或0至100%的父内跨度，仍须满足所处轴的放置规则。TextKey／LabelKey／TitleKey／PlaceholderKey／DescriptionKey SHALL 是翻译键；HelpRef 是不透明主题引用，不是值绑定或元素身份。

#### Scenario: 程序布尔与 XML 布尔
- **WHEN** 两种入口声明同一合法布尔布局值
- **THEN** 编译器与布局消费相同结果
- **AND** 非法或冲突声明在定位处失败，不因使用 C# 而静默生效

### Requirement: FL08-IDENTITY 节点、部件与业务目标分开
每个 Host 的元素／稳定项／命名部件 SHALL 有独立身份；同 Bind 不共享草稿、弹层或捕获。重排或相容定义替换 SHALL 保留同一目标的相容状态。目标作用域改变、移除或能力不相容 SHALL 结束旧输入而不将其草稿交给新目标；任意跨父迁移不被自动推断。Child 身份不是新增的独立绘制层。

#### Scenario: 相同绑定的两个编辑器重排
- **WHEN** keyed A/B 各有草稿，重排后仍代表原目标
- **THEN** 草稿各归原实例，捕获不随遍历顺序转移
- **AND** 一个实例被重新指向其他目标时不会把旧稿写过去

### Requirement: FL08-REPEAT 键控制成员而非行号
Repeat 的 Items SHALL 是有序稳定 key 的 `IReadOnlyList<string>` value，Template 必须存在；按该序列实例化模板；key 非空且源内唯一。模板有限锚点只解析同项，不允许跨项／跨树引用。下列绑定角色 SHALL 按 source/item/field 的无歧义地址限定：Bind、ActionBind、OptionsBind、VisibleBind、PayloadBind、SelectedBind、CancelBind、PointKeysBind、DisclosureBind、CheckBind、WidthBind、HoveredOptionHelpOutputBind；翻译键和 HelpRef 不受此改写。

#### Scenario: 新快照替换对象并重排
- **WHEN** 同 key 的对象被当前快照替换，项顺序改变
- **THEN** 项的 UI 身份保持，而字段读取／请求解析到当前对象
- **AND** 不使用旧 ordinal 或旧对象作为请求目标

### Requirement: FL08-ITEM-CONNECTION 按项辅助只拥有 UI 连接
可选按项辅助 SHALL 在改变连接前验证一次源快照的全部 key／地址，随后以既有批次更新保留对象的字段和命令。业务顺序与拒绝仍归模型；移除 key SHALL 立即释放其展示计算与订阅，并由下一次 Repeat 协调清理行输入，不销毁外部对象。权威成员与过滤 key 可分别提供；若过滤列表本身就是辅助源，离开它即结束该 UI 连接。

#### Scenario: 业务拒绝上移
- **WHEN** 按项命令请求移动而业务保持原顺序
- **THEN** UI 从实际结果刷新，不预移、不回滚、不重试
- **AND** 其他窗口和外部对象的寿命不受该连接释放影响

### Requirement: FL08-LATE-ITEM 已有按项失败条件不变
模板内缺失、错型或晚到的项绑定 SHALL 保留既有 fail-soft／抛读恢复条件，不增加逐项预注册前提；页级声明类型预检仍适用。模板原始 MatchContent 无独立父引用时 SHALL 保留自然高度退路并报告 ContentHeightUnavailable，不静默发明父依赖。

#### Scenario: 某一项绑定稍后到达
- **WHEN** 已合法建立的 Repeat 首次缺少该项字段，后来注册并按原条件通知
- **THEN** 该项可在正常同步恢复
- **AND** 不要求销毁整个 Host 或将其他项视为失败

### Requirement: FL09-PAGE-EXECUTION 页面执行方式按 Schema 取默认，作者只在需要时声明
`UiPage` SHALL 同时接受 `Schema="2"` 与 `Schema="3"`。两者元素与属性词汇相同，唯一区别是页面执行方式的默认值：

- **Schema 2：** 默认逐 pass 执行，与 0.8.x 相同。
- **Schema 3：** 默认[分层执行](../../contracts/presentation-execution/spec.md)。

根属性 `TieredEvents` 是可选布尔值，显式指定执行方式：`true` 为分层执行，`false` 为逐 pass 执行。它只在 `UiPage` 根上合法，非法值 SHALL 作为定位的定义错误报告。省略时取 Schema 的默认值。程序化定义 SHALL 有同义入口（FL08-DECLARATION）。

以 0.8.x 编写的 Schema 2 manifest，无论是否带 `TieredEvents`，在 0.9 中的含义都 SHALL 不变。一个页面只有一种执行方式；Host 不按控件或子树混用。

#### Scenario: 旧 manifest 原样加载
- **WHEN** 0.9 加载未声明 `TieredEvents` 的 Schema 2 页面
- **THEN** 页面逐 pass 执行，普通 getter 的调用条件与 0.8.x 相同

#### Scenario: 新作者使用 Schema 3
- **WHEN** 作者编写 Schema 3 页面，不声明任何执行属性
- **THEN** 页面分层执行，作者不需要了解 IMGUI 事件阶段

#### Scenario: 作者依赖每 pass 调用
- **WHEN** Schema 3 页面声明 `TieredEvents="false"`
- **THEN** 页面逐 pass 执行，与 Schema 2 默认相同

## 0.9 区域保留：已采用的显式新能力

本节仅约束主动采用的新能力；其余原条件保持。新增闭包为 Public-unstable；接口及执行源码已提供，实际支持与验证状态以实现账为准。

### Requirement: FL09-REGION-ENROLL 作者在原树上明确选择区域
作者 SHALL 可在原 Host 的唯一声明结构容器登记表示源及明确消费 map，包含 Scroll／Repeat，
不要求新 kind、Overlay、Catalog、Tools、reactive runtime 或迁移整个页面。
登记 SHALL 先验证再生效；首版重叠附件、歧义模板目标、无效类型／角色定位拒绝而不改原连接。
原 UpdateScope 嵌套不受附件重叠限制。缺登记的默认值为关闭，Schema 2／3 及 TieredEvents 意义不变；
逐 pass 页明确登记后仍每 pass 捕获来源，未采用消费保持原读取条件。

区域 SHALL 复用原节点／key／part／mount 身份，表示 item packet 不成为另一棵身份树。
相容重排／定义更新保相容 UI 状态；隐藏、移除、retarget、重挂和不兼容替换按原寿命处理。
登记或 packet 相同不能免除原绑定 reader 类型／owner 验证，旧 Repeat 的合法晚绑定条件继续适用。

#### Scenario: F09 只采用 b-actions
- **WHEN** 作者在原 Scroll 内的 Repeat 上登记新表示，其他页和部分 widget 未迁移
- **THEN** 只改变已明确选择的区域消费；既有 Items 向量及原字段仍完整存在
- **AND** 不要求删掉屏外／折叠 key，也不以注册整个产品作为采用门槛

#### Scenario: 同 key 在实际释放后重挂
- **WHEN** 同名节点取得新 mount
- **THEN** 旧区域记录／请求不复活，按当前源建立新记录
- **AND** 其他区域和 Host 的状态不被迁移或释放

## 0.9 Repeat 声明身份：已采用，r9 实施候选

本补充按 WO-FL09-RETAINED-DISPLAY-20261111 r8 及本轮采用授权纳入唯一 current，
只适用于已显式采用区域能力的 Repeat，不改变公开 API、原 reader／通知或寿命条件。
采用来源为 [fl09-repeat-declarations](../../../changes/fl09-repeat-declarations/proposal.md)；
PM 已在 r9 放行本项实现；源码与受影响既有检查见[实现账](../../../implementation.md#fl09-repeat-declarations)，
固定载体和原生暖开／递归工作证据分别记录，采用或源码不等于性能通过。

### Requirement: FL09-REPEAT-DECLARATIONS 未变声明不因值或开合递归重建
0.9 中按 FL09-REGION-ENROLL 显式采用的区域内，Repeat 的实际声明身份依据完备且未变时，
FL SHALL 保留其已证明的行／子树声明结果，或作等效局部更新；即使其他工作需要 full，
值变化或纯显示／折叠变化也 SHALL 不导致递归遍历整套未变行 spec 并重新派生相同子身份。
必要边界验证、成员判定及将已证明身份合入本轮 keep 依据仍可执行；
不得仅改收集方法名或把等量递归派生移至 capture／measure／prune 后声称满足本保证。
不要求某种缓存层数或容器，不承诺整个事务 O(变化节点) 或省略寿命判定。

完备依据 SHALL 覆盖实际 Host／Session、父与 Repeat 的规范身份、仍存的 node／mount 世代、
已按原条件接受的 source/item keys 到行声明的对应、实际模板／派生 spec 及全部影响子身份的声明结构；
ordinal 仅在原身份规则真正使用它时参与，不能取代稳定 item key。
嵌套 Repeat SHALL 计入各自当前合法物化或可靠隐藏时最后有效物化的成员与声明依据，
不能仅凭外层 keys 或静态模板相同忽略内层变化。
证明可利用原不可变定义及 owner 持有的真实变更事实，不要求每次重走所有子定义才能证明未变；
也不要求为尚未创建的隐藏后代预先建立节点／mount。
仅 packet 引用、业务 view、无 dirty、可见矩形或历史 keys 相同 SHALL 不构成完备身份依据；
反过来，不影响声明身份的值、表示包或开合变化 SHALL 不单独撤销完备声明依据。

每次成功事务交给原寿命 owner 的声明 keep 依据 SHALL 与完整正确收集的结果一致：
包含当前声明元素、已物化行的全部声明后代（不论可见性）、仍声明但本轮合法未访问的隐藏 Repeat
最后有效行集，以及相应嵌套声明；不以可见 entries、局部脏列表或未访问推断删除。
隐藏源不因此增加普通 getter 探读；其 key 在隐藏期间外部改变时，按原条件在下一合法访问协调当前成员，
不能用旧记录永久掩盖已实际观察到的移除。私有子节点仍依原 owner 存活规则处理。

真实增删、身份相关重排、模板／派生结构／嵌套成员变化、retarget、父身份或 mount 结束／重建
SHALL 更新或撤销受影响的声明依据；相容未变分支可继续保留，不把旧记录借给新 incarnation。
无法证明完整性的分支 SHALL 在原事务使用完整实际收集／验证退路，不能用不完整 keep 集继续 prune。
候选声明结果与成员／几何 SHALL 共同成功或放弃，失败保上一完整关系与待处理失效；
不得因此跳过真正移除的释放、推迟既定 prune、复活已退出状态或补发业务动作。
具体隐藏／结束／失败／关闭仍归寿命能力的 FL08-END、FL08-FAILURE-GEOMETRY 和 FL09-REGION-LIFETIME。

本保证 SHALL 不改变普通 Items／字段读取、绑定 reader 类型／owner 校验、晚绑定或失败恢复的原合法条件；
声明结果不是当前业务值、操作资格、布局或展示复用凭据，不新增公开 API、XML、Notify／修订号义务。
未采用区域的入口保原合同。

#### Scenario: 暖开或改值引发 full，声明未变
- **GIVEN** 已采用 Repeat 的 row／spec／模板／规范身份及实际 owner／mount 依据完备且仍相同
- **WHEN** 仅值或纯开合变化引发下一次 full
- **THEN** keep 依据完整保真，但不递归重新派生整套未变行子身份
- **AND** 实际需要的读、几何更新、输入与 Repaint 仍按原条件执行

#### Scenario: 隐藏后再显示，或隐藏时外部删除 key
- **WHEN** Repeat 或行仍声明而被可靠隐藏，本轮按原条件不访问其来源
- **THEN** 最后有效的行及嵌套声明保留相容状态，焦点／capture 挂起仍按原寿命规则，不探读隐藏 getter
- **AND** 下次合法访问采用实际当前 keys，已观察到移除的行及其自有状态／命中由原 owner 清理，不永久保留旧集

#### Scenario: 真正移除后同 key 重挂
- **WHEN** 当前合法协调移除行，或旧 mount 已结束，随后又声明同 key
- **THEN** 旧声明依据退休，原释放照常，新 mount 不复活旧稿／capture／请求
- **AND** 未退出的相容兄弟与其他 Host 保持原状态

#### Scenario: 模板或内层成员变化而外层 keys 相同
- **WHEN** 模板／派生声明改变，或某内层 Repeat 的当前合法成员改变
- **THEN** 受影响的身份闭包按真实结构更新，父 keep 依据包含准确的内层结果
- **AND** 外层 keys 或 packet 相同不掩盖变化；相容重排仍按原 key 身份保状态

#### Scenario: 候选失败或声明依据不完整
- **WHEN** 无法证明完整声明，或更新成员／声明／几何期间失败
- **THEN** 在原成功边界前完整收集／验证或丢弃失败候选，不发布半份 keep 或误删未访问节点
- **AND** 下一次可靠成功使用当前输入恢复，实际已结束交互与业务副作用不回滚、不重放

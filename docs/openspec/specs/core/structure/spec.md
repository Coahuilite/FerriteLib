# 结构、声明与稳定身份

## Purpose
定义页面中什么存在、它属于谁，以及相容更新时保留哪些身份。定义不持运行状态，业务目标不由视觉顺序推断。此能力的直接消费者是布局、交互及[定义更新连接](../../contracts/definition-update/spec.md)。

## Requirements

### Requirement: FL08-STRUCTURE 四项职责共用事实而不互相代管
FL SHALL 平行区分 Structure、Layout、Semantics、Appearance；它们分别决定成员身份、有效几何、合格操作与状态转换、呈现及其真实度量输入。解析和寿命是贯穿职责，不是四棵树、四条线程或四个新运行时。业务值、接受、持久化 SHALL 仍由消费者模型决定；草稿与请求均不是已接受值。

#### Scenario: 自定义呈现替换默认外观
- **WHEN** 作者为同一行为选择另一内部排列或只消费行为事实
- **THEN** 状态转换仍来自所选共用能力，布局使用实际度量
- **AND** 外观不复制一套激活／编辑状态机，也不因无默认绘制而失去行为

### Requirement: FL08-DECLARATION XML 与程序定义同义
XML 与 C# SHALL 使用同一不可变定义和编译／验证规则。构造输入的属性、子项、choices SHALL 被复制为只读定义；实例状态不写回定义。XML SHALL 不执行表达式、反射或任意 C#，并拒绝 DTD／外部实体／非允许文字内容。页面 Schema 为2；定义深度上限16、节点数512、inline Styles 文本32768字符。未知 kind、属性、矛盾布局、无效模板／引用及非法子项 SHALL 给可定位错误。

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

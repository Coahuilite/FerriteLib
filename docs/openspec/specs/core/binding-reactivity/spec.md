# 绑定、依赖与业务响应

## Purpose
使模型读取、失效、响应和 UI 投影各有明确权威。此能力既支持普通显式通知，也支持自愿接入的 tracked 图；向[展示执行连接](../../contracts/presentation-execution/spec.md)提供输入，而不取得业务调度或线程派发权。

## Requirements

### Requirement: FL08-ORDINARY 普通读取保持原采用条件
普通值／只读／options 注册、手写访问器和旧自定义 Measure SHALL 保持原通知及合法执行条件；Set 请求业务 mutation，不等于存储成功，也不自动 NotifyChanged。显式通知默认 Everything；None=0、Paint=1、Measure=2、Structure=4，Everything 为后三者并集。公告须到达真实消费者声明的 binding；尺寸／可见性等依赖不能被误归为纯 paint。未登记 getter 不因快路获得 pure、订阅或创建线程前提。

普通公告 SHALL 容纳已注册键的同值、冗余、批内重复及非当前布局键，不要求作者过滤当前可见项；
这不把 tracked equality 的抑制政策加给普通通知。每 Host 按其实际完整依赖验证，不由共享 bindings
的一次公告／另一 Host 提交代确认。待布局义务属于该 engine 最后成功 publication 的当前依赖域；
只有成功 publication 才确认其读取前采样的 revision，并退休已退出该域的键。隐藏不清模型通知，
复显读取当前值；读取或测量期间的新公告、失败候选均不能冒充已成功覆盖的 revision。

#### Scenario: 普通字段在 UI 外改变
- **WHEN** 业务改变影响文字测量的普通字段并按原注册 key 通知
- **THEN** 真实依赖在下次合法同步验证
- **AND** 不通过扫描任意对象字段或暗中启用 tracking 发现变化

### Requirement: FL08-TRACKED 显式纯来源与相等政策
新 tracked 计算 SHALL 只读一个 runtime 的已连接来源，投影所有实际使用的可变属性为不可变展示值。Int、Text、Boolean 分别用整数、ordinal 字符串、布尔相等；通用 Value/List 要求作者指定 equality。相等写入 SHALL 保留旧存储值且不失效；相同最终值的独立命令／选择仍为独立请求。换分支或同结果换源 SHALL 更新真实依赖，不保留旧成功分支。

#### Scenario: 两个对象显示相同计数
- **WHEN** 当前目标引用换到另一对象，但文字恰好相同
- **THEN** 依赖跟随新目标；作者不得只以显示计数把两目标当同一存储身份
- **AND** 不默认深观察其普通字段

### Requirement: FL08-LIST 结构快照与项属性独立
受控集合 SHALL 将结构读作一个来源，接受结构变化时发布新的只读外层快照；旧快照仍可读，不深拷贝项。Move 的目标是最终索引。相等有序替换、空 Clear、不存在 Remove、Move 到自身 SHALL 不失效。Count、索引、枚举、Snapshot 等显式读取仍跟踪；写操作本身不隐式订阅输出，但输入枚举／作者 equality 中的实际读取仍会跟踪。

#### Scenario: 只改一项属性
- **WHEN** b 的 Count 改变而有序成员不变
- **THEN** 读 Count 的计算失效，只读结构的计算不因此失效
- **AND** 保留可变项再在 Draw 读取不冒充一致的冻结投影

### Requirement: FL08-U1 立即数据与明确派生读取
赋值 SHALL 立即改变业务数据并先记录依赖失效；业务主动读取可见批中间值，显式派生读取 SHALL 不返回已知过期缓存。展示投影不替代业务读取。

#### Scenario: 批中先写后读
- **WHEN** 批次尚未结束，业务写 count=1 后主动读取 count 和依赖它的派生值
- **THEN** 两者反映当前1，而不是上一展示版本

### Requirement: FL08-U2 逻辑操作不是事务
Set、普通 Invoke、TryInvokeCommand 与 typed Invoke 等实际业务入口 SHALL 合并隐式嵌套批次，外部更新可显式 Batch／BeginOperation。自动响应等最外层结束；复合 Set→Invoke SHALL 使用同一逻辑操作边界。异常 SHALL 不撤销已发生业务写入。

#### Scenario: Checkbox 带独立 ActionBind
- **WHEN** 一次合格操作先 Set 后 Invoke
- **THEN** 自动响应不插入二者之间
- **AND** Set 抛错时不执行后续 Invoke，已发生副作用仍保留

### Requirement: FL08-U5 响应不依赖绘制时钟
逐次业务请求 SHALL 不因展示合并丢失；状态驱动响应可在批次末合并，先失效再读取。响应写入 SHALL 入后续调度而非递归订阅链；首次订阅发现依赖也走同一 drain。窗口、Repaint、GUI pass SHALL 不充当业务时钟。每个相同响应一次 drain 上限256次；超限仅定位并暂停该响应，独立队列继续，不宣称证明了全部业务循环；只在 callback 返回时检查，不能中断永不返回的 callback 或识别所有外部异步循环。

#### Scenario: 响应注册在批内
- **WHEN** 批中注册一个响应且随后继续写来源
- **THEN** 首次响应等外层结束读取完整当前状态
- **AND** 数百个各执行一次的独立响应不共用“同一响应256次”额度

### Requirement: FL08-U6 新纯阶段与旧入口分界
新 tracked、显式 ReadPhase 和新声明的纯捕获／计算／paint SHALL 不写业务、不保存、不发命令；输入处理仍可写，即使位于旧 Draw 栈。显式 runtime SHALL 由创建线程拥有，在所属上下文串行访问；动态纯读取不得跨 runtime，临时上下文须在嵌套 finally 恢复，最外层不保留图或业务状态。普通未接入调用不被追加此新线程／纯度条件。

#### Scenario: Draw 同栈执行按钮命令
- **WHEN** 实际输入路径执行合法命令
- **THEN** 它在业务操作边界读取即时模型并可写
- **AND** 不因方法名 Draw 被当作纯 paint，也不把整个旧 Measure 包入新纯阶段

### Requirement: R1 纯失败缓存实际失败依赖
纯计算失败 SHALL 保留当前无效状态、定位原因、停止范围及可选最后成功值；首次失败无值。失败尝试 SHALL 记录实际已读依赖，含新分支／失败子计算，撤销过时边。只有相关输入变化或明确 Retry 才授权重算；无关值、旧分支、读、绘制、时间、show 不重试。纯递归 cycle 与响应预算是不同故障。

#### Scenario: 新分支首次抛错
- **WHEN** 计算切换到新分支并失败
- **THEN** 新分支相关变化或 Retry 可恢复，旧成功分支变化不触发重试
- **AND** 最后成功展示值不作为当前业务答案返回

### Requirement: R2 可写响应由 owner 恢复
受管消费者回调失败 SHALL 暂停自身、保留已写结果、继续独立响应，初次失败也提供可检查 handle。来源变化不恢复它；owner 核对结果并修正接入后 Resume SHALL 只运行当前状态一次，不重播历史。Dispose SHALL 释放回调与订阅且幂等，不保存／回滚／销毁外部业务。

#### Scenario: 回调写入后抛错
- **WHEN** 通知已产生副作用后失败
- **THEN** IsPaused/Fault 可观察且业务写入保留
- **AND** 下一次来源变化不会自行重放，显式 Resume 也不补发旧通知

### Requirement: FL08-EXTERNAL 外部结果只显式安全接入
独立服务 SHALL 保持自己的锁、任务、结果新鲜度与接受政策。接入层将安全不可变结果送到 runtime 所属上下文后再批量更新投影；FL SHALL 不默认调度线程或从 getter 读取后台可变对象。UiNotifyAdapter 是显式映射订阅，非自动响应 drain；off-owner 通知须拒绝并可报告，不能伪装自动派发。其容量 Flush 不构成业务提交边界。

#### Scenario: 旧异步结果晚到
- **WHEN** 应用收到先前请求的结果
- **THEN** 是否采用由应用决定，采用后才在所属上下文更新投影
- **AND** Batch 不提供跨线程投递、业务事务或 latest-result 策略

### Requirement: FL08-PROJECTION 选择与展示投影共用实际通道
`UiChoice<T>` SHALL 携 text/value/helpRef/textKey；value 与 choices 可共享同一 binding key 而不通道碰撞。
tracked Choices/Options SHALL 在受跟踪计算内复制外层列表并冻结实际使用项属性，不延后可变读取到 Draw；
required item equality 由作者给出。BindTracked 展示 output 的可选 equality 可抑制同结果发布但不得保留旧依赖；
int/string/bool 用既定政策，其他 output 未给政策时保守公告成功重算，不暗选深相等／引用相等。
presentation 内读同步版本，外部 full read 读当前；bounded prefix 只加 Count／所需索引，不替 getter 消除其投影成本。

#### Scenario: choice caption 相同而 typed value 不同
- **WHEN** 两项提供同文案及不同真实值
- **THEN** 选择仍保真实值身份，观察不按 caption 合并它们
- **AND** 同key的当前值和choices保持各自通道

### Requirement: FL08-NOTIFY 通知映射和容量批次有明确边界
UiNotifyAdapter SHALL 只映射声明的 property→keys；MapAll 只处理空属性名约定，不替未映射的非空名字公告全部。
Attach handle/Detach/Dispose SHALL 退订但不 Dispose source。distinct pending keys 默认上限256、配置必须正；
到上限即使批内也flush，普通 Flush 在open batch不投递。结束外层 scope 可关闭其后遗失的内层；
未结束外层且未达容量的 pending 保有界可见，不承诺自行交付。此批次不是自动response drain。

#### Scenario: 非空未映射属性到来
- **WHEN** 已声明 MapAll 但该名字没有自己的Map
- **THEN** 记录 unmapped 而不扩大公告
- **AND** off-owner 通知仍拒绝、不排队重放

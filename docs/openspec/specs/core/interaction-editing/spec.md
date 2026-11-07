# 操作资格、捕获、请求与编辑

## Purpose
将一次 UI 意图准确交给业务入口，独立管理 UI 捕获和草稿。原生适配只提供事实与执行；默认按键、Live 和结束政策见[默认包装](../../policies/default-wrappers/spec.md)，本能力不强迫自定义控件继承默认外观。

## Requirements

### Requirement: FL08-SHARED 选用能力而非复制行为
标准包装、独立自定义、headless SHALL 共用相应激活／布尔／范围／选择／展开／点／编辑转换；作者可只选所需能力。语义输入不伪造 hover、节点或 rect；指针缺必要几何 SHALL 报可定位条件缺失，不以零矩形命中或默降只读。无行为的 decoration 不获得焦点／捕获。

#### Scenario: 无默认绘制的范围操作
- **WHEN** headless 调用者提供合法目标、范围和语义值
- **THEN** 同一范围能力成立一次请求而绘制调用为零
- **AND** 不启动另一套后台 UI 或测试专用状态机

### Requirement: FL08-U4 新操作使用当前资格
几何命中 SHALL 使用可靠的有效呈现几何；执行 SHALL 重查最新权威业务资格。disabled、readOnly、hidden、covered、modal 和 consumed SHALL 分开判定，不合并成一个“不可用”。旧 enabled 投影不授权已失资格的请求；业务仍可拒绝。鼠标起始、持有继续、结束分别判断，原生窗口资格与内容层资格不可互替。

#### Scenario: 外观仍 enabled 而业务已禁用
- **WHEN** 释放前业务资格改变
- **THEN** 旧外观不产生请求
- **AND** 已持捕获仍沿自己的合法结束路径释放

### Requirement: FL08-POPUP-WHEEL 弹层滚轮同时保护状态和消费
开放弹层只有在当前输入合格、几何可靠且指针位于其覆盖区域时 SHALL 改变行偏移并拥有滚轮消费；失资格时 SHALL 两者都不做。合格菜单已到范围端点仍拥有该滚轮，不让下层滚动。滚轮不被当作新选择或默认窗口拖动。

#### Scenario: 不合格回调仍有 ScrollWheel
- **WHEN** 原生帧保留滚轮事实，但该窗口 InputAllowed=false
- **THEN** 弹层行偏移不变且不消费事件

#### Scenario: 已在最后一行
- **WHEN** 合格菜单内向后滚动但偏移已达上限
- **THEN** 偏移保持合法且事件仍由菜单消费

### Requirement: FL08-CAPTURE 新起始守卫不阻断归属结束
已经拥有的交互 SHALL 在释放、失资格、丢原生捕获、目标替换、移除、abort、Close 时结束其 neutral 与 native 记录。指针拖出 rect 不是自动丢捕获；raw 已消费 MouseUp 仍可结束自身而不能激活。次键释放不冒充主键释放；不能释放另一 execution 的捕获。结束本身 SHALL 不提交草稿或生成业务请求。

#### Scenario: 主键释放已被消费
- **WHEN** 自有 capture 收到 raw 主键释放而普通 phase 为 Used
- **THEN** 只结束该归属，不触发激活或下层动作

### Requirement: FL08-REQUEST 请求不等于接受
已成立操作 SHALL 按其语义发出适用的一次请求，包含合法同值选择。正常返回 SHALL 结束该请求，即使业务拒绝／归一化；之后读实际模型。写后抛错 SHALL 保留原错误、目标上下文与实际副作用，不假定未写、不自动重试／补偿。后续 paint／读回失败 SHALL 不重放已结束请求；下一真实合格点击可为新请求。

#### Scenario: setter 正常返回后读回失败
- **WHEN** 请求已返回而展示 getter 抛错
- **THEN** 请求保持结束，随后绘制不再发同一请求
- **AND** 失败按展示／恢复合同处理而不是业务回滚

### Requirement: FL08-SELECTION 当前观察不修复业务
布尔、选择、展开 SHALL 使用实际 typed 值，含合法 null；Index、Missing、Selectable 是观察结果，不选择首项修复缺失值。节点／部件／项／InteractionScope SHALL 构成操作目标，Bind 不是操作身份。选择成立时先关闭自有弹层再请求；业务回调 Close 后停止正常遍历。只读菜单可检查显示，不因此可写选项；被动 trigger 不制造可回焦目标。

#### Scenario: 两个 dropdown 使用相同 Bind
- **WHEN** 其中一个打开并选择与当前相同的合法值
- **THEN** 只关闭该实例的弹层并发出一次选择
- **AND** 另一实例不共享 capture／popup 或被伪造焦点

### Requirement: FL08-EDIT 草稿协调不窃取模型
编辑 SHALL 分开模型、格式化文本、解析有效性、modified、conflict、焦点／composition 和提交状态。业务改变时，未修改稿跟随；已修改稿与新模型分开保留并标 conflict，具体自动提交政策由包装决定。明确不同目标不能继承旧稿；普通原生变化在没有持有编辑事务时保留其原即时返回条件，不强制新持稿前提。

#### Scenario: 模型在修改稿期间变化
- **WHEN** 用户改稿而外部业务换了当前值
- **THEN** 当前业务值仍可读取，稿与冲突各自可观察
- **AND** 编辑器不把显示文本当作已接受模型

### Requirement: FL08-READONLY 原生只读在变更前生效
只读 SHALL 在进入原生编辑变更前过滤 mutation 输入／命令，同时保留适用的选择、导航、复制；不得只依赖 TextArea 的样式标志或在 setter 拒绝后声称稿未改。返回的意外变更文本 SHALL 不被采用。disabled 的交互资格仍不同于只读。

#### Scenario: 只读多行收到粘贴
- **WHEN** 原生控件可能处理 mutation 命令
- **THEN** 输入在改稿前被阻止，原稿／模型不变
- **AND** 合法复制与选区导航仍可用

### Requirement: FL08-TEXT 合成输入保留原生权威
文字缓冲、选择、剪贴板、composition 和候选定位 SHALL 使用原生服务，不自造字符编辑器。合成中间态 SHALL 不作为 Live 值；候选确认／取消所用键不得再作为全局 Accept／Cancel。布局变化后候选定位使用当前逻辑坐标；原生 leaf 或请求抛错时，焦点投影仍须完成其既定更新／释放，不因重排串实例。

#### Scenario: 中文候选用 Return 确认
- **WHEN** 原生 composition 正在消费该键
- **THEN** 只完成候选动作，不同时提交表单或关闭窗口

### Requirement: FL08-TAKEOVER 本地组合须声明参与关系
复合控件 SHALL 在普通 blur 之前声明参与编辑的捕获／操作；capture 本身不清稿，只有已确定的有效替代值或 Step 才可接替。另一同 Bind 编辑器或独立 Button 不因相邻而取得清稿权。无效稿不能用 Step 推导有限数；可由真实范围选值或 Cancel 解决。

#### Scenario: thumb 被按下但值未变
- **WHEN** 组合已有修改稿，用户仅开始范围捕获
- **THEN** 持稿保留，不先 blur 发布旧稿
- **AND** 后续实际选值才依明确接替政策处理

### Requirement: L2 精确重叠服从同一有效操作几何
默认 interactive SHALL 提供真实操作区域，Overlay 后绘制的上层在重叠处取得合格输入；纯 decoration 不设 opaque box。配置的可见操作即使临时 disabled/unavailable 仍阻挡下层但不能激活；hidden／removed／未配置操作不阻挡。可选 IUiPointerLayout 的 custom SHALL 与 Draw 共用几何，Child 按真实 Parent 找 owner，不另建 capture／topmost state。旧 custom 无该能力仍合法，但未知私有原生区域的精确首帧遮挡不承诺，不能猜整个外框 opaque。

#### Scenario: 禁用小按钮覆盖大按钮
- **WHEN** 指针落在小按钮实际配置区域
- **THEN** 两者都不发请求；小按钮是阻挡而非可激活
- **AND** 移除该操作后不留下虚构遮挡

### Requirement: FL08-COMPOSITION 操作先于纯外观
UiWidgetParts 的已选 operate SHALL 先运行；owner仍active才在DisplayOnly作用域paint。
自定义可以整体替换内部布局／呈现，不固定私有子树，但必须提供所选行为需要的真实目标与映射：
Button的背景／图标不是多个按钮，Slider的方向／行程／数值到位置映射不能由画法猜。
只有画法变化不改变资格；圆角不自动改hit，直轨改圆弧的取值是输入映射变化。

#### Scenario: operate 回调关闭 owner
- **WHEN** 同一组合随后本来有paint
- **THEN** 不继续正常paint或提交下一请求
- **AND** 不把Close后的未访问子项误当正常删除遍历

## 0.9 public named completion and semantic editing

Named completion/cancellation is owned by the existing field wrapper and UiEditSession, without a Tools
prerequisite. It preserves parsing, conflicts, composition ownership and the existing one-request
boundary. A semantic SetDraft is local; explicit completion submits through the same typed request,
and cancellation reads the actual model without writing it. Another active editor, unavailable/readonly
submission, composition, modal state and host eligibility are rechecked by the owner. Existing native
pointer blur, Live and Accept/Cancel behavior remains unchanged. A handled edit result is not a save.

### Custom wrappers use the same public completion owner

`UiControls.EditSemantic<T>(context, binding, operation, text, format, parse, live, request, readModel)`
is public-unstable. Custom authors provide the same typed binding, codec, native Live policy and
callbacks used by their normal `Edit<T>` call. It owns editor creation/configuration, local SetDraft,
CommitEdit/CancelEdit, Session focus registration/release and node invalidation. No state table or
second edit state machine is exposed. SetDraft remains local even for a native Live field; this explicit
intent does not become a native typing event. The original editor's automatic-publication hold keeps an unchanged later native Live/blur pass from submitting the semantic draft; actual subsequent typing or explicit completion follows its original policy. Invalid drafts remain focused for correction, explicit
confirmation keeps the original conflict policy, and cancel restores the current model without a write.

The entry rechecks composition, effective disabled state, Host input/reliable transaction state,
modal/capture ownership, arranged declaration ancestry and another active editor; readonly submission
is refused. Host semantic dispatch retains target/visibility/command authority. Pending native completion
flags are consumed before the semantic attempt; callback failure cannot leave one queued for automatic
replay. Focus bookkeeping/release and invalidation run on exit, including ordinary callback failure;
no normal frame work is resumed after Host disposal. Existing native Edit/Live/Accept/Cancel callers do
not gain a Tools prerequisite. A handled result is distinct from business acceptance or persistence.

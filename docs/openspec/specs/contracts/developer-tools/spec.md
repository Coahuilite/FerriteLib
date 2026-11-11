# 开发工具、语义访问与同源预览连接
状态：现行，0.9.x；工具接缝在正常单一载体中休眠。公开等级仍由API tiers决定，新增接缝public-unstable。

## Purpose
让开发工具读取和操作作者的真实页面，复用原 Host、布局、编辑和寿命；库提供中立接缝，不内置工具产品。

0.9 区域保留已按 WO-FL09-RETAINED-DISPLAY-20261111 r2 采用、未实施；本页新增区域条款及
相关显式例外仅适用于主动采用的新能力，不表示当前 API 已可调用。行为唯一正文归本页，
采用来源见 [fl09-retained-regions](../../../changes/fl09-retained-regions/proposal.md)。

## Requirements

### Requirement: DEV-01 一个库载体，可选连接
库 SHALL保持单一载体；普通 Host、独立控件、visual-only、R9、日志与既有合法 Dev 仪器不增加 Tools 前提。工具 UI、快捷键、协议、传输、LLM接入及脱机图形宿主由外部工具持有。工具连接只附着真实 Host／Session，不复制业务、节点树或第二个身份权威。新增工具仪器休眠，选择兼容目标并显式启用后才采集；连接退出沿 owner 恢复原仪器状态，不关业务页、不提交草稿。同一Host同时只由一个连接拥有工具资格。断开取消pending；后台断开立即关闭队列，在下次owner回调恢复仪器，Host／Session关闭也关闭连接。已有直接诊断订阅保原条件。
#### Scenario: 不安装工具
- **WHEN** 普通消费者按原入口创建页面
- **THEN** 原功能、日志和合法诊断可用，不要求开发包或工具进程

### Requirement: DEV-02 纯读与一次原 owner 操作
R9 与语义 capture SHALL纯读、有界、注明来源；不创建编辑器、prepare、arrange、draw 或推进业务。人类与 agent 的投影共用原节点事实，缺自定义描述／操作时如实 Unsupported。工具操作 SHALL独立排队，在原 owner 下一合法边界至多取一个 intent，并重核可见、已布局、只读、命令谓词、模态／capture 和当前原生输入资格。

可选坐标读取 SHALL使用原draw完成的pointer区域、popup层序与canonical node，在原logical Host-window空间返回至多一个topmost owner及原资格事实；disabled配置区域仍阻挡，未知custom私有区域不由外框猜测。读取不推进pointer、capture、焦点、选择或动作，不重新收集事实树；新读取需要当前完整有效draw，过期／失败／重排后未draw时明确不可用。命中不是操作地址，capture仍由原owner处理；旧Capture／选择／绘制及独立合法入口不增加该读取的前置条件。

地址 SHALL绑定 Host／Session、definition、mount、interaction scope、part/item；关闭、替换及不兼容重载不复活旧地址。同一连接 requestId 只尝试一次；Queued／Handled／Rejected／Closed／Failed保实际结果，Handled不保证业务接受／保存。owner异常给Failed，超时查询原ID而不重放。只读连接不可排队；后台协议线程仅使用已允许的Queue／Result／Latest／Dispose，活读取与仪器属于Host线程。

连接 queue为64、ledger为256（连接内不淘汰）、targets至多512；到额明确拒绝／截断，不无限保留。R9自己的默认64／8／512／32及上限512／32／4096／128继续由观察规格拥有，不采用某个外部工具的不同默认。
#### Scenario: 重新Capture而已有请求尚未执行
- **WHEN** 同一连接用新capture替换当前地址集合
- **THEN** 旧未排队地址不再用于新Queue，已排队项保其原目标并在owner重核
- **AND** 不改投新capture同名节点；截断／Stale／缺席和关闭分别报告

#### Scenario: 旧字段被移除后执行
- **WHEN** 已捕获字段的mount被替换，同key稍后重建
- **THEN** 旧请求Closed／Rejected且不改投新节点；重复ID只返回原结果

#### Scenario: 完成draw后读取同源坐标
- **WHEN** 同一Host已有当前完整有效draw，工具将观看点反算到原logical Host-window空间
- **THEN** 返回原popup／content层序和clip判定的至多一个canonical owner；配置disabled区域仍阻挡，不从未知custom外框猜测
- **AND** 不选择、不派动作或转移capture；后续操作仍需原opaque地址和owner重核

#### Scenario: 重排后尚无完整draw
- **WHEN** 读取发生于过期／失败draw或重排后未draw状态
- **THEN** 明确不可用且不给旧命中，不为读取arrange／draw；旧语义Capture的原条件不变

### Requirement: DEV-03 同源 headless 与作者准备
headless SHALL复用正式manifest、registry、binding、layout与操作，显式推进而不Draw／加载GPU。作者提供测量来源，结构几何不冒充原生字形。活原生窗口只由实际callback推进，不被工具再headless推进。

可选prepare SHALL只在同一Host显式advance／原生arrange边界调用，包括语义操作后的重排；同一Host仅一个准备附件，读取绝不调用。准备可重复，只设原布局预算，不采业务、采样或递归advance。旧preview构造metadata及无prepare用法保留。初次prepare失败使队列尚未尝试；操作后prepare失败不撤销或重放已产结果；途中关闭终止后续arrange／pump／draw，Dispose沿Host再释放preview资源。
#### Scenario: 展开帮助后同轮重排
- **WHEN** 原语义owner改变帮助展示状态
- **THEN** 同一作者预算在重排前采用；纯Capture不能替代该推进边界

### Requirement: DEV-04 文档、来源与布局输入各守原 owner
完整作者XML／模板／Styles SHALL作为一个候选经原文档事务投影。错误保last-good全文、定义及有效修订；成功后文档定位、页面和工具状态指向同一已采用来源，重复项回原模板。watch显式开启，status读取不做磁盘IO；source-path变化也不能用旧全文假装新来源已生效。

运行布局输入 SHALL针对原声明容器，区分声明值、owner输入、解析值及last-arrangement，不改写XML或definition修订；兼容草稿／滚动由原Session保持，原结构失效沿正常清理。真正作者文档结构更改仍走更新合同。外部工具不取得磁盘任意写权或定义发布owner。

可选非提交预算测量 SHALL复用同一Host／Session的原测量实现、当前目标约束／绑定／Styles／字体与真实Scroll宽度，给出当前候选几何而非旧目标快照。首次节点可在同步借用范围内准备，返回后丢弃暂存，不形成第二业务事实树；不发布成功整页、不钳制／恢复scroll，不消费scroll target或改变已提交身份／布局／输入。未选择下述新纯Build的自然叶测量只采用既有无副作用IUiMeasureParticipant合同，缺能力明确拒绝，不调用旧Measure再假称能撤销业务／缓存副作用。新使用的读者和测量回调遵守纯读与不逃逸借用；失败丢弃候选并保原状态、给实际原因，关闭不被恢复。旧MeasureAndArrange仍在成功发布时立即合法钳制，旧Measure与所有原调用条件不增加这些新前提。

显式区域 participant 的新纯 Build 可在同一非提交预算边界提供自然测量事实，与原 IUiMeasureParticipant 并列；仅该已采用的新消费适用，不放宽旧 Measure 的上述拒绝。候选 SHALL 采用当前表示和真实约束／局部状态，不发布区域版本／缓存／有效性、操作调度或 hit，不清通知；查询 Status／Capture 仍不得调用捕获或推进。预算返回后的借用及失败／Close 规则保持。

#### Scenario: 作者保存无效样式
- **WHEN** 原文档事务拒绝该候选
- **THEN** 页面与来源仍为最后完整有效修订，给明确失败；状态读取不重新加载或发布半候选

#### Scenario: 新目标预算只作候选
- **WHEN** 作者在原准备边界以当前约束／字体显式调用非提交预算测量，并遵守纯读及同步借用条件
- **THEN** 返回当前候选而不发布布局、不clamp或恢复scroll；reader外确定最终输入后，普通成功排列仍立即合法clamp
- **AND** 隐藏／未物化ID缺席，不从上一目标快照补齐；临时node／context／state handle不带出借用范围

#### Scenario: 自然叶缺纯能力或预算失败／关闭
- **WHEN** 新预算遇到既未选择新纯Build又缺IUiMeasureParticipant的自然叶、测量／reader异常或显式Close
- **THEN** 缺能力定位拒绝，其余失败丢弃候选并给原原因；清理也失败时保两方原因，关闭不复活
- **AND** 不运行旧Measure再假称能撤销任意副作用，不发布恢复高度或trip旧节点；未采用此能力的旧Measure条件不变

#### Scenario: 对已登记区域请求当前非提交预算
- **WHEN** 原 prepare 边界按新纯输入合同请求候选
- **THEN** 可以运行必要新 Build／纯测量并得到真实约束候选，但原 region publication／UI 状态不变
- **AND** 旧自然叶没有纯能力仍明确拒绝，读取 Status 不触发该工作

### Requirement: DEV-05 几何与成本保原来源
描框 SHALL来自原draw walk和当前有效clip，区分声明／输入／解析／实际arrangement与draw，不用第二renderer或旧窗口框。几何、timing、作者window-cost分别有原pass／修订及预算；不以一次capture假称同一native帧。

选择事实与选中注记绘制 SHALL可独立控制：显式同步作用域只抑制所选连接的选框墨迹，原selection／mount、frame／definition、window／clip及过期判断继续由原draw walk更新。默认保原框，作用域退出恢复；连接关闭立即使遗留作用域失效，不影响其他Host。此控制不重定义独立诊断geometry overlay，也不以清空重选或省略几何更新实现。

可选window-cost provider SHALL纯投影原sampler的保留事实，请求沿同一有界队列、ledger与串行owner预算，至多一次。附件替换／断开／关闭不改投pending；provider持采样政策、gate、计数及cleanup，库不新增sampler。Unsupported／NotSampled／Failed、nullable未知值、partial与endReason及原pass均保留，读取不启停／推进采样，也不给普通采样／日志增加Tools门。
#### Scenario: 页面没有window sampler
- **WHEN** 工具读或请求window cost
- **THEN** 如实Unsupported或拒绝，不创建替代计时器、不将未知记零

#### Scenario: 干净目标draw仍保选择事实
- **WHEN** 原owner在显式注记抑制scope内完成干净draw，再退出scope作检查draw
- **THEN** 干净draw不画选框，原selection／mount及frame／definition／Window／Clip正常更新，检查draw恢复原框
- **AND** 嵌套／重复Dispose保持可释放，异常沿finally退出；连接关闭立即使遗留scope失效，不改变独立诊断overlay

### Requirement: DEV-06 目标宿主与观看变换分离
采用虚拟视口的原生adapter SHALL消费明确的目标逻辑空间、有效UI缩放／DPI与原生输入资格，沿原窗口测量、裁剪和执行链。工具观看缩放／平移仅由工具合成层持有，不成为库布局输入、不改definition或业务状态。库只提供真实缺口所需的中立接缝，不包含消费者名称、页签、地图或工具窗口政策；不预先规定RenderTexture-only或新增核心程序集。

正式窗口的原native入口与隔离宿主 SHALL复用同一窗壳绘制、内容矩形和单Host寿命实现。原native入口保留其virtual／event、关闭veto、键盘／catalog与实际输入资格。隔离preview显式声明窗能力，采用其已有同一Host，不另建树或提交作者文档。attach在首draw前、detach在Host释放前；创建／attach／page失败释放Host，下一pass给终局notice而不重试。preview与窗壳不得重复拥有Host释放；执行后端与作者资源维持明确的借用／所有权，关闭走原owner。旧page-only preview构造、注册和provider条件不增加窗能力前提。
#### Scenario: 工具侧栏挤小观看区
- **WHEN** 目标逻辑空间未变，仅观看区域缩小
- **THEN** 原Host布局预算、节点身份和草稿不变；工具负责显示映射

#### Scenario: 隔离完整窗采用既有Host后退出
- **WHEN** 作者显式采用已有Host，随后正常关闭、attach期间关闭或page失败
- **THEN** attach在首draw前，detach看到尚存活Host；Host与preview资源沿各自owner释放，不双重Dispose，不提交草稿或作者文档
- **AND** 失败不自动重试；isolated Dispose终局，原native同实例关闭后重开条件仍保留；借用执行／metrics／业务owner不被转移

### Requirement: DEV-07 新原生开发跟随 1.7
新原生适配 SHALL以RimWorld 1.7为目标，跟进当前命名空间、程序集和API，不按小构建号／rev／游戏MVID白名单锁定支持。实际缺少接口或资源时报告具体不兼容并修复适配，不静默载旧游戏依赖或换近似绘制。该政策不放宽FL API区间、同进程单载体或旧1.6合同。自动记录实际输入只用于诊断／证据，不给用户增加小版本管理步骤。
#### Scenario: 官方移动接口命名空间
- **WHEN** 1.7更新使旧引用不再编译
- **THEN** 对当前接口修订adapter及受影响调用；原证据保原身份，不以固定旧构建作为产品路线

### Requirement: DEV-08 编辑完成与局部寿命沿原状态机
0.9的公共CompleteEdit与Controls.EditSemantic SHALL使用原formatter／parser、typed request、readModel、Live政策及UiEditSession，不另建agent编辑器。SetDraft仅局部，Commit至多一次业务请求，Cancel还原当前模型且不写；无效稿保留纠正。组合输入、只读、模态／capture、可靠事务及实际焦点资格由原owner重核，返回handled不表示保存。

显式EndElementLifetime SHALL只退休点名子树，按原Session路径释放输入／资源，不完成编辑或改变业务；兄弟草稿／焦点／滚动保留。下次合法推进可重挂新incarnation，旧排队地址Closed。折叠、帮助量高或尺寸改变不以结束寿命替代正常布局。
#### Scenario: 退出局部编辑区域
- **WHEN** 作者明确结束一个已挂载子树
- **THEN** 该子树旧地址失效且不提交draft；其余页面继续，重挂不复活旧请求

## 实现与用法
[连接指南](../../../../development/0.9/tools.md)给当前签名、注册与构建方法；[实现状态](../../../implementation.md)区分源码机制、后续舞台消费、原报告和未验宿主。


## 本次已采用目标与Tools交接

[基础能力change](../../../changes/fl09-basic-ui-capabilities/proposal.md)仅持已采用、未实施的双轴／画布／后续焦点目标；
已有CoordinateHitTest v1、窗壳、选框抑制与MeasureBudget仍按本文及指南可用。当前公开语义、源码版本和限制的
[实现对账](../../../implementation.md#fl09-tools-semantics)分别覆盖通知、缺席／寿命、空间、预算、新能力、退出。
Tools不以新目标发明签名，不绕过Stale／在读中推进，也不取得借用execution的释放权。

## 0.9 区域保留：已采用、未实施

本节仅约束主动采用的新能力；其余原条件保持。新增闭包已定 Public-unstable，尚无对应导出。

### Requirement: DEV-09-REGION 区域观察只读同源实际记录
区域 status／Tools 投影 SHALL 来自原 Host 的有界记录，区分 source／layout／最后实际 Paint
版本、当前失效、Unknown／Fallback／Failed／Detached／Closed 及实际原因，不包含 live handles。
有效性只描述已观察的合法边界和已知失效；未公告的外部变化不能被纯查询自动发现。
已启用的既有仪器可报告边界捕获、纯 Build、区域／调度复用、interactive／unknown 操作及
Paint callback／裁剪计数；未采样为 NotSampled，不把 PaintCalls 当 native 调用、GPU 提交或 FPS。
读取 SHALL 不 capture 来源、Build、arrange、编辑、重试或创建第二棵事实树。
region 保留不伪造当前完整 draw；原 DEV-02 坐标读取和原操作队列的可见／mount／资格重核条件保持。
连接退出不关闭业务页或改变其保留策略；不采用 Tools 仍可使用新区域与基本定位状态。

#### Scenario: Build复用但当前draw尚未完成
- **WHEN** 工具读取该区域及坐标状态
- **THEN** 可以如实报告输入／纯结果复用，坐标仍遵原不可用／Stale条件
- **AND** 不为给Sampled而调用source或draw

#### Scenario: native性能报表比较开关
- **WHEN** 记录区域复用与实际绘制计数
- **THEN** 各指标保原pass／候选／边界，另报完整帧与Host父段及实际绘制延迟
- **AND** 不用不同列中位数相加、不把未知GPU／scanout记作零

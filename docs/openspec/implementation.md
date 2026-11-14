# 开发映射、现状与开放偏差

这是源码／证据映射，不是第二份规范。现行要求从[入口](../specification.md)到达；
[来源覆盖](source-coverage.md)给旧条款和当前唯一归属。非本次修复段落的历史行号为一基，源基线
cf1cf6456096171fb47c012369bcd0c2f29e968f。FL-F01/F02/F03 已在该 HEAD 的 dirty 工作树修复；
下列对应段落区分实际源码机制、已执行既有检查及未执行原生／精确故障组合。

## 0.9 双目标载荷：编译与本地试分发

正式采用提交 `5d12091` 先更新唯一 current 的 FL08-CARRIER／DEV-07，随后才接线包元数据与脚本。
0.9 保留普通默认 RW1.6/net472 和显式 RimWorld17/net10.0 两个实际编译入口；Neutral不进游戏包。
原 stager／exporter／release packer增加目标选择，实际读取DLL的configuration、TFM、游戏引用、
来源身份及MVID；单目标只公告其版本，Dual要求相同源码身份、分别置于1.6／1.7目录。
每个游戏只选匹配版本的一个DLL，归档不含游戏引用或共享根DLL。0.8源文件和原1.7编译路径未改。

两个普通Release均从干净 `5d12091` 构建，零警告／错误、无测试钩子；后续改动仅在包／脚本／文档，
未改变产品C#或构建配置。固定载荷及来源／MVID收据在本地忽略目录 `dist/pub14-dual-target/`，
程序集producer与后继包装脚本commit分开记录，不用docs-only HEAD冒充DLL来源。
实际Dual归档、两个单目标stage、1.7 export及原有carrier-export／license／Version／readonly-api检查通过。
export首次因PowerShell参数与旧destination局部变量同名失败，改用独立变量后原检查通过，断言未改。
这是普通编译／本地包路由证据，不是发布、安装、1.7游戏输入／绘制或165FPS验收。
使用方法见[目标选择](../development/0.9/tools.md#select-a-game-payload-or-a-dual-target-archive)。

<a id="fl09-repeat-declarations"></a>
## FL09 Repeat 声明身份：1.6 r5工程证据，165 FPS未达

规格候选 `51dceef`、正式采用 `6cc62e0` 后，WO-FL09-RETAINED-DISPLAY-20261111 r9 仅放行
[FL09-REPEAT-DECLARATIONS](specs/core/structure/spec.md#requirement-fl09-repeat-declarations-未变声明不因值或开合递归重建)。
源码只在原 RepeatState 保存声明结果，未采用 editor 实验、未改公开 API／原 reader／prune条件。

- `CollectRepeatItemIdentities` 与隐藏路径均经 `CollectRepeatDeclarations` 处理完整实际 rows。
  仅显式附件仍有效、源已知、隔离后端且直持 UiBindings 时建立新记录；Unknown、forwarding reader、
  未采用及无隔离路径保原完整递归。实际 MaterializeRows／VerifyReaderSubtree 未省略或改凭据。
- Repeat 层核实际附件、node／parent 与 mount、声明及实际模板对象；每行核稳定 key 对应的
  immutable 派生 spec、规范 rowId 和仍存 node／mount。检查不深遍历子定义，不依赖 packet 引用相同、
  view／值／draft／开合或尺寸未变。相容重排沿原规范身份；新身份／模板／mount需重建相应声明记录。
- 冷首建仍调用原 `CollectChildIdentities` 完整递归，保存行根及全部声明后代的身份数组，包含隐藏后代。
  暖命中只做行边界核对和完整身份合并，不递归派生子身份；未把递归挪进 proof。
  仍复制候选 row-record 字典／合并全部 keep 身份，未承诺事务 O(变化节点)，冷建有额外记录分配。
- full 的 RepeatState 副本只共享不可变行结果；候选字典增删不污染 rollback。实际 key移除同步退休记录，
  `PruneNodesExcept` 的完整 keep 和调用时点不变。定义重建、显式 EndElementLifetime 沿原引擎退休；
  Close 中失败回滚恢复旧 state 时额外清掉声明 owner 引用，不复活已退出寿命。
- 当前合法 Host 模板在 `ValidateTemplateIdentityGrammar` 拒绝内含 Repeat，原既有拒收检查保持；
  本次不放开嵌套 Repeat 词汇，也不以静态外层证明给未知动态嵌套背书。嵌套容器／区域沿原身份树处理。

已有 Repeat／item binding／visibility／failure-lifetime／document reload／layout reuse／shared editing／
window host、readonly-api 与 Core 391 检查通过，无新增用例。普通 RW1.6 net472 固定载体由 owner
交付记录持 producer／MVID；本条实现不改变原 r4 配对的已接受停手行为。

固定普通 Release producer 为 `a9d9f5f3a1d87044987a8bf6d77cbc27ecdde01f`，实际 MVID
`83d7162b-c701-4032-8800-aad838b76447`，InformationalVersion 为 `0.9.0-dev+` 加该完整 SHA。
从干净提交源 --no-incremental 构建，UiKitTarget留空、FerriteTestHooks=false，零警告／错误；
实际引用 Krafs.Rimworld.Ref1.6.4871／Assembly-CSharp1.6.9676.17735，FER_TOOLS且无FER_TEST／FER_DEV。
后继 docs-only HEAD不刷新该载体；旧 r3／r4 固定配对保留作原生基线，新载体先供 US 重编。

选择范围的原始证据是一次r4暖开58.449 ms中的声明收集9.820 ms，不把它当作净节省。
后续1.6 r5真实暖开未重派未变行子身份；Host p50为7.398 ms，1413/1413样本仍超过6.06 ms，
165 FPS未过。该工程证据不扩展为net10／1.7原生验收。
原 `CollectRepeatItemIdentities`、`CollectHiddenRepeatIdentities`、`CollectChildIdentities`
边界保留供既有仪器按调用祖先归因；页面静态 `CollectDeclaredIdentities` 的递归仍存在，不能混算为行子树。
未新增常驻计数；MeasureRegion余数、Draw及总帧节奏的后续工作保原责任边界。

<a id="fl09-retained-regions"></a>
## FL09 显式区域保留：实现候选，原生待验

WO-FL09-RETAINED-DISPLAY-20261111 r3 在 FL `8f02776`／US `3e32a57` 的独立采用提交后放行代码。
新增闭包为 Public-unstable，旧 Stable inventory／签名／默认不变。接口和作者规则唯一归
[region-api](specs/contracts/presentation-execution/region-api.md)及各能力 current；change 保留采用来源。

### 实际执行链

- `UiHost.MeasureAndArrange → UiRegionRuntime.Begin → captureCurrent → UiLayoutEngine.ArrangeRoots`。
  Capture 在每个原合法同步边界实际调用；普通 getter／request reader 不被包装或替换。Value／Choices
  为独立 typed root/item 通道；显式结构端口消费 mapped Visible／Width／Tab／Items，错误不伪装成旧读取洞。
- `UiLayoutEngine.Regions` 在实际结构作用域保存局部 box、分项 display、原 topology／Repeat／依赖收据。
  当前 root/item 不可变对象、reader owner、定义、约束、继承属性 generation、有效字体及其度量环境、
  language／theme 和 UI revision 均有效时，从作用域边界返回，不进入后代 Configure、需求准备、Build 或测量。
  增量入口在建立 PipelineContext 前跳过整个已证明 span。环境按实际使用的不同字体合并验证，
  包含 Build 直接调用 ctx.Metrics 的字体；未知 ruler 不获此证明。
  可变 resolved theme 的真实 revision 写边用弱 node owner 推进失效，不为察看样式重走后代 context；
  全局 theme 保原时钟。直持 UiBindings 的新区域另核其实际注册寿命；任意 forwarding reader
  不获端点稳定证明，保原真实遍历校验。没有移植0.8 DerivedRow 凭据，也不改旧 reader 使用条件。
- 分项 packet 改变或 UI 脏时，原 full 几何事务计算变化闭包，未变 item／子分支 box 仍复用。
  原 Repeat reader 校验保留；成员／次序、共享列、后续位置、Scroll 双宽度遍历／范围／clamp 沿原 owner，
  不用静止外框代替尺寸证明。缓存只在候选成功时保留；异常恢复原缓存及几何，不回滚已完成业务操作。
  每个作用域最多保留两个实际约束变体，避免 Scroll 的预留／未预留宽度互相挤掉未变行；
  当前采用的变体保持在 head，后续验证不误用另一宽度的字体／环境证明。idle 增量成功不复制 region 缓存表。
- 自定义 `IUiRegionParticipant` 的 Build／局部 pointer／自然高共享同一 display；Operate 保每事件原次序。
  内置 wrapped text、button、rule 有明确表示端口。Dropdown 的无业务天然测量可随布局保留，
  其旧 Draw／choices／资格／pointer 仍是实时洞。未映射消费、未知 widget、旧 diagnostic／自定义 natural-body
  回调保原读取／计算域；它们不因旁边接入 participant 就变 pure。
- Draw 仍通过原事件／hit／help／disabled／popup 漏斗。已证明容器和 participant 均复用已准备 context，
  不复用资格或命令结果；滚动时转换当前 origin。native editor、输入、help 和 unknown Draw 不省略。
  有展示隔离的后端只在 Repaint 调用纯 Paint；有效 clip 非空且没有完整输出证明时，屏外 layout rect 也照画。
  PaintClip 通过现有 drawing.Clip 强制，平移 drawing outlet（含 numeric text／嵌套 clip）以保原逻辑坐标；
  不包住 Operate。没有隔离能力／当前无原生执行的普通入口保持旧 Measure／Draw。

### UI 状态、故障与退出

原 edit owner 比较已有 draft／focus／modified／conflict／composition，不额外读取 Valid／parser；
新 editor、formatter/parser 改变和焦点挂起也使已采用节点失效。custom 其他状态仍由原 Invalidate 写边负责。
私有 ctx.Child 控件沿实际 Parent 找到所属 attachment；证明还核对真实 node／mount incarnation，
不凭同一逻辑 ID 把已退出节点的几何／状态借给新 mount。
Build／Paint 的新契约禁止普通业务读、UI mutation／帮助／请求；guard 拦截控件／editor／state 创建、
Node invalidation／PublishHelp 及经原 bindings 的写／命令。作者仍须遵守纯 callback 合同，
没有拦截普通 Get 来改变其含义；旧调用不获得新纯度前提。
Build 失败保最后成功 Paint 的不可变资料或恢复带，停止该失败片段的 Operate；不凭相同失败输入反复 Build。
纯 Paint 失败锁存，相关输入／环境／UI／Retry 才重新派生；操作失败仍沿原 request／guard 回复，不自动重发。
来源或几何失败丢候选，恢复绘制不运行旧 Draw／Operate、普通 source read、popup/native editor；
只画最后完整静态结果／恢复带并保 fault，可靠同步后恢复输入。纯恢复自身失败不会每 pass 重试。

候选删除不提前 detach；文档 Seal 后的 root 删除／不兼容替换、显式 mount 结束和 Close 退休记录。
附件退出请求原路径完整重验，保相容 editor／Session，不 Save／blur／Cancel；Close 终止后续回调和发布。
同 Host 的 Capture／Build 重入在推进前拒绝。`MeasureBudget` 在原非提交 scope 内使用私有计算 engine／
region workspace：可用新纯 Build，但不改实际 attachment 状态、source/layout clock、缓存、clamp 或业务。

### 观察、载体与证据边界

Attachment.Status 与原 Host.Observe／Tools 的 `region-status` facts 来自同一有界 owner 记录；
只描述已观察边界，读不 Capture／Build／arrange。`region-work` 在原 timing／detail 仪器显式启用时统计
BoundaryCaptures、Builds、ReusedRegions、ScheduleReuses、InteractiveCalls、UnknownCalls、PaintCalls、
ClippedDisplays；否则 NotSampled。PaintCalls 不是 native 调用、GPU 提交或 FPS。

原 r3／r4 普通 net472／RW1.6 Release producer 为 `f5e022ee56a981f7d2fd2d09b339654ee6ae65c6`，
实际 MVID `00b1ed28-9ff5-4d5a-b876-90c82d75a56c`，InformationalVersion 为该完整 SHA 的 `0.9.0-dev+` 值；
干净提交源、UiKitTarget 留空、FerriteTestHooks=false、--no-incremental 构建零警告／错误。
固定目录由 producer READY／DELIVERY 持有，不从后续 docs-only HEAD 推断或覆盖；早期 `d707ddf` API 载体和 `d2a2e82` 中间载体保历史。
针对性既有布局／Repeat／编辑／输入／API／containment 检查、中性 391 检查、
adapter 140 assertions 与 execution-lifetime 20 已有通过记录；
最终候选与检查范围随 owner delivery 一起交付。未新增测试／case／等价 probe。

本节是源码及 managed 证据，不是 F09 原生签收。交接前已鲜读 US 的纯 Build 短字段接线，
旧 Operate 仍保完整绑定地址；实际消费引用与完整生产 source 的可信鲜验／分项复用由 US owner 负责。
CONSUME、同代固定配对 NATIVE、整帧／首次及暖态性能和 165 FPS 仍由各原 owner／PM 继续。
历史 1.6 A/C-r2、144 Hz、独立60Hz 或编译通过不签0.9原生，更不签1.7；GPU／scanout 未测保持未知。

<a id="fl09-event-tiering"></a>
## FL09 事件分层执行（2026-11-11）

[采用来源](changes/fl09-event-tiering/proposal.md)及其 design D1–D7 已进入 0.9 源码；
现行正文归 presentation-execution FL09-TIERED／TIER-SAFE／CLIPPED-DISPLAY、structure
FL09-PAGE-EXECUTION、binding FL08-ORDINARY 与 adapter FL16-DISPLAY-ISOLATION。
本节只记录实现和证据，不把保留 1.6 的隔离能力扩成 1.7 支持声明。

- `UiHost.DrawFrame` 持每 Host 的 Repaint 周期，使用输入 `UiFrame.Phase`，不读平台帧计数。
  首 pass／Repaint／已宣告变化后的 pass 同步，其他 pass 复用该周期最近的成功 snapshot。
  `PipelineInputsCurrent` 与 `TryIncremental` 共用尺寸、definition、translation、theme 和身份检查；
  pending validation、dirty nodes、当前依赖 revisions、tracked demand、已完成业务请求另使周期重同步。
  同步点以独立的 `synchronizeInputs` 要求鲜读，不把一次同步本身记为外部失效；已宣告变化
  仍走原 pending validation／dirty／revision 路径。普通布局输入、旧 Measure 与未知 ruler
  在合法同步点鲜验，相等结果可保留几何，不再仅因测量 Pass 改变而提交新的 measurement。
  手动 `MeasureAndArrange` 保原入口并退休 DrawFrame 的周期复用；Tools prepare 仍逐 pass 调用。
- IDLE-SYNC 采用 1.6 owner 的 `ce0028d`／`a3d4616` 机制。成功 full 遍历记录实际需求顺序，
  鲜验按该顺序检查 Visible／Width／Tab、Repeat、Auto 与共享 label；首个控制条件变化立即回 full，
  不枚举历史字典去探读已隐藏或退出的后代。滚动条可改变响应式读取域的情形保守回 full。
  0.9 的同键 Repeat 仍调用原 `VerifyReaderSubtree`，不引入 0.8 的 DerivedRow 凭据。
  demand-order 集合、记录及额外宽度域扫描仅在 tiered 同步收集；旧非 tiered 入口不承担这些分配。
  候选记录由 full 事务的 try/finally 管理，失败不发布；非提交 budget guard 与原失败恢复保留。
- `UiLayoutEngine.Draw` 复用同步后的内容命中几何与 entry context；scroll 位置改变仍同 pass 重收集。
  hit pass、命中层发布、即时资格、Cancel、help 与 widget `Draw` 保逐事件执行。
  pointer producer 的 presentation 可用性改变请求下一 pass 的输入验证；失败不留半份 layer cache。
- 库内部 `IUiDeferredDisplay` 只分开三个内置 wrapper 的 operate 与 display。
  `UiSessionGuard` 把 Deferred 与成功区分，保 presentation fault 和最后成功 paint；恢复展示同样隔离样式。
  Schema 2 的普通 `SelectedBind` 仍在原 pass 读取；静态／tracked 文本和 button／checkbox 的 getter-free
  展示段才取得默认省略。转发 reader 不因 owner 的 tracked 标记被擅自认作 getter-free。
- `UiLayoutManifest` 接受 Schema 2／3，保解析后的 SchemaVersion，定位拒绝非法／非 root TieredEvents
  及非 Overlay／非布尔 DeferClippedDisplay。`WithTieredEvents(bool)` 是唯一新增公开签名，
  返回不改原页的显式策略；旧 Create overload、其默认值与 stable 签名保持。
  PAGE-ATTRIBUTE 返修在页面的 `ReadSubtree` 边界用 `XElement.Load(..., SetLineInfo)` 保留原坐标，
  对内嵌 Styles 的全部后代检查保留属性 TieredEvents，再交原样式解析器处理外观词汇。
  错置属性成为带原页行／列的定义错误；独立 UiStyleDocument 与其他未知样式属性保持原宽容策略。
- 保留的 1.6 `VerseUiExecution` 隔离 `Text.fontStyles` 三个 label style 的 alignment／wordWrap／fontSize，
  `finally` 恢复，不恢复合法焦点／hotControl 变化；沿用该单项 containment allowance。
  FER_RW17 不声明隔离，内置 display 保逐 pass 回退；T2 周期仍可使用。
- 四项 candidate-C 实现分别移植：unscoped draw context wrapper、window-local 相等字体环境 snapshot、
  leaf context/source representation、full-layout authoring snapshot。
  0.9 专属非提交预算检查保留；0.8 原生性能数字只属于原候选，不作为本线测量。

新增 `KernelEventTieringTests` 覆盖周期边界、T1/T2/T3、声明变化、Repaint 普通鲜读／未知 ruler、
Schema 与程序入口、隔离回退、clip 继承／复显、fault retained paint、control 顺序、Cancel 与 help grace。
既有相关 suites 和 readonly-api 的 managed 结果随 change tasks 记录；
初版的 Release Neutral／RimWorld17 与 Core 使用项目原入口；这些是历史构建证据。
native 对比、实体键盘和 IME 接受仍归 task 7，
没有在本工作中运行游戏或据此宣称原生性能提升。

PM 的 2026-11-11 验收裁决已接受 `WithTieredEvents(bool)` 为 Stable，但未接受原 T 的静置／低速回归。
WO-UIKIT-IDLE-REGRESSION-20261111 的 PAGE-ATTRIBUTE 修法已先独立交付；IDLE-SYNC 在保留
0.9 Schema／发布状态的前提下适配上述唯一 1.6 设计。当前修复的受影响既有检查与普通
net472／RW1.6 Release 身份由该轮交付记录；本轮不构建 1.7，也不运行游戏。
PM 提供的 1.6 stable-page full=0 和 C15 编辑恢复属于其原生候选，不证明 0.9 原生性能；
总 FPS 的其他父段帧节奏及本线 native 接受仍待各自 owner。

<a id="fl09-reliability"></a>
## FL09 R1 修复与已采用目标（2026-11-09）

本节依据实际dirty源码与冻结v3完整source-form，不沿用上文历史源基线。采用来源见
[来源覆盖](source-coverage.md#fl09-capability-adoption)；[change](changes/fl09-basic-ui-capabilities/proposal.md)的扩展目标仍未实施。
R1 已按 WO-RELIABILITY-CLOSEOUT-20261109 r1 落源码；下文原运行保其历史身份，新工件不覆盖旧包。

### R1 通知依赖生命周期／布局提交与观察资格不一致

**状态：源码修复、生产构建／既有检查及原宽通知 caller 的准确新工件 after-success 完成，交 CO／PM 接受。**
允许普通宽松通知，不加“当前布局／实变值”条件。修复只改变内部通知／publication 账：

- `CommitNotifications` 对当前 `keyNodes` 采样并标 dirty，不再提前确认 `committedRevisions`。
  本次稀疏 `candidateRevisions` 经 full 或 stable incremental 的成功边界才发布。
- full 成功才退休退出当前依赖域的历史键；失败保旧成功域。新／复显键首次读取前采样，
  visibility／width／tab 同步采用；同键多位置和增量转 full 保最早采样，晚通知仍待后续合法成功。
- bindings 的业务值与 revision 不清零，其他 Host 独立确认。Close 的原 cache release 也清候选账。
  旧 observation 失效条件和 last-good／输入可靠性分离不变；没有新增公开 API、版本或采用前提。

隔离交付 `dist/reliability-20261109/fl-a001-r1/` 含 Release Neutral／RimWorld17 固定载体、
完整实际 source-form、IDENTITY 和日志。两目标生产构建通过；既有 Core 391 项通过；既有 net472
LayoutReuse／Visibility／FailureLifetime／Observation／Invalidation／ContainerTab／API tier suites
通过。net472 harness 使用 FER_TEST，区别于交付的无 FER_TEST 正常载体；它是 managed stub 证据。
RPT-RELIABILITY-TOOLS-A001 r2 已由获准 caller owner 对接新载体；FL owner 核对了实际载入身份及原件。
原 `slice-001` provider MVID `9c47da59-e64d-43d3-ae58-f87c943e24c6`，保原宽
`NotifyChanged(notifications.ToArray())`／`Host.Invalidate` 和未改的概率 timeline；实际载入
本线 Native17 MVID `54659e82-7b01-4e3b-b338-9b40a95a11a2`。原04死亡0.8时121个Stale几何／
targets0，现同一时点121节点／30能力、Stale几何0，仍可请求发狂0.9；实际预算值保持死亡0.8／
发狂0.19999999／成功0%。新进程九时点均同Session、Host4／definition0、Sampled、未截断；
条件关开节点121→79→121、能力30→20→30，七个动作均各有同request终态及实际读回。
证据为 `r1-raven-slice001/identity.json`、`comparison-and-action-chain.json`、02～10原始moments；
旧before仍为 `raven-slice001-consume02/04-death-eighty.json`，不是005／006的consumer过滤后成功。
Tools也更换为本批构建，且旧逐键账缺失，故具体历史pending键仍未唯一定位。

该after是独立Background Native17：visible=false／focused=false，正常close的cleanupCompleted=true、
exit0及descriptor closed。没有扩成Neutral真实caller、Game17、准确1.6、物理输入／IME或异常Dispose证据。
本次未发现需追加代码修复的反例，未重跑未变检查；旧冻结载体及包未更新。

以下为**修复前** Kernel／冻结 carrier-v3 的 Phase A 文本对照，保留缺陷与旧运行的可追溯范围：

| 实际路径／符号（一基行号） | 机制／影响 |
| --- | --- |
| `Kernel/UiBindings.cs:670–693 NotifyChanged` | 只验非空key；重复公告推进revision，注释容纳generous通知；不验当前可见或值是否变化。 |
| `UiLayoutEngine.cs:404–447 CommitNotifications` | 只确认当前keyNodes；`:216–231`full重建当前键表，`:301–320`剪布局输入缓存但未剪历史revision。 |
| `UiLayoutEngine.cs:1231–1243 VisibleChildren` | 隐藏祖先仅记录自身声明，不递归后代；直接隐藏叶与隐藏整段不同。 |
| `UiLayoutEngine.Observation.cs:225–243` | pending遍历历史committedRevisions；layout公告落后即整体Stale，reason还合并dirty／content／definition等其他分支。 |
| `UiHost.cs:752–769,980–997` | 合法UpdateDefinition走prepare/stage/seal并重建engine，能改变历史域；不应强制消费者用它代替合法Tab隐藏。 |
| `UiLayoutEngine.Tools.cs:7–36` | 非Sampled观察不给新targets；这解释工具能力受阻，不证明物理输入／Rect数值／业务写失败。 |

具体静态成立条件：同engine曾见k，k退出当前提交域，后有Measure/Structure公告令历史revision落后；
在k不重进域／不换engine且无其他确认路径时，可持续pending。只清历史账也可能漏掉复显或其他同key真实依赖，
故修复须保每Host／多owner、成功publication、提交期新公告、失败／Close的完整义务。

### 原运行与反证（转录，不要求外部仓库）

固定FL v3 Native17 MVID `76ae2e76-4b33-406d-853e-977a7f4fd44c`，Neutral `5ea548d4-5a81-4650-97d5-e1225442f1b6`；
Release/net10.0/x64、FER_TOOLS，API0.9.0／`[0.9.0,0.10.0)`；其库内IDENTITY／USE／完整source-form入口在下文stage-evidence。

| 原件身份与局部文件 | 原观察／可用结论 | 不得扩大 |
| --- | --- | --- |
| Raven slice-001，RPT-DEMO-RAVEN-A001 r1；`raven-slice001-consume02/03-ember-scrolled.json`、`04-death-eighty.json`、`unavailable-emberBloodBerserkChance.control.json` | death0.8／berserk0.19999999／成功0%；121个geometry全Stale、targets空、observation与targets均未截断。layout-frame82→120→442仍未恢复。 | 高可信指向上列机制，未运行记录实际pending key／两表及完整通知时序，非唯一根因证成；layout frame也不证明k已commit。不能怪默认128或称物理输入冻结／漏绘。 |
| scene-002；A002基线COVERAGE更正；`raven-scene002-consume02/21-Base-before-conditional.json`、`22-Base-mimicry-off.json`、`23-Base-mimicry-restored.json` | 全量通知仍在，targets51→41→51、geometry222→180→222均Sampled，definition11；切页换engine。 | 推翻“全通知必失败／必须先换002”；非单变量实验，未复验旧概率连续路径。 |
| scene-003；A001 r1 | 通知收敛候选已构建，自身native未验 | 不借后续版回填其after-success。 |
| scene-005/006；RPT-DEMO-RAVEN-A002 r2；`key-state-series.json`／`mimicry-state-series.json` | 005死亡0.8后发狂0.1／成功10%，targets54／stale0；006拟声关开52→42→52／stale0；consumer成功可复用。 | 继续同v3，定义／通知过滤也变，不证明FL核心修好；18项Drawn不等于全像素、物理输入／IME或Game17。 |

Nivarian原r3与v3/v4/v5/v6状态支持既有布局／详情组合：v3进度0.35／0.2及scroll156，v4作者Cols3→1→3保态，
v5分页／XML详情／六处HitTest后独立owner动作，v6窄栏Small/16全文高53、原动作0.35→0.45。
物理鼠标0；Button平移／缩放和Rule组合不等于通用视口输入。固定v6 MVID `f3e27ba6-f64e-4454-941d-76ae6c31c89d`，
v5 `7f21668d-b219-446e-a8a0-5add5bf964b4`，不同Session不拼接。

### 新能力实现差距

当前`UiLayoutEngine.MeasureScroll:1481–1537`按自然纵高／纵条预留测量，无作者横extent合同；
`UiExecution.cs:6–31 UiFrame.Wheel`为标量，`Adapters/RimWorld16/VerseUiExecution.cs:26`取delta.y。
新双轴完整范围／轴仲裁、有限transform／空白抓取、变换内editor/caret、交互详情／完整K1及OS桥都未因本次采用而实现。
inline详情用既有结构；shader、研究拓扑／业务及运行期InfoCard／Def保持库外owner。实施顺序和退出条件由
[0.9计划](../development/0.9/extension-plan.md)持有，不以本表授权实施。

<a id="fl09-tools-semantics"></a>
### FQ1–FQ6公开语义与实际来源

| 交接项 | 现行合同与实际来源 | 当前可用／限制 |
| --- | --- | --- |
| FQ1通知 | ORDINARY、RU2-B/E、R9-FACTS；上列R1机制 | 旧宽松公告保留；R1源码及原caller准确新载体after-success完成，待CO／PM接受。Tools不在Capture里推进或无视Stale。 |
| FQ2缺席／寿命 | END/H1/H2、PREPARE/STAGE/SEAL；`UiLayoutEngine.Observation.cs:23–49`、`UiLayoutEngine.Tools.cs:53–99` | 存活Host的未知／未物化子树可NotFound；retained稿可读但geometry/value NotSampled。真实退休新mount不继承旧能力；definition变化重捕，不改投。Tools的“NotFound都已关闭”反馈应在其产品修，FL不改义。 |
| FQ3空间 | DEV-02、R9-FACTS；`UiTools.HitTest.cs:25–104`、`UiLayoutEngine.cs:552–615,826–860`、`UiToolSelection.cs` | 已有CoordinateHitTestVersion=1用completed-draw Host-window区域／clip，popup优先；selection读原Window/Clip。R9 page-local不能当Host点，`UiWidgetContext.ToWindowRect:263–265`只适用真实draw context，不是任意R9投影服务。 |
| FQ4预算 | R9-STATUS/DISCOVERY、DEV-02/04；`UiTools.cs:130–181,211–229`、`UiLayoutEngine.Tools.cs:7–36` | R9默认64/8/512/32，上限512/32/4096/128；targets512／queue64／ledger256各独立。新Capture换未排队地址，pending保原能力再重核；精确满额也可保守提示截断。MeasureBudget v1纯participant仅限新预算，不倒灌Observe／旧Measure／provider。 |
| FQ5新增 | 本change的layout／interaction／1.7 adapter ADDED；上列当前Scroll／frame | 已采用未实施，不猜API／版本。neutral持语义／UI寿命，adapter持真实输入／资源，Tools持观看；深平台证据单列。 |
| FQ6退出 | END/RELEASE、STAGE/SEAL、DEV-01/03/05/06；`UiTools.cs:248–270`、`UiWindowShell.cs:220–269`、`UiPreviewInstance` | detach在Host活时、随后释放Host／自身preview资源；后台断开关闭queue而owner回调恢复仪器。Host不Dispose借用execution；platform owner释放自建execution。原native同实例重开保留，isolated Dispose终局。 |

content区域用`Neutral/UiPointerLayers`的正尺寸、左上含／右下不含与clip交集；disabled配置区仍阻挡，未知custom外框不猜。
HitTest只读、不选择／执行／转移capture，不是操作地址；同frame/definition/content/mount和有效完整draw条件只属于该新读取。
旧Capture／R9和独立Session条件不变。新viewing反算与Tools自有资源释放归Tools，实际输入／资格仍由原owner重核。

本节是静态交接；原Native17离线证据、Game17构建、物理输入／IME及旧1.6/c3-dev欠验仍按下文原边界。

## 0.9 舞台连接现状

[开发工具连接](specs/contracts/developer-tools/spec.md)持要求；[指南](../development/0.9/tools.md)持签名／用法。
正常单一载体包含休眠FER_TOOLS；工具产品、目标视口与观看合成归Tools。完整窗口的隔离Native17
纵切已有后续消费，不再待实施。新原生开发跟随当前1.7接口，不锁小build；0.8／1.6、Neutral、
Native17与Game17分别判断。下列为2026-11-08静态对账，未构建、运行或刷新冻结包。

### 已有保证、真实新增与恢复

| 连接／实际源码 | 合同差量及保留条件 | 直接消费者 |
| --- | --- | --- |
| `Adapters/RimWorld16/UiWindowHost.cs` → `Kernel/UiWindowShell.cs` → `UiPreviewInstance.WindowShell` | 原native外框／内容几何／fit与Host寿命抽到单一实现是既有保证恢复／共享；七参preview显式采用同一Host是新增可选能力。原virtual/event、关闭veto、catalog、键盘资格及旧page/provider构造保留；native同实例关闭后重开已恢复，isolated Dispose终局。 | 正式US窗与隔离shell共享作者presentation；Tools的`ToolsApplication.DrawNativePreview (ToolsApplication.Windows.cs)`每次只调用一个shell/page分支，不另建树。 |
| `UiToolSelection.cs`原draw walk → 抑制scope | 新增仅控制所选注记墨迹；selection／mount、frame／definition、Window／Clip仍先更新，默认框及诊断overlay各保原义。失败／取消用finally退出；断开立即使遗留scope失效。 | Tools工作区clean scope包住实际目标draw，不是只包texture读回；检查draw在scope外。 |
| `UiTools.HitTest.cs` → 原`UiPointerLayers`／Session层表 | 新增只读坐标查询，复用完成draw的真实区域／popup／clip和canonical node，非第二命中树。只对该新读取要求完成draw；旧Capture／选择／独立Session不变。Stale／NotSampled／NotFound不返回旧命中，命中不授予操作资格。 | Tools `VirtualStage.QueryPoint`只反算一次观看与有效UI缩放，再经Workspace调用原连接；操作仍Capture／Queue原opaque地址。 |
| `UiLayoutInput.MeasureBudget` → `UiLayoutBudget.cs` → 共享`MeasureRoots/MeasureElement` | 新增可选非提交测量：当前绑定／Styles／真实字体与scroll宽度重测，fresh派生工作区、不发布／clamp／恢复scroll／消费target。自然叶仅用原可选纯participant；临时node及context不逃逸，失败清理、Close优先。旧Measure／MeasureAndArrange保原条件和成功后立即合法clamp。 | US `PreparePageCore/PrepareHelpBudget`在US_LAYOUT_BUDGET分支先取候选，再于reader外写最终预算；自然叶共享原测量并采用IUiMeasureParticipant，普通Measure/Draw仍持缓存发布。 |

窗壳、注记、命中与预算均为此前授权实现的public-unstable新增／扩展，不是本次文档任务新造的运行
保证。原page/provider ABI、纯读与measure participant的原适用条件不变；只给采用新预算者规定纯读
及同步借用条件。原scroll问题的恢复是去掉作者准备阶段的中间成功整页，不是撤销合法钳制。

<a id="stage-evidence"></a>
### 固定输入与后续消费证据

库内原件（可选本地交付证据，不是公开使用前置）：

- `dist/virtual-stage-20261107/fl09-selection-a001/REPORT.md`，RPT-VS-FL09-SELECTION-A001 r1：
  冻结carrier-v1、两目标生产构建、既有WindowHost／Catalog／CancelLadder／API及Dev geometry检查。
  初次closed-shell阻断旧Window重开的实际失败已纠正；PageLifecycle整体及containment整体仍因TEMP
  fixture／阳性控制创建拒绝fail，未改ACL或断言。原报告保持partial。
- `dist/virtual-stage-20261107/fl09-hit-scroll-a001/REPORT.md`及`report-r2-20261108/REPORT.md`：
  坐标接口与原scroll责任；中间viewport141为原源码／几何推断，不是首写栈或中间帧。
- `dist/virtual-stage-20261107/fl09-hit-scroll-a001/measure-v3-20261108/REPORT.md`，
  RPT-VS-FL09-HIT-SCROLL-A001 r3：carrier-v3完整source-form／USE／IDENTITY，两目标最终生产构建；
  原391项Neutral检查覆盖共享／旧行为，不是新预算异常／首次挂载的专门动态覆盖。最终额外Pure scope
  只由最终生产构建覆盖。原报告保持partial，提交时的下游未验不回填为producer通过。

外部消费证据转录（不依赖私有管理路径或同级仓库存在）：RPT-VS-TOOLS-A001 r13的实际输入是
US RPT-VS-US09-A001 r6与FL预算r3。FL v3 Neutral MVID为`5ea548d4-5a81-4650-97d5-e1225442f1b6`，
native17为`76ae2e76-4b33-406d-853e-977a7f4fd44c`；US完整provider为
`db8e5a70-b391-4e61-8c77-dfc318c49fb4`，Tools Offline Core／Native17为
`b26b370c-decf-42fc-876b-f64d6b88991d`／`3792f10f-6614-43ad-802c-dce6f2fcb091`。
实际1.7.5000.1275只定位该输入，不是支持上限。不同v1／v2／v3、p2／p5和目标DLL不可混标。

| 证据层／原件身份 | 支持的实际范围 | 仍未覆盖／复用边界 |
| --- | --- | --- |
| FL08／1.6维护；原c3-dev承接r6 | 原维护与恢复材料及其当时人验 | 不由FL09接缝或1.7隔离图形签准确1.6投影、UI缩放、输入／IME、生命周期、UI层级、浏览B录制A、高级参数采样、Report连续链／原位32-pass／partial。 |
| FL09 v3 Release/net10/x64；FER_TOOLS，无FER_DEV/FER_TEST | Neutral与RimWorld17生产编译；原共享Neutral／adapter检查按原输入复用 | 构建不等于新接口运行；TEMP两lane fail仍在。Dev geometry原检查不是新Release模块的动态实测。 |
| Tools r13 默认嵌入完整窗；session `681f60b0845d46d2b0e817e090981fd7` | Host4／definition0；clean frame39800与inspection39804分别关联同帧Host／geometry／selection，mount22保持。原opaque操作100→75及invalid draft“-”有原owner读回。 | 背景隔离Native17，不是Game17。两图不是同一个帧；不把默认嵌入与作者会话合并。 |
| Tools r13 `target-sequence.json`／`author-live-coordinate-state.json` | 同filter mount40的scroll7跨三个目标保留；帮助关闭后viewport221／content136合法clamp0。Hit与imageSource同帧10180／10200／10218／10240。 | 原r10的scroll7→0仍fail；新v3结果不重写它。物理输入、popup／disabled／capture动态组合未实测。 |
| r13作者session `76afb368846d41b5be44d62e16fc37d8`；`style18-observation/adoption-and-first-pump.json`／`style16-final/restore-adoption.json` | 同作者inline Styles Font16→18→16，definition1→2→3，frame104000／191450对应图／geometry／selection，保草稿、scroll与展开。 | 旧Gap8→错误拒绝保last-good→Gap6属于旧session `f8e67856d4f0495d92b8e38bbe691159`，仅按原输入复用，没有在v3新session重演错误链。 |
| r13 `style16-final/normal-close-and-release.json` | measure stop后计数不推进；Host NotFound、Hit／Capture Closed、旧地址attempted=false，随后断开及正常进程退出／资源Dispose返回。 | 不覆盖所有专门异常／重入／原生退出组合；运行后旧地址与live session不可复用。 |
| Tools r14用法修复／r15当前账和最终双包 | 文档及无编译重包；r13运行身份保留，Game17只构建／包装 | r15为冻结交付；本次新源文档未进入旧包，不当作新运行、安装或release。 |

上述后续消费由舞台WO r6技术／产品核收；用户最终验收仍由负责PM／用户处理。本次新文档Goal
不重开已结束实施。US原报告partial、历史UiLogic fail及资源清单后续独立纠正、原r10失败、TEMP
两lane fail及基线流程fail各保原对象。独立CPU／prepare／GPU／GC／Kernel／FPS仍未知，原cost
Unsupported不记零；RPC或同步elapsed不换算这些指标。新增协议和静态文字不签任何未验宿主。

## 0.9 formal window shell and selection presentation (2026-11-07)

`UiWindowShell` v1 is the single neutral implementation of the original formal window face,
content geometry and Host attach/detach/failure lifecycle. The existing native `UiWindowHost`
forwards its policy and native primitives into it; its public/protected signatures remain unchanged.
An additive seven-argument `UiPreviewInstance` constructor transfers the existing page Host into
that shell and exposes `WindowShell`; old constructors/providers remain page-only and legal.
Documents and last-good publication still belong to the existing author service.

`UiToolConnection.SuppressSelectionOutline()` v1 scopes only selected-node ink. The original draw
walk updates frame/definition/Window/Clip before the suppression decision, using the same selection
and mount. Nested scope disposal and connection closure restore presentation without altering
SetInstruments' independent diagnostic overlay.

Usage/ownership: [the connection guide](../development/0.9/tools.md#draw-the-formal-window-around-the-existing-preview-host).
The isolated producer output is `dist/virtual-stage-20261107/fl09-selection-a001/`; its identity and
report own the exact carrier/source form and actual checks. Native stub checks are not game or
standalone graphics acceptance. That producer report retains its submission-time downstream gap;
[the later paired consumer evidence](#stage-evidence) now supplies the isolated full-window, Styles,
selection-preserving export and normal-close observations under their actual identities.

## 0.9 preview preparation and original-window cost peers (consumer feedback)

The actual author's dynamic help budget required preparation before both initial and post-operation
arrangement. The additive UiPreviewInstance overload attaches a repeat-safe callback to its existing
Host, invoked only by explicit AdvanceHeadless / real DrawFrame, never by reads. Its original constructor
metadata remains unchanged for already compiled providers. Callback failure/close follows the original
owner; no extra Host, registry or scheduler exists.

The optional IUiCostSampleProvider attaches the actual window's existing sampler to that Host.
ReadCostSample projects retained bounded facts without enabling sampling. QueueCostSample shares the
existing queue, ID ledger and one-entry owner budget; an attachment incarnation prevents replacement
from retargeting work. Counts/metrics/context remain author facts with nullable unknowns. This peer
adds no sample accumulator/state machine and no Tools prerequisite to ordinary logging/sampling.
Independent AuthorExamples compile the callback adapter and real native Host attachment. Actual
author/Tools preparation and window-sampler consumption remain with those consumers; local compile
and existing checks do not sign their new native or cost results. This is A001/DSP1/WO1 ordinary repair.

## 0.9 custom-editor public repair (consumer feedback)

The actual sparse-field consumer could describe and operate boolean/selection values but could not
call the internal complete editor operation from its own assembly. The same Controls.EditSemantic<T>
production entry is now public-unstable. It retains the original editor, codec/callbacks and focus owner,
adds no Tools prerequisite and exposes no state table. Explicit SetDraft remains local; finally-based
focus bookkeeping/invalidation and early completion-flag consumption preserve the request/failure exit.
The existing separately compiled CustomControl forwards its native float codec through this public peer.
The exact public signature and current adoption instructions are in the connection guide.

CO has reported actual p1-r2 Tools/US provider workflows and a separately authorized Tools native bridge.
Those results are not p4 custom numeric-editor acceptance; new consumer adoption/readback remains with US.
FL does not wait for or implement the Tools bridge under this repair. Old fixed inputs and their failed/
valid evidence remain unchanged. The repair still executes A001/DSP1/WO1; no new worker is introduced.
## 0.9 selected core (2026-11-05)

The 0.9 source adds Host-owned compatible tool connections, bounded semantic addresses/results,
original-wrapper operations, same-source explicit non-rendering advance and preview registration.
Node mount and semantic part/item incarnations prevent requests from reviving after a bounded owner
view retires them. Only explicitly captured/pending nodes are followed while connected; a connected
but uncaptured Host has no semantic polling. Disconnect removes that interest and tool-owned
subscription/resources; old borrowed subscriptions, R9 and normal logs retain their conditions.
Normal Release includes FER_TOOLS; document fault injection is compiled only with FER_TEST.

UiHost.SetLayoutInput changes owner height/variant constraints through the established full layout
transaction without swapping the definition/session. CompleteEdit uses the existing field parser,
conflict/IME and one-request boundary; EndElementLifetime uses the existing subtree release path.
API/release/build axes are 0.9.0. New advanced entries are public-unstable; existing stable signatures
and defaults are not redefined. The public author range and separate preview registration sample are
compiled with the current axes.

The explicit net10.0 RimWorld17 target references the actual local 1.7.4998 rev1299 game assemblies,
with Private=false; the full library and separate public Window/Settings AuthorExamples compile.
The same source also builds an official platform-free net10.0 target. Numeric editor1.7 retains
reported preset fallback; unavailable font revision revokes optional reuse. Native/render/IME,
actual Tools/consumer semantic workflow, last-good tool reload and CO performance acceptance remain
separate. The separately authorized Tools bridge/native owner supplies standalone visual evidence.

The current [connection/target guide](../development/0.9/tools.md) carries usage; individual fixed
producers carry their actual source form, command results and coverage. Prior 0.8/1.6 r6 evidence and
pending native responsibility remain attached to that line, not silently superseded by this target.
## 2026-11-05 尺寸、content hover 与领域日志

当前8c6d0ff基线的 dirty 源码实现 Button NaturalBody／同源 caption inset、literal Auto 原串、
单行 placeholder，以及 context-aware hover 的自身 effective clip。press／capture／editor 与
popup window-space 路线不改；follow／reading-hold／显式 pin 仍由 consumer 持有。

public-unstable VerseUiDiagnosticLog／UiDiagnosticLogOptions 经真实 UiHost internal constructor
在创建前绑定 consumer，UiPageWindow 实际采用，旧构造／factory／metrics override 保持。
原有动态诊断 route 在 push/pop 内借 Session，不新增持久 logger index／事实 ring／native owner。
Trip／LastRequestFailure／parser issues／reload reports／已有 subscription 先记录，再可选原生三级
输出；默认 daily、detail 早 gate、有限输出账本与采样／原 facts 预算独立。Host 关闭会完成后续
清理后保原单异常或聚合，Session 在清理结果后释放 binding。可用 API、预算／摘要及迁移写法
集中在[领域诊断](../development/0.8/ui-diagnostics.md)。

本轮复用已有 layout/widget/help/input/edit/invalidation、Diagnostics／FailureLifetime／Reload／
Style／WindowHost／Catalog／Observation、containment／Stable API 及 neutral Core 入口；不新增或
弱化测试。具体实际命令／候选身份随固定交付保存。新 logger 的精确 quota、sink-failure、创建
异常组合没有专用新 case，源码／既有路径检查不冒称这些分支的独立行为覆盖。真实字形、hover／
阅读、IME／caret／selection／copy、稳定 Repaint／高级展开热点及1.7 native 均未由本轮签绿。
更广 metrics 缓存／culling 和条件性空文本恢复未据新实测扩张；没有 FPS 改善数字。
A001 的 Dev instrument 整体 gate 和 Release KernelAtomTests 曾保留两条旧精确期望失败
（:221／:224，button16／余量304 未含 caption inset）。WO r3 明确授权后，A003 仅将这两处
期望及说明按独立合同推导纠正为28／292；场景、断言数量、Near精度及其他断言不变。
原 Atom suite／Dev instrument gate 已通过，旧失败日志保留；产品 runtime 未重编译，复用 r3 DLL。

<a id="appearance-freedom"></a>
## 2026-11-03 外观自由度：作者入口与组合生产链

当前 c1a5091 基线的 dirty 源码实现七个独立 TextColor 槽、只读 text/wrapped 默认修正和
FontSize 1..96。新入口为 public-unstable，不给旧 Stable 类型增加公开／protected 成员或
implements，不改冻结签名、API 区间或闭合检查。作者写法见[消费指南](../consumers/consume-from-0.8.0-dev.md#choose-one-effective-font-for-measurement-and-drawing)。

### 作者书写仍由原属性 owner 解析

- XML：TextColor 及六个状态槽、FontSize；UiDefinitionCompiler 定位拒绝非法 inline 值。
  Scheme：同名 TextColor 元素，Metric Token="Font"／"FontSize"；非法数字令 style 候选
  StructurallyInvalid，正常更新流程报告并保留 last-good。
- UiTextColours.Theme/Instance/Local 与 UiTextFonts.Theme/Instance/Local 返回现有
  UiPropertyDeclaration；Set 保存同值意图，Remove 只揭示当前下一来源，透明色不是 absence。
  Theme／ElementProperties 自己持有数据，没有全局服务表。
- TextColours partial 复用 UiPropertyResolver，七槽独立解析后才按有效状态选色。FontSize
  partial 与原 Font 来源共同确定描述；Font-only 的最近层重置外层数字，size-only 继续找别名。
  authoring capture／rollback、Clone／Restore／page application 和 resolution reuse 同时维护新值。
- UiTextColours.Resolved 返回活只读槽／来源；UiTextFonts.ResolvedSize 与 Requested 分开
  数字来源和请求描述。旧 ResolvedFont／Styles.Font 继续只读资源别名。
- 独立 Session 的 CreateContext 不改变条件。新属性作者调用 UiTextFonts.Apply（同
  UiTextColours.Resolve）及权威 host theme，返回同 node/session 的 resolved context；测绘共用
  它，变更声明后重新解析。retained Host 在原 pipeline 自动做这一步。
- 无 Session 的视觉作者直接配置 Theme，以 UiTextFonts.Requested／Resolve 成对使用实际
  ruler/drawing；最终颜色取 theme.Styles.Resolve(...).Text。旧 IUiDrawing.Text 的显式 colour／
  font 参数仍是原含义，旧 UiThemeDraw.Label 不被重解释为数字调用。

### 一个 native owner，标准和独立入口实际采用

VerseTextExecution 组合唯一 VerseUiExecution，委托 identity、PrepareControl、Capture、Focus、
Frame、Images、Edit、WithState 和 Dispose；不复制控制表或 session 状态。无参构造拥有 owner，
传入 owner 默认借用，关闭先 Session/Host 后 owner。VerseTextMetrics 组合原
VerseFerriteTextMetrics singleton；旧 singleton、旧接口和合法使用条件不变。

标准 UiWindowHost/UiPageWindow 的 SetFrame、Accept/Cancel、DrawFrame 实际供应组合端口，
Host.AdoptTextMetrics 只替换同一原生 ruler 引用；旧自定义 Metrics override/自带 Host ruler
不被绕过。protected Execution 仍为原 owner。chrome 标题／副标题／关闭、notice、页内容、
Host audit 用同一对能力；数字可增加自然 chrome 行高／关闭尺寸，显式几何 override 仍负责分配。
独立 Host 使用同一新 ruler 与 execution；Session/custom widget 使用其实际 context 和
UiTextFonts.Resolve(context)，不需创建示范框架或额外 Host。

原生编辑链：UiControls.Edit 解析 Editor run 和有效状态色 → UiTextEdit（旧 UiNativeEdit +
生效 UiTextFont + optional colour）→ VerseTextExecution.Edit → 同 owner.EditText/EditCore →
GUI.contentColor 与真实 Widgets.TextField/TextArea。显式字色清除外层 GUI.color 的乘色，
无显式颜色保留原 native preset ink。数字临时设置 CurTextFieldStyle／CurTextAreaStyle.fontSize；
标签仍经 CurFontStyle 与 Widgets.Label，不绘后缩放、不插 rich-text size。
WithState、WithFontSize、SizedEditor 在 finally 恢复共享样式／GUI 状态；focus、caret、selection、
IME、只读复制和 Cancel 仍由原编辑 funnel/native owner 持有。

### 测量、布局、绘制与观察使用生效描述

UiTextRun 只有 metrics 和 drawing 都声明该用途能力才保留数字；Editor 还要求编辑载体能力。
否则同一 run 回别名预设，报告请求／生效／用途／原因，按元素路径与原因去重。
内置 text/banner/empty/header/button/checkbox/slider/number/text-field/stepper/dropdown/mode/tree、
container title、Auto、SharedLabels、换行、自然行高及 popup 行／命中几何均消费这一生效描述；
显式 Height/FieldWidth 等分配不被擅自改写，空间不足仍由 fit／layout 报告。

- UiLayoutEngine 的 context/stamp、role 与 measure inputs 包含有效数字及适用 Editor 描述。
  UiTextMetricsCache 的键为资源别名／有效字号／用途／文字／宽度，按实际环境失效。
  CaptureEnvironment=null 不授予跨同步的 text/role reuse；原先 !layoutRequested 的 idle-snapshot
  条件对 numeric 和 legacy 一致保留，不把无 FontSize 当作已知字体环境。
- UiFitAudit.CheckText 以同一生效字体和真实 audit ruler 计量；ruler 缺数字能力时明确报告，
  不用预设测数字。旧 UiOverflowReport 外形不改，UiTextFonts.FontOf/ColourOf 读新并行元数据。
- UiRetainedPaint 捕获成功绘制的 theme/生效字体/最终色及编辑 draft；retained field 是
  display-only，不调用 native edit 或业务请求。它与 native-editor 的来源标记分开。
- Host.Observe 有界 facts 输出请求字号来源、各用途请求／生效／降级和最后 paint 描述／色。
  Dev capture 的 ScopeTextFont 为请求，LabelFont/EditorFont 和对应 Colour/Source 为本次最后
  outlet 事实；未触达仍 not-sampled。旧 PaintedFont 保留原 unknown 语义；不把 scope 推断成像素。

WrappedTextWidget 只将展示角色入口改为 writable:null；共享 Resolve(writable:false) 与交互资格
不变。H1 仅两条指定期望修为只读文字保留 Danger／正常 Neutral。A、B 与重用新 FL 的 delivery-r8
共同变亮，即便其调用方源码未改。

### 证据与宿主边界

1.6 数值标签／输入均为 **静态候选、待原生**。1.7.4995 rev309 标签链为源码候选，
VerseFerriteTextMetrics.NumericLabels17 是唯一标签开关（当前 true）；输入数值不支持，
回预设并报告。1.7 字体环境无可靠指纹，仍 null，不借字段增加假定纯度。未宣称1.7引用编译、
JIT 或原生接受；原平台证据保身份。

复用已完成干净基线和颜色切片。当前源码完整原 verify-local 在
`dist/verification/a004-complete-20261103-0359/summary.json` 记录18组通过、0失败／阻塞；
Core391、adapter140，Release/Dev构建零警告零错误。Dev仪器实际执行40项指定断言，
4188条 ok、11行节点 dump；已有按钮／banner输出实际preset字体／字色，并未触达新数值分支。
既有 checks
不逐一动态覆盖七槽／来源撤销、数值解析边界、成对降级、native glyph、style 异常还原及
新 retained/fit 元数据分支。只维护批准的 containment 两个 getter、Verse.Text 独立 field/area
backing 与 NativeFixture 的具体 Owner/实际 supplied port 兼容，不改业务断言或增加 case。
原生数值测绘、caret/selection/IME、语言回退、UIScale、极端字号与1.7绑定/开关仍待用户确认。

## 真实生产管线与责任

    model / sources / BindingOwner / revisions
      → UiBindingPresentation需求与current validity
      → Host MeasureAndArrange / keyNodes / definition与Repeat协调
      → property source resolution / actual measurement / local plans
      → candidate transaction / complete or sparse geometry publication
      → current effective draw / hit / clip / scroll / popup anchors
      → explicit native frame / Controls shared behavior / one business request
      → bounded observation / owner cleanup / Close

| 能力／连接 | 当前生产入口与下一个真实消费者 | 实现／证据边界 |
|---|---|---|
| structure/definition | UiLayoutManifest.Parse/Create、UiElementSpec、UiAttributeSchema；UiWidgetRegistry.Define → Host compiler／Repeat | immutable copy、完整 scoped roles及typed声明；非另一XML解释器。KernelAssembly/Repeat/Identity是已有托管证据 |
| binding/reactive | UiBindings/UiBindingPresentation、Neutral/UiReactiveRuntime/Owner → Host需求／执行endpoint | ordinary显式通知与tracked动态依赖分开；ReactiveFoundation/Inventory和真实Kernel对应lane，不深观察普通对象 |
| item connections | UiItemBindings.Refresh/UiBindingAddress → 当前key字段与实际Repeat | 当前对象解析／全部key先验证／立即断订阅；ItemBinding与KernelItemBinding，不替外部服务定线程或存档身份 |
| properties | UiElementProperties.Apply、UiPropertyResolver、UiTheme → Measure／pointer projection／paint | 实际声明／完整祖先／mutable theme revision检查，held readonly view仍活；StyleScope/CloseoutAppearance/真实Layout |
| layout/publication | UiLayoutEngine.ArrangeRoots／UiLayout／UiLayoutReuse → node／snapshot／draw/hit | 同源算法，RU2真实输入，后续增量候选见下；LayoutReuse/Placement/Layout而非像素验证 |
| behavior/editing | UiControls/Neutral UiInteractionSession/UiEditSession/UiNumericEdit → wrapper请求／native leaf | standard/custom/headless同实现；SharedBehavior/Editing/Chart、BSE及既有edit/eligibility lane |
| definition update | UiHost.UpdateDefinition → Prepare/Stage/Seal；UiDocumentService批次 → 每Host retirement/Accept | all-host staging rollback 与 irreversible seal 分开；本次F03路径及证据见下 |
| host/resources | UiWindowHost/UiPageWindow/Catalog → UiHost.DrawFrame；explicit embedded frame → 同Host | WindowHost/Catalog/Adapter.ContractTests/StandaloneNativeNumber，含借用execution、finally、foreign scope |
| images/background | VerseUiExecution.Images/VerseUiImages → UiImage → UiImageLayout/UiThemeDraw/window background | supplier对象不Destroy，fit/clip／paint-order记录，不是native截图 |
| help | Widgets/OptionHelp、UiSession帮助pass → Session topic／owner-bound output endpoint → 消费者panel | 两种寿命不同；selected后的withdrawal不重放。高级Session成员未升stable |
| observation | UiDiagnosticSubscription/Hub、UiHost.Observe、UiFitAudit → detached facts／optional sink | 基本bounded与实验R9不同；Observation/Diagnostics/Instrument覆盖原输入，F02精确异常组合未执行 |
| platform closure | NeutralSources.props包含整个Kernel；Adapters/RimWorld16原生执行 | Core.ContractTests无engine依赖、Adapter/native-stub/AuthorExamples是不同层；不推断1.7已完成 |

实现路径均在 Source/FerriteLib.UiKit 下；中立文件在 Kernel（含 Neutral/），
原生文件在 Adapters/RimWorld16。普通作者入口由 S1–S7 和 API tiers 定义，不需要照实现表接内部函数。

<a id="incremental"></a>
## 增量同步的实际收缩及保守域

现有 Release 与 Dev 共享生产链，稳定域尝试在 full backup 之前完成验证／候选。
RU1 retained descriptor inventory、keyNodes/revisions/UpdateScope 与 RU2 leaf／local plans复用。
first-touch journal只捕获变更派生node；leaf records首次实际写才copy；文字memo共享只读记录，
font miss／environment replacement才取可写存储，每font512。geometry用32-entry pages，
保留entry／稳定index，小目录复制而非全表重组；旧snapshot不链式包上一snapshot，也不指向mutable patch map。
full-path仍持原完整backup。放弃候选不回滚业务动作或runtime declarations。

验证仍扫描 committed entries／真实reader revisions，Draw/hit/native callback继续；因此不能宣称全部callback
为O(变更节点)或retained像素。冷开需要建立索引，不承诺冷开变快。
全部退回条件见现行 layout FULL-FALLBACK：ordinary新同步／legacy measure／未知环境／结构需求／共享列／Auto/
变化MatchContent／Fill／动态demand／diagnostic callbacks等，不缩旧合法合同。

r4补上多根remaining-height外部依赖：
Kernel/UiLayoutEngine.cs:243附近为后续根提供height-y；
Kernel/UiLayoutEngine.Reflow.cs:17起与 UiLayoutEngine.Reuse.cs:77附近保守判断
multi-root及root/subtree consumes-available-height。
前根高变化后，后根或后代 Fill必须full，不只MoveY。r3历史候选704c4c1未放行，不能用其证据接受r4。

现有opt-in arrange事件的 Reason=work-total 是Host/session累计工作：
passes为ArrangeRoots同步次数，full为完整candidate attempts，reuse/sparse为成功结果；
captured-nodes是派生备份，copied-scopes/leaf-records是复制引用数，geometry-visits/published-entries仅发布工作，
不含validation/Draw/hit walk，不是分配字节、帧或FPS。失败／回退之前工作仍在累计值内。
同一Host连续total才可相减。既有Counter序列 full3→1、captures17→7、publication visits21→13只代表原托管输入；
多根tracked增长后接Fill没有既有targeted组合case，仍明确保留该盲区。

参考机制固定
[Qz-UILib-Core 32bfefae](https://github.com/QuanhuZeYu/Qz-UILib-Core/tree/32bfefae7e345c106d7ca129ba036225c74ee4a4)、
[MIT](https://github.com/QuanhuZeYu/Qz-UILib-Core/blob/32bfefae7e345c106d7ca129ba036225c74ee4a4/LICENSE)：
采用geometry/paint失效区分、当前几何准备输入、settle后发布；独立实现，没有移入Java。
其完整tree工作不证明FL sparse方案来自既有实现。
未采用GPU像素保留／自动调度／按GUI采样／framebuffer坐标；
[原设计§9](https://github.com/QuanhuZeYu/Qz-UILib-Core/blob/32bfefae7e345c106d7ca129ba036225c74ee4a4/specs/2026-10-07-uilib-core-contract-design.md#L221)
不改变FL逻辑坐标、UIScale、指针与字号语义。

<a id="fl-f01"></a>
## FL-F01：popup wheel 的状态和消费共用当前资格

恢复 interaction-editing 的 FL08-POPUP-WHEEL／CAPTURE，没有新增需求 delta。
实际链仍为 Window/embedded execution → Session.SetFrame → Engine.Draw →
TakePopupWheelIfCovering → ScrollPopupByRows → ConsumePointerEvent。

UiControls.TakePopupWheelIfCovering 在改 offset 和 consume 前同时检查当前 PointerInputAllowed、
IsInputGeometryReliable、实际 wheel、开放菜单与覆盖。只有无资格／不可靠几何才退出这一个 wheel 入口；
合格覆盖下即使 ScrollPopupByRows 没有移动仍 consume。PointerInteraction／PointerPhase 原有失资格、
lost capture 和 raw Used 主键 release 的 neutral/native 结束路径未被全入口早退拦住。

Release／Dev 的既有 KernelInputEligibilityTests 已执行通过，覆盖合格 inside/outside/端点及既有
capture 资格／raw release 路径。false-window-qualification wheel／不可靠几何的精确 wheel 组合未新增或执行；
这两项的关闭依据是同一入口的源码守卫，不是原生窗口事故复现。

<a id="fl-f02"></a>
## FL-F02：两项新保证已采用，Trace／legacy-fit 出站已隔离

2026-10-29用户批准 WO-USFL-CONTRACT-REPAIR-20261029 r1，正式采用
[两项 ADDED 的来源记录](changes/isolate-observation/proposal.md)，唯一现行要求在
[观察连接](specs/contracts/observation-boundary/spec.md)。这是新保证，不伪称原有普遍隔离。

| 实际出站 | 窄 catch 之后的真实消费者 |
|---|---|
| UiControls.Interactions.PointerInteraction 的结束 Trace | 返回 result → ButtonWidget 的 UiInteractionRequests.Activation → 原 command；capture/consume 已完成，不重做 |
| 同文件 PopupOption 的 option Trace | 返回 result → UiPopup.DrawChoices 关闭自有 popup → 一次 Selection 请求 |
| UiSession.ScrollPopupByRows 的 wheel Trace | 返回 Controls.TakePopupWheelIfCovering → 适用 consume，端点规则不变 |
| UiPopup.PushPopupLayer 的 publish Trace | hit layer 已发布 → Panel／row paint；paint 不在观察 catch 内 |
| UiFitAudit.Report／ReportStyleFallback 的 legacy sink 及纯诊断 payload | 原显示／恢复继续；Check 中必要 MeasureWidth/MeasureText 及订阅 buffer publication 不在 sink catch 内 |

UiObservationBoundary 只分类普通 managed failure，并提供既有 System.Diagnostics.Trace 最终出口的
有界非递归报告；不调用失败的作者 Trace／Warning／fit sink 来再报告。
Session 自身只花一次备用报告额度。legacy 的两个现有 attachment 各自持一次额度，Reset 重置，
Detach 释放；没有新进程级 registry/queue，未注销作者 callback，也没有每次 callback 分配日志闭包。
异常 ToString 的纯诊断格式化失败保原类型／context；备用 listener 再失败也不递归。
OOM、stack overflow、access violation、SEH／app-domain/thread fatal 不作为普通成功隔离。

setter／command、managed writable response、普通 getter／Measure、必要 metrics／paint、明示
HostAttached/Detached hook 的失败合同、API tiers、no-Host/headless/visual-only 条件保持。
既有 US 三 Host 到 SqueakLog.PopupTrace 的 callback 形态不变；US logger 自身修复由其 owner 负责，
FL 不依赖该 logger 成功来满足上述出口保证。

Release／Dev 的 Diagnostics、Session、TextAudit 等既有路径通过；collecting Trace、普通 fit、
必要 paint／metrics 和原失败路径只支持各自输入。throwing Trace 四出口／throwing legacy sink／
最终 listener 二次失败未新增或执行精确组合；新保证有源码边界证据，不冒称原生抛错或组合运行 PASS。

<a id="fl-f03"></a>
## FL-F03：逐项释放、恢复与不可逆 seal 的结果已闭合

恢复 lifetime FL08-RELEASE／RECOVERY 和 definition-update FL08-SEAL，没有新增 delta。

- UiNode.ResetCustomState 先取得当前 release 集并撤下注册，再逐项捕获普通 release 失败；
  ReportCleanupFailure 把异常格式化与 Warning／最终 Trace 出口都保护在 reporting 边界。
  同节点余项仍尝试；实际 Dispose 的 Node.ReleaseState 汇集原输出／custom 原因，Session
  逻辑关闭、继续其他资源后仍聚合错误。重复 Dispose 不重试失败副作用。
- UiSessionGuard.Record 在 Warning 前保 Trip/path/kind／原诊断；原异常格式化失败保原类型与归因。
  Warning 普通失败后仍尝试 PublishRecovery，再到 DrawWidget/DrawOrFallback 或 measure 的既定 fallback。
  真正 fallback paint 保外层保护。
- UiHost.UpdateDefinition 只在 CommitLayoutCandidate staging 失败时 RestoreDocumentRollback；
  SealDocumentCommit 位于可回滚 try 之外。revision 随已 stage 的 seal 推进，退役不会伪装回旧 tree。
- seal 在回调前收集去重 doomed nodes，逐项尝试 output/custom/editor 释放；State 本身已在
  ValueStates 中，不再重复 ResetState。native focus 失败也在 finally 清自身 editor 状态；
  剩余节点和 capture 清理继续，清理原异常在完成后报告。ReleaseHotControl 在 fallible native
  release 前结束自身归属，不重试副作用。
- UiDocumentService 原生产入口仍先全部 prepare/stage，失败完整回滚；全部成功后逐 Host seal，
  再 state.Accept／Accepted report。由于每个 seal 的普通 cleanup/reporting 失败均被上述独立边界记录，
  它们不打断后续 Host／Accepted，不需另建事务或修改 DocumentService 的业务提交逻辑。

Release／Dev 的 KernelAssembly（含真实 single/multi-Host output 失败路径）、DocumentReload、
Session／Diagnostics，以及 Release 的 PageLifecycle 既有入口通过。原检查中的 Warning 不抛、单异常成功不等于
release+Warning+最终出口的双／多异常组合已执行；这些组合的证据限于上述实际生产源码路径。

## 2026-11-01 release-gate narrow repairs

FL-R1 在 `UiSessionGuard` 两个 draw 入口恢复 FL08-REQUEST。fallback 前，Session 保留一份最新
`UiRequestFailure` 和 guard node 身份；原异常链及明确操作目标的 node、target/business scope、part、
item 保持原值。直接 tracked Set/Invoke 失败还保留可得的操作与 endpoint key。既有 Warning 出口无需
订阅即可报告；格式化／观察失败在该出站边隔离，备用报告沿既有有界非递归机制。不 trip、不自动
重试／补偿，也不另读模型推断写入结果。已发生业务副作用不撤销；仅由异常不能知道其完整范围。
Session Dispose 释放保留的异常及目标。

FL-R2 在下一次 `MeasureAndArrange` 的 `RefreshBindingOwner` 接通 Host 建立后才明确 enroll 的同一
registry：创建 per-view presentation、请求输入验证，随后走原 Synchronize/Enter 与实际 demand 管线。
`DrawFrame` 在 draw 前经过该点；合法配对的手动 Draw 不在一轮中途创建 presentation。Close 仍只释放
该视图。普通未 enroll 的 getter／通知条件不变，不要求早注册或自动 enroll 普通绑定。

FL-R3 将初次 `UiDocumentService.Attach` 的 layout/style staging 纳入已有
CaptureDocumentRollback/RestoreDocumentRollback。准备成功的初始候选一起 stage；style stage 保留 layout
待退役集合。stage 失败恢复原 Host，旧 dependency/service 连接不变，首次连接则仍未登记。
prepare 拒绝仍报告并保原 last-good／订阅行为。stage 全部成功后才替换依赖，在可回滚 catch 之外 seal
一次。stage callback 关闭的 Host 不重新登记。seal cleanup 保原已提交结果，不在退役后假称回滚。

本次既有 Release/Dev suite 覆盖请求失败后新点击、标准 presentation demand／失效及视图释放、初次
Attach/preflight、重新连接、批次回滚和退役。Core 与 adapter 仍分别属于托管／stub 证据。精确失败记录
内容、请求 observer／formatter 抛错、同 owner 首次晚 enroll、初次 Attach stage 异常及 layout/style
合并退役没有现成 targeted 动态 case；这些修复依据实际生产源码，不冒称游戏复现。未新增 test/case/
probe/runner 或放宽断言。未运行原生画面、字体／IME 和窗口输入。

## 本次本地技术证据与消费条件

源身份为 cf1cf6456096171fb47c012369bcd0c2f29e968f + dirty，含继承规格重建和本次产品修复，未提交。
固定 Release 技术输入为 `dist/contract-repair-20261029/producer-release-r1/FerriteLib`，
VersionSuffix 为空、无 FER_DEV；标准 stage 的 version.txt 明示 dirty。它不是正式发布资产，
也不是旧 held producer。构建／既有检查日志、实际 source diff 和本次报告在同一 ignored 根。
消费者须显式选择这份 DLL 编译并配对同一运行包，不能以 moving build 或旧 DLL 代替。
本次运行 Core391、Adapter140+20；受影响 Release11 个与 Dev7 个既有 suite 通过，
没有新增测试／case/probe 或放宽断言。曾误把辅助 KernelTripGuard 当 suite 调用而得到无 RunAll 的
反射入口错误，随后改用其实际消费者 KernelSessionTests；该入口错误不当产品失败或目标复现。
游戏未启动，真实字体／IME／窗口输入／听感与 Config 未被本次自动检查接受。

## 平台证据的实际目标

现有1.6编译引用为Krafs.Rimworld.Ref1.6.4871，既有native文字合同目标记录为
RimWorld1.6.4871／Unity2022.3.35f1；实查1.6 Assembly-CSharp版本1.6.9676.17735。
这些是既有来源／证据目标，不把每个1.6构建或任意mod组合预填接受。
既有1.7研究输入为1.7.4981rev136，缺deps.json；已测scope含UnityStubs/RimUnity/Waylith等差异，
不构成1.7 SDK齐备或已交付FL adapter。详细独立metadata仍保原证据页。

2026-10-30 的基础兼容候选另以 Local17 1.7.4990 rev1069 的完整快照核对共同文字接缝，
按现有 optional metric-environment 合同隔离该 host 缺失的 Font.fontNames；1.6 指纹与正常测量保持原路径。
[构建说明](../build-and-debug.md#basic-17-compatibility-candidate)记录候选范围，固定包自带实测身份与验证收据。
这不补签1.7引用编译、原生渲染或主要交互；完整1.7 adapter仍未交付。

## 原有证据保身份，当前未做原生运行验收

| 证据 | 可支持／不可外推 |
|---|---|
| Core.ContractTests与NeutralSources.props | 同源中性行为／依赖闭包，不是native字体／IME |
| main harness、Adapter.ContractTests、无friend AuthorExamples | modeled原生协议与真实公开入口，不是游戏像素／WindowStack实机 |
| 早期A004 user input失败；后来9ee0703/f362c99反馈 | 后一对用户报告page input恢复，所有buttons仍慢；这条反馈不是对palette候选的测试，也不是全native接受 |
| palette6f0238d A006；property2309cd3 A007 | A006性能失败；A007接近报告baseline仍波动／首开瞬态。32 Host-pass成本不是render frames，旧结论不改签 |
| A009r2 US68b30e3／FL36fa7ad | 保持独立比较与体验候选身份，不转签后续管线结果 |
| US2f22bec／FL833c14f4 r4 | 用户接受stable FPS；bounded Host-call summaries支持方向，首arrange最大54.424ms仍在，不证明全帧benchmark或HUD无影响 |
| USea610fa／FL833c14f4 | 用户接受总体内部试玩可进入RC准备；未记录细粒度font/IME/overlap/scaling/narrow-screen/config/audio条件仍是限制，不要求重演已接受试玩 |
| US3e42cc180f91f5f58b040724e4ad76ea060802e6／FL6ec0c31a76b552dc74f30d7969862949c955d4a8 | 实际固定编译消费与标准整对；6ec是held producer，不是本次当前源码身份 |

完整独立原来源接入与平台差异保在
[platform-and-real-integrations](../development/0.8/platform-and-real-integrations.md)，
A004/A006/A007数值与candidate条件保在[窗口指南](../consumers/rimworld-window-layout.md)。
1.6 font/IME/selection/clipboard、真实scroll/overlap/scaling及图像像素仍未完整验收；
1.7 compiled/native adapter未交付。源码反例不冒称游戏复现，手工文档检查不关闭这些偏差。

## 实际机器读取与活跃入口

FerriteLibApiTierTests.cs:26,28–30,254,426–455读 docs/api-tiers.md 的三个英文标题／backtick type条目，
并读 docs/development/0.8/stable-api.txt；Advanced :218–227排除Session/Node/Trace/R9等明确成员。
KernelContainmentTests.cs:45–49读同一tier文档的精确原生边界句和三个adapter路径。
本次二者输入内容、路径、机器标题与原签名不变；本次既有 API／containment reader 已执行通过。
这些lexical／reflection门不等于完整native接受。

为保持本轮机器输入不变，tier文档仍链接四个旧地址；这些地址只留过渡导航，public-contract保所需旧S6/help fragments，
落到实际新正文而非空页。其他活跃README／指南／AGENTS/MEMORY/TODO均改当前入口。
本库没有被此次路径迁移影响的独立skill文件；未生成／改全局skill、CI或agent集成。

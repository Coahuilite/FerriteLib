# 开发映射、现状与开放偏差

这是源码／证据映射，不是第二份规范。现行要求从[入口](../specification.md)到达；
[来源覆盖](source-coverage.md)给旧条款和当前唯一归属。非本次修复段落的历史行号为一基，源基线
cf1cf6456096171fb47c012369bcd0c2f29e968f。FL-F01/F02/F03 已在该 HEAD 的 dirty 工作树修复；
下列对应段落区分实际源码机制、已执行既有检查及未执行原生／精确故障组合。

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

# 来源覆盖与唯一正文对账

本表追踪有效条款／稳定 ID／值和条件，不另立规范。源基线为
cf1cf6456096171fb47c012369bcd0c2f29e968f；被替代正文在[完整归档](../archive/specs-before-20261029/INDEX.md)。
表中旧文件名均指该归档内原同名文件。后续已确认的细则优先于旧文中的准备／pending措辞；
实现错误不削弱要求。原基线核心行为是整理与补录，不产生新行为 delta；本次FL09真正新增的已采用目标另归下节change，尚未实施。

<a id="fl09-capability-adoption"></a>
## FL09基础能力：2026-11-08采用对账

采用依据：WO-UIKIT-CAPABILITY-SPEC-20261108 r2及其PM PLAN（2026-11-08T23:26:09+08:00）；
r3正式采用FL Phase A r2的寿命／焦点与IME限定（PM于23:28:43消费），不改变r2放行范围，
输入RPT-UIKIT-SPEC-FL09-A001 r1/r2、RPT-UIKIT-SPEC-TOOLS-A001 r1。它们是文档目标采用，不是实现或native接受；
以下转录足以让clone理解来源，不要求私有协调目录或另一产品树存在。

r3采纳FL补充r2的原合同限定；现有寿命／交互正文、change及FQ已符合，故保留其语义。既有composition与确认／取消优先仍是旧保证；新跨层文本索引要求仅约束实际新增selection/replacement/caret接缝。

| 来源／决定 | 唯一正文／含义 | 状态 |
| --- | --- | --- |
| 冻结v3 UiBindings.NotifyChanged注释；既有ORDINARY、RU2-B/E、R9-FACTS；可靠性WO r1双线修复 | binding普通公告、layout完整依赖验证、observation当前资格；同值／冗余／离页键不加可见过滤条件 | 旧保证澄清与源码修复；[R1现状](implementation.md#fl09-reliability)含构建／既有检查及原caller准确新载体after，待CO／PM接受，不造ADDED |
| END/H1/H2、CAPTURE/TEXT、DEV-02/04/06；WO r3采用FL补充r2 | 隐藏保相容稿、实际退休新mount不复活稿／地址；同Host／多Host义务分开；晚公告／失败／Close不漏义务；读取不执行 | 旧保证保留；反例是规格推演，未新增执行case |
| R9-STATUS、DEV-02；Tools分析FQ1–FQ6 | NotFound子树不等于Host关闭；prefix缺席不证不存在；HitTest空间、Capture更替、借用退出各保原义 | 旧语义澄清，源码／版本见[语义对账](implementation.md#fl09-tools-semantics)；Tools产品修复另归其owner |
| WO r2新双轴端点／范围采用 | [FL09-SCROLL-2D](changes/fl09-basic-ui-capabilities/specs/core/layout/spec.md)、[FL09-AXIS-INPUT](changes/fl09-basic-ui-capabilities/specs/core/interaction-editing/spec.md) | 已采用ADDED、未实施；仅显式新能力，旧纵Scroll不变 |
| WO r2有限画布／输入采用 | 同change的FL09-VIEWPORT、FL09-VIEWPORT-INPUT；[FL17-CAPABILITY-INPUT](changes/fl09-basic-ui-capabilities/specs/adapters/rimworld17/spec.md) | 已采用ADDED、未实施；正比例缩放／平移，单一几何，编辑留视口外，不先定签名 |
| WO r2 inline优先、K1／modal／OS桥后续 | FL09-DETAIL-FOCUS、FL17-ADVANCED-EDIT；inline本身用现有结构 | 后续ADDED、未实施；K1延期不改签完成，游戏InfoCard／Def与shader不纳入基础核心 |

原运行来源转录：RPT-DEMO-RAVEN-A001 r1、A002 r2及a002-input-baseline/COVERAGE的更正；
slice-001 provider MVID `9c47da59-e64d-43d3-ae58-f87c943e24c6`失败；scene-002 `efa37169-9aa5-4636-b222-4c3ae0745765`全通知成功反例；
scene-003 `92b3f0a4-05f1-4790-a529-6e86bed2efcf`仅候选构建／native未验；scene-005 `6253d424-6051-449b-a2e9-0942cb9e800f`、
006 `82b67f3a-c09c-45d1-b227-4fb47c7ca701`后续consumer成功。均按实际固定v3，不用当前HEAD代载体；
[实现账](implementation.md#fl09-reliability)保关键值、强度和未知。

RPT-DEMO-NIVARIAN-A001 r3／当前CAPABILITIES及v3/v4/v5/v6原状态说明：Button平移／缩放、Rule连线、
分页与inline XML详情可组合；v6窄栏全文修复已有原生定点证据，不能泛称库不支持信息展示。
缺的是通用横向内容／空白画布抓取与变换接缝，不是研究业务／shader。原图与游戏运行期Def来源缺口保原owner。
这组真实consumer证据支持一般能力需要，不用于晋级新specialized kind。

官方机制来源与采纳取舍见[change design](changes/fl09-basic-ui-capabilities/design.md)；只作机制比较，不整套采用Qt/WPF/Web规范。

## 条款和稳定 ID

| 原有效来源／条款 | 唯一现行正文与保留含义 | 处理 |
|---|---|---|
| README A1；A2.1 | [structure](specs/core/structure/spec.md) FL08-STRUCTURE：四项平行职责，业务权威，不是四树／线程；[public-authoring](specs/policies/public-authoring/spec.md) EXTENSION-BOUNDARY | 合并架构目的；R1结构、R2布局、R3行为、R4会话、R5绑定、R6外观、R7声明、R8适配、R9观察分别映射下列能力，不固定类／程序集数 |
| README A2.2–A2.3；s01 shared behavior；acceptance FL08-SHARED/H1–H5 | [interaction-editing](specs/core/interaction-editing/spec.md) SHARED；S4/S5 | 默认／独立／headless共实现，无绘制不等于隐藏／删除；指针要真实几何，语义不伪造；旧H1–H5是共享证据场景，不与后续寿命H1–H3混作新要求 |
| README A3.1；acceptance FL08-IDENTITY/I1–I2；bse owner scopes | [structure](specs/core/structure/spec.md) IDENTITY/REPEAT；interaction SELECTION | 同Bind独立实例、key非ordinal、目标作用域与UI身份、相容更新、retarget不串稿 |
| public S1；assembly定义段 | [structure](specs/core/structure/spec.md) DECLARATION/LITERALS；[public S1](specs/policies/public-authoring/spec.md#s1) | XML/C#共同不可变输入、parsed属性、共同compiler/schema、located错误，无表达式／反射 |
| assembly Repeat；item-binding全文；public S3 | [structure](specs/core/structure/spec.md) REPEAT/ITEM-CONNECTION/LATE-ITEM；S3 | 完整 scoped binding roles、同项anchors、injective地址、先验全部key、当前对象、删除立即断展示订阅；过滤成员与权威成员分开；晚绑定原fail-soft保留 |
| README A3.3；acceptance FL08-END/E1–E5 | [lifetime-recovery](specs/core/lifetime-recovery/spec.md) END/FAILURE-GEOMETRY/RELEASE；interaction REQUEST | 隐藏保稿不blur、移除释放不转移、Close幂等／回调Close停遍历、业务副作用不回滚、未访问不等于删除 |
| README FL08-U1 | [binding-reactivity](specs/core/binding-reactivity/spec.md) U1 | 立即写／主动读／不返已知过期derived |
| README FL08-U2；reactive logical operation | binding-reactivity U2 | 嵌套外层batch／复合Set→Invoke，不回滚业务 |
| README FL08-U3；reactive present consistent；incremental stable demand | [presentation-execution](specs/contracts/presentation-execution/spec.md) U3/SPLIT-PHASE | 每轮先需求同步，稳定回调也冻结；处理中变化下次合法点，不固定tick/Repaint/frame |
| README FL08-U4；s01 F1；host qualification | [interaction-editing](specs/core/interaction-editing/spec.md) U4/CAPTURE；presentation IMMEDIATE-REQUEST | hit有效几何，执行重查最新业务；资格读暂离展示不drain；终止不被新起始guard阻断 |
| README FL08-U5；reactive responses；failure budget | binding-reactivity U5/R2 | 请求不合并丢、先失效后读、非递归队列、初次响应同drain、每响应256预算／独立队列继续 |
| README FL08-U6；reactive serial/pure | binding-reactivity U6/ORDINARY/TRACKED | 新显式纯阶段和owner串行／跨runtime诊断；不倒灌旧getter/Measure/串行交接普通注册条件 |
| README A3.4；reactive scalars/options/structure全文 | binding-reactivity TRACKED/LIST；presentation BINDING-OWNER | equality明确、同结果换依赖、结构与项属性独立、immutable展示、无deep观察、endpoint同通道 |
| public S2 proxy后续合同；refactor proxy | presentation BINDING-OWNER | BindingOwner、真实minting endpoint、between-pass换backing、验证后替换／失败保旧、重新取得endpoint，不推断新业务目标 |
| failure纯显示／R1；README A3.6 | binding-reactivity R1；lifetime RU3 | 当前invalid≠last display、首次unavailable、失败实际branch依赖、相关变化/Retry-only，不绘制重试 |
| failure managed callback／R2 | binding-reactivity R2 | handle初次失败仍在、pause、owner Resume读当前，业务写已生效，无自动重放 |
| failure H1/H2/H3 | lifetime H1/H2/H3 | 多Host共享独立owner、明确需求不等于焦点、隐藏不停止共享服务、A17非B23，迟回调不请求 |
| failure retained control details | lifetime RU3；appearance DESCRIPTION | 纯paint事实不运行Draw/Observe/requests，可靠稿静态显示，坏eligibility拒绝，必要共享layout失败整Host输入停 |
| README A4.1；layout全文；acceptance FL08-LAYOUT/C3；L1 | [layout](specs/core/layout/spec.md) LAYOUT/L1/PLACEMENT/SCROLL | 父持外框、自然需要／固定限制、Min/Max不足可定位；9容器、finite siblings、显式scroll/clip、1px不是可用min |
| layout SharedLabels；property labels | layout SHARED-LABEL | 五field真实文本/font/density贡献，max传播，窄stack、空无band、Dropdown未opt-in80列 |
| layout-reuse RU2 A–E全文 | layout RU2-A…E | scope/完整captured inputs/legacy Measure条件/metric stamp/local plans/effective publication/事务 |
| incremental全文 | layout RU2-E/FULL-FALLBACK/LAYOUT-FAIL；[implementation](implementation.md#incremental) | 稳定域事务收缩、source同步先行、候选写隔离／旧snapshot可读；实现存储和work counters移开发映射，不当public ABI |
| incremental r4 remaining-height | layout FULL-FALLBACK | 多roots后续含Fill，前root任何高度改变含shrink均完整传播；既有检查缺组合targeted case据实保留 |
| README A4.2；property全文；s01 P1–P6；acceptance FL08-PROP | [appearance](specs/core/appearance/spec.md) PROP/PROP-REMOVE/LIVE-PROPERTIES | runtime/inline/local/ancestor/page/host逐属性、同值意图、删除当前下一来源、sparse/Mixed/活只读view、不回滚runtime声明 |
| README P1；theme-migration；property palette | appearance P1/PALETTE；[default](specs/policies/default-wrappers/spec.md) DEFAULT-LOOK | 30roles独立、五metrics、有限颜色语法与透明、无旧26bag/旧ColorXML；字体不乘density，bare容器padding不继承 |
| assembly page appearance；s01 host declaration flags | appearance PAGE-STYLE；definition-update STYLE-UPDATE | sparse页面应用独立于当前host声明，Remove不复原创建时全包，ApplyTo原条件不变 |
| wrappers七描述用途；README B5 | appearance DESCRIPTION；default INVENTORY | Text/Heading/Status/Image/Decoration/Progress真实事实，描述无input／draw，known-null与unknown分开，raw range不改模型 |
| layers/images图像与BG1全文 | appearance IMAGE/BACKGROUND；default DEFAULT-LOOK；adapter DISPOSE | scope token／supplier纹理、Contain/Cover/Stretch、inner alignment、AspectHeight式子／fallback、manifest/instance/null分权、paint顺序 |
| layers L2；s01/contextChild | interaction L2 | 默认真实操作区、disabled可见操作仍阻挡、decoration不opaque、optional custom精确能力、旧custom未知边界不追加前提 |
| bse boolean/selection/expansion全文；README B2 | interaction SELECTION/REQUEST；default F1/F2/F3 | typed真实值含null、Missing不repair、同值一次、部件优先、close-before-request，不制造focus return |
| editing全文；README B1；E1–E5 | default E1…E5；interaction EDIT/READONLY/TEXT | Live=false与合法显式true、有效blur、invalid/conflict保稿、normal-return先结束、失败不标发布／下一明确请求、Cancel非Undo |
| README B3；editing N1–N3 | default N1/N2/N3；interaction TAKEOVER | 10/20/Step1→21；明确30→只30；capture先于blur但不清稿，其他sameBind无接替权，invalid不能step |
| README B4；acceptance FL08-TEXT/A2/G1 | interaction TEXT/READONLY；default EDITOR-KEYS | 单/多行原生selection/copy/IME、composition优先不双提交；Return/Ctrl+Return，实际滚动，readonly前置不采用变更返回 |
| wrappers chart；README B5 | default COLLECTION-DISPLAY/POINT-IDENTITY | normalized点、6逻辑单位圆、0.0001差量、press无request、changed drag一次，keys可选／legacy index条件不变 |
| host A8/K1；README A3/B2/B3后续决定 | default EDITOR-KEYS/CANCEL-LADDER | 首版新增focus/traversal与Button/Checkbox/Slider键映射延后；Slider无声明时1%跨度／Home/End只是延后建议而非现行默认；原生编辑和现有Accept/Cancel未延后；ladder逐层一次 |
| public HelpRef后续条款；refactor命名与fallback | [help-output](specs/contracts/help-output/spec.md) HELP/GRACE/OUTPUT/RELEASE | opaque明确refs、无推断Value/Text、pass原时序、publisher ownership连端点、same-topic新owner不清、cleanup不重放 |
| assembly update全文；public seal后续条款 | [definition-update](specs/contracts/definition-update/spec.md) PREPARE/STAGE/SEAL | 所有Host先stage再退休、失败完整rollback、accepted revision真实、清理失败不能伪tree-onlyrollback |
| README A5.1/R7；文件更新原合同 | definition-update DOCUMENT | watcher/scheduler有界与主线程／time source、last-good，非后台Host |
| README A5.2/R8/FL08-PLATFORM；refactor完整closure | [rimworld16](specs/adapters/rimworld16/spec.md) PLATFORM/SUPPORT | 同源完整中立传递闭包，排除platform/object/reflection；线程安全≠native执行权；1.6/1.7分层 |
| host标准／embedded；public S6及borrow后续条款 | adapter FRAME/EXECUTION/DISPOSE/WINDOW；S6 | 一次callback同frame、实际native资格、borrow执行不由Host销毁、idle switch/active refuse、finally native释放 |
| README A5.3/R9；host bounded read全文 | [observation](specs/core/observation/spec.md) R9/FACTS/STATUS/DISCOVERY | detached事实、各独立时间/来源、prefix预算、能力未知不猜、无Draw/querywrite、512weak例外 |
| public S7；legacy fit现有XML及audit F02 | observation DIAGNOSTICS/FIT；[observation-boundary](specs/contracts/observation-boundary/spec.md) | 基本stable与详细experimental分开；fit默认/Attach/Detach语义；Trace/legacy-fit窄隔离已由2026-10-29批准的两项ADDED补入观察连接 |
| public S1–S7全文；api-tiers | [public-authoring](specs/policies/public-authoring/spec.md) S1…S7 | 入口与必要依赖，ordinary/tracked/noHost/headless/visual-only均合法；全部Session不升stable；原机器清单不动 |
| README A7；public/API tiers后续major规则 | public-authoring COMPAT/CARRIER | 同adapter的source/DLL/XML/meaning/conditions；major加两完整minor时间下限；[min,max)；单载体/version轴 |
| README A6、charter、AGENTS ecosystem规则 | public-authoring GROWTH/KIND-GATE/EXTENSION-BOUNDARY | 一般capability无双consumer门槛，specialized kind保一个release使用后的独立promotion条件；无新全局可变队列／后台Host |

## 关键值、单位与默认的完整归属

以下保留真实来源事实，不把独立预算混成统一“每帧预算”。原API常量和optional defaults还由原位签名清单固定。

| 原条款／来源事实 | 值／单位／条件 | 当前归属 |
|---|---|---|
| Manifest及public S1 | Schema2、depth16、nodes512、inline styles32768字符 | structure DECLARATION |
| Theme／P1 | Small；Padding6/Spacing4/Gap6/RowHeight28/Hairline1逻辑单位；banner无声明fallback Tiny | default DEFAULT-LOOK |
| default editor | Live=false；Multiline=false；numeric invariant0.## | default E1/E3 |
| chart | radius6逻辑单位；normalized差量tolerance0.0001 | default COLLECTION-DISPLAY |
| invalidation | None0/Paint1/Measure2/Structure4/Everything并集 | binding ORDINARY |
| help | grace0 passes；US15是consumer政策，不是FL默认 | help GRACE |
| UiNotifyAdapter XML合同 | pending keys默认256、正数配置；capacity可批内flush，MapAll不涵盖未映射非空property | binding NOTIFY |
| reload | quiet0.25/retry0.5/maxDefer2秒，maxRetryAttempts3，zero retry合法 | definition DOCUMENT |
| document limits | docs64/dependencies256/reports32/bytes1048576；Dev AutoWatch true/Release false | definition DOCUMENT |
| diagnostics | subscriptions64；budget32默认/256最大；timing60 Host passes非FPS | observation DIAGNOSTICS |
| R9 budget | 默认64nodes/8depth/512chars/32items；最大512/32/4096/128 | observation STATUS |
| distinct engineering budgets | discovery512 weak／同response每drain256／font memo512／sparse32-entry pages | observation DISCOVERY；binding U5；layout有效性，存储在implementation |
| layout不足 | diagnostics128+Truncated；Stepper 80→131需要/51不足，field37..101、plus107..131；186 label stack门槛非硬min | layout L1；细节旧实测说明保归档，不升级可用性 |
| shell | margin0/title56/side-bottom20/accent3逻辑单位 | default WINDOW-DEFAULTS |
| catalog/nativeoptions | 多实例true、SetFocusOnActivate true、FollowClicks默认；OpenOnly/Manual；nullable选项null | default WINDOW-DEFAULTS |
| images/BG1 | Contain/Center/Center；AspectHeight显式，Height优先 | appearance IMAGE/BACKGROUND；default DEFAULT-LOOK |
| popup既有XML合同 | 24逻辑单位/row，3rows/notch，最低一行例外；只画可见行，不自动缩宽／通用clamp | default POPUP-GEOMETRY |
| fit既有XML合同 | disabled、MaxReports48、Tolerance1.5逻辑单位；Attach不Enable | observation FIT |
| Api/build/About | 0.8.0主次一致；源SHA非compat | public-authoring CARRIER |

## 归档、替代和不进入 current 的内容

- 20份直接被替代规范／工程规范全文保留；各页加历史状态说明并重定位 Markdown 目标使归档仍可阅读，未删原段落。
  [归档索引](../archive/specs-before-20261029/INDEX.md)列原路径、当前去向与历史身份。
- README A7旧 Draw/Live/Font变迁表、acceptance S0–S4实施准备／当时pending、迁移矩阵的逐次证据、
  refactor/theme old→new拼写属于历史／作者内部迁移参照，不把旧内部用户前提交给首次公开作者。
- 真实接入证据和 counter 教程原位保留，更新当前链接；原0.7及更早记录不改语义，
  其native接受不能外推本线。dated消费者报告保独有比较表，不删或改签。
- “US尚未采用Dev”被后续固定消费事实取代：3e42cc180f91f5f58b040724e4ad76ea060802e6消费
  FL6ec0c31a76b552dc74f30d7969862949c955d4a8；不表示本次文本已被重新编译。
- theme-migration旧 Unity color indexer 和property“platform indexer”用语不正确：
  当前签名／UiTheme.cs:126 是 UiColour；按已批准中立闭包补录，不造新API。
- wrappers“Register签名仍在”、bse旧 DropdownOptionRow(session)是被已批准refactor替代的内部旧形状；
  public S4、refactor表、registry Define及API清单是现行依据，不恢复ambient第二路径。
- layout早段“L1待最终决定”已被该源集后续确认所替代；E/N/P、S1–S7也非pending。
- layout-reuse开头把当时新surface一概称public-unstable的旧状态，由后续S1–S7及现行API tiers逐type/member清单替代；本次不改分级，IUiMeasureParticipant等按原位清单，不按旧概括推断。
- FL-F01/F03恢复已有要求；FL-F02 Trace/legacy-fit窄隔离为2026-10-29批准采用的新保证，见[开发映射](implementation.md)与[采用记录](changes/isolate-observation/proposal.md)，
  不把两者混作新增全部诊断保证。
- 与本区无关的音频动作／时钟／配置决定不抄入FL；consumer筛选／分页／window断点保持consumer owner。

原文来源补录与本次编排不替代行为证据。需核具体实现和关闭条件时，继续读[开发映射](implementation.md)，
而非把表中“已覆盖”解读为运行通过。
补录的既有源码合同：UiWidgetParts.cs:39–45（operate→active检查→DisplayOnly paint）；UiWidgetCatalog.cs:17–35,61–74（detached ordinal catalog／精确pair不fallback）；UiNotifyAdapter.cs:7–60,146起（MapAll范围／256容量批次）；UiNumericEdit.cs:12–30（反向bounds Clamp→min）；UiHost.cs:97–118（外部style优先且显式报告inline被忽略）。它们分别归 COMPOSITION、S4、NOTIFY、E3、STYLE-SOURCE，属于现有行为补录而非新保证。

# 公开作者入口与采用承诺

## Purpose
使作者按需要采用完整能力切片，知道依赖、寿命、等级和升级边界。FL0.8.x 是首条面向公开作者的基线；更早内部版本是历史材料，不是先升级旧框架才能学习当前用法的前提。

0.9 区域保留已按 WO-FL09-RETAINED-DISPLAY-20261111 r2 采用、未实施；本页新增区域条款及
相关显式例外仅适用于主动采用的新能力，不表示当前 API 已可调用。行为唯一正文归本页，
采用来源见 [fl09-retained-regions](../../../changes/fl09-retained-regions/proposal.md)。

## Requirements

<a id="s1"></a>

### Requirement: S1 同一声明入口可装配与替换
作者 SHALL 可用 XML Parse/ParseFile 或 UiLayoutManifest.Create/CreateWithBackground 取得同义定义，以普通 C# 函数组合 UiElementSpec，而不序列化成 XML。实际 Host 采用共同 kind／schema／绑定验证并提供 UpdateDefinition，文件工作流另选 UiDocumentSource/Service、ReloadPolicy、显式 time/main-thread；不强制文件注册。具体不变式唯一归[结构](../../core/structure/spec.md)与[更新连接](../../contracts/definition-update/spec.md)。

#### Scenario: 程序组件更新标题
- **WHEN** 作者创建相容新定义并调用 UpdateDefinition
- **THEN** 同一 Host 接受完整候选后才发布新 revision，不需文件服务

<a id="s2"></a>

### Requirement: S2 普通／tracked／请求通路均可独立采用
作者 SHALL 可用 UiBindings 注册普通值、只读、action/command、choices 和 NotifyChanged，或显式接入 ReactiveRuntime/owner/tracked；运行控件只需 IUiBindingAccess。UiValueEndpoint/UiChoiceEndpoint 持真实类型／writability／owner，selection 用 `UiChoice<T>`；BindOptions 保一般 collection 角色而非第二选择协议。读／请求、batch、purity、proxy与失败依[绑定](../../core/binding-reactivity/spec.md)及[展示执行](../../contracts/presentation-execution/spec.md)，不以 Host/订阅为全部公共采用前提。

#### Scenario: 已有普通业务类接字段
- **WHEN** 作者只使用 BindValue/NotifyChanged
- **THEN** 无需模型基类／runtime graph／纯度声明仍可使用默认页面

<a id="s3"></a>

### Requirement: S3 稳定项辅助与手工地址并存
UiItemBindings/Schema SHALL 可把安全有序快照映射为 current-key 字段／command，手工调用也用 UiBindingAddress(source,item,field).Key；可选 helper 不成为普通绑定或原生控件的注册前提。筛选连接寿命、刷新及外部事件 detach 由作者明确；业务对象不归 helper 销毁。

#### Scenario: 没有持久 ID 的第三方对象
- **WHEN** 接入层分配 consumer-owned session-lifetime key
- **THEN** 可按该明确寿命接入而不改第三方存档格式
- **AND** 不宣称它跨实际销毁永久保持身份

<a id="s4"></a>

### Requirement: S4 自定义选择能力，native 需要显式 owner
作者 SHALL 可实现 IUiWidget 或组合 UiWidgetParts 的 measure/paint/operate/pointer/describe/validate，以一个不可变 UiWidgetDefinition 和 schema 经 UiWidgetRegistry.Define 注册；scope 本地优先再 core，不存在 Register alias。catalog 查询只读、detached、按scope/kind ordinal排序且不实例化 factory；TryGet查询精确声明pair，不做resolver的core fallback。普通 widget 接收 context，可使用 Bindings/Controls/Drawing/Theme/Metrics/Translation/ViewWidth/Child/State/Properties/InteractionScope/Invalidate/SharedLabelWidth 等；Context.State 的明确 release 是 UI 所有，不自动 Dispose 任意业务对象。

#### Scenario: 原生控件不采用 Host
- **WHEN** 作者保留 UiSession，CreateContext 用明确 source/scope/metrics/theme/translation/reader/width，实际回调仅 SetFrame 一次
- **THEN** 可在同 frame 操作多个命名控件
- **AND** 缺失／foreign／released owner 诊断而不转向 ambient active node

<a id="s5"></a>

### Requirement: S5 独立视觉不依赖页面
作者 SHALL 可直接用 UiTheme/Palette/Colour/Geometry、style resolver、UiThemeDraw、IUiDrawing/ITextMetrics 与中性布局；无 Host/XML/registry/reactive 前提。font、颜色、五度量分别 Set/Remove，具体来源及图片尺寸规则依[外观](../../core/appearance/spec.md)。原生纹理经实际 execution.Images 登记为 scoped UiImage，不将 Texture 类型注入核心。

#### Scenario: 单独画一个表面
- **WHEN** 调用者提供 drawing、theme 和 rect
- **THEN** 可测量／绘制而不创建 Session 或页面树

<a id="s6"></a>

### Requirement: S6 标准窗口与 embedded 是平等公开路径
UiPageWindow SHALL 可直接构造，Catalog 可选按 UiWindowKey/Options/FocusPolicy 注册／open／activate／lookup／close；UiWindowHost 保支持的窄 shell/failure/close hooks。embedded 作者保留 Host 与 execution，向 DrawFrame 明确 rect、CaptureFrame(nativeInputAllowed)、execution。InputState、ElementProperties、scroll／scope／Invalidate、Accept/Cancel 与 lifecycle hooks SHALL 支持普通外围作者，不需 runtime tables。借用 execution、cleanup 和原生权威唯一归[1.6 adapter](../../adapters/rimworld16/spec.md)。

#### Scenario: 不通过 Catalog 打开一页
- **WHEN** 作者直接构造标准页或嵌入已有窗口
- **THEN** 仍获得相同核心能力与恢复合同，不追加注册门槛

<a id="s7"></a>

### Requirement: S7 定位错误与基本观察可用
作者 SHALL 可使用版本 Require/API、located contract error、LayoutFault、presentation/recovery status 及基本 bounded Diagnostics；详细 R9/discovery/budgets 与 Dev geometry 保实验。公开方法的实际采用等级 SHALL 只由[API tiers](../../../../api-tiers.md)及其明确 member exceptions 决定；supported signatures/defaults 唯一在[stable-api.txt](../../../../development/0.8/stable-api.txt)。S1–S7 不提升 Session／Node 全部成员；包括现有帮助访问在内的 advanced 用途仍合法但非 stable 承诺。

#### Scenario: 需要 CurrentHelpRef 的高级作者
- **WHEN** 它使用现有 advanced Session seam
- **THEN** 可按[帮助连接](../../contracts/help-output/spec.md)工作
- **AND** 不由此推导整个 Session 或 Trace 已稳定

### Requirement: FL08-COMPAT 支持语义与签名共同受保护
同一支持 game/adapter 的 stable source、已编译 DLL、XML、数据含义、合法条件、文档行为 SHALL 在消费者 [min,max) 内受保护。发布后 breaking supported stable 要求 major；替代有效后才能 deprecated，且再保两个完整后续已发布 minor，RC/patch不计、1.x不缩短。例如0.9弃用须保0.10/0.11，0.12只是时间下限，不授权 breaking minor removal。public-unstable 后续 minor 可明确迁移，冻结 patch 不任意破坏。当前首次公开基线整合已批准内部改造，不建立永久双 API。

#### Scenario: 后续实现需要更改 stable 合法条件
- **WHEN** 签名没变但旧合法调用不再可用
- **THEN** 仍按行为／支持破坏处理，不能称内部优化绕过承诺

### Requirement: FL08-CARRIER 单载体与版本轴明确
仅 coahuilite.ferritelib SHALL 分发 net472 的1.6/Assemblies/FerriteLib.UiKit.dll；不加 Def/Patch/Languages/textures 内容。消费者 SHALL 不捆绑第二份 DLL，并在自己的入口 Require 已测 [min,max)；游戏 modDependencies 不能表达版本。Api、About modVersion、VersionPrefix major/minor 一致，当前0.8.0；ProductVersion/InformationalVersion 的源码身份不是兼容轴。标准安装包闭合文件集为About/About.xml、LoadFolders.xml、LICENSE、version.txt及上述DLL，不能带PublishedFileId或游戏内容。公开消费取实际 Release asset；本地 fixed input 与未来 tag/mainSHA 不互冒。

#### Scenario: 运行环境有两份 DLL
- **WHEN** Require 枚举已加载载体
- **THEN** 给可读冲突诊断，不能依加载序默认为消费者选中正确版本

### Requirement: FL08-GROWTH 内聚新能力有正向路线
一般能力 SHALL 按真实需要、职责／状态／寿命／变更与失败边界选择扩展既有能力、独立模块、新核心或正式责任迁移，不以类数／文件数／两个消费者作门槛。提出方明确 need/gap、authority/lifetime/thread/clock、最小完整入口→消费者切片与证据、adoption/exit。原 owner 提供具体合同约束和替代，不是无限否决；跨职责实质决定依项目授权裁定。接纳后切换单一权威和实际 callers／cleanup，不永久维持例外。

#### Scenario: 通用能力无法由现有 owner 内聚表达
- **WHEN** 新核心能完整持一个真实共享保证，且邻接合同清楚
- **THEN** 可提出最小完整能力及采用／退出路线
- **AND** 不要求先有两个消费者或新 DLL／通用 plugin manager

### Requirement: FL08-KIND-GATE specialized kind 不等于一般能力
新增命名 specialized kind SHALL 保既有 provenance／中立性／无新增全局 mutable static／harness-drivable gate：来源是外部真实手写需求，不以本库 demo 代 consumption。可先在树上组合 registry kind 与共享能力，经过一个 release 的使用后附来源转录与度量提出 promotion；只有页面结构上不可能表达的用途才走明确豁免。一般 capability 真实需求可直接立项，不被此 kind 门槛阻断。own measure/interaction/hit 的组合可有稳定名字，不强迫原子化全部复合。

#### Scenario: 作者请求一个便捷包装
- **WHEN** 仅有名称诉求而没有该 kind 需要持的真实状态／测量／几何责任
- **THEN** 先用既有树组合，不因请求就冻结新 vocabulary
- **AND** 真正一般身份／通知／协调／恢复能力不因缺 specialized kind 引用被否决

### Requirement: FL08-EXTENSION-BOUNDARY 部分采用合法
消费者 SHALL 可保库外业务和 raw IMGUI；FL 对树内测量／身份／恢复等保证不扩成对任意外部绘制的证明。map-layer/world-space text 永不属 UiKit；无 uGUI、reflection/DI/codegen、manifest表达式、Def hot-reload、通用业务undo、虚拟列表或拖放框架承诺。新增能力须走明确完整 scope，不能借文档归并暗建框架。

#### Scenario: 作者只采用一个 shared editor
- **WHEN** 其他游戏 UI 保持库外
- **THEN** 本编辑器仍按其显式合法 context 工作
- **AND** 不要求把全部窗口、业务或原生调用迁入 FL 才获得该能力保证

## FL09已采用目标的范围

[基础能力change](../../../changes/fl09-basic-ui-capabilities/proposal.md)持显式新双轴、有限画布和后续输入／详情／可访问目标，
均未实施；一般能力不等于新specialized kind。现有inline详情可组合，游戏InfoCard／运行期Def接线仍归consumer/game adapter。
shader不属基础补齐；通用仿射、变换内原生编辑／caret、完整modal／K1与OS桥不因本次文档而成为当前可用承诺。
旧合法入口／兼容、单载体及各目标边界不变；当前1.7目标仍沿[DEV-06/07](../../contracts/developer-tools/spec.md)，不跟改保留0.8／1.6合同。

## 0.9 区域保留：已采用、未实施

本节仅约束主动采用的新能力；其余原条件保持。新增闭包已定 Public-unstable，尚无对应导出。

### Requirement: FL09-REGION-AUTHOR 新能力主动选择，旧语义不退休
公开作者 SHALL 可在普通／embedded Host 上选择单个已声明区域、明确表示读取 map，
并为现有自定义 kind 实现公开 participant，不依赖 internal、Tools、Catalog、模型基类或整页 reactive。
XML／程序定义继续共用原声明；新增附件不重解释 Schema 2／3、不增加全页默认、不替换旧绑定注册。
旧签名、旧调用／override、旧读取与释放语义 SHALL 保留，无弃用或退休；退出新能力恢复原调用路线。
无Host视觉／Session独立控件保持原合法入口，新区域保留不成为其前提。

本组整个新增 API／必要新类型／实现接口闭包 SHALL 按 PM 的 r2 决定为 Public-unstable；
现有 Stable 依赖不降级，后续迁移不解除功能合同或旧 Stable 保护。0.9 采用是实施目标，
未实现的名字不得列为已导出／当前可调用 API；不以换minor免除既有兼容义务。

#### Scenario: 作者只迁移一个 sparse kind
- **WHEN** 新 participant 只在一个区域被明确采用
- **THEN** 未采用实例仍运行原 IUiWidget，普通 Get／Set／命令不被新源拦截
- **AND** 可以不迁其他页、未证明贡献或独立原生控件

#### Scenario: 新增闭包已采用但尚未导出
- **WHEN** 采用记录明确等级和依赖闭包
- **THEN** 后续源码／文档据其实现并提供真实迁移，旧 Stable 入口持续合法
- **AND** 采用文本与性能目标本身不宣告API已可用或native已通过

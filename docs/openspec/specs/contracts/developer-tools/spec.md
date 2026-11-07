# 可选开发工具与同源预览连接
状态：现行，0.8.x已选维护扩展；public-unstable，不升API版本或Stable等级。仅采用 `uikit.offline-workbench.v1`／Legacy08 的0.8构建提供本扩展，任意历史0.8不自动支持。普通net472／1.6生产入口不变。

## Purpose
让开发工具读取和操作作者的真实页面，复用原 Host、布局、编辑和寿命；库提供中立接缝，不内置工具产品。

## Requirements

### Requirement: DEV-01 一个库载体，可选连接
库 SHALL保持单一载体；普通 Host、独立控件、visual-only、R9、日志与既有合法 Dev 仪器不增加 Tools 前提。工具 UI、快捷键、协议、传输、LLM接入及脱机图形宿主由外部工具持有。工具连接只附着真实 Host／Session，不复制业务、节点树或第二个身份权威。新增工具仪器休眠，选择兼容目标并显式启用后才采集；连接退出沿 owner 恢复原仪器状态，不关业务页、不提交草稿。同一Host同时只由一个连接拥有工具资格。断开取消pending；后台断开立即关闭队列，在下次owner回调恢复仪器，Host／Session关闭也关闭连接。已有直接诊断订阅保原条件。
#### Scenario: 不安装工具
- **WHEN** 普通消费者按原入口创建页面
- **THEN** 原功能、日志和合法诊断可用，不要求开发包或工具进程

### Requirement: DEV-02 纯读与一次原 owner 操作
R9 与语义 capture SHALL纯读、有界、注明来源；不创建编辑器、prepare、arrange、draw 或推进业务。人类与 agent 的投影共用原节点事实，缺自定义描述／操作时如实 Unsupported。工具操作 SHALL独立排队，在原 owner 下一合法边界至多取一个 intent，并重核可见、已布局、只读、命令谓词、模态／capture 和当前原生输入资格。

地址 SHALL绑定 Host／Session、definition、mount、interaction scope、part/item；关闭、替换及不兼容重载不复活旧地址。同一连接 requestId 只尝试一次；Queued／Handled／Rejected／Closed／Failed保实际结果，Handled不保证业务接受／保存。owner异常给Failed，超时查询原ID而不重放。只读连接不可排队；后台协议线程仅使用已允许的Queue／Result／Latest／Dispose，活读取与仪器属于Host线程。

连接 queue为64、ledger为256（连接内不淘汰）、targets至多512；到额明确拒绝／截断，不无限保留。R9自己的默认64／8／512／32及上限512／32／4096／128继续由观察规格拥有，不采用某个外部工具的不同默认。
#### Scenario: 旧字段被移除后执行
- **WHEN** 已捕获字段的mount被替换，同key稍后重建
- **THEN** 旧请求Closed／Rejected且不改投新节点；重复ID只返回原结果

### Requirement: DEV-03 同源 headless 与作者准备
headless SHALL复用正式manifest、registry、binding、layout与操作，显式推进而不Draw／加载GPU。作者提供测量来源，结构几何不冒充原生字形。活原生窗口只由实际callback推进，不被工具再headless推进。

可选prepare SHALL只在同一Host显式advance／原生arrange边界调用，包括语义操作后的重排；同一Host仅一个准备附件，读取绝不调用。准备可重复，只设原布局预算，不采业务、采样或递归advance。旧preview构造metadata及无prepare用法保留。初次prepare失败使队列尚未尝试；操作后prepare失败不撤销或重放已产结果；途中关闭终止后续arrange／pump／draw，Dispose沿Host再释放preview资源。
#### Scenario: 展开帮助后同轮重排
- **WHEN** 原语义owner改变帮助展示状态
- **THEN** 同一作者预算在重排前采用；纯Capture不能替代该推进边界

### Requirement: DEV-04 文档、来源与布局输入各守原 owner
完整作者XML／模板／Styles SHALL作为一个候选经原文档事务投影。错误保last-good全文、定义及有效修订；成功后文档定位、页面和工具状态指向同一已采用来源，重复项回原模板。watch显式开启，status读取不做磁盘IO；source-path变化也不能用旧全文假装新来源已生效。

运行布局输入 SHALL针对原声明容器，区分声明值、owner输入、解析值及last-arrangement，不改写XML或definition修订；兼容草稿／滚动由原Session保持，原结构失效沿正常清理。真正作者文档结构更改仍走更新合同。外部工具不取得磁盘任意写权或定义发布owner。
#### Scenario: 作者保存无效样式
- **WHEN** 原文档事务拒绝该候选
- **THEN** 页面与来源仍为最后完整有效修订，给明确失败；状态读取不重新加载或发布半候选

### Requirement: DEV-05 几何与成本保原来源
描框 SHALL来自原draw walk和当前有效clip，区分声明／输入／解析／实际arrangement与draw，不用第二renderer或旧窗口框。几何、timing、作者window-cost分别有原pass／修订及预算；不以一次capture假称同一native帧。

可选window-cost provider SHALL纯投影原sampler的保留事实，请求沿同一有界队列、ledger与串行owner预算，至多一次。附件替换／断开／关闭不改投pending；provider持采样政策、gate、计数及cleanup，库不新增sampler。Unsupported／NotSampled／Failed、nullable未知值、partial与endReason及原pass均保留，读取不启停／推进采样，也不给普通采样／日志增加Tools门。
#### Scenario: 页面没有window sampler
- **WHEN** 工具读或请求window cost
- **THEN** 如实Unsupported或拒绝，不创建替代计时器、不将未知记零

### Requirement: DEV-06 目标宿主与观看变换分离
采用虚拟视口的原生adapter SHALL消费明确的目标逻辑空间、有效UI缩放／DPI与原生输入资格，沿原窗口测量、裁剪和执行链。工具观看缩放／平移仅由工具合成层持有，不成为库布局输入、不改definition或业务状态。库只提供真实缺口所需的中立接缝，不包含消费者名称、页签、地图或工具窗口政策；不预先规定RenderTexture-only或新增核心程序集。
#### Scenario: 工具侧栏挤小观看区
- **WHEN** 目标逻辑空间未变，仅观看区域缩小
- **THEN** 原Host布局预算、节点身份和草稿不变；工具负责显示映射

### Requirement: DEV-07 新原生开发跟随 1.7
新原生适配 SHALL以RimWorld 1.7为目标，跟进当前命名空间、程序集和API，不按小构建号／rev／游戏MVID白名单锁定支持。实际缺少接口或资源时报告具体不兼容并修复适配，不静默载旧游戏依赖或换近似绘制。该政策不放宽FL API区间、同进程单载体或旧1.6合同。自动记录实际输入只用于诊断／证据，不给用户增加小版本管理步骤。
#### Scenario: 官方移动接口命名空间
- **WHEN** 1.7更新使旧引用不再编译
- **THEN** 对当前接口修订adapter及受影响调用；原证据保原身份，不以固定旧构建作为产品路线

## 实现与用法
[连接指南](../../../../development/0.8/offline-workbench.md)给当前签名、注册与构建方法；[实现状态](../../../implementation.md)区分已有能力与舞台接入缺口。


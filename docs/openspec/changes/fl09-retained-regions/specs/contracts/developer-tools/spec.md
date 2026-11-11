# Spec Delta — 候选，未采用

基准：d07cdc04f9985bb6818a34f5f90ba20c345f0206 的唯一 current。MODIFIED 保留完整原要求与全部仍适用场景；新增的显式区域义务仅适用于本 change 的主动采用。

## MODIFIED Requirements

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

## ADDED Requirements

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

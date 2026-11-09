# 有界诊断与运行事实

## Purpose
让作者取得当前可归因的错误和只读事实，不另建模型、事件总线、影子树或写入句柄。基本错误／订阅合同与实验 R9 明确分级；观察者出站异常边界由[观察连接](../../contracts/observation-boundary/spec.md)定义。

## Requirements

### Requirement: FL08-DIAGNOSTICS 基本记录有界且按 owner 释放
基本 Diagnostics Snapshot/TryGetLatest/CountOf/Clear/Dispose SHALL 提供有界定位的版本、contract、layout、style、reload、recovery 等真实事实；订阅默认预算32、最大256，最多64个。timing 采样默认60 Host passes，记录阶段样本而非 FPS；Clear／Dispose 不重放操作或改变业务。Dev geometry sampling／overlay／dump 及详细 R9 SHALL 保持现有实验等级。

#### Scenario: 诊断 buffer 到达预算
- **WHEN** 后续事件超过有界保留额度
- **THEN** 保持有界并明确丢弃／截断事实
- **AND** 不通过记录把 subscriber 提升为业务或 UI owner

### Requirement: R9 观察不驱动 UI
详细查询 SHALL 在 UI owner 上读实际 lifecycle metadata、last committed tree 和可用只读 getter／translation／parser；不 Draw、Arrange、Set、Invoke、Observe editor、开 editor、capture 或 retry。结果 detached，无 Host/node/model/editor handles、callback 或写方法；普通 getter 自身原副作用与成本条件不被查询改写。已知 retained/hidden 节点可报稿，未 materialize 的 conditional/template 不伪造为节点。

#### Scenario: custom 没有描述／typed read 能力
- **WHEN** 观察者查询该细节
- **THEN** 报 Unsupported，控件原合法用途不受影响
- **AND** 不反射猜类型、不要求补订阅或注册

### Requirement: R9-FACTS 来源、时间与未知分别呈现
观察 SHALL 分清模型当前值、独立 editor 草稿／validity/conflict/focus、last wrapper selection 与当前可选值；保留 Host/Session/key/path/part/item/definition 归因。几何是 last-arrangement page-local，标 scroll/clip 祖先；layout time/revision、query time、当前定义不同。属性来源为 last-property-resolution；native focus、window origin、当点遮挡、像素等未采样不得由 page rect 推断。已公告 dirty 标 stale，未公告变化不能自动推断。

#### Scenario: 布局尚未跟上声明变化
- **WHEN** 观察得到旧几何及当前 invalidation
- **THEN** 旧几何标 Stale 并保自己的时间／revision
- **AND** 不把它冒充当前 screenshot 或原生命中结果

### Requirement: R9-STATUS 有界 prefix 不伪造完整结果
Sampled、NotFound、Unsupported、NotSampled、Stale、Failed SHALL 不互换；读失败保原因而不是0／空成功。默认 nodes64/depth8/text512字符/items32，最大512/32/4096/128，非法预算拒绝；节点／深度、单项文字、collection prefix 的截断均明确。typed choices 的 caption/value/type/selectable 分开；无 detached identity seam 的 opaque 对象 Unsupported，不以 caption 合并身份。bounded read 不新增全列表 walk，getter 自己投影成本不因此消失。

#### Scenario: options 数量超过32
- **WHEN** 默认预算查询其 prefix
- **THEN** 可报实际总数及截断的前缀，保留源索引
- **AND** 不复制全部 options／chart series 来回答一个有界请求

### Requirement: R9-DISCOVERY 发现索引不能成为第二权威
live discovery SHALL 只保最多512弱 Host 引用，复用既有身份，成功构造后登记、Close/Dispose移除、查询可清 dead。不得保强 Host、node/model copy、业务状态或第二身份。容量拒绝及 caller truncation 须明确；曾拒绝后缺 id 是 unknown/NotSampled，不谎报 NotFound。已持 Host 的查询不依赖此索引。

#### Scenario: 曾达到容量的 discovery 结果为空
- **WHEN** 查询无法证明某 host 从未存在
- **THEN** 保留不完整／容量拒绝信息而不是“全部不存在”

### Requirement: FL08-FIT fit 报告不同于必要测量
legacy fit SHALL 默认为 disabled，MaxReports48、Tolerance1.5逻辑单位；Attach 不自动 Enable，Detach 禁用并清 sinks，Reset 清 findings。订阅 Host 使用自身 scope／metrics／budget，不被 legacy globals 接管，style fallback 独立于 text enable。实际 metrics／paint 不是可忽略 observer；legacy optional sink 的普通异常隔离依[观察连接](../../contracts/observation-boundary/spec.md)。

#### Scenario: 只接了 legacy sink
- **WHEN** 作者未开启 fit
- **THEN** Attach 不自行启用采样，独立视觉仍不需 Host
- **AND** 不把未报 overflow 当已验证像素适配

### Requirement: FL08-DOMAIN-OUTPUT 可选领域出口不接管事实或采样
采用的 native domain binding SHALL 在原 Trip／请求失败／文档报告／fit 等事实 owner 之后输出，
不另建事实 ring，不通过 Attach／Configure 开启 fit、timing、geometry 或订阅。创建期 consumer／出口
须在真实 Host 构造前传入；旧构造、factory、metrics override 与独立无 Host 用法保持合法。
normal／warning／error 与 daily／detail 分开；合法拒绝不自动是 error，detail 在可选采集及格式化前 gate。
native sink 普通失败只隔离出站，不重试业务或改变确定结果。实际 adapter 入口及预算见
[领域诊断](../../../../development/0.8/ui-diagnostics.md)。

#### Scenario: 输出额度已满后出现独立错误
- **WHEN** binding 的 error 输出预算耗尽，新类别被拒绝
- **THEN** 首次额度通知及边界摘要明确未打印身份／计数，下一窗口恢复资格
- **AND** 不把输出账本与原 ring 的保留／丢弃规则合并，也不永久屏蔽新错误

#### Scenario: 创建失败或关闭清理失败
- **WHEN** consumer 已通过创建入口绑定，实际构造／清理失败
- **THEN** 原失败先归原 owner，native 输出带可信 consumer／可用 session，未知上下文为 NotSampled
- **AND** 创建失败仍抛原异常；关闭尝试完整清理并释放自身输出，Host 不 dispose 借用 execution

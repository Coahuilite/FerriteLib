# UI 寿命、故障展示与统一结束

## Purpose
让隐藏、目标替换、删除、可协商关闭、实际销毁和异常恢复保持不同结果。Host／Session 持 UI 状态及其自有连接；业务服务、借用 execution 和供应纹理由各自 owner 持有。

0.9 区域保留已按 WO-FL09-RETAINED-DISPLAY-20261111 r2 采用、未实施；本页新增区域条款及
相关显式例外仅适用于主动采用的新能力，不表示当前 API 已可调用。行为唯一正文归本页，
采用来源见 [fl09-retained-regions](../../../changes/fl09-retained-regions/proposal.md)。

## Requirements

### Requirement: FL08-END 生命周期结束不补业务动作
可靠隐藏／折叠 SHALL 结束不适用输入而保留相容稿，不补发 blur；显示时核对当前模型，不自动恢复 capture 或重放输入。移除、retarget、kind 不相容 SHALL 释放旧 UI 连接且不转移旧输入。可协商 close 可被业务 veto；实际 Dispose SHALL 幂等且不可反悔，不保存、提交、回滚、删除业务对象或销毁共享服务。回调内 Close 后 SHALL 不继续正常遍历／常规帧结项。

#### Scenario: 同 key 节点被删后再建
- **WHEN** 老节点已实际释放，后来新建相同 key
- **THEN** 新节点没有从永久缓存复活旧 capture／草稿
- **AND** 其他 Host 的状态保持有效

### Requirement: H1 视图只关闭自己的寿命
Host SHALL 拥有自身 presentation 计算、UI 订阅、节点及交互；共享 computation／response 置于明确外部 owner。关一个视图 SHALL 不关闭另一个视图、独立服务或其业务时钟。跨实际关闭保留长稿应由应用 pending-edit 模型持有，不承诺销毁 Session 后自动保存草稿。

#### Scenario: A/B 共用服务而 B 关闭两次
- **WHEN** B Dispose 后再次 Dispose
- **THEN** B 自有资源只尝试一次，A 和服务继续
- **AND** Dispose 不产生 Save 或业务 Undo

### Requirement: H2 可靠无展示需求可延后纯工作
显式 SetDisplayDemand SHALL 表示可靠可见需求，不是焦点或推断遮挡。可靠隐藏 Host 的回调不做展示求值／测排；已知隐藏默认子树的独占 tracked 需求可延后，共享／可见／显式消费者仍正常读。失效与订阅、R1 故障及 R2 暂停 SHALL 保留；show 采用当前输入，不补中间操作。隐藏 SHALL 挂起焦点／弹层、清 pending submission，拒绝其 Accept／Cancel，但保可靠稿。

#### Scenario: 失焦窗口仍可见
- **WHEN** B 仅失去原生／catalog 输入焦点
- **THEN** 不把它当隐藏而停止显示更新
- **AND** 只有明确无展示需求才延后相应纯工作

### Requirement: H3 外部确认保留原目标
非阻塞确认及其 UI 回调 SHALL 归自己的生命周期，保留打开时的目标，不能在确认时改读后来选择。业务入口仍核最新资格；重复 close 或销毁后迟到回调 SHALL 不发操作。本能力不规定 B-local modal 或全窗口遮挡分析。

#### Scenario: 打开时 A17，后来选 B23
- **WHEN** 用户确认原确认框
- **THEN** 只向 A17 的实际业务入口请求并允许其拒绝
- **AND** 不把这次确认转向 B23

### Requirement: RU3 当前有效性与最后展示分开
失败当前输入 SHALL 保持 invalid；首次失败是 unavailable，不伪造0／空列表。默认控件可用最后成功的纯 paint 事实及定位标记，不得调用原 Draw、Observe、native edit、资格／绑定读取或请求。可靠同目标稿静态显示，挂起不可靠输入、清待提交、关自有 popup；恢复不自动提交。独立可靠控件仍可用。

#### Scenario: choices 当前读取失败
- **WHEN** 曾成功的选择控件进入受管故障
- **THEN** 旧选项可作带故障标识的静态展示，不能当当前可选集合
- **AND** reset 等独立可靠操作不因该局部故障永久失效

### Requirement: FL08-FAILURE-GEOMETRY 失败不发布半成品
需要共享宽度／可见性／成员／布局的读取或排列失败 SHALL 保留上一完整树关系、Repeat 成员和几何／缓存；不回滚业务副作用、运行声明、已结束交互。Host SHALL 暂停不可靠几何输入并结束自有 capture，直至完整有效 layout／hit pass 成功。纯呈现失败若身份／行为可靠，不应一律清空全部状态；中途未访问不等于删除。未分类结构／适配错误保留外层 fatal 保护。

#### Scenario: fresh arrange 在协调中失败
- **WHEN** 候选已产生部分节点／几何后抛错
- **THEN** 旧完整结果恢复，新增候选不污染已发布共享容器
- **AND** 下一次成功排列前不能用半份命中继续输入

### Requirement: FL08-RELEASE 每项自有释放须尝试
实际 close SHALL 先逻辑生效，再逐项尝试 fallible cleanup。注册的 custom release 即使前项释放或其 Warning 失败也 SHALL 各尝试一次，不能先丢掉未尝试回调。Session SHALL 继续其他资源并在逻辑关闭后报告聚合错误。普通 reporting 失败不取代原 release 原因；此为既有要求，实现与证据见[状态映射](../../../implementation.md#fl-f03)。

#### Scenario: 第一项 release 与 Warning 都抛普通异常
- **WHEN** 同节点尚有其他注册释放
- **THEN** 剩余项和 Session 其他资源仍被尝试
- **AND** 重复 Dispose 不重试失败副作用

### Requirement: FL08-RECOVERY Warning 不阻断既定恢复
widget 原错误 SHALL 保留路径／节点归因并进入既定 record／fallback；普通 Warning 回调失败 SHALL 不阻断该恢复尝试。恢复绘制本身失败仍归外层保护，不能冒称正常完成或屏蔽真实业务／native 错误。Trace／fit 的窄出站隔离另依[观察连接](../../contracts/observation-boundary/spec.md)。

#### Scenario: guard 报警时观察者也失败
- **WHEN** widget 原错误已触发 guard
- **THEN** 原始原因、恢复记录及 fallback 尝试仍可达
- **AND** 不因 Warning 代替原错误而遗漏恢复

## 0.9 local mounted lifetime

An owner may explicitly end a named subtree's UI lifetime. The existing Session releases only that
subtree's editor/capture/popup/output/custom resources without business completion. Its next mount has
a new incarnation, even when the declaration key is unchanged; old requests close rather than move.
Siblings keep compatible state. The Host retains its identity and takes a complete layout fallback.

## 0.9 区域保留：已采用、未实施

本节仅约束主动采用的新能力；其余原条件保持。新增闭包已定 Public-unstable，尚无对应导出。

### Requirement: FL09-REGION-LIFETIME 表示、几何和调度共同成功
区域候选 SHALL 在原成功事务共同发布输入版本、成员、纯结果、plan／位置及有效性；
失败丢候选，保上一完整结果与真实待处理失效，不回滚业务、已结束交互或运行声明。
几何不可靠 SHALL 暂停相关输入并沿原路径结束 capture；纯 Paint 可用最后成功 immutable
资料作 fault 标识的静态展示，不运行旧 Draw／Operate／普通业务读，不把 retained 当当前成功。
纯 Build 的相同失败输入只在相关变化或 Retry 后重试，普通源鲜读失败仍保其原恢复条件。

新能力 SHALL 在 owner 上串行；capture／Build 重入同 Host 推进在修改候选前拒绝，
期间新失效不能被早期版本确认，Close 后不能复活／继续正常工作。
Dispose attachment SHALL 幂等退出新路径，释放本能力记录及 source 引用，下一安全边界
完整重验原路径，保相容稿／身份而不隐式 Save／blur／Cancel；不释放借用 execution 或外部业务。
Host Close、root删除／不兼容替换及显式 mount结束 SHALL 退休对应记录；可靠折叠不冒充销毁。
清理逐项尝试并聚合实际原因，不新增进程全局 cache 或保留所有历史表示。

#### Scenario: 区域候选已变化而后续排列失败
- **WHEN** 新成员／纯结果已进入候选，但共享几何无法完成
- **THEN** 所有旧完整事实仍只读可用，新记录不混入发布，可靠恢复前不开放不可靠输入
- **AND** 已完成业务操作不撤销、不重试

#### Scenario: 附件退出后继续原页面
- **WHEN** 作者 Dispose 区域 attachment 而 Host 仍存活
- **THEN** 原 reader／Measure／Draw 路径恢复原条件并验证当前输入
- **AND** 不关闭业务、销毁 Session 或提交稿来模拟退出

#### Scenario: 关闭与重入发生在捕获回调
- **WHEN** callback 关闭 Host 或尝试递归推进同一 Host
- **THEN** 关闭立即终止后续正常工作，递归推进在修改本候选前定位拒绝
- **AND** finally 只清理、不使旧记录重新有效，也不对未采用调用追加新 purity 条件

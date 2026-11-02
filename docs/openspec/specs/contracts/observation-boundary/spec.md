# 可选观察出站连接

## Purpose
区分输入／popup／recovery／cleanup 产生的事实和接收诊断的 callback。观察者不持请求结果或 UI 寿命；异常隔离只作用于下列明确出站，不扩成普遍业务容错。

## Requirements

### Requirement: FL08-CALLBACK-ROLES callback 按职责分类
Trace、Warning、fit-report 诊断观察 SHALL 与 setter、command、managed writable response、必要 metrics／paint、具有明示失败合同的 Host hook 分开。诊断处理 SHALL 不把后者吞成成功、接受或保存，也不能给合法普通调用暗加 NoThrow。HostAttached/Detached 的原明示不得抛错及失败清理条件仍适用。

#### Scenario: command 带副作用后抛错
- **WHEN** 同时安装 Trace 观察者
- **THEN** command 按业务请求失败语义处理
- **AND** 不因它位于有日志的调用栈就当作可忽略观察异常

### Requirement: FL08-OBSERVATION-AUTHORITY 记录不是接受回执
诊断 payload SHALL 标真实阶段、owner 与可用来源；它不是业务接受、持久化回执或第二可变 UI 状态。Warning 在既有 release／recovery／seal 路径须维护各自现行结果；Trace／legacy-fit 依下列窄隔离保证，不添加普遍 NoThrow 前提。[开发状态](../../../implementation.md#fl-f02)分别记录实现与证据。

#### Scenario: Trace 文本称 option fired
- **WHEN** business endpoint 尚未给实际结果
- **THEN** 该消息只说明其记录阶段，不证明业务接受或磁盘已保存

### Requirement: FL08-OBSERVATION-TIER 合法 advanced 观察不改变采用合同
安装现有 advanced Trace 或使用 advanced Session 帮助成员 SHALL 仍是合法公开采用，不提升其等级或 Session 全体。bounded 订阅保护和某个 native logger 的自身安全 SHALL 各保本来范围，不能互相签发安全证明。现行基本诊断不要求所有视觉／普通作者订阅。

#### Scenario: no-Host 视觉作者保留 legacy fit
- **WHEN** 它按原条件设置 sink
- **THEN** 用途保持合法，不被强制转为 Host/subscription
- **AND** 可选 sink 失败依下列出站保证隔离

### Requirement: Trace 和 fit 观察失败不得控制 producer
可选 Trace／fit-report observer 的普通 managed 异常及仅用于诊断的 payload 准备失败 SHALL 在该出站边隔离，
不得阻断原合格请求、适用的 event-consumption、popup publication／paint、recovery 或 cleanup。
本保证 SHALL 不添加 caller-NoThrow 前提，不吞业务／必要 native callback。

#### Scenario: 激活结束后的 Trace 失败
- **WHEN** capture／consume 已按原合同结束，而返回 wrapper 前 Trace 抛普通异常
- **THEN** wrapper 仍能得到结果并发适用的一次请求
- **AND** 已发生 release／consume 不撤销或重做

#### Scenario: wheel 行偏移变化后 Trace 失败
- **WHEN** 合格菜单已更新 offset
- **THEN** 仍到适用 consume，不为日志成功重试 wheel 或业务

#### Scenario: popup 发布后的 Trace 失败
- **WHEN** popup hit layer 已发布，panel／row paint 之前 Trace 抛普通异常
- **THEN** 仅观察失败不阻断剩余 popup 工作或触发 terminal page failure
- **AND** 真正 paint／business 错误仍服自己的失败边界

#### Scenario: legacy visual-only fit sink 失败
- **WHEN** 合法无订阅／无 Host 调用者的可选报告 sink 抛普通异常
- **THEN** 原展示／恢复结果继续其合同
- **AND** 不增加 Host／订阅／pure 前提

### Requirement: 观察失败报告须有界且非递归
普通观察失败报告 SHALL 不递归调用同一失败出口、不保无限队列、不替代原业务／cleanup 原因。
失败事实 SHALL 归现有 owner 与 bounded 机制。OutOfMemoryException 及 runtime fatal termination 不属于普通隔离。

#### Scenario: 备用 reporting 也不可用
- **WHEN** 可选 observer 失败且报告出口也不可用
- **THEN** 原 control／recovery／cleanup 结果仍遵其本来合同
- **AND** 不递归重试或无限积压

#### Scenario: 真正业务 hook 失败
- **WHEN** setter／command／managed writable response／明示失败合同 Host hook 抛错
- **THEN** 保原异常结果，不能按此观察保证转为成功

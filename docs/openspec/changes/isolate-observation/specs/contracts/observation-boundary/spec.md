## ADDED Requirements

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

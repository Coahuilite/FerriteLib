# Spec Delta — 采用来源（非 current）

0.9 已采用、未实施；来源为 bb7da49 及 WO-FL09-RETAINED-DISPLAY-20261111 r2 的 choices／paint证明与源捕获补充。
本文保留采用时完整 delta，不作为第二份 current；唯一正文见 [对应能力](../../../../../specs/core/lifetime-recovery/spec.md)。

## ADDED Requirements

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

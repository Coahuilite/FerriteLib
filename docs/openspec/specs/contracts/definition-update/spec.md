# 定义与样式更新的 prepare／stage／seal 连接

## Purpose
让程序更新及文件更新在同一事务边界提交完整 UI。定义生产者、Host、文档协调者与退役资源各有明确结果；不可逆 release 不被误当作可回滚 staging。

## Requirements

### Requirement: FL08-PREPARE 更新前验证相同定义合同
UpdateDefinition SHALL 是普通显式入口，不需文件服务注册；XML／C# 采用同一编译／绑定／锚点语法验证。prepare SHALL 不退休旧资源；拒绝型别、图或声明时保留旧定义、editor 与完整派生结果。program component 是返回定义的普通函数，不是另一运行时组件框架。

#### Scenario: 候选包含循环锚点
- **WHEN** 候选在 prepare 被拒绝
- **THEN** 老页面和编辑仍可用，错误定位候选来源
- **AND** DefinitionRevision 不增长

### Requirement: FL08-STAGE 跨 Host staging 完整可退回
多 Host 更新 SHALL 先 preflight 全部，再 stage 全部；某 Host stage 失败 SHALL 恢复所有已 stage 的旧树／resolver／精确主题声明与有效应用，含显式来源而不把默认提升为声明。相容身份可保状态；业务写入和不可逆副作用不是 UI 回滚对象。失败候选不得污染共享 mutable 缓存。

#### Scenario: 第二 Host staging 失败
- **WHEN** 第一个已 stage 成功
- **THEN** 两者均保持原完整结果，并且旧资源尚未不可逆退休
- **AND** 不只回滚一个可见 tree 留下其他派生状态

### Requirement: FL08-SEAL 提交与清理结果诚实
所有受影响 Host stage 成功后 SHALL seal：完成新定义封存及应退休节点、旧输出、capture／focus 清理；DefinitionRevision 仅随成功 seal 推进。普通 cleanup／reporting 失败 SHALL 不把已提交定义伪装为失败并只回退 tree；UpdateDefinition 成功或 UiReloadReport.Accepted SHALL 对应实际提交。各 doomed 节点和剩余释放仍须尝试，不自动重试已发生 cleanup，保留[释放合同](../../core/lifetime-recovery/spec.md)。

#### Scenario: seal 中旧输出 setter 与 Warning 失败
- **WHEN** 新定义已通过全部 staging
- **THEN** 继续适用退役和自有 capture 清理，并诚实记录已提交结果及 cleanup 问题
- **AND** 不宣布回到旧定义却已销毁旧状态

### Requirement: FL08-DOCUMENT 文件只是更新生产者
文件服务 SHALL 有实例内有界 watcher／scheduler、明确主线程与时间来源；实际 UI 更新在其 owner 执行，非后台 Host。默认 quiet0.25秒、retry0.5秒、最多重试3次、maxDefer2秒，0次重试合法；trailing-edge 合并不是 GUI pass 业务采样。上限为64 documents、256 dependencies、32 reports、单文档1048576 bytes；AutoWatch 仅 Dev 默认true、Release默认false。过界／加载失败／非法 style SHALL 报实际问题并保 last-good，不删除作者文件。

#### Scenario: 文件连续被写，随后恢复有效
- **WHEN** 合并／有界重试在合法 owner 接到完整新内容
- **THEN** 走相同 prepare/stage/seal 并给实际 accepted/refused 结果
- **AND** 不把一次 watcher 通知当作已提交或无限重试许可

### Requirement: FL08-STYLE-UPDATE 只撤应用而不复原旧宿主快照
样式更新 SHALL 消费[属性来源](../../core/appearance/spec.md)，只应用文档明确声明项；删除页面 sparse style 露当前 host 声明。失败回滚 SHALL 恢复原 effective application 与 intent；公开 ApplyTo 仍保原显式应用条件。样式问题只读 held view 保留其正常后续更新。

#### Scenario: 删除页面 Gap override
- **WHEN** host 在页面打开后已经改变自身 Gap
- **THEN** 新页面应用撤去后显示当前 host Gap，而非最初值

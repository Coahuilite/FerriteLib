# Spec Delta — 采用来源（非 current）

0.9 已采用、未实施；来源为 bb7da49 及 WO-FL09-RETAINED-DISPLAY-20261111 r2 的 choices／paint证明与源捕获补充。
本文保留采用时完整 delta，不作为第二份 current；唯一正文见 [对应能力](../../../../../specs/core/structure/spec.md)。

## ADDED Requirements

### Requirement: FL09-REGION-ENROLL 作者在原树上明确选择区域
作者 SHALL 可在原 Host 的唯一声明结构容器登记表示源及明确消费 map，包含 Scroll／Repeat，
不要求新 kind、Overlay、Catalog、Tools、reactive runtime 或迁移整个页面。
登记 SHALL 先验证再生效；首版重叠附件、歧义模板目标、无效类型／角色定位拒绝而不改原连接。
原 UpdateScope 嵌套不受附件重叠限制。缺登记的默认值为关闭，Schema 2／3 及 TieredEvents 意义不变；
逐 pass 页明确登记后仍每 pass 捕获来源，未采用消费保持原读取条件。

区域 SHALL 复用原节点／key／part／mount 身份，表示 item packet 不成为另一棵身份树。
相容重排／定义更新保相容 UI 状态；隐藏、移除、retarget、重挂和不兼容替换按原寿命处理。
登记或 packet 相同不能免除原绑定 reader 类型／owner 验证，旧 Repeat 的合法晚绑定条件继续适用。

#### Scenario: F09 只采用 b-actions
- **WHEN** 作者在原 Scroll 内的 Repeat 上登记新表示，其他页和部分 widget 未迁移
- **THEN** 只改变已明确选择的区域消费；既有 Items 向量及原字段仍完整存在
- **AND** 不要求删掉屏外／折叠 key，也不以注册整个产品作为采用门槛

#### Scenario: 同 key 在实际释放后重挂
- **WHEN** 同名节点取得新 mount
- **THEN** 旧区域记录／请求不复活，按当前源建立新记录
- **AND** 其他区域和 Host 的状态不被迁移或释放

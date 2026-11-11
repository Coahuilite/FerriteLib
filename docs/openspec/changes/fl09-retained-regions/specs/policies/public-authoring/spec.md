# Spec Delta — 区域作者路线，候选未采用

基准：`d07cdc0`。S1–S7、FL08-COMPAT、平台中立／单载体及既有独立视觉路线保持。

## ADDED Requirements

### Requirement: FL09-REGION-AUTHOR 新能力主动选择，旧语义不退休
公开作者 SHALL 可在普通／embedded Host 上选择单个已声明区域、明确表示读取 map，
并为现有自定义 kind 实现公开 participant，不依赖 internal、Tools、Catalog、模型基类或整页 reactive。
XML／程序定义继续共用原声明；新增附件不重解释 Schema 2／3、不增加全页默认、不替换旧绑定注册。
旧签名、旧调用／override、旧读取与释放语义 SHALL 保留，无弃用或退休；退出新能力恢复原调用路线。
无Host视觉／Session独立控件保持原合法入口，新区域保留不成为其前提。

新增 API 的公开等级 SHALL 在采用时由 PM 明确决定，并同步其必要类型／实现接口闭包；
Public-unstable 的后续迁移不解除功能合同或旧 Stable 保护。Stable 若被选择，保护完整输入、
phase／失败／寿命行为而非仅方法名，不以换minor免除既有兼容义务。

#### Scenario: 作者只迁移一个 sparse kind
- **WHEN** 新 participant 只在一个区域被明确采用
- **THEN** 未采用实例仍运行原 IUiWidget，普通 Get／Set／命令不被新源拦截
- **AND** 可以不迁其他页、未证明贡献或独立原生控件

#### Scenario: PM核定新增接缝为Public-unstable
- **WHEN** 采用记录明确等级和依赖闭包
- **THEN** 后续源码／文档据其实现并提供真实迁移，旧 Stable 入口持续合法
- **AND** 候选文本与性能目标本身不宣告API已可用或native已通过

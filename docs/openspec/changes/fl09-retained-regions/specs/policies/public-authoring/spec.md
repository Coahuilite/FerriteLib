# Spec Delta — 采用来源（非 current）

以下为0.9 r2采用时的 delta；r3 源码与证据见实现账。来源为 bb7da49 及 WO-FL09-RETAINED-DISPLAY-20261111 r2 的 choices／paint证明与源捕获补充。
本文保留采用时完整 delta，不作为第二份 current；唯一正文见 [对应能力](../../../../../specs/policies/public-authoring/spec.md)。

## ADDED Requirements

### Requirement: FL09-REGION-AUTHOR 新能力主动选择，旧语义不退休
公开作者 SHALL 可在普通／embedded Host 上选择单个已声明区域、明确表示读取 map，
并为现有自定义 kind 实现公开 participant，不依赖 internal、Tools、Catalog、模型基类或整页 reactive。
XML／程序定义继续共用原声明；新增附件不重解释 Schema 2／3、不增加全页默认、不替换旧绑定注册。
旧签名、旧调用／override、旧读取与释放语义 SHALL 保留，无弃用或退休；退出新能力恢复原调用路线。
无Host视觉／Session独立控件保持原合法入口，新区域保留不成为其前提。

本组整个新增 API／必要新类型／实现接口闭包 SHALL 按 PM 的 r2 决定为 Public-unstable；
现有 Stable 依赖不降级，后续迁移不解除功能合同或旧 Stable 保护。0.9 采用是实施目标，
未实现的名字不得列为已导出／当前可调用 API；不以换minor免除既有兼容义务。

#### Scenario: 作者只迁移一个 sparse kind
- **WHEN** 新 participant 只在一个区域被明确采用
- **THEN** 未采用实例仍运行原 IUiWidget，普通 Get／Set／命令不被新源拦截
- **AND** 可以不迁其他页、未证明贡献或独立原生控件

#### Scenario: 新增闭包已采用但尚未导出
- **WHEN** 采用记录明确等级和依赖闭包
- **THEN** 后续源码／文档据其实现并提供真实迁移，旧 Stable 入口持续合法
- **AND** 采用文本与性能目标本身不宣告API已可用或native已通过

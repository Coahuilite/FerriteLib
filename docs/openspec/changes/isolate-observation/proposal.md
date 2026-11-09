# 输入、观察与 owned recovery 边界修正：已采用记录

状态：**2026-10-29已采用并实施**。用户批准 WO-USFL-CONTRACT-REPAIR-20261029 r1，
正式采纳本记录中 Trace／legacy-fit 的两项 ADDED；唯一现行正文是[观察连接](../../specs/contracts/observation-boundary/spec.md)。
本目录 delta 保留新保证的采用来源，不是第二份 current。
当前要求、修复机制与证据见[开发映射](../../implementation.md)。

## Why
采用前，已成立操作与下一消费者之间的可选诊断出站可因异常打断请求、wheel consume 或 popup paint。
本次同时恢复原有 wheel 资格、逐项释放、guard 恢复及 seal 诚实结果的源码偏差。

## What Changes
恢复现有 wheel 状态／consume 资格，不阻断已持 capture 的合法结束；恢复剩余 release、
原错误归因／fallback、跨 Host stage/seal 的真实结果。以上无新需求 delta。
只对 Trace／legacy-fit 新增明确普通观察异常隔离和有界非递归失败记录保证，见[ADDED](specs/contracts/observation-boundary/spec.md)。

## Real Scope
- owner：现有 Controls/Session、node/guard、Host/DocumentService、fit/Trace 出站；不新建 manager。
- caller → consumer：Engine wheel → Session offset → native consume；interaction → wrapper request；
  popup layer → paint；node/guard/seal → reporting → 剩余 cleanup／accepted result。
- 不改：业务 setter/command、managed response、必要 metrics／paint、明示 Host hook 失败合同；
  API/XML/tier、ordinary getter/Measure、mutable Theme、no-Host/headless/visual-only条件。
- 无 scrollbar、筛选分页、HUD窗口管理、async scheduler、input采样、保留像素或1.7实现。
- 影响：本次采用改变 advanced Trace／legacy sink 的行为承诺，但不提升 Session。
  下游 logger 自身安全仍由消费者负责。
- 证据：复用已定位源码及现有输入／assembly／reload／lifetime／adapter检查；精确 false-wheel、
  throwing-observer 组合与原生故障频率未执行，不以文档场景充当运行通过。

## Capabilities
- Modified capability：contracts/observation-boundary，仅 ADDED 的新保证。
- Restored implementation：interaction-editing、lifetime-recovery、definition-update、rimworld16 的原有要求；
  不把实现缺陷合法化，不为纯恢复造 delta。

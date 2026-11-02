# 已采用设计：守住最窄出站边

不建立第二状态 owner。wheel 只在状态变更及消费前校验当前资格／可靠几何，不对整个 Controls 提前返回，
以免挡住 ineligible／raw Used release 的自有 capture 终止。

Trace 四条路径及 legacy-fit sink 仅在可选观察调用和必要的诊断 payload 准备处隔离普通异常。
不包住业务、真实文字度量／绘制或 Host hooks。失败归现有 owner 的有界记录，不能递归打同一失败 sink，
也不静默注销消费者 callback。OOM／runtime fatal 不算普通隔离。

release 循环须各项独立尝试，保原因，不先丢未尝试项。guard 的记录／fallback 必须在 Warning 出错后仍可达；
fallback 真实绘制失败保外层保护。仅更名 ReportWarning 不证明最后出口安全。

prepare／stage 保可回滚；全部 Host 成功后才开始不可逆 retirement。
single Host 与 DocumentService 两条 seal 路都要保持诚实 commitment 和剩余 cleanup，不能 tree-only rollback。
业务写入不回滚／补偿／重放。实际 owner-local 预算及实现入口见开发映射，没有新增公开 API。

反例：宽 catch 吞 command 会伪造业务成功；全回调资格早退会漏 capture release；
reporting 再调用自身会递归；原生 logger 频率未知不能据此承诺 native 已稳定。

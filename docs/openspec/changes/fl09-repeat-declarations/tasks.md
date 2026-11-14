# Tasks — Repeat 声明身份补充

本清单记录规格门及后续责任，不授予实施、构建或游戏权限。

- [x] SPEC-1 从 `fl/0.9-tools-20261105@1da6ca6` 核对实际收集→prune 路径、HOT-R4 与现行条文，
  仅为缺失的工作避免保证提出 ADDED delta；原隐藏／移除／失败／reader 保证不变。独立 docs-only 候选提交。
- [x] SPEC-2 按本次 PM 授权，候选 `51dceef` 后另作 docs-only 正式采用：并入唯一 core/structure current，
  lifetime 只连接原事务责任，更新 specification／来源路由；注明0.9、已采用未实施。
- [x] GATE PM 在 WO r9 核对采用 `6cc62e0`，仅放行该声明身份增量；不放开 editor 实验或其他热点。
- [x] IMPL 原 FL owner 在已有 RepeatState 内保留不可变行声明结果，保持完整 keep、原嵌套限制／隐藏、
  真正移除／新 mount、模板变更与失败回退；不合入 editor 实验、不新增 API或通知前提。
- [x] CHECK 已运行受影响既有 Repeat、item binding、visibility、failure-lifetime、reload、layout reuse、
  shared editing、window host、readonly-api 及 Core 391；通过且未改测试／断言。普通 net472／RW1.6
  固定交付另记真实身份；不新增单测／case／等价 probe。
- [ ] NATIVE Terra／PM 使用同代真实 r4 基线复验暖开工作与寿命，区分声明派生／keep 合并／prune及仍需执行的工作；
  普通构建与条文采用不计作游戏、net10／1.7或165 FPS验收。

源码机制、冷建／暖命中工作及限制见 [实现账](../../implementation.md#fl09-repeat-declarations)。
已有后续1.6 r5暖开工程证据（未重派未变行子身份），数值和165 FPS未达限制归实现账。
上列NATIVE完整寿命／同代归因责任不因这条暖开结论关闭；未新增仪器或测试，不将其扩作1.7验收。

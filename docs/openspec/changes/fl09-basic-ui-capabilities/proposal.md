# FL09基础能力：已采用目标，待实施

状态：**2026-11-08按WO-UIKIT-CAPABILITY-SPEC-20261108 r2采用，r3正式采用原END/H1/H2的寿命／焦点与IME限定；ADDED扩展仍未实施。**
R1 原合同修复另经可靠性 WO r1 授权，双线源码／隔离构建／既有检查及准确新载体真实 caller 消费已完成，待 CO／PM 接受。
现有保证仍由[现行入口](../../../specification.md)路由；新保证只在本change的ADDED正文持有，
不是当前载体已支持清单。[实现账](../../implementation.md#fl09-reliability)区分源码缺口与运行证据，
[0.9计划](../../../development/0.9/extension-plan.md)持后续顺序；公开等级不变，不冻结新API签名或XML词汇。

## Why

普通通知、隐藏成员与完整布局发布必须收敛到同一当前资格。已定位的历史键域差异属于原合同可靠性差距，
不是禁止冗余通知的理由。真实消费者另暴露横向内容和可交互画布视口需要；这些是一般能力，不要求先注册专用kind。

## What changes

- **已有保证澄清／实现修复目标：**普通重复、同值和非当前布局键公告仍合法；当前完整依赖、成功发布和观察资格对齐，
  复显验证最新值，多Host互不代确认，晚通知／失败／Close不漏义务。正文归binding/layout/observation，不造ADDED。
- **ADDED已采用、未实施：**[双轴与有限视口](specs/core/layout/spec.md)、[输入及后续详情／焦点](specs/core/interaction-editing/spec.md)、
  [1.7平台接入与后续桥接](specs/adapters/rimworld17/spec.md)。首批原生编辑位于变换视口外。
- inline详情用既有结构组合，不新造研究专用kind。交互popup/modal、完整K1和OS可访问桥是明确后续ADDED，不偷渡成首批能力。

## Complete owner slice

消费者模型／定义 → 原binding与Host → 原布局／Session状态 → adapter输入与绘制 → 同源观察／可选Tools。
FL持UI身份、几何、交互资格和自有寿命；consumer持内容、拓扑、业务、保存与游戏InfoCard／运行期Def；
Tools持观看、协议、工具UI和自建宿主。新输入随每个能力完整交付，不能先交错位命中再补输入。
失败保last-good、取消不提交、Close终局；资源按创建／借用owner释放。不建第二scenegraph／renderer／全局调度或永久稿缓存。

## Evidence and compatibility

采用依据为Phase A分析／反例及PM r2决定，来源转录在[来源覆盖](../../source-coverage.md#fl09-capability-adoption)。
旧运行不能唯一识别pending key，后续consumer成功不等于核心修复；见[design](design.md)。
旧纵Scroll、NotifyChanged、普通Measure／provider、R9／Capture、独立Session和无Tools采用条件保持。
新能力的显式轴／纯度／变换限制只约束新用法。ADDED不扩入0.8／1.6；R1原合同可靠性修复分别交两线。
1.7跟当前接口而不锁小build，单载体／API区间／stable保护不变。
shader、通用仿射、变换内原生编辑、研究自动排布、全游戏Def数据库、业务保存／网络与world-space均不在首批基础范围。

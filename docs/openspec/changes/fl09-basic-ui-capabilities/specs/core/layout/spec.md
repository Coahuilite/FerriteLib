# 双轴滚动与有限画布视口

状态：WO-UIKIT-CAPABILITY-SPEC-20261108 r2 **已采用ADDED目标，未实施**。仅显式新能力适用；
[现行布局](../../../../../specs/core/layout/spec.md)的旧纵Scroll／普通布局条件保持。无新签名／XML词汇决定。

## Purpose
让横向内容与基础可交互画布在原树上共享完整有效几何，而不是各自复制测量、命中和滚动状态。

## ADDED Requirements

### Requirement: FL09-SCROLL-2D 双轴范围和发布一体成立
显式新双轴能力 SHALL 区分内容extent、实际viewport和Session持有的offset，以UiKit逻辑长度表示（不是目标物理像素或逻辑条目数）；有限约束与两条滚动条的相互预留共同决定范围。成功发布 SHALL 同时采用各轴clamp、实际内容再测量和clip/draw/hit/popup/scroll-target结果；内容缩短、目标／有效UI缩放变化不能保留超界偏移或半份新旧几何。不静默缩字、删内容或改分页。旧纵向声明与政策保持。

#### Scenario: 横条出现又使纵向溢出
- **WHEN** 显式双轴内容先横向溢出，横条占位使纵向也需滚动
- **THEN** 两条滚动条占位与实际viewport在同一完整结果中成立，各轴offset按最终范围钳制
- **AND** draw、hit与popup不得使用未预留另一条滚动条的旧区域

#### Scenario: 内容缩短而旧offset超界
- **WHEN** 新完整内容采用后任一轴范围缩短
- **THEN** 该轴offset合法钳制，实际可见内容和命中共同更新
- **AND** 非提交测量不提前钳制／恢复或消费真实scroll-target

### Requirement: FL09-VIEWPORT 有限变换只有一个有效几何权威
首批基础画布 SHALL 只支持有限正比例缩放和平移；显式声明内容／视口空间，采用同一有效transform版本供paint、clip、topmost hit、popup anchor、selection及观察归因。指针缩放 SHALL 保持操作点下的内容点，键盘／语义缩放 SHALL 保持当前有效视口中心的内容点，resize SHALL 保持旧有效视口中心的内容点位于新有效视口中心；显式fit/reset请求另按其声明。有限范围clamp可限制锚点保持，结果须报告实际变换，不从观看比例或上次截图猜测。消费者持业务拓扑和位置算法；不建第二scenegraph／renderer。非法／非有限变换 SHALL 拒绝发布并保last-good。

#### Scenario: 非零外层scroll和window origin下缩放
- **WHEN** 画布采用新的合法平移／比例，外层scroll及窗口内容origin均有效
- **THEN** 实际内容绘制、裁剪、当点命中及popup使用同一版本的完整映射
- **AND** Tools观看Fit／平移变化不改变Host预算、definition或业务状态

#### Scenario: resize或无指针缩放
- **WHEN** 视口尺寸变化，或键盘／语义命令请求新比例
- **THEN** 采用该操作明确的锚点政策并保相容身份／状态，不借重建Host完成定位
- **AND** 不把缺指针位置伪造成指针中心缩放

#### Scenario: 首批变换内容要求原生编辑
- **WHEN** 作者把需要原生caret／composition的编辑叶放进首批变换内容
- **THEN** 该新组合明确拒绝，编辑可保在视口外
- **AND** 不影响旧普通编辑入口；变换内编辑／caret及通用仿射属于后续目标
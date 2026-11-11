# 新视口输入与后续详情／焦点

状态：WO-UIKIT-CAPABILITY-SPEC-20261108 r2 **已采用ADDED目标，未实施**。
旧capture、TEXT、READONLY及请求语义仍由[现行交互](../../../../../specs/core/interaction-editing/spec.md)持有；现有K1延期不变。

## Purpose
让每个新增几何能力同时交付其完整输入、取消和退出，不把语义驱动误当物理输入或平台焦点。
首批详情使用既有inline结构保全文、身份／动作与资源缺失；这不是新增控件或新ADDED能力。

## ADDED Requirements

### Requirement: FL09-AXIS-INPUT 新双轴输入按真实位移仲裁
显式新双轴能力 SHALL 按实际轴事实和当前资格处理输入；该轴应用合法范围后实际无位移，才可把该轴的未处理增量逐祖先交接。已处理的轴不得重复驱动祖先。popup保持现行独占，包括端点；无资格时不改offset也不消费。旧纵Scroll的政策不因新能力改变，单位／轴缺失 SHALL 明确而非猜X为Y。

#### Scenario: 内横向已到端点而外层仍可移动
- **WHEN** 合格双轴输入作用于内层，某轴钳制后实际无位移
- **THEN** 仅该未处理轴可逐祖先交接，实际发生位移的轴不再向上重复执行
- **AND** 同一点有合格popup时仍按popup独占，不滚下层

### Requirement: FL09-VIEWPORT-INPUT 新画布输入与取消完整交付
画布抓取、子节点操作和缩放 SHALL 以同一可靠几何及当前资格仲裁；原owner持capture，新起始guard不能阻断已拥有交互的释放／取消。移除、替换、失资格、丢原生capture、abort和Close SHALL 结束自身输入而不提交业务、不改投同key新mount。键盘／语义等价路径 SHALL 纳入设计，不能以程序Set值证明物理通道成立。

#### Scenario: 拖出画布后释放并切换定义
- **WHEN** 原owner已有capture，随后收到自身raw结束事实或目标实际退休
- **THEN** 原capture可靠结束，不能激活下层、转交新实例或重播请求
- **AND** 仅失焦但仍可见不自动变成隐藏，原生资格与展示需求分开

### Requirement: FL09-DETAIL-FOCUS 后续交互详情与K1须显式完成
真正交互popup/modal、完整焦点遍历和命名角色／状态的中立语义为后续ADDED；采用该后续能力时 SHALL 明确初始焦点、背景资格、键盘可达、关闭返回及Escape／composition优先，不把有交互内容统称tooltip。现有K1不改签已实现。

#### Scenario: 后续modal关闭而原触发项已移除
- **WHEN** 采用该后续能力的详情关闭，原触发者不再是当前有效实例
- **THEN** 焦点落到明确的可用工作流位置，不转给同key新mount或发业务请求
- **AND** 原生IME候选确认／取消所用键不同时提交表单或关闭详情
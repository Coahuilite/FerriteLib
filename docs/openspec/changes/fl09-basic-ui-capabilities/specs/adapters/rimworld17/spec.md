# 新能力的RimWorld 1.7输入与平台边界

状态：WO-UIKIT-CAPABILITY-SPEC-20261108 r2 **已采用ADDED目标，未实施**。
当前1.7目标政策和已有接缝唯一归[DEV-06/07](../../../../../specs/contracts/developer-tools/spec.md)；
本文不改保留1.6 adapter合同，不锁游戏小build，不冻结新接口形状。

## Purpose
把新双轴／视口的中立语义接到真实1.7输入和原生服务，保完整坐标、生命周期与资源归属。

## ADDED Requirements

### Requirement: FL17-CAPABILITY-INPUT 新能力必须随平台接入一起可用
每个新增双轴／视口能力 SHALL 同步接入真实输入轴／单位、窗口资格、消费／raw结束事实、capture取消以及有效逻辑坐标；neutral持交互语义与寿命，adapter持真实输入／文字／资源，不泄漏平台类型或通过object／reflection暗藏它们。缺能力 SHALL 报具体缺失，不静默降为不同轴／只读或伪造输入。实际1.7接口演进不得被旧固定输入小版本白名单替代。

#### Scenario: 新视口已有绘制却缺真实轴事实
- **WHEN** 当前adapter无法提供所采用新能力必需的输入事实
- **THEN** 明确该新组合不可用，不称其交付完成
- **AND** 不影响未采用新能力的旧合法窗口／编辑／独立控件入口

### Requirement: FL17-ADVANCED-EDIT 后续变换内编辑与OS桥独立验收
首批原生编辑 SHALL 位于变换视口外。后续变换内editor/caret、所选宿主／输入方式的IME与焦点及OS可访问桥 SHALL 分别取得实际平台支持：native保preedit／commit／选区／剪贴板权威，候选位置与当前caret有效映射一致；实际跨层传递的文本索引／范围须明确单位。Tools描述／命令树不自动构成原生读屏桥，结构几何／语义动作不证明物理输入，也不宣称所有IME或读屏实现通用通过。

#### Scenario: 后续caret桥在缩放后报告位置
- **WHEN** 未来采用变换内编辑，且原生composition活动
- **THEN** 候选定位、选择范围和绘制使用同一有效逻辑坐标／明确索引单位，确认键仅被原生composition处理一次
- **AND** 没有该平台证据时保持该后续组合未验，不用离线SetDraft或Tools HitTest代签

#### Scenario: 新能力关闭且execution为借用
- **WHEN** 新输入／原生资源退出，或清理出现异常
- **THEN** 按原owner尝试自身清理，取消不提交，借用execution／supplier纹理不被库销毁
- **AND** Game17、离线Native17、物理输入、IME及OS桥分别保自己的证据
# Spec Delta — 保留1.6后端的区域提交，候选未采用

基准：`d07cdc0`。以下是0.9中保留1.6执行后端的新能力；不同adapter分别声明与验证。
FL16-REPAINT／COORDINATES／EXECUTION／DISPOSE 继续适用，不更改1.7支持声明。

## ADDED Requirements

### Requirement: FL16-REGION-PAINT 复用结果仍实际提交可见内容
采用区域 participant 的1.6路径 SHALL 沿原 execution／绘制与有效坐标链，
每适用 Repaint 真实提交当前可见显示；只能省略已证明纯且隔离的派生／调度／裁剪展示。
原生 editor、control ID、资格、事件消费、滚动、popup、焦点／IME 与捕获结束 SHALL 仍由原 owner 每事件操作。
纯 Paint 的临时样式／group／font／enabled SHALL 在 finally 恢复，合法焦点与 hotControl 不回滚。
布局、实际主线程绘制调用、GPU、scanout SHALL 分别报告，方法数或保留几何不证明省后两者。

适配器缺少对应隔离／字体／绘制能力 SHALL 明确走原路径，不近似绘制或静默使用另一宿主实现。
RW1.6 native 只证明其实际候选，不能签给中性环境或1.7；本 change 不引入1.7实机前提或显示设置改变。

#### Scenario: 滚入一个曾完全裁剪的字段
- **WHEN** Session 的当前滚动令该字段在实际 Repaint 可见
- **THEN** 使用当前完整显示与当前位置真正调用后端绘制，native 输入仍按原顺序
- **AND** 不把旧截屏／framebuffer 当作新内容已呈现

#### Scenario: 后端没有声明必要隔离
- **WHEN** 区域尝试省略可能影响后续控件的展示
- **THEN** 回退原同步调用并报告不支持的原因
- **AND** 不为获得局部复用更改 Stable execution 的 implements／旧 override 语义

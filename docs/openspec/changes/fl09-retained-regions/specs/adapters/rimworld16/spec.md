# Spec Delta — 采用来源（非 current）

0.9 已采用、未实施；来源为 bb7da49 及 WO-FL09-RETAINED-DISPLAY-20261111 r2 的 choices／paint证明与源捕获补充。
本文保留采用时完整 delta，不作为第二份 current；唯一正文见 [对应能力](../../../../../specs/adapters/rimworld16/spec.md)。

## ADDED Requirements

### Requirement: FL16-REGION-PAINT 复用结果仍实际提交可见内容
采用区域 participant 的1.6路径 SHALL 沿原 execution／绘制与有效坐标链，
每适用 Repaint 真实提交当前可见显示；只能省略已证明纯且隔离的派生／调度／裁剪展示。
Repaint 仅在 effective clip 真空，或全部实际可能 paint footprint 已证明／由原后端强制限制且
全部在 clip 外时省纯 Paint；layout rect 不相交不构成证明，不能证明仍实际调用。
显式 PaintClip 只约束新纯 Paint，沿原 drawing clip 强制执行并保持坐标／finally；不自动裁旧 custom。
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

# Spec Delta：RimWorld 1.6 适配

## ADDED Requirements

### Requirement: FL16-DISPLAY-ISOLATION 展示隔离是执行 owner 显式声明的能力
1.6 原生执行 owner SHALL 声明自己是否支持展示隔离。支持时，它在内置展示 wrapper 的展示期间隔离并在 `finally` 中恢复已知的可变标签样式字段（对齐、换行、字号），使被省略或延期的展示不影响后续控件看到的共享样式；它 SHALL 不恢复或改变焦点、hotControl 等合法的原生状态变化。未声明该能力的后端，T3、FL09-TIER-SAFE 与 FL09-CLIPPED-DISPLAY SHALL 退回逐 pass 展示。展示周期只由事件阶段推出，本能力不读取 Unity 帧计数，也不新增其他 Unity 接触点。FL16-REPAINT 不变：Repaint 与原生编辑调用照常执行。

#### Scenario: 非 Repaint pass 省略静态标签
- **WHEN** 隔离能力生效，后续控件读取当前标签样式
- **THEN** 它看到的对齐、换行、字号与逐 pass 执行时相同

#### Scenario: 展示中途抛出
- **WHEN** 隔离作用域内的展示失败
- **THEN** 样式字段在 `finally` 中恢复，失败按现有 presentation fault 合同处理

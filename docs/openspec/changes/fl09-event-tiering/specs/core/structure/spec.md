# Spec Delta：结构、声明与稳定身份

## ADDED Requirements

### Requirement: FL09-PAGE-EXECUTION 页面执行方式按 Schema 取默认，作者只在需要时声明
`UiPage` SHALL 同时接受 `Schema="2"` 与 `Schema="3"`。两者元素与属性词汇相同，唯一区别是页面执行方式的默认值：

- **Schema 2：** 默认逐 pass 执行，与 0.8.x 相同。
- **Schema 3：** 默认[分层执行](../../contracts/presentation-execution/spec.md)。

根属性 `TieredEvents` 是可选布尔值，显式指定执行方式：`true` 为分层执行，`false` 为逐 pass 执行。它只在 `UiPage` 根上合法，非法值 SHALL 作为定位的定义错误报告。省略时取 Schema 的默认值。程序化定义 SHALL 有同义入口（FL08-DECLARATION）。

以 0.8.x 编写的 Schema 2 manifest，无论是否带 `TieredEvents`，在 0.9 中的含义都 SHALL 不变。一个页面只有一种执行方式；Host 不按控件或子树混用。

#### Scenario: 旧 manifest 原样加载
- **WHEN** 0.9 加载未声明 `TieredEvents` 的 Schema 2 页面
- **THEN** 页面逐 pass 执行，普通 getter 的调用条件与 0.8.x 相同

#### Scenario: 新作者使用 Schema 3
- **WHEN** 作者编写 Schema 3 页面，不声明任何执行属性
- **THEN** 页面分层执行，作者不需要了解 IMGUI 事件阶段

#### Scenario: 作者依赖每 pass 调用
- **WHEN** Schema 3 页面声明 `TieredEvents="false"`
- **THEN** 页面逐 pass 执行，与 Schema 2 默认相同

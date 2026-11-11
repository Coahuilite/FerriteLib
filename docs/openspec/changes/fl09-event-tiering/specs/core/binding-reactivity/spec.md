# Spec Delta：绑定、依赖与业务响应

基准文本：0.9 工作树中当前的 FL08-ORDINARY，包括“曾显示的键离开当前布局”场景。该文本在 2026-11-11 时尚未提交；采用时如有变化，以届时的当前文本为基准，重做本 MODIFIED。

## MODIFIED Requirements

### Requirement: FL08-ORDINARY 普通读取保持原采用条件
普通值／只读／options 注册、手写访问器和旧自定义 Measure SHALL 保持原通知及合法执行条件；Set 请求业务 mutation，不等于存储成功，也不自动 NotifyChanged。显式通知默认 Everything；None=0、Paint=1、Measure=2、Structure=4，Everything 为后三者并集。公告须到达真实消费者声明的 binding；尺寸／可见性等依赖不能被误归为纯 paint。未登记 getter 不因快路获得 pure、订阅或创建线程前提。

普通公告 SHALL 容纳已注册键的同值、冗余、批内重复及非当前布局键，不要求作者过滤当前可见项；
这不把tracked equality的抑制政策加给普通通知。每Host按其实际完整依赖验证，不由共享bindings的一次公告／另一Host提交代确认。
待布局义务属于该 engine 最后成功 publication 的当前依赖域；只有成功 publication 才确认其读取前
采样的 revision，并退休已退出该域的键。隐藏不清模型通知，复显读取当前值；读取或测量期间的
新公告、失败候选均不能冒充已成功覆盖的 revision。

“合法执行条件”取决于页面的执行方式。逐 pass 执行时，普通展示读取与旧 Measure 按每个 pass 的同步执行。[分层执行](../../contracts/presentation-execution/spec.md)生效时：普通布局输入与旧 Measure SHALL 在同步点执行；内置展示 wrapper 的普通展示读取 SHALL 只在 Repaint 执行；业务请求、执行资格与 operate 中的读取仍逐 pass 执行。在这两种方式下，普通 getter 都 SHALL 在被同步点读到之后才被视为已采用，且 SHALL 不因复用而被推断为 pure。普通 getter 的调用次数与时点在两种方式之间可以不同；依赖每 pass 调用次数的作者 SHALL 选择逐 pass 执行。

#### Scenario: 普通字段在 UI 外改变
- **WHEN** 业务改变影响文字测量的普通字段并按原注册 key 通知
- **THEN** 真实依赖在下次合法同步验证
- **AND** 不通过扫描任意对象字段或暗中启用 tracking 发现变化

#### Scenario: 曾显示的键离开当前布局后仍收到公告
- **WHEN** 同一Host隐藏祖先后，作者合法公告其后代旧键，包括未变值或重复键
- **THEN** 当前无布局依赖的历史键不永久阻塞其他有效布局；仍存的同键依赖继续同步
- **AND** 复显时验证最新值，不要求换定义／重建Host或只公告可见键

#### Scenario: 分层页面的普通展示值在帧内改变
- **GIVEN** 页面启用分层执行，一个 `text/wrapped` 绑定普通 getter
- **WHEN** 该 getter 的值在同一帧的两个非 Repaint 事件之间改变，且没有通知
- **THEN** 非 Repaint pass 不调用该 getter
- **AND** Repaint 调用它并绘制当前值，与逐 pass 执行的像素一致

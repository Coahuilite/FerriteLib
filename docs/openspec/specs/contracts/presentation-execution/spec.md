# 模型输入到展示与执行的连接

## Purpose
明确 registry／reader → presentation → measure/layout → draw/hit → business endpoint 的双方义务。模型持即时值，Host 持一轮展示，layout 持有效几何；没有第二个业务快照权威或自动异步派发器。

## Requirements

### Requirement: FL08-BINDING-OWNER registry 与访问器连接一致
注册、NotifyChanged、sealed endpoint 和可选 tracked 能力 SHALL 归真实 registry；Host 仍通过 IUiBindingAccess reader 读／请求。forwarding reader SHALL 明确 BindingOwner 为当前 minting registry 并一致转发 Value/Choice endpoints；不做 reflection/type probing。缺 owner 或声明 endpoint 型别／writability 不符 SHALL 定位到 widget／root／container／materialized item。普通 reader 不因声明 owner 而被 enroll tracking。

#### Scenario: 合法代理在两轮间换 backing
- **WHEN** Host 验证新 backing 成功
- **THEN** 才换 per-view projection、失效相关输入／布局并释放旧需求
- **AND** retained endpoint 仍属于 minting registry，作者需从新 proxy 重新取得；换 backing 不自动等于新业务目标

#### Scenario: 新 backing 验证失败
- **WHEN** 新声明能力不匹配
- **THEN** 保留旧 backing 的有效投影和连接，不把候选泄漏给当前页

### Requirement: FL08-U3 同轮输入一致，下一合法点采用新值
Host SHALL 在合法呈现机会先同步自身需求，再测量／排列、绘制；该轮普通展示消费同一输入，包括稳定 idle 回调。默认包装持自己的完整 demand；自定义 tracked 消费者通过 schema roles 或可选 demand 明确实际 key，不 prepare-everything。处理中业务新值 SHALL 到下一合法点采用，不递归重排，不固定 tick、Repaint 或必然下一帧；hover／pressed／capture 即时反馈不一概冻结。

#### Scenario: 较早 Button 改变模型
- **WHEN** 同回调后续控件读取已同步展示输入
- **THEN** 它继续本轮一致输入，下一合法同步再采用新值
- **AND** 不因 stable fast path 跳过本轮必要需求准备

### Requirement: FL08-IMMEDIATE-REQUEST 业务执行暂离展示读视图
Set/Invoke/typed request 以及 CanExecute／执行资格 SHALL 读取当前业务值而非旧展示；成功或失败后都恢复展示 scope。资格读取本身 SHALL 不开关业务批次、drain 或发布新展示。直接 source／computed 读取仍是业务即时读。业务拒绝权不由缓存投影替代。

#### Scenario: 当前资格谓词调用 bindings.Get
- **WHEN** 模型已变而展示尚未同步
- **THEN** 谓词看最新业务值并决定当前资格
- **AND** 随后普通展示仍用原本完整轮次输入

### Requirement: FL08-PRESENTATION-VALIDITY 当前无效不能冒称旧成功
presentation SHALL 分 key 保留 current validity、Fault 与可选 last value，独立完整输入可继续更新。当前失败 Get SHALL 抛，只有专用 display 路径可消费 retained paint。必要 layout 输入失败 SHALL 进入完整几何回退／暂停输入，不以旧宽度或空 items 当新成功。显式 Retry 只重试相关纯工作，不重做业务。

#### Scenario: 一项展示失败，另一项完整成功
- **WHEN** 同步得到这两种结果
- **THEN** 分别报告其状态，不让局部成功伪造全页有效
- **AND** 可靠独立消费可继续，不发布不一致共享几何

### Requirement: FL08-SPLIT-PHASE 手动分阶段须配对
合法手动 MeasureAndArrange 与 Draw SHALL 消费同一对应结果，中间不插另一轮 arrangement；frame 和 execution 显式供给。DrawFrame SHALL 自持一次 frame、measure、draw、hit、popup 及失败清理，不要求普通作者重写 Begin/End。回调 Close 后停止剩余工作。

#### Scenario: 嵌入已有原生窗口
- **WHEN** owner 将实际 content rect、一次捕获的 frame 与 execution 交给 Host
- **THEN** Host 完成同一连接管线，native owner 仍持资格和最终 execution 释放

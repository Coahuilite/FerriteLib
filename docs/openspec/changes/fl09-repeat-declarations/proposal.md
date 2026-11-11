# FL09 Repeat 声明身份：有界补充

状态：WO-FL09-RETAINED-DISPLAY-20261111 r8 已选定范围；本提交为独立规格候选，尚未并入 current。
仅适用0.9已显式采用区域能力的 Repeat；不包含产品实现、构建或验证执行。

## Why / 实际缺口

r4 同代真实配对已通过这页展开停手的区域行为，暖展开仍有明显 full 尖峰。
`opus-20261111/snapshot09/HOT-R4.md` 在一次无 Gen0 计数变化的暖开中记录：
full Arrange 58.449 ms，Repeat 声明身份收集 9.820 ms / 1次，根 CaptureCurrent 1.708 ms；
MeasureRegion self 20.012 ms 尚未细分。这些是含仪器与调度的墙钟，不是可直接删除的纯 CPU 清单。
本次只选择已定位的声明身份工作，不处理 MeasureRegion 余数、源捕获或 Draw。

静态依据为 `FerriteLib@f5e022e:Source/FerriteLib.UiKit/Kernel/UiLayoutEngine.cs`：
full 在 `CollectDeclaredIdentities → CollectRepeatItemIdentities → CollectHiddenRepeatIdentities`
之后将完整声明集交给 `UiSession.PruneNodesExcept`。已物化行和未访问的隐藏 Repeat 行
都再次调用 `CollectChildIdentities`，递归从行 spec 构造每个子身份；此前区域 box 命中不省掉这段工作。
Session 的 prune 按声明身份及私有子节点真实 owner 决定寿命，不按本轮可见 entries 决定。

现行条文已经规定：

- 结构 `FL08-IDENTITY`、`FL08-REPEAT` 和 `FL09-REGION-ENROLL`：稳定 key／当前目标、原身份和 mount，登记或同包不免 reader 验证。
- 布局 `FL09-REGION-UPDATE`：纯 Build／Capture／Measure、context、pointer 和局部变化闭包可保留。
- 寿命 `FL08-END`、`FL08-FAILURE-GEOMETRY`、`FL09-REGION-LIFETIME`：隐藏保相容稿、真正退出不复活、完整事务失败回退。

它们没有明确约束**即使发生 full，也不能因值／纯开合变化递归重新派生整套未变 Repeat 声明子身份**。
因此只 ADDED 这一可检验工作保证，不复制或改弱上述身份／寿命正文。

## Preferred direction / delta 与 owner

在原结构 owner 内复用或增量维护已证明的声明身份结果，仍向同一寿命事务提供完整真实 keep 依据。
依据来自实际 row／派生 spec／模板／规范身份及所属 mount，不来自业务快照相同或屏幕可见集合。
不规定缓存层数、集合类型、算法或新增管理器。

- **ADDED `FL09-REPEAT-DECLARATIONS`，core/structure：** 完备声明依据未变时避免递归重复派生；值或纯开合变化不单独撤销它。覆盖隐藏、嵌套、实际增删／重排、重新挂载和无法证明时的完整退路。
- **core/lifetime-recovery：** 只连到上述声明依据；原隐藏／释放、失败／关闭及发布保证不变，不另写第二份规则。
- **公开与跨库：** 无新 API、XML、通知、稳定委托或 reader 前提；普通读取和原 `VerifyReaderSubtree` 条件不变。US UI-10 无需重复修改。

真实链为已有 Host full arrange → 结构声明身份 → Session keep/prune → editor／capture／hit 等原寿命。
新保证不把身份集合当作布局、输入资格或业务新鲜度证明，也不采用 `3732c7a` 的 editor 实验。

## Evidence and gate

完整 delta 见 [structure](specs/core/structure/spec.md)，决定性边界见 [design](design.md)。
本提交先保留候选；随后按本次指令另作 docs-only 采用提交，把条文并入唯一 current 并更新入口。
采用后仍停在代码门，由 PM 核对后才实施。已有检查、普通载体和真实暖开／寿命证据分别交付；
不新增单测／case／等价 probe，不把规格、编译或 RW1.6 的 r4 证据当作 net10／1.7 或165 FPS签收。
任务与状态归 [tasks](tasks.md)。

# Design — 只减少已证明的声明派生

状态：0.9 采用时设计来源；候选 `51dceef` 后另次文档采用，PM r9已放行本项。
后继源码机制及执行证据见实现账，不从本设计记录推定验收。
本文件保采用时设计来源，解释取舍、不指定实现层数；唯一行为正文归 core/structure current。

## 原边界与新工作保证

`UiLayoutEngine.ArrangeRoots` 在测排后组装完整声明身份，再调用 `UiSession.PruneNodesExcept`。
结构 owner 拥有 immutable 定义、RepeatState／实际派生行和本轮 materialized receipts；
Session 拥有节点、mount、私有子节点及 editor／capture／hit 的结束，不迁移任何权威。
成功发布与异常恢复仍走原 arrangement 事务。

`CollectRepeatItemIdentities` 对已物化行、`CollectHiddenRepeatIdentities` 对可靠隐藏的最后行集
都递归调用 `CollectChildIdentities`。因此值或显示状态虽然不改声明图，full 仍重新生成全部子身份。
要避免的是这段重复派生，而不是完整 keep 集、成员观察或 prune。
已有 `FL09-REGION-UPDATE` 仅明确纯布局／展示工作；新条款补这一独立结构输出的可观测义务。

## 最小完整证明

采用已有 region enrollment，不增开关或作者通知。依据必须把当前实际 rows 和对应派生声明，
连接到其模板、规范 parent/item 身份及仍存 mount。不可变声明引用可作为真实证据，
不要求保存若干固定层级的缓存，也不要求每次深比较所有子项。
具名项重排不应凭新 ordinal 改目标；匿名段使用原真正参与身份的声明索引。

身份依据不包括不相关的值、错误文字、自然高度、宽度或表示包引用。
它们可要求重新测排／Build，却不能单独令同一声明集合重新递归派生。
隐藏但未创建的声明子节点不必为了取得证明而提前 mount。
普通 reader 和 `MaterializeRows` 的 `VerifyReaderSubtree` 条件保留；本补充不把其父桶5.670 ms都当可删。

## 隐藏、嵌套与失效

完整 keep 的输入不是可见 entries。当前完整声明、当次物化、可靠隐藏的最后有效物化应按原身份规则组合。
隐藏源不为了刷新身份而增加读取；原下一合法访问发现的 key 删除仍须清理。
嵌套 Repeat 的动态成员是另一个真实依赖，不能只枚举外层静态模板就宣布 keep 完整。
现有隐藏 walker 从 roots/spec.Children 递归；实施时须对照实际派生行内的嵌套 materialization 路径，
不能以这一静态扫描形状代替嵌套完整性保证。本次未执行嵌套故障复现。

模板／声明图变化、身份相关位置变化、真实移除或 mount 更替令受影响依据失效；
相容未变分支可保留。无完整证据时保原完整收集／验证，不能用快照相同或 skip prune 兜底。
新结果仅随原完整事务发布，失败不污染旧关系，也不能吞掉回调中新失效。
Close／detach／显式 lifetime end 不借声明复用延寿，仍遵守已有 owner 释放和退出条件。

## 可观察的验收与不选择的范围

后续实施获准后，在原真实暖开／改值 full 路径核对：完整 keep 与寿命结果仍成立，
未变行子声明的递归访问／新身份派生实际停止，而非更名、挪入别的父桶或隐藏场景。
允许仍做必要边界验证、成员判断和已证明身份的合并；不要求总事务或合并本身与变化节点严格成正比。
可用既有源码路径、既有检查及获准真实调用旁观记录；不为此新增测试／case／等价 probe或公开计数 API。

保留 r4 固定配对作为此改动前基线。HOT-R4 中无 Gen0 计数变化的暖开 58.449 ms、身份9.820 ms
是选择问题的证据，不是保证净省9.820 ms；MeasureRegion self20.012 ms仍须其原 owner 另行定位。
不采用 editor-read 实验，不改变 format/parser 失效条件、Draw、普通输入或后端，
不签 net10／1.7、GPU／scanout 或165 FPS。

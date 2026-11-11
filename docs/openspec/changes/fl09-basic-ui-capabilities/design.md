# 决定性机制、反例与退出

## 原合同可靠性先修，不能把规避变前置条件

采用结论是“冻结v3与当前相关源存在提交／观察键域不一致的静态路径；旧现场高可信指向该机制但非唯一证成”。
源码及输入身份只在[实现账](../../implementation.md#fl09-reliability)，规格不钉内部表名。
曾见键离开当前提交域又收到布局公告时，历史pending可能无当前提交者。修复应对齐每Host的实际依赖、已验证版本与成功publication；
不能全清pending、全局ack、忽略Stale或换Host。消费者通知收敛是选择，不是兼容修法。

| 能反驳弱方案的条件 | 必须保住的结果 |
| --- | --- |
| 隐藏祖先、同值冗余公告、再显示 | 无当前依赖的历史键不永久封锁无关几何；复显仍读最新值／测量；直接隐藏叶不等于隐藏整个后代。 |
| 同key多位置／共享bindings多Host | 一个owner卸载不解除其他真实依赖；一个Host发布不替另一个确认。 |
| 通知在测量／提交期再更新，候选失败后重试 | 不能把较新revision无条件抄为已验证；保完整旧结果与原失效，成功后恢复，业务副作用不回滚。 |
| 相容定义更新／实际退休后同key重挂／回调Close | 相容状态按原约定保留；真正新mount不继承旧地址／capture／稿；Close不被回退复活。 |
| 更多draw帧／读取Capture | frame不证明某key提交；读取不驱动UI、不能为解锁而强制arrange。 |

## 新能力从一份有效几何走到真实输入

双轴内容明确extent／viewport／offset逻辑单位；两条滚动条相互改变可用区域，一次完整结果一起发布。
新能力按轴实际位移仲裁：无位移才逐祖先交接；popup端点仍独占；旧纵Scroll不改。
有限画布只有正比例缩放＋平移，不能暗含旋转／倾斜／通用仿射。本目标选择指针缩放保指针下内容点，键盘／语义缩放保视口中心内容点，resize把旧中心内容点置于新中心；clamp优先于无法满足的锚点，显式fit/reset另按声明；
一个有效变换结果供paint、clip、topmost hit、popup和工具事实，Tools观看变换不进入布局。
首批原生编辑留视口外；变换内editor/caret明确拒绝，旧编辑入口照常。缺平台输入事实就报告缺能力，不伪造物理输入。

## 隐藏、焦点、虚拟化和详情不能互相替代

隐藏保相容稿，真正退休释放旧UI；跨实际关闭的长稿由应用pending-edit持有，不造永久节点缓存。
仅失焦仍可见时继续展示；是否结束capture按真实资格／原生归属，不按“失焦”一词补业务提交。
inline详情先保全文、身份、缺图与动作；交互内容不冒称tooltip。未来modal才要求背景资格、焦点圈和关闭落点；
触发者已移除时不转向同key新mount。K1／OS桥是后续目标，不是Tools语义树自动带来的功能。

## 一手参考与本项目判断

[Pointer Events 3](https://www.w3.org/TR/pointerevents3/#implicit-release-of-pointer-capture)用于明确up/cancel结束归属；
[Qt坐标体系](https://doc.qt.io/qt-6/graphicsview.html#the-graphics-view-coordinate-system)用于分离item/view映射；
[ScrollViewer](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/scrollviewer)用于区分物理增量／逻辑滚动；
[Qt IME](https://doc.qt.io/qt-6/qinputmethodevent.html)用于区分preedit/commit；
[APG modal](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/)用于后续焦点与关闭落点；
[WPF虚拟化](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls)用于区分容器和业务状态。
这些机制指导反例，不把Web／Qt／WPF平台API整套变成FL合同或当前实现证明。

## 证据与退出

旧失败、反例成功、后续consumer成功各保其版本，不拼成隔离实验，不重放18项。
实现后用有关既有检查及真实caller取得对应after-success；没有新增单测/case/等价probe/runner。
真正native几何、物理鼠标／键盘／IME及Game17生命周期按对应操作授权分别取得；离线语义动作不代签。
每个切片都有last-good、取消／失败／关闭和资源退出，未满足则不称完成，也不反写旧合法条件。
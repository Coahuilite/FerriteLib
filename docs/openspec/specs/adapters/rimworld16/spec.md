# RimWorld 1.6 原生适配

## Purpose
将 Verse IMGUI 原生窗口、事件、坐标、文字及资源事实接到中性核心。原生 WindowStack 持窗口资格／焦点／遮挡／模态，FL 持 adopted UI 状态；Catalog 不是另一个原生 z-order authority。当前以1.6为主要交付目标，1.7共用 GUI 路线仅有基础兼容候选，其原生渲染与主要交互仍待用户验收。

0.9 区域保留已按 WO-FL09-RETAINED-DISPLAY-20261111 r2 采用、未实施；本页新增区域条款及
相关显式例外仅适用于主动采用的新能力，不表示当前 API 已可调用。行为唯一正文归本页，
采用来源见 [fl09-retained-regions](../../../changes/fl09-retained-regions/proposal.md)。

## Requirements

### Requirement: FL08-PLATFORM 完整核心闭包中立
核心结构、行为、属性、布局、响应、寿命及可替换服务 DTO 的完整传递依赖 SHALL 只用中性合同与标准运行库；不得暴露 Unity、UnityStubs、Verse、RimWorld、Waylith、Harmony 类型，也不经 object、反射或 generic resource 暗藏原生能力。中立与线程安全、实际可在哪个线程执行 SHALL 分开；同一核心源码不是复制模拟，单 DLL 不妨碍此边界。

#### Scenario: 独立业务只使用纯语义
- **WHEN** 无引擎环境消费中性能力
- **THEN** 不需要引擎／runtime stubs 才得到该合同
- **AND** 此事实不授权后台线程调用 native Host

### Requirement: FL16-FRAME 每个真实回调只捕获一次
标准 UiWindowHost SHALL 从真实 WindowStack.GetsInput 等原生归属取得资格；embedded owner 自供实际 rect／资格。frame SHALL 分开 phase、raw phase、pointer/wheel/key/composition 与 InputAllowed，即使不合格也保留结束 owned capture 所需事实。实际 Event.Use 结果对后续控件可见，不重造未消费事件或转发第二次。原生 enum 采用明确映射，不依赖别名 ToString 或数值恰同。

#### Scenario: 同一回调操作多个无 Host 控件
- **WHEN** retained Session 已 SetFrame 一次
- **THEN** 它们共享同一事件及演进的 consumed 状态
- **AND** 不为后一个控件重新捕获以恢复点击资格

### Requirement: FL16-COORDINATES 度量、绘制与指针同逻辑坐标
适配 SHALL 使用 Verse 已应用 UIScale 的逻辑 rect、pointer、window origin 与 scroll/clip 变换；测量与绘制匹配同一有效字体／语言／宽度环境，候选定位同位。不二次缩放，不迁入 framebuffer pixel 策略。布局响应重排与整个页面比例缩放不是同一能力，map/world-space 绘制永久不属 UiKit。

#### Scenario: UI scale 改变
- **WHEN** 原生给出新的逻辑 viewport 和字体环境
- **THEN** 按实际约束失效并重新测排，draw／hit／popup 使用共同坐标
- **AND** 不把缩小viewport当作任意比例缩字的实现

### Requirement: FL16-REPAINT 原生要求的绘制继续执行
适配 SHALL 执行所需 Repaint／原生 editor 调用；retained 结构或几何不得用于跳过 Repaint 永久保留旧像素。输入不合格仍可按可靠展示合同绘制；GUI callback 次数不构成业务时钟／帧数／FPS。临时 font/colour/anchor/wrap/enabled/group SHALL 在异常 finally 恢复，不撤销合法焦点变化。

#### Scenario: 不合格窗口收到 Repaint
- **WHEN** 当前展示有效
- **THEN** 继续画当前 UI，不因不能新输入而留旧像素

### Requirement: FL16-NUMERIC-TEXT 已采用的按用途数值能力
数值文字 SHALL 分开标签类与输入框类能力；只有实际宿主声明支持该用途才下发数字。
1.6 标签与输入框路径的证据等级均为 **静态候选、待原生**；临时改共享 GUIStyle.fontSize
必须在 finally 还原，标签保留 Widgets.Label 的缩放网格对齐和 contentOffset，不重复乘 UIScale。
旧别名路径和无数字调用保留原行为。

生产能力 SHALL 由 public-unstable VerseTextExecution／VerseTextMetrics 组合提供，不给旧 Stable
类型追加公开／protected 成员或 implements。执行组合只有一个 VerseUiExecution owner，
frame、capture、focus、native identity、images、edit、cleanup 仍委托该 owner；Session 保留实际
供应端口。标准 Window 的旧 protected Execution 与自定义 Metrics override 不被绕过。
独立 Host/Session/custom widget 可直接供应同一组合对；关闭先逻辑 Session 再自有 native owner。
新 UiTextEdit 组合旧请求、有效字体与可选字色，实际 TextField/TextArea 消费字色和字体，
无显式字色保留 native preset。其实现 helper 可内部扩展，旧 UiNativeEdit 外形不变。

1.7.4995 rev309 的源码链仅支持标签数值候选；GUI.TextField/TextArea 固定传默认字号，
输入类 SHALL 回到别名预设并报告。标签能力须有独立明确开关，关闭即可让该宿主全部回到预设。
判断宿主沿用 Font 的定义程序集，不用反射；Unity 独有成员隔离在 NoInlining 私有方法。
未知字体环境仍返回 null、撤销跨同步复用，不因数字字段放宽有效性；未请求同步时保留
原 idle-snapshot 条件，不宣称 null 环境已可靠。
两宿主的数值新路径均未原生验证；当前实现状态见
[开发映射](../../../implementation.md#appearance-freedom)。

#### Scenario: 宿主不支持请求的用途
- **WHEN** 作者请求字号 N，宿主声明该用途不支持
- **THEN** 测量、绘制、缓存和 fit 同用别名预设，并报告“请求 N，生效 预设，原因：宿主不支持（用途）”
- **AND** 每个元素路径每种原因只报一次，请求值不冒充生效字号

### Requirement: FL16-EXECUTION 借用资源不能默迁 active 状态
Host/Session SHALL 借用 execution；idle replacement 合法，且独立 session 可共用外层 execution。open pass/capture/active或focused edit 时，SetFrame 更换 execution SHALL 在改 frame／qualification／ownership 前拒绝；先在旧 execution 结束或 SuspendEditor。可靠挂起稿可保留，但原生 focus IDs、capture mapping、image token 不迁移。

#### Scenario: 编辑期间尝试换 execution
- **WHEN** 当前 editor 仍活跃
- **THEN** 拒绝替换且原 frame／owner 不变
- **AND** 不释放另一 execution 同逻辑 key 的输入

### Requirement: FL16-DISPOSE 原生 owner 最终释放自己的资源
Session 逻辑关闭／尝试自有清理后，platform owner SHALL 在 finally dispose execution，即使 session 聚合失败。Host close 不 dispose 借用 execution。映射／focus names／images 按 execution scope 隔离；adapter 释放纹理引用但不 Destroy supplier Texture/RenderTexture。资源 getter 异常不是 missing image，应保原因进入 shell failure；正常 TryRead=false/空值是 no-image。

#### Scenario: 背景 getter 抛错
- **WHEN** shell 不能取得应画的资源
- **THEN** 本 pass 不继续 page 或伪画正常 notice，恢复 GUI 状态并释放自有 page
- **AND** 后续沿原 terminal page-unavailable 状态，不自动重试业务 getter；chrome／close 保原保护

### Requirement: FL16-WINDOW 标准外壳与嵌入平等合法
UiPageWindow SHALL 可直接构造；Catalog 注册非采用前提，UiWindowHost 是定制 shell 接缝。Catalog 的 consumer/kind/contextKey 去重与 activation 不代替 native stack reordering／qualification。embedded Host 与标准页共享 frame、默认能力、失败和 Close；外围 native owner 仍持 save/veto。SetDisplayDemand 是可靠需求不是 focus。

#### Scenario: 激活已有 catalog key
- **WHEN** 对应窗口仍在
- **THEN** 保持它的 Host、稿、scroll 并激活原实例
- **AND** 不声称 Notify_ManuallySetFocus 等于 bring-to-front，也不关闭重开来模拟导航

### Requirement: FL16-SUPPORT 交付与未交付平台分别报告
当前载荷 SHALL 是 net472 的1.6 adapter，1.7基础兼容候选复用同一载荷。真实1.7源码／metadata、同源中性执行、1.6 reference/stub、1.6 native 与1.7 native SHALL 分开报告；Unity 同名兼容类型不证明二进制／输入／文字／合成兼容。基础候选不代表完整1.7 adapter、direct GPU backend、world-space HUD 或新增全局 focus/key 系统已经交付。

#### Scenario: 1.7 来源含 UnityEngine 命名空间
- **WHEN** 实际 assembly scope 为 UnityStubs/Waylith 等
- **THEN** 仍需独立真实适配证据，不拿1.6 DLL／旧 stub 通过签绿

### Requirement: FL16-DISPLAY-ISOLATION 展示隔离是执行 owner 显式声明的能力
1.6 原生执行 owner SHALL 声明自己是否支持展示隔离。支持时，它在内置展示 wrapper 的展示期间隔离并在 `finally` 中恢复已知的可变标签样式字段（对齐、换行、字号），使被省略或延期的展示不影响后续控件看到的共享样式；它 SHALL 不恢复或改变焦点、hotControl 等合法的原生状态变化。未声明该能力的后端，T3、FL09-TIER-SAFE 与 FL09-CLIPPED-DISPLAY SHALL 退回逐 pass 展示。展示周期只由事件阶段推出，本能力不读取 Unity 帧计数，也不新增其他 Unity 接触点。FL16-REPAINT 不变：Repaint 与原生编辑调用照常执行。

#### Scenario: 非 Repaint pass 省略静态标签
- **WHEN** 隔离能力生效，后续控件读取当前标签样式
- **THEN** 它看到的对齐、换行、字号与逐 pass 执行时相同

#### Scenario: 展示中途抛出
- **WHEN** 隔离作用域内的展示失败
- **THEN** 样式字段在 `finally` 中恢复，失败按现有 presentation fault 合同处理

## 0.9 区域保留：已采用、未实施

本节仅约束主动采用的新能力；其余原条件保持。新增闭包已定 Public-unstable，尚无对应导出。

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

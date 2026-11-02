# RimWorld 1.6 原生适配

## Purpose
将 Verse IMGUI 原生窗口、事件、坐标、文字及资源事实接到中性核心。原生 WindowStack 持窗口资格／焦点／遮挡／模态，FL 持 adopted UI 状态；Catalog 不是另一个原生 z-order authority。当前以1.6为主要交付目标，1.7共用 GUI 路线仅有基础兼容候选，其原生渲染与主要交互仍待用户验收。

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

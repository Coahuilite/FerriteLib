## 4. 未完成项与阻塞（最终汇总）

**外部依赖（不由本团队解除）**

- **实机验收 A1–A11**：阻塞于真实游戏会话；本团队环境无游戏操作能力。步骤见 §3.1/§3.2，必须逐行回填结果，"没有报错"不是结果。
- **真实消费者接入**：阻塞于消费者团队排期。库侧已交付接口、开发包、迁移步骤与已知限制（[30-consumer-handoff.md](30-consumer-handoff.md)）；
  消费者在自身树里编译并运行之前，任何条目都只能是"库侧就绪"，不能记为"已接入"。

**已登记、未关闭的实现边界**（每条都注明来源包、当前行为与恢复线索；不是"忘了"，是不在本轮范围或已由独立验证判定可接受）

| # | 边界 | 来源 | 分类 | 恢复线索 |
| --- | --- | --- | --- | --- |
| 1 | 壳首帧的 chrome fit 发现走进程级旧通道（首帧在 `CreateHost` 之前绘制） | P1/P5 缝合 | 可接受 | 与"host 尚不存在"同源；若要覆盖需重构 notice 契约 |
| 2 | 终态 `PageUnavailable` notice 无活订阅可归属（host 已释放） | P1/P5 缝合 | 可接受 | 需 shell 自持订阅（占 1 个 hub 槽位），属产品决定 |
| 3 | `UiWindowOptions.NormalSize` 语义暂定、无消费者证据 | P1 | **待文档（已写）** | 消费者在真实窗口上验证后再当默认值 |
| 4 | 激活组合（`ActiveKey` + `Notify_ManuallySetFocus`）不是 bring-to-front，真实键盘路由未验证 | P1 | 待实机 | A1/A3b |
| 5 | `UiSession.ContentRevision` 保留为排布缓存时钟 | P2 | 可接受（已三处写清） | 若删需迁移 `UiHost` 的时钟调用点 |
| 6 | `UiNative.TextField(Rect, string)` 无 session/无 key，无法查询禁用态 | P2c | **待文档（已写）** | 用带 session 的形式或自持 kind |
| 7 | 失效类别在注册时声明，无逐元素覆盖 | P2 | 待文档（已写） | 需要两类就拆两个 key |
| 8 | 元素存在之前发出的通知不补发（首次排布直接读当前值，不丢更新） | P2 | 可接受（已写） | —— |
| 9 | 重载会把"文档可设置的主题令牌"整体恢复为构造时值（上一份文档声明过页面级默认值时） | P4/P4b | 待文档（已写） | 逐令牌收窄需要 `UiStyleResolver` 的映射成为共享写者 |
| 10 | 页面级默认值规则只在**重载**路径生效；首次加载是软 no-op，同一文件两种语义 | P4c | **待关闭（已显式记录）** | 让该规则在首次加载与重载上对称 |
| 11 | 公开面：页面级名字无法解析会拒绝整个候选版本；此决定**不易回退** | P4c | 已裁定（§6） | 回退会重开预校验空洞；真回退需新机制 |
| 12 | 热重载候选的模板结构错误在**提交**阶段拒绝（整批回滚），不在 P4 预检阶段 | P3 | 待文档 | 让 `TryPrepareLayoutCandidate` 也校验模板表 |
| 13 | 引擎的模板校验器**不**复制 `UiHost` 的数值/窄态语法：模板里的畸形数字按程序化 spec 降级，而不是创建期失败 | P3 | 待文档 | 把两份词汇表合成一个共享写者 |
| 14 | 手工构建 roots 列表且不传模板表时，`<Repeat>` 只有一条有界报告、没有行 | P3 | 可接受（已写） | 用 `UiLayoutManifest` 或 `UiPageWindow` |
| 15 | `UiFitAudit.Enabled` 仍是进程级开关（测量全局、路由分 host）；订阅期间旧通道的 `ReportedCount` 只描述旧通道 | P5 | 可接受（已写） | —— |
| 16 | 预算独立性用 lane 断言但**没有**植入突变（去重那半有） | P5 | 待补 | 为预算独立性补一处植入突变 |
| 17 | 构造期样式丢弃记录发生在可订阅之前，仍走旧通道（同时有整份文档丢弃的警告） | P5 | 可接受（已写） | —— |
| 18 | `UiHostLedger` 上界 512（实测真实计数 132） | P5 | 可接受 | —— |
| 19 | 死 lane 守卫已入库（`b6b621d`）——此前「lane 存在但从未注册」无门禁；最终验证发现它去重已注册名，重复注册不可见（F6） | P6 | **已关闭**（`b812dfc`）：守卫现在也会命名重复注册 | —— |
| 20 | 未订阅路径的分配 lane 比声明窄：主体是合成两次调用循环而非 `UiHost.DrawFrame`，且未断言主体循环真的执行过（无法区分"0 分配"与"循环被省略"） | P5 | 待关闭（已收窄声明） | 让 lane 直接驱动 `DrawFrame` 并断言主体执行次数 |
| 21 | **交错帧归属 lane 缺失**（不是「归属未被证明」）：`plan-P5-diagnostics.md:27-29` 明确**要求**一条 A,B,A,B 交错、各自计数的 lane。现有 lane 已证伪「共享槽位」与「进程级去重表」（多 host 共存 + M6 突变转红），缺的只是交错帧 + 共享元素路径这一种形状 | P5 | 待补（独立验证判定为记录项而非 must-fix：增量风险低） | 按 plan-P5 补交错帧 lane，应由 diagnostics 侧实现 |
| 22 | 夹具页注释「此行以下不知道矩形」对**测试半边**不成立（测试半边确实用 Rect 驱动事件泵） | P3 | 待文档（已修） | 注释已限定为"页面半边" |

## 4.1 需要外部输入才能推进的两项

1. **实机会话**：A1–A11；其中 A1/A2/A3/A3b（窗口/焦点/输入穿透/模态共存）与 A6/A7/A8（热重载与输入安全）是本轮最关键的未验证面。
2. **消费者编译**：本轮所有"公共面"声明都止于本仓 harness。消费者一旦在自身树里钉住 `[0.5.0,0.6.0)` 并编译，就能把
   "库侧就绪"推进为"已接入"，也可能发现本仓发现不了的形状问题——那正是让消费者反馈驱动修订的入口。

# 40 自动化验证、实机清单与未完成项

> 证据分类见 [README.md](README.md)。本文件是唯一的验证结果面；每次合并后由 Lead 更新。

## 1. 门禁与 harness（库侧自动化的全部）

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1
pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev
```

| 门 | 断言 | 基线（`c53bd37`） |
| --- | --- | --- |
| setup | 全新树 restore 引导 | OK |
| 1 | harness：kernel lane + 版本契约 + 中立性 + 边界 | ALL PASS |
| 2 | library Dev build（warnings as errors） | OK |
| 3 | library Release build（warnings as errors） | OK |
| 4 | payload 落在消费者绑定的路径（比对 MSBuild `TargetPath`） | OK |
| 5 | 载荷无游戏内容（Defs/Patches/Languages/…） | OK |
| 6 | LICENSE = MPL-2.0 全文，无不兼容声明 | OK |
| 7 | `About.xml` 身份与版本轴 | OK |
| 8 | net472 陷阱扫描 + harness stub 覆盖扫描（含自检） | OK |
| 9 | rule (c) 消费者半边 armed + tree-sensitive | OK |

**门禁能证明什么、不能证明什么**：
它证明"库内部自洽 + 契约轴一致 + 载荷形状正确"；它**不**证明消费者仍能编译，
也**不**证明游戏里的组合行为（层级、焦点、输入穿透、模态共存）。后两者分别属于消费者与实机。

## 2. 各包的自动化证据

| 包 | 新增 lane | 结果 | 突变证明 | 状态 |
| --- | --- | --- | --- | --- |
| W0 | —— | 基线在 `c53bd37` 与 `99acc5f` 的**干净提取**上各复跑一次：门禁 exit 0、harness 867 ok / 0 FAIL / ALL PASS | —— | 完成（独立复跑） |
| P1 | `KernelWindowCatalogTests`（91→108 断言） | 合并后门禁 exit 0；harness 1050 ok / 0 FAIL | **7 处**：M1 去重、M2/M6 `AllowMultipleInstances` 与原版精确类型规则、M3 失活释放捕获、M4 激活点击被消费、M5 chrome 身份带 key、M7 调原版焦点设置 —— 独立复跑全部转红、无一留绿 | 已合并 |
| P1h（task-10） | 同上（+9 断言） | 门禁 exit 0；harness 1081 ok | 2 处 stub 突变（精确类型 vs 可赋值，双向）+ M1 由 UNHANDLED 变为 3 条命名 FAIL | 已合并 |
| P2 | `KernelInvalidationTests` + `KernelCommandStateTests` + `KernelVisibilityTests` | 门禁 exit 0；harness ALL PASS | **6 处转红**（a1 忽略声明类别 3、a3 去掉按 key 失效 13、b1 去掉可执行守卫 3、b2 去掉漏斗禁用守卫 1、c1 隐藏后重编号 4、**c2 过度剪枝 7**——作者报 6，独立复跑为 7，多出的一条是 Tab-隐藏兄弟断言）+ **1 处诚实的负结果**：只删每节点 `MarkDirty` 而保留时钟 → 全绿（已写进代码注释） | 已合并 |
| P2b（task-9） | 同上 | 门禁 exit 0 | 含在 P2 的 c2 突变内（过度剪枝 6 转红） | 已合并 |
| P4 | `KernelDocumentReloadTests`（81 断言） | 门禁 exit 0；harness 948 ok | **3 处**：批次原子性 6、状态清理 5、重复版本跳过 2 | 已合并 |
| P4b（task-8） | 同上（103 断言） | 门禁 exit 0；harness 1072 ok | **4 处**：AutoWatch 旧行为 3、style 预校验分支移除 7、回滚移除 1、无条件基线恢复 1 | 已合并 |
| P3 | `KernelRepeatTests`(**61**) + `KernelControlKindTests`(**47**) + `KernelFixturePageTests`(**19**)（作者报 60/46/14，独立复跑修正） | 合并后门禁 exit 0；harness 1426 ok | **5 处全部转红**（作者计数被独立复跑修正）：声明身份集合漏掉行 **18** FAIL（作者报 14）、行身份改用位置 **24** FAIL（报 14）、只读 checkbox 保留命中规则 **1**、引擎唯一禁用决策永不触发 **8**、隐藏集合被回收 **2**；恢复后 SHA256 校验一致 | 已合并 |
| P5 | `KernelDiagnosticsTests` | 合并后门禁 exit 0 | 作者报 **7 处**（会话归零、释放清空全部、去重键旁路、reload 钩子丢弃、注册表单一槽位、recovery kind 丢失、植入静态 host 引用）；独立复跑 6 处均转红。未订阅路径**实测** 0 分配（对照循环 483,328 B），但**该 lane 比其声明窄**：主体是合成的两次调用循环而非 `UiHost.DrawFrame`，且没有断言主体循环确实执行过——见 §4 第 20 条 | 已合并 |
| P1/P5 缝合（task-13） | `KernelWindowCatalogTests`（+13 断言） | 合并后门禁 exit 0 | 2 处：作用域参数置空、整段 using 移除 | 已合并 |

填写规则（**不得伪造 PASS**）：每条 PASS 必须写明 ①在哪个 revision 上跑、②跑的命令、
③是"变更证明（mutation-proven）"还是"未来回归护栏（regression guard）"。
只有"把功能改坏后该 lane 变红、还原后变绿"才算变更证明。

## 3. 实机检查清单（待外部执行）

本仓**无法**替代实机。下列场景来自主计划 §6，必须在真实游戏里跑并回填结果：

| 编号 | 场景 | 核心断言 | 状态 |
| --- | --- | --- | --- |
| A1 | 同型双窗、同 key 重开、不同上下文 | 正确共存或激活，命令目标不串线 | 待实机 |
| A2 | 双窗 + 原版确认窗，鼠标/键盘/Cancel/Accept | 原版模态有效，无穿透、连关、误操作 | 待实机 |
| A3 | 双窗同时打开，面板外地图选取/相机、面板内点击/滚动/拖动、分辨率变化 | 面板外正常操作地图；面板内不穿透；重叠区域只由前方目标处理 | 待实机 |
| A3b | 面板 A 草稿 → 点面板 B → 点地图 → 回面板 A | 草稿/选择/滚动保留；键盘只到当前目标；返回不误提交 | 待实机 |
| A4 | 后台任务改变进度/资源/资格 | 相关窗口自动更新；固定尺寸读数不引起持续整页测量 | 待实机 |
| A5 | 列表插入/排序/删除，删除正在编辑或持有弹层的行 | 状态跟随 key；捕获与订阅释放；重复操作数量不增长 | 待实机 |
| A6 | 保存布局与共享样式 | 局部更新正确，共享更新一致；自动与手动路径均可用 | 待实机 |
| A7 | 坏 XML、未知绑定、文件暂时缺失、重复写入 | 旧页面仍可用，告警有界；修复后可加载；无半批更新 | 待实机（库内桩侧有对应 lane） |
| A8 | 编辑输入、拖动、开弹层时重载 | 不误提交命令；草稿策略成立；无遗留 hotControl | 待实机 |
| A9 | 多窗诊断与关闭/重开、换存档、目标销毁 | 事件归属正确，订阅与缓存清理，无跨游戏引用 | 待实机 |
| A10 | 英文/简中、窄宽窗口、UI scale、滚动和长文案 | 真实字体无关键遮挡（桩测试不能代替） | 待实机 |
| A11 | 打包后从实际 Mod XML 路径加载 | 外部文件存在；缺失时内嵌回退；消费者不带库 DLL | 待实机 |

### 3.1 P1 窗口/焦点/实例（实机步骤）

- **A1**：打开 kind K 的上下文 A，再打开上下文 B——两窗都在；关闭 A 不影响 B；重开 A 是激活既有面板（不出现第三个窗口），其滚动/选择/草稿原样保留。记录 `WindowStack.Windows` 顺序、游戏报告的焦点窗口，以及第二次 Open 是否曾被 `RemoveWindowsOfType` 取代第一个。
- **A2**：两个面板打开时，打开一个原版确认框——Accept/Cancel/关闭 X 都作用在对话框上；两个面板都不被关闭、也不再接收输入；对话框关闭后恰好一个面板是键盘目标。
- **A3**：依次点击面板 A、面板 B、地图——同一时刻只有一个活动目标（`UiWindowCatalog.ActiveKey`）；"选中一个失活面板"的那一次点击**不得同时按下该面板里的控件**（一次点击只选中，第二次才操作）；地图点击后任何面板都不再收到键盘输入。记录每一步之后原版焦点落在谁身上，以及重叠面板的表现。
- **A3b**：在 A 里输入草稿，点 B，点地图，回到 A 的输入框——草稿/滚动保留，键盘只到"最后点击的面板"，不重复提交、不抢焦点；若 A 中已捕获一个拖拽，点 B 后在地图上拖动——A 的控件不得继续接收该拖拽。
- **UNVERIFIED-IN-GAME**：P1 的激活组合（`ActiveKey` + 可选 `WindowStack.Notify_ManuallySetFocus`）**不是** bring-to-front，也不重排窗口栈；它是否足以支撑真实键盘路由，由 A1/A3b 判定。

### 3.2 P4 文档/热重载（实机步骤）

- **A6**：编辑面板 A 的布局 → 只有面板 A 的窗口更新；编辑共享样式 → 所有实际依赖它的窗口一致更新；自动路径与手动 `Reload` 都可复现同一结果。
- **A7**：写入坏 XML → 旧页面继续可用、告警有界；修好后能恢复；同一批用两个窗口验证"任一失败则整批不提交"（**注意**：该原子性目前只在 layout 文档上被自动化证明，style 文档见 task-8）。
- **A8**：在输入框打字/拖动/打开弹层时触发重载——不误提交命令、草稿策略成立、无遗留 hotControl。

## 4. 未完成项与阻塞

见 [10-work-packages.md](10-work-packages.md) §4 的实时状态；本节在最终交付时汇总：

- 实机验收（A1–A11）：**阻塞于真实游戏会话**，本团队环境无游戏操作能力。
- 消费者接入（真实页面）：**阻塞于消费者团队排期**；库侧交付接口、开发包与迁移步骤。
- 两者都完成后才能把任何条目记为"已实机验证"。

## 5. 独立审计发现与处置（wave A，`c69ab9b` → 处置于本提交）

独立审计（`docs/development/0.5/verification/wave-A-audit.md`）在基线与 W0 文档上做了两件事：
在干净提取（`git archive` 到临时目录，**不是工作树**）上复跑门禁与 harness
（`c53bd37` 与 `99acc5f` 均 exit 0、867 ok / 0 FAIL / ALL PASS），以及核对 W0 文档的每一条"已实现"陈述。

| 编号 | 发现 | 处置 |
| --- | --- | --- |
| F1 | `00-baseline.md` §2.1 仍称 `UiNative.YieldsToCoveringPopup` 是下拉触发器的私有分支——该符号在 `99acc5f` 的 `Source` 下 0 命中，0.4.0 的 hit stack 已删除它；`MEMORY.md` 与 `TODO.md` 同处亦有同一条过时说法 | 已按源码改写三处：元素级 yield 由 `UiSession.IsPointerOverHigherLayer` 集中裁决，五个 atom 均走两参数入口，唯一裸调用是壳的关闭按钮（已登记）。**这是本轮最严重的一条——W0 的"纠正过时说法"自己重复了一条过时说法** |
| F2 | `00-baseline.md` 把"完整 z 序调度"记成 P2/P3 的目标，但没有任何包拥有它 | 改为明确的**本轮不做**边界（内容层互不裁决），恢复条件仍在 `docs/api-tiers.md` 的 `UiHitLayer` 条目 |
| F3 | `api-tiers.md` 与 `MEMORY.md` 称 `ParseFile` "zero callers/no caller"——harness lane 直接调用它 | 三处改为 "no production caller"，与 `MEMORY.md` 既有的准确说法一致 |
| F4 | `40-verification.md` 的 A3b 行使用了第二消费者的窗口名称 | 改为中立的"面板 A / 面板 B" |
| F5 | `30-consumer-handoff.md` 的 `ModRequirement` 字段表漏 `alternativePackageIds` | 已补全 |
| F6 | `api-tiers.md` 仍在讲一个已经关闭（`v0.4.0-rc1` 已切）的"0.4.x window" | 改为"0.4 线未完成、在 0.5 窗口继续" |

**未能复核**（照实登记）：真实游戏 `Assembly-CSharp.dll` 的哈希与 IL 控制流（只核了固定版本引用程序集里符号存在）；
GitHub release 状态（本环境 `api.github.com` 不可达）；任何消费者在 0.5 上编译——三项均为"尚待外部验证"。

审计工具链自身的一条纪律也被确认：`git grep`/`git show` 于**已提交的 sha**上取证，
而不是读工作树——`MEMORY.md` 已记录过一次"lane 文件存在但从未注册"的同类事故。
## 6. 本轮对失败阶梯的一处刻意收窄（Lead 裁定 2026-09-15；独立验证后重写 2026-09-15）

**实测事实（独立验证在 `9078812` 的干净提取上测得，本节此前三句话是错的，已按实测重写）。**

失败阶梯原口径：元素内容不把页面拖垮（bucket 1）、结构 fail-closed（bucket 2）、外观值 fail-soft 且不得静默。
P4c 把**页面级**默认值（`DefaultScheme`/`DefaultDensity` 指向本文件未声明的名字）改为
**在重载路径上拒绝整个候选版本**，让"候选先校验"这条契约真的可被证伪。

- **这是第三类失败：版本级失败并保留 last-known-good**，不是"结构 fail-closed"。
  页面不会因此停机——它继续用上一版渲染，所以不违反阶梯里唯一的那条绝对规则（"外观不得把页面拖垮"）。
  独立验证的判定：**可接受**；此处 fail-soft 会是作者无法诊断的静默 no-op。
- **代价**：该文档里其余合法的 scheme/density 在这一版里也不生效，改对名字后一起生效。

**实测的边界与不对称（必须照实转述给消费者）：**

- **首次加载不走这条规则。** 一个结构合法、但页面级名字无法解析的样式文件在**首次加载时被直接采纳**
  （无拒绝报告，`Attach` 接受它，名字不生效）；只有**已加载文档的重载**才会被拒绝并保留 LKG。
  因此**同一个文件在首次加载与重载下语义不同**：首次加载是"页面级软 no-op、其余照常生效"，
  重载是"整份候选被拒、LKG 保留"。
- **重载被拒时的 LKG 可能就是首次加载那个有问题的版本**（实测：先采纳坏版本，再重载第二个坏版本被拒，LKG 仍是第一个坏版本）。
- **没有"内嵌布局回退"这回事**（此前的措辞是错的）：样式源首次加载失败（结构非法/不可读/超限/文件缺失且无可用内嵌文本）时，
  host 保留**构造时的样式来源**（`manifest.Styles`，页面没有 `<Styles>` 节时即空文档；或构造时传入的样式文档）。
  内嵌文本只在**外部文件缺失**时被采用，且它本身必须是一份样式文档。
- **恢复路径并非"把页面级规则改回软丢弃"**（此前的措辞不可行）：结构非法的样式文档在 `ReadCandidate` 阶段就被拒，
  根本到不了 `TryPrepareStyleCandidate`，所以不存在"用既有结构失败驱动该 lane"的替代。
  改回软丢弃会**重新打开空洞**。诚实的说法是：**这一步不易回退**；真要回退，需要先给该预校验一个
  解析器能真正拒绝、且首次加载与重载对称的规则——那是一件新机制，不是改一行。

**剩余关闭项（本轮不做，登记而不隐藏）**：让页面级存在性规则在首次加载与重载上对称（要么首次加载同样拒绝，
要么重载改为只丢页面级并采纳其余）。当前的不对称由本节显式记录，消费者侧同样照实说明（`30-consumer-handoff.md` §4）。
## 7. 外部独立 review（2026-09-15，对 tip 0117c01）：结论 Request changes

一份外部独立 review（主审 + 3 个分审，自带 net472 公共 API 探针）对 `0117c01` 判 **Request changes**：
「库侧尚不应按『全部完成、只剩外部验收』交付」。**Lead 在本机重新运行了该探针，逐行复现了它的全部输出**
（`dist/review-0117c01/`，gitignored，不入库）。这是本轮最重的一次更正：§1–§6 里「已合并、门禁绿」仍然成立，
但**「仅剩外部验收」的结论不成立**。

| 编号 | 严重度 | 缺陷 | 复现证据 | 处置 |
| --- | --- | --- | --- | --- |
| R1 | P1 | 批次失败回滚只恢复旧文档树，不恢复已被清掉的交互状态（用户未提交草稿丢失） | `ROLLBACK rejected=True restoredRoot=one draft=''`（原草稿 `uncommitted-draft`） | **已修复合入**：提交拆成非破坏性 stage + 整批成功后 seal（剪枝与释放捕获）；回滚清空待剪枝。探针复跑 `draft='uncommitted-draft'`；lane + 3 处突变 |
| R2 | P1 | 已有有效外部版本时，文件暂时缺失仍切回内嵌旧版 | `MISSING before=external-valid` → `accepted=True after=embedded` | **已修复合入**：内嵌仅用于首次无可用版本；已有 LKG 时缺失=拒绝并按版本去重、文件回归后恢复。探针 `accepted=False after=external-valid`；lane + 5 处突变 |
| R3 | P2 | 关闭活动窗口后剩余窗口没有恢复为活动目标 | `WINDOW after close active=null remaining=1` | **已修复合入**：PreClose 记录「关闭者原本是否活动」，PostClose 按策略交给 survivor；拒绝关闭不触发。探针 `active=a remaining=1`；14 条断言覆盖四种形状 + 突变 |
| R4 | P2 | 第二个 host 的诊断订阅覆盖第一个 host 的测量尺（溢出判断互相污染） | `METRICS A before=0` → 仅订阅 B 后 `A after=1` | **已修复合入**：诊断作用域携带本 host 测量尺；外部探针复跑为 `A after B subscription=0`；harness lane 先红后绿 + 突变证明 |
| R5 | P2 | 首次样式应用绕过候选校验：同一文件首次加载与重载语义不同 | `initial attached=True reports=0` → 仅改空白后 `reload rejected=True` | **已修复合入**：首次 attach 走与重载同一条预校验，拒绝时保留 host 自身文档并给出可归属报告。探针 `attached=True reports=1`；lane + 2 处突变 |
| R6 | P2 | 换绑文档服务后关闭 host，旧服务仍持有该 host（依赖泄漏） | `REATTACH after close firstDeps=1 secondDeps=0` | **已修复合入**：换绑时释放旧服务依赖（同一服务重绑安全）。探针 `firstDeps=0 secondDeps=0`；lane + 4 处突变 |
| R7 | P2 | 内容哈希与解析来自两次独立读取（TOCTOU：记录的版本与实际解析的树可能不同） | 控制流确认；未做并发保存压力复现 | **已修复合入，但独立复验把它的证据等级降了一档**：单次 `ReadAllBytes` 快照同时供大小约束、版本哈希与解析（BOM 感知解码）。其 lane 是**源码文本断言**——verifier 仅追加一行注释即令其变红，而若第二次读盘换成它未列举的形态（如 `ReadAllText` + `Parse`）它仍会绿。因此准确说法是「历史上那种双读形态已被守住」，**不是**「竞态已被证明消除」 |

**已接受的额外风险（review 提出，不混入上述已复现结果）**：

- **指针坐标空间混用**：review 时 `ResolvePointerDown` 传窗口局部坐标、`NotifyPointerDown` 比较屏幕 `windowRect`。**该缺陷已在 R3 修复中一并改掉**（统一到窗口空间，并有偏移窗口 lane）。
  **仍然属实机-only 的**是重叠窗口按目录自持的 activationOrder 而非原版窗口栈实际顺序解析（`UiWindowCatalog.cs:385-389`），以及 `windowRect` 取本 pass 自身值而非实时查询——**A3 项**。
- **两个 catalog 同时工作**：仍无组合 lane，登记为待补。
- **普通 content 的激活标志与原版键盘路由的关系**：未实机证明。

**文档修订（已由 Lead 处理）**：`30-consumer-handoff.md` 的诊断条目自相矛盾（同时写「已实现」与「尚不可用」）已删除；
「消费者先自建再谈晋升」已按 `AGENTS.md` 的现行范围改写为两条不同通道；诊断反馈覆盖缺口（构造期样式丢弃、首帧 chrome、终态 notice）已声明；
`50-dev-package.md` 的「逐字节相同」已更正为「源码未变、程序集未做字节比较」。

**交付结论（本节覆盖 §1 的乐观表述）**：R1–R7 已全部修复并合入；review 自带探针在本机与独立复验的干净提取上复跑，七行输出全部转为已修复形态；
门禁与不变量在最终 tip 上独立复验通过。**独立复验的深度是部分的**（覆盖 R1 的 seal 半边与 R7，其余为作者测量），见 §7.4；
**实机 A1–A11 与真实消费者编译从未完成**，因此本轮交付结论是「库侧缺陷已清、仓内护栏齐备、外部验收与部分独立复验仍缺」，
消费者可按 `30-consumer-handoff.md` 接入联调，但不应当作已验收基线。
### 7.1 修复的自动化证据（每个缺陷都要有仓内 lane，探针不能是唯一护栏）

| 编号 | 提交 | 仓内 lane | 突变 | 外部探针复跑 |
| --- | --- | --- | --- | --- |
| R1 | `feat/0.5-documents` | `VerifyRolledBackBatchKeepsInteractionState`（旧文档 + 草稿 + 命名槽 + 滚动位置） | 3 FAIL | `draft='uncommitted-draft'` |
| R2 | 同上 | `VerifyTransientlyMissingFileKeepsLastKnownGood` | 5 FAIL | `accepted=False after=external-valid` |
| R5 | 同上 | `VerifyFirstStyleAttachValidatesLikeReload` | 2 FAIL | `attached=True reports=1` |
| R6 | 同上 | `VerifyRebindingReleasesTheOldService` | 4 FAIL | `firstDeps=0 secondDeps=0` |
| R7 | 同上 | 单次读取管线 lane + 植入双读对照 | 1 FAIL | （探针无此用例） |
| R3 | `feat/0.5-windowing` | `VerifyClosingAWindowHandsOverTheActiveTarget`（四种形状 14 断言） | 3 FAIL | `active=a remaining=1` |
| R3 附带 | 同上 | `VerifyPointerSpaceIsConsistentAcrossOffsetWindows` | 4 FAIL | ——（探针不覆盖） |
| R4 | `feat/0.5-diagnostics` | 双尺交错 A/B lane + 旧通道尺隔离 lane | 5 FAIL | `A after B subscription=0` |
| 契约 | `feat/0.5-collections` | item-local 缺键/错类型/迟到三 lane + wrapped atom 缺键 lane | 4 FAIL | —— |
| stub | `feat/0.5-windowing` | double 五个默认值字面量 pin + option lane 按游戏静止值断言 | 3 FAIL | —— |

### 7.2 review 附带风险与后续项的处置

- **指针坐标空间混用**：已修（`ResolvePointerDown` 统一到窗口空间，`NotifyPointerDown` 只收一种空间），并有偏移窗口 lane。
  仍属实机的部分：重叠窗口按目录自持的 activationOrder 解析，而非原版窗口栈实际顺序；`windowRect` 取的是本 pass 自己的值而非实时查询。**A3 项**。
- **两个 catalog 同时工作**：仍无组合 lane，登记为**待补**。
- **诊断覆盖缺口**（构造期样式丢弃 / 首帧 chrome / 终态 notice）：已在 `30-consumer-handoff.md` §4 显式声明缺失范围。
- **窗口 double 默认值**：已按游戏源码对齐并 pin。
- **R7 的并发替换竞态**：没有 harness 钩子，证据是结构护栏 + 源码装配 lane，**不是复现的竞态**；独立复验需判定该 lane 是否只在满足该实现时才绿。
- **R3 附带发现（window lane 的 NRE 硬化）**：把 `ApplyOptions` 的默认方向写坏时，7 条命名 FAIL 之后还有一条 lane 因解引用 `TryGet` 结果而抛 `NullReferenceException`（与 task-10 同一类）。产品行为不受影响，但该 lane 宜按 task-10 的做法改为命名失败。登记为**待补**。

### 7.3 修复后的独立复验（verifier，clean extraction @ `0d1f38b`）

- `pwsh -NoProfile -File scripts/verify-local.ps1` -> exit 0，`[setup]` + 9 门禁 OK。
- harness -> exit 0，**ok=1515 fail=0 ALL PASS**（交付候选 `0d1f38b` 前为 1426；R 修复 + task-18/19/20 增加 89 条）。
- 外部探针七行全部转为已修复形态（原始输出见 `verification/post-fix-verification.md`）。
- 不变量：tier 74 = 74（未分级 0、悬空 0）；lane 注册 **32 = 32**（无未注册、无幽灵）；三轴 0.5.0；无冲突标记；隐私与中立性干净。
- **该轮未关闭的部分**：R1–R7 的「植入旧缺陷、确认仓内 lane 变红」逐条扫查、七个新缺陷探针、以及 review 附带风险的登记核对，属于 verifier 报告的追加部分；在其落库前，本文件只把「门禁 + 探针 + 不变量」记为已复验。

### 7.4 R1–R7 仓内护栏存在性核对（Lead，@ 最终 tip）

独立复验的追加部分（逐条植入旧缺陷、确认仓内 lane 变红）**未完成并已如实登记**：verifier 报告了门禁/探针/不变量三项后停止，
两次唤醒均未产出追加报告。为避免把「已修复」写成「已逐条复验」，Lead 在最终 tip 上核对了另一半——**每个缺陷的仓内护栏确实存在且已注册**：

| 缺陷 | 仓内 lane（文件内 RunAll 调用） |
| --- | --- |
| R1 | `KernelDocumentReloadTests`: A rolled-back batch restores the old document AND the interaction state it held |
| R2 | `KernelDocumentReloadTests`: A transiently missing file keeps the last known good instead of the embedded text |
| R5 | `KernelDocumentReloadTests`: First style attach runs the same validate-and-apply rule as a reload |
| R6 | `KernelDocumentReloadTests`: Rebinding a host to another service releases the old dependency |
| R7 | `KernelDocumentReloadTests`: The document path reads one snapshot and stays backend-free |
| R3 | `KernelWindowCatalogTests`: VerifyClosingAWindowHandsOverTheActiveTarget |
| R3 附带 | `KernelWindowCatalogTests`: VerifyPointerSpaceIsConsistentAcrossOffsetWindows |
| R4 | `KernelDiagnosticsTests`: Two hosts with distinct rulers each measure with their own ruler |
| 契约 | `KernelRepeatTests`: item-local 缺键 / 错类型两条 lane（+ wrapped atom 缺键） |
| stub | `KernelWindowCatalogTests`: the double rests on the game's own defaults |

**追加部分（verifier，`aa3d240` @ `0d1f38b`）**：独立复验了 **R1 的 seal 半边**（注释掉 `SealDocumentCommit` -> 6 条命名 FAIL）与 **R7**（见上：注释级改动即变红）。
并逐条核对了 review 附带风险的登记：指针空间已修（`UiWindowHost.cs:511-516`、`UiWindowCatalog.cs:392`）而原版栈序重叠仍属实机（`UiWindowCatalog.cs:385-389`）；两个 catalog 同时工作仍未测；
诊断覆盖缺口已登记；窗口 double 默认值已修并 pin。

**仍未由独立复验测量（verifier 自陈的具名缺口）**：R2–R6 的「植入旧缺陷」测量，以及七个新缺陷探针（R1 seal/dispose、R2 文件回归、R3 关闭 survivor、R4 未订阅旧通道尺、R5 无样式来源、R6 同服务重绑、R7 大小上界 + BOM）。
因此 R1–R7 的准确记法是：**已修复合入 + 每项都有仓内护栏（Lead 核对）+ 作者突变证据 + 独立复验只覆盖 R1(seal) 与 R7**。
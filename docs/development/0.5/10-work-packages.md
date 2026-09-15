# 10 工作包、依赖图与集成顺序

分支模型：`0.5.x` 是集成分支与唯一可发布线。每个工作包在**独立 git worktree + 临时功能分支**上开发
（`feat/0.5-<pkg>`），由 Lead 串行 merge 回 `0.5.x` 并复跑门禁。同一时刻**一个文件只有一个写者**。

## 1. 工作包

| 包 | 内容 | 依赖 | 主要写者文件 | 完成判据（自动化） |
| --- | --- | --- | --- | --- |
| **W0** 合同对齐 | 基线、取代关系、工作包/所有权、接口交接顺序、开发文档骨架 | 无 | `docs/development/0.5/**`、`AGENTS.md`、`MEMORY.md`、`TODO.md` | 文档可独立阅读；门禁仍绿 |
| **P1** 窗口壳 + keyed 实例 + 焦点 + 暂停策略 | `UiWindowKey`/`UiWindowCatalog`/`UiWindowOptions`；同型多实例；同 key 重开=激活；激活/关闭/生命周期；活动目标与键盘跟随；暂停/相机独立策略（不给产品默认值） | W0 | `Kernel/UiWindowHost.cs`、新增 `UiWindowKey/UiWindowCatalog/UiWindowOptions/UiFocusPolicy` | 新 lane：同型双实例共存、同 key 去重、关闭释放、选项独立生效 |
| **P2** 绑定通知 + 失效分类 + 命令态 + 条件显隐 | `IUiBindings` 通知/修订；批量合并；paint/measure/structure 分类；`canExecute` → 统一禁用交互；条件显隐与受支持结构更新；删掉全局 `ContentRevision` 的源码文本式护栏 | W0 | `Kernel/IUiBindings.cs`、`UiBindings.cs`、`UiSession.cs`、`UiLayoutEngine.cs`、`UiValueState.cs` | 新 lane：按 key 通知只失效相关元素；合并批；禁用不执行；显隐改结构不改无关状态 |
| **P3** 集合 + 公共控件 | keyed repeater（item 局部绑定作用域、节点复用、删除清理）；`input/checkbox`、`display/progress`、`container/tree`（只表达层级） | P2、P4 | `Kernel/UiLayoutEngine.cs`、`UiLayoutManifest.cs`、新增 `Widgets/*`、`AtomVocabulary.cs`、`KernelCoreWidgetRegistrar.cs` | 新 lane：排序不串状态、增删不累积、按 key 复用、删除清理；三个控件各自的交互/测量合同 |
| **P4** 文档来源 + 热重载 | 文件来源与依赖追踪；候选解析+校验；批次原子提交；last-known-good；手动重载；开发模式自动监听可配置；工作线程只发信号；稳定 key 状态保留 | W0 | `Kernel/UiHost.cs`、`UiLayoutManifest.cs`、`UiStyleDocument.cs`、新增 `UiDocumentSource/UiDocumentService/UiReloadReport` | 新 lane：改文件→已开窗口更新（桩内）；坏文件保 LKG；批内任一失败则整批不提交；手动重载可用 |
| **P5** 多 Host/Session 诊断隔离 | 每 host/session 诊断订阅与有界事件（reload/fit/recovery + 计时）；关闭即释放；两 host 并存不互相覆盖 | P1、P2、P4 | `Kernel/UiSession.cs`、`UiHost.cs`、`UiFitAudit.cs`、`UiHostLedger.cs`、`UiSessionGuard.cs`、新增 `UiDiagnosticHub/*` | 新 lane：两 host 并存事件归属正确；关闭一窗另一窗继续报告；额度/去重独立 |
| **P6** 验证与交付 | 独立验证（含反向突变）；中立夹具页；开发包；文档与实机清单 | P1–P5 | `docs/development/0.5/**`、`tools/.../Kernel*.cs`（验证 lane）、`KernelContainmentTests.cs` | 门禁全绿 + 每个 PASS 标明证据类与 mutation/regression 归属 |

## 2. 依赖图与集成顺序

```
W0 ──┬── P1 (窗口壳/keyed/焦点)
     ├── P2 (通知/失效/命令态/显隐) ──┐
     └── P4 (文档/热重载) ──────┬────┴── P3 (集合+控件)
                                 └──────── P5 (诊断隔离)
P1..P5 ── P6 (独立验证 → 开发包 → 文档)
```

固定合并顺序（Lead 串行执行，每步后跑 `verify-local.ps1`）：

1. `W0` → `0.5.x`（含 0.5.0 三轴版本）
2. `P1` → 3. `P2` → 4. `P4` → **第一个开发包 + 消费者交接说明**（最小可用接口尽早交付）
5. `P3` → 6. `P5` → 7. `P6` → 第二个开发包 + 交付文档定稿

## 3. 文件所有权（同一时刻单一写者）

| 文件/目录 | 写者 |
| --- | --- |
| `docs/development/0.5/**` | Lead |
| `AGENTS.md` / `MEMORY.md` / `TODO.md` / `docs/api-tiers.md`（最终裁决） | Lead |
| `tools/FerriteLib.UiKit.Tests/KernelContainmentTests.cs`（后端漏斗白名单） | Lead |
| `scripts/**` | Lead |
| `Kernel/UiWindowHost.cs` + 新增窗口类型 | P1 |
| `Kernel/IUiBindings.cs`/`UiBindings.cs`/`UiSession.cs`/`UiLayoutEngine.cs`/`UiValueState.cs` | P2（P3/P5 在该包 merge 后才可动同一文件） |
| `Kernel/UiHost.cs`/`UiLayoutManifest.cs`/`UiStyleDocument.cs` + 文档服务类型 | P4 |
| `Kernel/Widgets/**`（新增控件）、`AtomVocabulary.cs`、`KernelCoreWidgetRegistrar.cs` | P3 |
| 诊断类型 | P5 |

**共享但追加式**的三个文件（每个包都要加一行/几条）：`tools/.../Program.cs`（lane 注册）、
`docs/api-tiers.md`（公共类型分级）、`scripts/stub-coverage-exemptions.txt`（仅在确有必要时）。
各包在自己分支里就地改，Lead 在串行 merge 时解冲突并复跑门禁。
约束（`MEMORY.md` 教训）：**公共类型、tier 行、lane 注册必须在同一个提交里**；
`dfba792` 曾把 491 行 lane 提交了但没注册，门禁全绿而断言从未运行。

## 4. 当前状态

| 包 | 状态 | 说明 |
| --- | --- | --- |
| W0 | **已完成** | `99acc5f`：本目录 + `0.5.x` 分支（已推送）+ 三轴 0.5.0；基线门禁全绿 |
| P1 | **已合并**（`2b37d3d` → merge） | keyed 实例/焦点/通用 XML 页壳；91 断言；**7 处突变证明**（含同型多开与 vanilla 精确类型规则）；stub 增补已披露 |
| P2 | **已合并**（`369400f` → merge `0ee55b6`） | 按 key 通知/失效分类/命令态/条件显隐 + P2b 节点剪枝；6 处突变转红 + 1 处诚实负结果；3 个新 lane |
| P4 | **已合并**（`bd2e04c` → `85faab0`；P4b `079aa5e` → `6e0781a`） | 文档服务 + 原子热重载 + LKG；lane 103 断言；P4 三处突变 + P4b 四处突变均转红；AutoWatch 重武装与 style 批次跨 host 原子性已补 |
| P3 | **进行中**（`collections`） | 依赖已清（P2/P4 合并），分支 `feat/0.5-collections` |
| P5 | **进行中**（`diagnostics`） | 依赖已清（P1/P2/P4 合并），分支 `feat/0.5-diagnostics` |
| P6 | 进行中（Lead + `verifier`） | wave-A 审计已合并（`8d4bdb0`）并处置 6 条发现（`8098ca0`）；P4 逆验证已合并并派生 task-8；死 lane 守卫已入库（`b6b621d`）；P1 逆验证待做 |

状态只在 Lead 合并后更新（单一状态源原则）；各包分支内部进度写在提交信息与任务回交里。
## 5. 提交纪律（本轮实测教训）

**本环境的 agent 运行时会覆盖 git 的 author/committer 身份。** 主检出与每个 worktree 的 `user.name`/`user.email` 都是
正确的单账号 noreply 值，但子 agent 提交后落到的是 `<agent> <agent@local>`——环境变量优先级高于 config。
`scripts/privacy-audit.ps1` 会以 `identity: non-noreply author/committer address(es)` 判失败。

因此：**每一次提交都必须把四个身份变量放在同一个 pwsh 命令里**：

```powershell
$env:GIT_AUTHOR_NAME='Coahuilite'
$env:GIT_AUTHOR_EMAIL='19252128+Coahuilite@users.noreply.github.com'
$env:GIT_COMMITTER_NAME='Coahuilite'
$env:GIT_COMMITTER_EMAIL='19252128+Coahuilite@users.noreply.github.com'
git commit -m "message"
```

`git -c user.name=...` **不够**（config 低于环境）。提交后以
`git log --format='%h | %an <%ae> | %cn <%ce>'` 自查。

配套两条流程规则：

1. 隐私审计必须**作为 push 的前置条件**，不能用 `;` 与 push 串在同一条命令里——本轮第一次 push 就是这样把
   一个非 noreply 身份送上了远端，随后只能改写并 force-push（仅 `0.5.x`，无 tag/无 release/无消费者，代价只是记录）。
2. 审计扫描 `--all` refs，因此**未推送的队友分支上的坏身份同样会让整个仓库审计失败**，必须在 push 前修掉，
   不能留到 merge 时。改写范围 `99acc5f..8098ca0`，改写前内容与改写后逐字节相同
   （`git diff --stat 766b6cd 8098ca0` 为空）。

### 4.1 独立验证派生的后续项

| 任务 | 来源 | 状态 |
| --- | --- | --- |
| task-8 P4b | P4 逆验证：AutoWatch 无法重新武装（must-fix）、style 批次未跨 host 预校验（must-fix）、主题基线重置（待裁定文档化或修正） | **已完成并合并**（`079aa5e` → `6e0781a`）；三项全部修复，主题基线为「门控修复 + 明确残余」 |
| task-9 P2b | P4 逆验证：被删除元素的 node 未从 session 表移除、`GetNodeByElementId` 的无 `IsArranged` 过滤与自身注释矛盾 | **已完成并合并进 P2**；选择保留「按身份而非按排布」的契约并改注释，未加 `IsArranged` 过滤（过滤会隐藏隐藏元素的草稿） |
| task-10 P1 hardening | P1 逆验证：stub 只用一个窗口类，`AllowMultipleInstances` 的「精确类型 vs 可赋值」半边未钉；M1 去重突变靠未捕获异常变红而非命名断言 | 已派发（`windowing`） |
| 死 lane 守卫 | P6 自查：仓库被"lane 存在但从未注册"伤过两次且无门禁 | 已入库并突变证明（\`b6b621d\`） |
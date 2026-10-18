# FerriteLib UiKit — 2026-09-25 消费者影响记录

本报告原用于 NivariansGrandStructure（NGS）判断旧 UI 是否需要重写。它是 **2026-09-25 的只读源码、
历史和既有产物检查**，没有运行构建、测试、打包、变异或游戏。2026-10-18 整理时，将重复的当前 API 清单、
迁移表和执行步骤收回各自正式入口，保留此处独有的比较范围与产物观察。

现在接入库请读 [0.7 消费指南](consumers/consume-from-0.7.0.md)；当前源码地图见
[architecture.md](architecture.md)，后续设计见 [0.8 首版规格](development/0.8/README.md)。

## 1. 当时比较的是什么

- 起点：0.7 开线提交 `8fe3026`（2026-09-17，三条版本轴均为 0.7.0），不是所谓“首个 0.7 RC”。
- 首次观察的 HEAD：`1c05b0f3d95a285dada7d113cffc6344e1de71a6`；交付前只读复核时为 `41ad08d`。
- 当时本地没有 0.7 tag；远程发布状态没有取证。不能由本地 tag 清单证明远程不存在 Release。
- 消费者区间 `[0.7.0,0.8.0)` 在协调开发期间没有随着每次新增变化；因此同一 API 数字不能替代具体产物身份。

## 2. 这次比较的有效结论

当时的源码比较记录：注册 kind 从 16 到 17，新增 `input/text-field`；新增 `UiOption` 和带身份的
`UiNative.TextField` 重载，以及 placement、help、payload、selected、width、match-content 等词汇。
这些数字只描述上述比较区间。结构容器与注册 kind 是不同集合，`Repeat` 又由引擎接管，不能据此推断
当前全部控件、导出类型或真实消费者覆盖数。

对消费者最有用的能力组合是：测量驱动的整行命中带、重复行的载荷与选中态、单行编辑和帮助身份、
样式/密度数据化、响应式布局与 Dev 几何诊断。这是功能选择建议，不是 NGS 已经接入或完成游戏验收的证据。

迁移实质包括容器间距缺省变化、作者 tone 与状态分离、`HoverPoint` 移除、Auto 宽度和创建期校验修正，
以及模板 `SelectedKey` 的 item scope。删除元素释放状态是 0.4 → 0.5 已引入的合同，并非 0.7 新增。
详细规则集中在 [0.7 合同](development/0.7/05-api-contract.md) 和消费指南，不在此复制第二份。

## 3. 原报告中不应继续沿用的说法

- “0.7 从未被任何消费者编译”在报告当时也已过时；继续留在 0.7 的原因是显式的协调开发裁定，不是无人使用。
- 两套产品 palette、`new UiTheme()` 空 bag 是较早的决定，已被 R2 的 Vanilla 部分覆盖包替代。
- 漏掉 packageId 和 `out diagnostic` 的双参数 `Require` 示例不是可用调用；当前指南已给出四参数返回值处理。
- 原版概要称“4 条破坏”只指最初四类，后面的详细表还包含布局/校验修复和既有迁移，不能把 4 当完整计数。
- 报告中的几何数字是源码/合同常量，不是实时测量；历史覆盖率不能外推到其他页面或修订。
- 当时没有真实游戏证据不等于此后永远没有。限定的 2026-10-09 观察见
  [0.7 验证记录](development/0.7/40-verification.md)。

## 4. 留下的取证限制

本次历史检查没有验证实时游戏、多模组共存、NGS 编译或远程 tip；没有运行该 HEAD 的完整门禁。
`UiFitAudit.Enabled` 的跨模组影响当时只是风险，不能写成已复现故障。类型层级仍以
[api-tiers.md](api-tiers.md) 为准，旧引证失效不等于已经内部化。

## 5. 为什么保留下面的身份表

这两次读取得出一个可复核的结果：源码 HEAD、配置隔离的构建输出、根兼容 carrier 和已暂存 Dev 包
并不是同一对象。表保留原日期、提交、大小、mtime 和已记录的 SHA-256，以定位当时被比较的字节；
本次整理没有重新计算它们，也不把它们作为今天的安装目标。
## 6. 历史产物身份观察（2026-09-25，未重新测量）

- **HEAD**：`1c05b0f3d95a285dada7d113cffc6344e1de71a6`，日期 **2026-09-24 15:35:44 +0800**，标题
  `docs(memory): 'derivable' means rebuildable, and only three of eleven cases have it by another name (0.7.x, task-19)`；工作树干净（`git status --porcelain` 为空）。

| 产物 | 字节 | mtime | SHA-256 | 内嵌 ProductVersion | 与 HEAD 同步？ |
| --- | --- | --- | --- | --- | --- |
| `ferritelib/1.6/Assemblies/FerriteLib.UiKit.dll`（根交付 carrier，Release） | 253,440 | 2026-09-23 01:24:37 | `416BE3C1BDA44055BAF10EB0C5722E61533DBC30DEA616F9529894B9D84ECC79` | `0.7.0-dev+9938121f8b36...` | **否**：内嵌 `9938121`，且缺 `07f3c40` 对 `UiDevGeometryProbe/UiHost/UiNative/csproj` 的 Source 改动（`git diff --stat 9938121..HEAD -- Source` 非空） |
| `ferritelib/dist/build/Release/FerriteLib.UiKit.dll` | 253,440 | 2026-09-24 15:26:26 | `8A1A98ADB253D6A7381FE529F136504CC31401972BFFEE8E47C8639C2479D037` | `0.7.0-dev+e7f221521592...` | **代码同步、提交未同步**：`git diff --stat e7f2215..HEAD -- Source` 为空，但内嵌提交是 `e7f2215`（HEAD 之后只有 docs/tools 提交）；要内嵌 HEAD 需重建 |
| `ferritelib/dist/build/Dev/FerriteLib.UiKit.dll` | 281,088 | 2026-09-24 15:26:32 | `0F50D58969D9109274737CFCD943C0E7276DFA37A9C1C762AF346E46245BA8BE` | `0.7.0-dev+e7f2215...` | 同上（Dev 配置，含 `FER_DEV` 诊断） |
| `ferritelib/dist/dev/FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll`（Dev 包） | 281,088 | 2026-09-24 11:53:24 | `4729E2758929BBA52906828110E5F7980A0C77AC19CCF965AE3AC65E83B4956A` | `0.7.0-dev+07f3c401e3b9...` | **否**：内嵌 `07f3c40`，比 HEAD 旧；其 `version.txt` 自述 `commit=07f3c401e3b9 / payload-sha256=4729E2...`；`About/About.xml` mtime 2026-09-17 |

- 根 carrier 是兼容交付路径（不是已发布 Release 的证明）（消费者 `<HintPath>` 的常见目标），**当前是 stale 的**；`docs/consumers/consume-from-0.7.0.md:18-19` 里引用的哈希 `271128299A9CFF…FC8F82F` 与以上任何一份都**不匹配**，文档自己也注明那是「一次工作树的 rehearsal identity，作为历史引用」。
- 说明：本次**没有**运行任何构建，所以无法给出「在 HEAD 重建后」的新哈希；上表是**现有磁盘产物**的事实。
- 结论：**源码 HEAD ≠ 任何已构建 DLL 的内嵌提交**；`dist/build/*` 的**代码**与 HEAD 一致（仅 docs/tools 提交在其后），根 carrier 与 `dist/dev` 的**代码也落后**。这是该次取证对旧 carrier 的结论；不构成现在重建或导出的指令。

### 6.1 身份复核（2026-09-25 22:26，只读、同一方法）

§6 是**取证时刻（21:40）**的磁盘事实；此后工作区又动了三个提交（T29：`312f9b7` 22:12:35 / `ebb1a76` 22:15:35 / `41ad08d` 22:18:33）并做了一次构建（22:15:36/43）。交付前按同一只读方法复测——`Get-FileHash` + PE 版本资源 `FileVersionInfo.ProductVersion`，**不加载程序集**（与 `scripts/export-carrier.ps1` 打印身份的方式一致）：

| 产物 | 字节 | mtime | SHA-256 | 内嵌 ProductVersion | 相对 §6 |
| --- | --- | --- | --- | --- | --- |
| `1.6/Assemblies/FerriteLib.UiKit.dll`（根交付 carrier，Release） | 253,440 | 2026-09-23 01:24:37 | `416BE3C1BDA44055BAF10EB0C5722E61533DBC30DEA616F9529894B9D84ECC79` | `0.7.0-dev+9938121f8b36…` | **未变** |
| `dist/build/Release/FerriteLib.UiKit.dll` | 253,440 | 2026-09-25 22:15:36 | `1738EC4AC45457212CF853E6C1134F027F0108145B13CED7C3241C0E9F30420A` | `0.7.0-dev+ebb1a7699a60…` | **已变**（原 `8A1A98AD…` / `e7f2215`） |
| `dist/build/Dev/FerriteLib.UiKit.dll` | 281,088 | 2026-09-25 22:15:43 | `F7A1BBFA0D504885234E0940DB4BBA0814B064E6F5BD465D507D4A964ACF6889` | `0.7.0-dev+ebb1a7699a60…` | **已变**（原 `0F50D589…` / `e7f2215`） |
| `dist/dev/FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll`（Dev 包） | 281,088 | 2026-09-24 11:53:24 | `4729E2758929BBA52906828110E5F7980A0C77AC19CCF965AE3AC65E83B4956A` | `0.7.0-dev+07f3c401e3b9…` | **未变**（`version.txt` 自述 `commit=07f3c401e3b9`） |

- **HEAD** 现为 `41ad08d`（2026-09-25 22:18:33）。`git diff --stat e7f2215..HEAD -- Source` 仍为**空**，`git diff --stat 9938121..HEAD -- Source` 仍为**非空（4 文件）** ⇒ §6 的两条判断（`dist/build/*` 的代码与 HEAD 一致；根 carrier 缺 `07f3c40` 的 Source 改动）**都仍然成立**。
- §6 那句「工作树干净」在交付时**已不成立**：本文件当时未被跟踪（即本报告本身）。

**证据类别**：本报告**没有任何运行时测量**——它没有构建、测试、打包或变异。
- 字节 / SHA-256 / mtime 是**交付时刻磁盘上的事实，事后直接读取**；本报告**不主张**这些字节与任何构建/运行时刻相同（那需要 before/after 之类的单调量，本报告没有）。
- 全部 `px` 数字（如 `section/header` 的 1px 分隔线、placement 的像素微调）都是**源码 / 契约里声明的值（约定）**，**不是实测布局几何**；行高类数字若出现，其约定值以实测校准表为准。
- 凡源文档标注 UNRUN、或本机未取证的项，一律按 UNRUN / 未取证照写，不升级。

---

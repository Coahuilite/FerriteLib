# T3 目录描述（catalog）— lane 对照与证据状态

实现：`Source/FerriteLib.UiKit/Kernel/UiWidgetCatalog.cs`、`UiWidgetDescriptor.cs`、
`UiWidgetRegistry.cs`（仅新增 internal 访问器，公开注册面未改签名与行为）。
lane：`tools/FerriteLib.UiKit.Tests/KernelWidgetCatalogTests.cs`（`KernelWidgetCatalogTests.RunAll`，
47 条断言），在 `Program.cs` 的 T3 连续注释块内注册（`KernelLaneRegistrationTests` 会因未注册而红）。
复跑：`pwsh -NoProfile -File scripts/verify-local.ps1`。

**证据等级：已实现 + 已自动化验证。** 没有任何实机或真实消费者计数；T4 的演示面板如何渲染本描述不在本文件范围。

> **声明边界（declared boundary — pre-existing behaviour, NOT a defect）**
>
> `Register` 会在注册表锁内复制调用方传入的 schema/label 集合；`Resolve` 会在锁内调用工厂。
> **Lead 裁决（task-26 回交时）：两者保持不变**——本轮指令明确不扩大公开契约，且要求的不变量窄于
> "任何锁内都不得有消费者代码"，而是：**后台/列举路径不得在锁内暴露可写注册表、工厂或消费者回调**。
> T3 的 lane 证明的正是这一条（每条 listing 路径既不枚举传入集合、也不调工厂）。
> 因此 **T5 读者请把它作为边界记录一次现状，不要为这两条种突变、也不要要求本包修复**；
> 若判断该边界有误，请在报告里写明不同意见和依据，而不是在该处种一个注定红的突变。

## 逐项对照

| # | 要求 | lane（Run 名） | 断言要点 | 突变证明 |
| --- | --- | --- | --- | --- |
| 1 | Snapshot 列出每个已声明 (scope,kind)，同名跨 scope，scope→kind 序 | `Snapshot lists every declared pair, scope-major and ordinal` | 精确等于 `cat-a/alpha, cat-a/zeta, cat-b/alpha, cat-b/gamma, core/probe-core`；两个 scope 各有一个 `alpha`；属性/标签集合按 ordinal 排序 | M6（去掉声明排序→字典序）红 4 条；M9（去掉属性集合排序→注册序）红 1 条；M1（跳过无 schema 的 kind）红 4 条 |
| 2 | TryGet 精确配对，无 core 回退 | `TryGet answers the exact declared pair and never the core fallback` | 精确命中并回报声明 scope；另一 scope 同名 kind 为 false；**同一断言里对照** `GetAttributeSchema` 对 core 会回退而非 null；null/空输入为 false（不是参数异常） | M2（TryGet 加 core 回退）红 1 条 |
| 3 | Scopes() 完整、ordinal、无空 scope | `Scopes() is complete, ordinal and free of empty scopes` | 精确等于 `cat-a, cat-b, core`；每次新副本；用反射把空 kind 的 scope 注入私有表后**仍不出现**；Clear 后两表皆空 | M3（返回取自表 key）红 1 条 |
| 4 | 隔离：改传入集合/改返回值都不能动注册表或下一次 Snapshot | `Snapshot and its descriptors are copies, not registry state` | 每次调用新 list、每个描述符新集合；`IList` 强转后 Add 抛 `NotSupportedException`；注册后再改调用方的 List 不动描述符与注册表；直接 new 的描述符同样持副本 | M5（描述符不复制、直接持有传入集合）红 4 条；M7（进程级缓存 Snapshot）红 8 条 |
| 5 | 列目录零工厂调用（计数工厂 + 抛异常工厂） | `Listing invokes no factory, even one that throws` | 3 轮 Snapshot/TryGet/Scopes 后计数工厂仍为 0、抛异常工厂为 0；抛异常工厂的 kind 仍被完整描述（含 schema）；随后**显式 Resolve** 证明该工厂确实会抛（前提可证） | M4（`Describe` 里 Resolve 一下）红 2 条 |
| 6 | 未声明 schema/label 仍列出 | `A kind with no declared schema or label set still lists` | `HasAttributeSchema==false`、`HasLabelSet==false` 且集合为空；`GetAttributeSchema` 同时返回 null；只声明一半的情况各自正确；**声明了空集合**仍算声明（`true` + 空集合） | M1（跳过无 schema 的 kind）红 4 条 |
| 7 | 晚注册：下一次 Snapshot 可见，无重启无缓存 | `A kind registered after a snapshot appears in the next one` | 旧 Snapshot 对象保持原样（是记录不是视图）；新 Snapshot 多一项且能 TryGet；scope 列表同步；第二次晚注册同样可见 | M7（进程级缓存）红 8 条 |
| 8 | 列举路径在锁持有期间不跑消费者代码（声明边界见文首：注册/解析两条既有路径仍持锁） | `Listing runs no consumer code while the registry lock is held` | 用调用方提供的 schema 集合（其 `GetEnumerator` 记录 `Monitor.IsEntered(Gate)`）证明：所有 listing 调用**一次都没有枚举它**，且计数工厂为 0 | M8（注册表直接保存调用方集合而不复制→listing 会枚举消费者集合）红 2 条 |

## 本 lane 明确**不**钉住的东西

- **锁边界的两处既有路径 = 声明边界，不是缺陷**：`Register` 在锁内用调用方传入的 schema/label 集合构造
  `HashSet`（lane 输出里 `note:` 记录为 `lockHeld=True`），`Resolve` 在锁内调用工厂。两者都是**本包不拥有的
  既有公开行为**，Lead 已裁定保持不变（见文首声明边界）；把注册路径也移出锁属于公开面行为变更，不在本轮授权内。
  本 lane 证明的是**每条 listing 路径**（Snapshot/TryGet/Scopes）完全不跑消费者代码（不枚举传入集合、不调工厂），
  这正是计划要求的那条窄不变量。
- **并发压力**：没有多线程 Register/Snapshot 交错压测；"锁纪律"来自代码阅读与单线程可观察量，不来自竞态复现。
- **渲染形态**：描述符是数据，能不能渲染成目录 UI 由 T4 的演示面板决定；本 lane 不画任何东西。
- **`TryGet` 返回 false 时的 out 值**：是 `default(UiWidgetDescriptor)`（结构体默认值绕过构造，集合为 null）。
  调用方必须看返回值；lane 未断言默认值可直接读集合（**这是记录在案的边界**，不是遗漏）。
- **属性顺序**：冻结契约没有规定集合顺序；本实现选择 ordinal 排序（lane 钉住），这是可再议的实现细节而非契约承诺。

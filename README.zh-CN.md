# FerriteLib

[English](./README.md) | [**中文**](./README.zh-CN.md)

RimWorld 1.6 的**无内容前置模组**。FerriteLib 只携带一个程序集——`FerriteLib.UiKit.dll`——供 Coahuilite
模组编译与运行时绑定。它不添加任何 Def、补丁、语言、贴图：单独启用它对游戏零影响。如果你的模组列表里
出现了它而没有任何依赖它的模组，说明消费者已被卸载或禁用；只有在没有已启用模组依赖它时，才可以禁用本模组。

## 库提供什么

两层，刻意分离：

1. **声明式页面引擎**——XML 清单、受约束布局、类型化绑定、按窗口会话、控件注册表、创建期契约校验、
   逐控件失败恢复，以及一个自带 chrome（背景、标题、关闭按钮）的窗口外壳，让消费者无需跳出树就能开页。
   消费者代码只写业务逻辑与绑定，结构活在 XML 里。
2. **视觉核心**——主题令牌、绘制助手、文本测量、文本适配审计与版本契约。**不采用页面模型也能单独使用。**

两条边界都是门禁，不是口头约定：视觉核心文件不得提及任何页面模型类型；对游戏即时模式界面的每次调用
都必须落在五个指定漏斗文件之内。树外的原生 IMGUI 并非被禁止，而是不被支持、也不被度量——它对 harness、
适配审计与会话恢复三者同时不可见，这正是使用页面树的理由。

## 依赖要求

- RimWorld 1.6
- 消费者需同时启用本模组与其自身模组；缺本模组时游戏会给出警告。**不要**把 `FerriteLib.UiKit.dll`
  复制进其他模组的目录——同一程序集只允许一个载体：两份副本会经由游戏唯一的全局 `AssemblyResolve`
  按加载序绑定，落败的一方毫无察觉。

## 身份

- packageId：`coahuilite.ferritelib`
- 命名空间根：`FerriteLib.UiKit`（内核表面：`FerriteLib.UiKit.Kernel`）
- 日志前缀：`[FerriteLib.UiKit]`
- 许可证：MPL-2.0，全文随每个包分发（包内 `LICENSE`）

## 给模组开发者

RimWorld 的 `modDependencies` 无法表达版本，消费者必须在自己的构造函数里断言 API 区间——
`FerriteLibVersion.Require(min, max, packageId, out report)` 会枚举已加载载体并报告冲突，失败信息可读。
编译请针对本仓库 GitHub Release 资产中的 DLL（发布页正文标注其 SHA-256）；程序集目标 `net472`。
公共 API 处于 **1.0 之前、暂定状态**：minor 递增即破坏性变更，冻结决定以第二个接入消费者为门槛。

**第三方使用已被邀请（维护者裁决 2026-09-10）。** 请针对 Release 资产编译；当你被迫手写了本库本该提供的
东西时开一个 issue——指向你自己可用代码的引用就是这个库的成长方式，只有请求不算。缺陷报告始终欢迎。
「不会破坏什么」是按类型承诺、不是按版本承诺：`docs/api-tiers.md` 把每个导出类型标成 **stable** 或
**public-unstable**，只有 stable 类型在你编译时所用的 `[min, max)` 区间内保持签名不变，其余类型每个 minor
都要重新编译。API 暂定期间 PR 不承诺合并，`CONTRIBUTING.md` 等表面冻结时再写。

## 本地验证与构建

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1            # 门禁套件（harness + 双 flavor 构建 + 载荷 + 无内容 + 许可 + 身份）
pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev   # + 暂存的 dev 目录 dist/dev/FerriteLib（不产出归档；放哪个 Mods 目录自己动手）
pwsh -NoProfile -File scripts/privacy-audit.ps1 -FullHistory   # 三向量隐私门禁，任何 push 前执行
```

## 文档索引

- 协议 / 不变量：`AGENTS.md` · 已核事实：`MEMORY.md` · 行动面：`TODO.md`
- 哪些类型承诺不破坏：`docs/api-tiers.md` · 只有你能跑的实机走查清单：`docs/in-game-walkthrough.md`
- 发布流程与 rc 方案：`.github/workflows/release.yml` 头注释（tag 方言即契约：`vBASE-rcN` 试版，
  裸 `vBASE` 必须落在最后一个 rc 的同一提交上）

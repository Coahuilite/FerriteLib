# FerriteLib

[English](./README.md) | **中文**

FerriteLib 是 RimWorld 的**无内容前置模组**，只携带一个程序集 `FerriteLib.UiKit.dll`：一套给模组作者用的界面工具包。
它不添加任何 Def、补丁、语言或贴图，单独启用不会改变游戏。

## 当前状态

| 项目 | 状态 |
| --- | --- |
| 开发线 | 0.9.0，API 区间 `[0.9.0, 0.10.0)`；见[当前工具合同](docs/development/0.9/tools.md) |
| 已发布 | [v0.8.0-rc1](https://github.com/Coahuilite/FerriteLib/releases/tag/v0.8.0-rc1)（GitHub 预发布，非 latest） |
| 本地候选 | 0.9 正常单载体，尚未发布；0.8 维护候选单独保留 |
| RimWorld 1.6 | 0.8 维护／恢复线；r6 仍待原生验收 |
| RimWorld 1.7 | 0.9 net10.0 已对本机 1.7.4998 rev1299 编译完整库及公共 Window／Settings 作者路径；native 未验证 |

逐项证据与已知偏差不写在 README，见[实现与开放偏差](docs/openspec/implementation.md)。

## 给玩家

- 只有装了依赖它的模组（例如 Universal Squeaker）时才需要它，并排在这些模组之前。
- **只装一份。**不要把 `FerriteLib.UiKit.dll` 复制进其他模组目录：两份副本会按加载顺序抢同一个程序集名，输的一方无法察觉。
- 列表里有它、却没有任何已启用模组依赖它时，可以安全禁用。

## 它是怎么工作的

一句话：**一套不依赖游戏的界面核心（Kernel），加一层 RimWorld 适配（Adapter），打包在同一个 DLL 里。**
核心保存界面结构、局部状态和几何；画字、输入、窗口仍交给游戏原生系统执行；
业务数据、保存以及“这个请求是否被接受”始终归使用它的模组。

### 主线：五个角色

```text
作者的模组 ─── 业务模型（值、资格、保存）
   │  经绑定读取值、发出请求
   ▼
UiHost ──────── 一个页面的组合入口：采用定义、同步展示、驱动布局与绘制、关闭
   ├─ UiSession / UiNode ── 局部界面状态：节点身份、草稿、焦点、弹层、滚动
   ├─ UiBindings ────────── 连接作者模型：读值、写请求、命令；响应式可选
   ├─ LayoutEngine ──────── 测量与排列、增量复用、几何结果
   └─ Widgets / Controls ── 默认控件：先处理合格输入，再绘制
          │
          ▼  中立端口：IUiExecution / IUiDrawing / ITextMetrics
公开 Verse Adapter（1.7 目标）── 真实事件、窗口、字体度量、原生编辑与 IME、绘制
```

| 角色 | 拥有 | 不拥有 |
| --- | --- | --- |
| UiHost | 页面定义、布局引擎、展示连接、关闭 | 业务模型 |
| UiSession / UiNode | 节点身份、草稿、交互、弹层、滚动、帮助状态 | 游戏对象寿命、配置保存 |
| UiBindings | 读值和发出写请求的通道 | 写入是否被业务接受 |
| LayoutEngine | 尺寸、位置、裁剪、命中几何 | 应用自己的断点政策（如窄屏切单栏） |
| Adapter | 原生事件、窗口、字体、IME、纹理引用 | 中立核心逻辑 |

### 一帧里发生了什么

1. 原生窗口回调触发，adapter 捕获本次事件，形成 `UiFrame`。
2. `UiHost.DrawFrame`：同步展示数据 → 测量与排列 → 按当前布局逐个控件处理输入并绘制。
3. 控件确认一次合格操作后，经绑定向作者模型发出请求；正常返回或抛错都如实保留，不自动重试。

两个常见误读：名为 Draw 的调用里也会处理输入；`DrawFrame` 是同步调用，不是游戏 tick 或动画时钟，同一显示帧可能被调用多次。

### 不必整套采用

| 你需要 | 用哪部分 |
| --- | --- |
| 完整的设置页或窗口 | XML 或 C# 定义 ＋ 绑定 ＋ `UiHost`；`UiPageWindow` 提供窗口外壳 |
| 嵌入自己的原生窗口 | 自己的宿主 ＋ `UiHost.DrawFrame` |
| 单个控件 | `UiSession.CreateContext` ＋ Controls |
| 只要交互语义 | `UiInteractionSession` 等中立状态 |
| 只要视觉 | `UiThemeDraw` ＋ `IUiDrawing` ＋ `ITextMetrics` |

### 源码地图

| 位置 | 内容 |
| --- | --- |
| `Source/FerriteLib.UiKit/Kernel/` | 整个中立核心，不引用游戏：定义与编译、绑定、Host／Session／Node、布局、外观、诊断与恢复 |
| `Kernel/Neutral/` | 最底层的纯逻辑：布局数学、交互与编辑状态机、响应式运行时、调色板 |
| `Kernel/Widgets/` | 默认控件：Button、Checkbox、Dropdown、Slider、TextField、WrappedText 等 |
| `Adapters/RimWorld16/` | 同源公开 Verse 适配，亦用于 1.7 编译：窗口宿主、`VerseUiExecution`、字体度量、时钟与主线程 |
| `NeutralSources.props` | 把整个 Kernel 同时编进运行时 DLL 和无平台合同测试 |
| `tools/` | 核心与 adapter 合同测试、作者示例 |

更细的说明见[架构](docs/architecture.md)和[现行规格](docs/specification.md)中的组合图。

## 给模组作者

- **在自己的启动代码里断言版本区间：**`FerriteLibVersion.Require(min, max, packageId, out report)`。RimWorld 的 `modDependencies` 无法表达版本；它会枚举已加载载体、报告冲突，并给出可读的失败信息。
- 0.9 针对提供的固定 1.7 DLL 编译，目标 `net10.0`；已发布 0.8 资产仍是 `net472` 维护输入。不要随你的模组复制这个 DLL。
- 只有 [API 分层](docs/api-tiers.md)中标为 **stable** 的类型在你编译时的 `[min, max)` 区间内保持签名；**public-unstable** 类型每个 minor 都需要重新编译。发布后破坏 stable 合同须升 major，弃用后至少保留两个后续已发布 minor。
- 起步顺序：[普通设置页配方](docs/consumers/ordinary-settings.md) → [公开作者路径 S1–S7](docs/openspec/specs/policies/public-authoring/spec.md) → [窗口布局指南](docs/consumers/rimworld-window-layout.md) → [0.9 连接／目标指南](docs/development/0.9/tools.md)。
- 欢迎第三方使用。当你被迫手写了本该由库提供的东西，请附上你自己可运行代码的引用开 issue；缺陷报告随时欢迎。

## 给贡献者

0.9 目标命令见当前连接指南；以下保留的维护校验／打包命令不生成 0.9 的 1.7 交付。

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1                  # 门禁套件：测试、双配置构建、载荷、无内容、许可、身份
pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev         # 另外暂存 dist/dev/FerriteLib（不产生归档）
pwsh -NoProfile -File scripts/privacy-audit.ps1 -FullHistory    # 完整历史隐私门禁，任何 push 之前执行
```

| 文档 | 用途 |
| --- | --- |
| [docs/specification.md](docs/specification.md) | 现行规格入口，行为保证的唯一权威 |
| [docs/openspec/specs/](docs/openspec/specs/) | 按能力拆分的规格正文 |
| [docs/openspec/implementation.md](docs/openspec/implementation.md) | 源码与规格的对应及已知偏差 |
| [docs/api-tiers.md](docs/api-tiers.md) | 公开类型的稳定等级 |
| [docs/build-and-debug.md](docs/build-and-debug.md) | 明确的 0.9 构建目标、诊断与旧维护机制 |
| [docs/in-game-walkthrough.md](docs/in-game-walkthrough.md) | 只能在游戏里完成的检查 |
| [docs/release-notes/](docs/release-notes/) | 各版本发布说明 |
| `AGENTS.md`、`MEMORY.md`、`TODO.md` | 开发代理的工作规则、持久事实与待办，不是用户文档 |

发布流程与 tag 规则见 `.github/workflows/release.yml` 头部注释：`vBASE-rcN` 为试版，正式版 `vBASE` 必须落在最后一个 rc 的同一提交上。

## 身份

- packageId：`coahuilite.ferritelib`
- 命名空间根：`FerriteLib.UiKit`（内核：`FerriteLib.UiKit.Kernel`）
- 日志前缀：`[FerriteLib.UiKit]`
- 许可：[MPL-2.0](LICENSE)，全文随每个包分发

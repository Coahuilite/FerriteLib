# 30 消费者团队接入说明（0.5.x）

本节面向消费者团队。**库侧提供接口与开发包；消费者的页面迁移与真实页面验收由消费者团队完成。**
库内示例与夹具不构成消费者验证。

## 1. 版本区间

| 项 | 值 |
| --- | --- |
| 本轮契约轴 | `0.5.0`（`FerriteLibVersion.Api`） |
| 建议消费者 pin | `[0.5.0, 0.6.0)` |
| 发布通道 | 消费者经已发布的 GitHub Release 资产集成；同目录兄弟文件夹的 `Private=false` 是开发便利，不是契约 |
| 单 DLL carrier | 只有本 Mod 交付 `FerriteLib.UiKit.dll`；消费者不得自带副本 |

游戏无法表达前置版本区间（`ModRequirement` 只解析 packageId/displayName），
所以**每个消费者必须在自己构造函数里断言区间**：

```csharp
FerriteLibVersion.Require(new Version(0, 5, 0), new Version(0, 6, 0), "your.packageId", out string diagnostic);
```

不通过时 `diagnostic` 会给出 `MISMATCH`、载入的 `Api` 与消费者编译时的下界——
这是可读的前置错误，而不是首次绘制时的 `TypeLoadException`。

## 2. 接入步骤

1. 引用 `1.6/Assemblies/FerriteLib.UiKit.dll`（`<Private>false</Private>`）。
2. `About.xml` 声明 `modDependencies` + `loadAfter`（版本区间只能在代码里断言）。
3. 构造函数里 `Require(...)`，失败则禁用自身 UI 并给出可读提示。
4. 页面：布局 XML + typed bindings；自定义组合件经 `UiWidgetRegistry.Register`。
5. 热重载：开发模式下由库的文档服务监听；生产默认关闭自动监听（可显式开启），手动重载始终可用。

## 3. 从旧线迁移（0.3/0.4 → 0.5）

按顺序检查，每条都对应 `docs/api-tiers.md` 的对应章节：

1. **session 状态按 node 键**（0.4 窗口内变更）：`GetScrollPosition`/`SetScrollPosition` 收 `UiNode`。
   用 `Session.GetNodeByElementId("your-id")` 做桥接。`TrippedNodes` 取代 `TrippedComponentIds`。
2. **窗口壳**：旧代码若自己管理"同类窗口去重"，迁移到 `UiWindowCatalog`（P1），否则同型多实例会被原版
   `RemoveWindowsOfType` 互相关闭。
3. **刷新**：若你在每次绘制里重建树或手动 bump 全局修订，迁移到按 key 通知（P2）。
   固定尺寸读数不应引起整页测量。
4. **禁用**：手写的"不可用就不画按钮"迁移到 `canExecute`（P2），禁用态由库统一处理，且不接收输入。
5. **集合**：手写的行集合迁移到 `Repeat`（P3）；提供稳定业务 key，否则排序会串状态。
6. **热重载**：内嵌 manifest 可继续使用；要获得"改文件即生效"，把布局/样式改为外部文件并交给文档服务（P4）。

## 4. 已知限制与边界（诚实清单）

- **C# kind 不热重载。** kind 是编译期词汇；改 kind 行为必须重新编译并由消费者重新发布。
- **原版职责不重复。** 跨窗口调度、层级、焦点、生命周期归 `Verse.Window`/`WindowStack`；
  库只补实例身份、页面生命周期、动态结构、绑定通知与诊断隔离。
- **暂停/相机是全局语义。** `WindowsForcePause`/`WindowsPreventCameraMotion` 对**全部**窗口取任一为真，
  不是窗口局部开关；多窗组合必须由消费者按产品策略选择，库只暴露配置。
- **地图层渲染不在库内**（永久非目标）。
- **实机行为未由本仓证明。** 激活/层级/输入穿透/模态共存等组合行为是 IL 级事实推导 + 自动化断言，
  必须由消费者团队在真实游戏里验收（清单见 `../../in-game-walkthrough.md` 与 [40-verification.md](40-verification.md)）。

## 5. 交接节奏

| 阶段 | 交付物 | 消费者可开始做什么 |
| --- | --- | --- |
| 第一个开发包 | P1+P2+P4 合并后的 `dist/dev/FerriteLib` + 本文档 | 多窗口壳、通知/命令态、外部 XML 热重载接入 |
| 第二个开发包 | 加上 P3+P5 | 集合与公共控件迁移、多窗诊断接入 |
| 最终 | `40-verification.md` 的证据表 + 实机清单 | 真实页面验收与反馈回交 |

消费者发现的缺口：**在消费者树内先组合实现**，把 `owner/repo@sha:path:line` 证据回交本仓，
由本团队评估是否晋升为公共能力；**跨仓修改由所属团队完成**，本团队不代改消费者。

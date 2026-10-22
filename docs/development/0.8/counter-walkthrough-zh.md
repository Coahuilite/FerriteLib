# 计数器：从手工通知到自动更新

0.8 Dev 已提供下面的实际入口。计数器用于看清一条链，不代表全部 FL 用途已迁完；
[完整用途表](migration-status.md)分别记录新响应接入、自动证据和原生状态。

## 先连接一次，之后普通赋值

```csharp
var runtime = new UiReactiveRuntime();
var count = runtime.Int();
var bindings = new UiBindings(runtime);
bindings.BindTracked("caption", () => "Count: " + count.Value);
bindings.BindTracked("count", () => count.Value, value => count.Value = value);
bindings.BindCommand("increment", () => count.Value++);
```

`text/wrapped` 的 Bind 接 `caption`，按钮 ActionBind 接 `increment`，按既有 XML/Host 入口装配。
getter 实际读取 count，库自动建边；不手写依赖列表、NotifyChanged 或缓存键。
`Int`／`Text`／`Boolean` 明确使用整数相等／ordinal 字符串相等／布尔相等；相同存储值跳过失效。
普通 C# 字段、任意引用对象和集合原地修改不因此自动可观察。

旧 `BindReadOnly("caption", () => "Count: " + plainCount)` 仍是普通 getter；业务改 plainCount 后仍需
`NotifyChanged("caption")`。新纯度条件不倒灌旧 getter／自定义测量。

## 一次操作，三个读时点

1. 指针按已有有效几何命中，合格松开成立一次请求；执行重查最新资格，业务仍可拒绝。
2. 命令进入隐式批次，赋值立即可读，依赖先失效；最外层结束再运行自动响应。
3. Host 在允许的呈现机会先同步展示输入，再测排／绘制。同轮沿用这一份输入；处理中新增业务值
   留待下个合法同步点。不固定为 tick、Repaint 或一帧，hover／pressed 等即时反馈仍可变化。

```csharp
using var business = runtime.CreateOwner("counter-business");
var caption = business.Compute("caption", () => "Count: " + count.Value);
business.Respond("observe", () => Console.WriteLine(count.Value));
using (bindings.BeginOperation())
{
    bindings.Set("count", 1);
    Console.WriteLine(bindings.Get<int>("count")); // 1，业务即时读
    Console.WriteLine(caption.Value);             // Count: 1，显式派生读不旧
    bindings.Set("count", 2);
}
// 自动响应随后见 2；展示也可以只采用最终 2。
```

自动响应不依赖窗口存在；需要逐次保存时放在业务命令／事件中，不挂在文字 getter 或 Draw 上。
两次加 0 仍是两次业务操作；存储同值跳过不能吞掉请求。异常不回滚或重放已发生的业务写入。
Checkbox 的 Set→Invoke 已接同一共享逻辑操作，自动响应不插进两者之间。

## 从一个数扩展到普通对象和列表

`InventoryItem` 是普通 C# 类，Count 属性里读写一次接好的 `runtime.Int`，不继承 FL 基类。
集合明确接入结构，项属性独立接入；作者不用重复列依赖：

```csharp
var list = runtime.List(new[] { first, second }, (a, b) => ReferenceEquals(a, b));
var current = runtime.Value(list, (a, b) => ReferenceEquals(a, b));
var total = business.Compute("total", () => current.Value.Sum(item => item.Count));
bindings.BindTracked("caption", () => "Total " + total.Value);
bindings.BindTrackedOptions("selected", () => current.Value
    .Select(item => new UiChoice<int>(item.Key + ": " + item.Count, item.Code))
    .ToList().AsReadOnly(), (a, b) => a.Text == b.Text && a.Value == b.Value);
```

`Add`／`Remove`／`Move` 改结构；`item.Count = 10` 只改该属性源。计算总计会读结构和 Count，
只生成 Repeat key 的计算不读 Count。集合／对象换源后，下一次成功计算撤掉旧边，即使显示结果相同。
其他值和引用用 `runtime.Value(initial, equal)` 明确选择相等；不自动深观察普通字段。
比较为相等意味着保留旧存储值，不能拿两个对象恰好相同的 Count 当作目标身份相等。

options 投影在受追踪读取中把 Count 变成不可变文字；只复制外层列表后留着可变 item，不能保证同轮一致。
实际 Dropdown／ModeRow 读这份投影，Repeat 用原稳定 key 和同一节点树。列表例中的逐项文字及数值绑定
按 key 解析当前对象，不捕获已移除的对象；新增 key 只注册一次自身绑定。完整可编译代码是
`InventoryPageExample.cs`，同源中立和实际 Host 测试共用它。

## 独立业务保留自己的权威

可编译双例在 `tools/FerriteLib.UiKit.Core.ContractTests/`：`ReactiveExamples.cs` 是简单计数器；
`WorkshopService.cs` 是无 FL 依赖的工坊服务，自己持锁和业务规则；`WorkshopProjectionExample.cs`
把它提供的不可变安全快照送到 FL 投影的所属上下文，再用一次 Batch 更新 Completed／Accepting。
UI getter 只读投影，不因为通知已送达 UI 就无保护地读取后台可变对象。命令回送业务，业务决定是否接受。
任务调度、跨线程投递和新旧结果取舍属于消费者／接入层，不由 FL 接管。

多项版本是 `OrderBoardService.cs`＋`OrderBoardProjectionExample.cs`：服务在自己的锁内取得不可变行快照；
接入层将安全结果交付到 FL 所属上下文，再 Batch 更新保留项的 Count／Accepting，以及真正变化的结构。
相同完整快照不会让全部来源失效；只改 a 时，b 的独立属性计算不重算。Batch 不是跨线程投递，也不是事务。
UI 不在 getter 里读取后台服务的可变列表；业务仍可拒绝最后一次投影认为合格的命令。

关闭 Host 只清自己的展示计算／订阅。独立业务 owner 和服务继续工作；最终由各自 owner 释放，
Dispose 不保存、不回滚、不销毁外部模型。换分支会撤旧依赖；失败计算保留旧展示但当前派生读取仍失败；几何不发布半份，不重放操作。

## 声明范围，复用未变的工作

`UpdateScope="true"`可在组合根声明并嵌套；Section／Surface／Scroll／Clip／Repeat和页面根已有默认接入：

```xml
<Column Id="page" UpdateScope="true" Padding="0" Gap="0">
  <Column Id="counter" UpdateScope="true" Padding="0" Gap="0">
    <Widget Id="caption" Kind="text/wrapped" Bind="caption" />
    <Widget Id="add" Kind="input/button" ActionBind="increment" Text="Add" />
  </Column>
  <Surface Id="details" Padding="0">
    <Widget Id="help" Kind="text/wrapped" Text="Details" />
  </Surface>
</Column>
```

Count:0→Count:1仍是一行时，只验证变化的真实输入并计算该文字，父级尺寸相同便可保留局部排列。
内容新增一行时，父级推进后区位置；后区不因搬动重测原文字。绘制／命中／裁剪与弹层锚点仍用新位置。
固定Scroll外框不代表内容范围没变，共享标签组也比较实际最大值；这些角色由库接好，不需页面罗列输出。

工坊的每个Repeat行可在模板组合根声明范围，Add／Remove／Move继续用业务key和原节点／草稿／捕获。
只改a时，b的属性求值和测量／局部排列可分别复用；服务仍独立运行，范围不取得业务调度权。
新自定义控件通过可选`IUiMeasureParticipant`提供完整捕获输入；旧getter／IUiWidget保原通知和合法同步条件。
具体有效性及作者示例见[布局局部复用合同](layout-reuse-contract.md)。

## 失败时重试显示，不重放业务

可编译的 `FailureLifetimeExamples.cs` 补充两个普通入口：`FailureCounterExample` 的 Count 是业务值，Caption 是纯计算，Host 持自己的展示；`FailureWorkshopExample` 的服务持业务结果，Notification 是独立消费者自动回调。

- 计数器格式失败后保留旧文字并标 `!`，首次失败是不可用；`Caption.Value` 仍抛可定位故障。无关值和旧分支不触发再试，相关 Count／格式输入变化或 `Caption.Retry()` 才允许。绑定 `retry-display` 可直接调用这个纯入口，不要求重做业务命令。
- 工坊回调写入后抛错，`Notification.IsPaused/Fault` 告知停止范围；owner 核对结果、修正接入，再 `Resume()` 只读当前快照。新快照／绘制／显隐不恢复它，已生效业务不回滚。
- 默认 Checkbox、编辑／选择／Tree／Chart 的旧控件展示通过专用纯 paint 入口保留，不重跑 Draw、Observe 或请求。编辑草稿仍属于同一目标；故障期间停止不可靠操作，独立 reset 仍可用。
- `host.SetDisplayDemand(false/true)` 或标准 shell 同名入口表达可靠隐藏／复显，不是失焦。共享 owner 让 A／B 各按需求读同一计算；关 B 不释放 A 或服务。确认在打开时捕获 A17，不能在确认时读取后来选择的 B23；业务入口仍核最新资格。

完整作者用法、实际停止范围、循环诊断和旧 custom 等级见[失败与寿命合同](failure-lifetime-contract.md)。

## 本包已证明什么

无引擎 E2E 使用同一生产 `NeutralSources.props`：实际业务操作／共享布尔请求 →
值与依赖 → 批次与响应 → 展示同步 → 中立布局几何；计数器和独立安全业务投影都覆盖。
可控适配测试另把双例接真实默认 Button／text/wrapped，验证按下不派发、松开一次命令、
下个合法同步点实际文字和内容相关几何更新，并覆盖 Checkbox。资格谓词即使用 bindings.Get
也读最新业务值，不改变本轮普通展示读；异常恢复相同。原生输入、字体、IME 和合成仍需实机。

RU1 另用默认 Dropdown／ModeRow／Repeat／NumberField 验证对象列表、typed options、同值选择、
增删重排及同 key 节点／草稿／捕获；同步后修改 Count 和结构，本轮仍绘旧投影，下轮才一起采用。
独立多项安全投影走同一生产图、真实包装及有界 Host 观察。数据计算与实际订阅数量见本轮验证记录。

RU2的同源`LayoutReuseTests`与真实`KernelLayoutReuseTests`另报每个实际对象的求值／测量／排列次数和有效几何，
覆盖同结果换源、计数同尺寸／新增行、独立安全多项投影、Scroll80→140且视口100、共享60→70→120、
宽度／字体环境／纯颜色／整体搬动、增删重排、草稿／捕获／弹层及失败不发布半份。
18用途／9容器的具体新缓存等级与未变旧条件仍以唯一用途表为准，不用双例替代整库覆盖。
任意引用隐式缺省、最终集合API／粒度、RU3 的 `FailureLifetimeTests`／`KernelFailureLifetimeTests` 另证首次／新分支／标签失败、纯重试、回调 owner 恢复、有限及超限反馈、旧展示／草稿／资格、真实隐藏及双 Host 所有权。RU4剩余迁移和RU5平台承诺仍分项保留。

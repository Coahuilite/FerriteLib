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

## 独立业务保留自己的权威

可编译双例在 `tools/FerriteLib.UiKit.Core.ContractTests/`：`ReactiveExamples.cs` 是简单计数器；
`WorkshopService.cs` 是无 FL 依赖的工坊服务，自己持锁和业务规则；`WorkshopProjectionExample.cs`
把它提供的不可变安全快照送到 FL 投影的所属上下文，再用一次 Batch 更新 Completed／Accepting。
UI getter 只读投影，不因为通知已送达 UI 就无保护地读取后台可变对象。命令回送业务，业务决定是否接受。
任务调度、跨线程投递和新旧结果取舍属于消费者／接入层，不由 FL 接管。

关闭 Host 只清自己的展示计算／订阅。独立业务 owner 和服务继续工作；最终由各自 owner 释放，
Dispose 不保存、不回滚、不销毁外部模型。换分支会撤旧依赖；失败构建不发布半份展示／几何，不重放操作。

## 本包已证明什么

无引擎 E2E 使用同一生产 `NeutralSources.props`：实际业务操作／共享布尔请求 →
值与依赖 → 批次与响应 → 展示同步 → 中立布局几何；计数器和独立安全业务投影都覆盖。
可控适配测试另把双例接真实默认 Button／text/wrapped，验证按下不派发、松开一次命令、
下个合法同步点实际文字和内容相关几何更新，并覆盖 Checkbox。资格谓词即使用 bindings.Get
也读最新业务值，不改变本轮普通展示读；异常恢复相同。原生输入、字体、IME 和合成仍需实机。

**局部布局缓存尚未实现。** 本包新展示输入变动沿既有完整测排，不把“外框未变”当作内部不用测排。
RU2 将决定范围输出／跨范围协作／缓存有效条件；引用比较、集合表面、详细失败／隐藏、
剩余旧入口迁移与平台承诺仍分项待定。实际接口、阶段及故障边界见
[生产合同](reactive-foundation-contract.md)。

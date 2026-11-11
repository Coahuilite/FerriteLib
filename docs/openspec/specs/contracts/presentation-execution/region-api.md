# 0.9 区域保留：已采用接口（未实施／未导出）

状态：WO-FL09-RETAINED-DISPLAY-20261111 r2 正式采用，整个新增闭包 Public-unstable。
本文是本组计划接口的唯一现行约定；行为／phase／paint证明归 [展示执行](spec.md)，
来源完整性及 Value／Choices 独立通道归 [绑定](../../core/binding-reactivity/spec.md)，
事务及局部传播归 [布局](../../core/layout/spec.md)。未改任何已导出签名或既有使用条件。

## 公开接口与参数

整个新增闭包已定 Public-unstable；原有 Stable 依赖不降级，不改旧接口 implements、构造默认或 XML。
本组是0.9已采用的待实施接口约定，不是已导出 API；不另建 snapshot／epoch 两套入口。
下列仅列 public／protected 表面；未列出的构造不公开，附件只能由 Attach 创建。

```csharp
[Flags] public enum UiRegionUse { Layout = 1, Display = 2 }

public sealed class UiRegionSnapshot
{
    public static UiRegionSnapshot Create(Action<UiRegionValues> fill);
    public T Get<T>(string key);
    public IReadOnlyList<UiChoice<T>> Choices<T>(string key);
    public UiRegionSnapshot Item(string source, string item);
}
public sealed class UiRegionValues // 仅 Create 的同步 builder，不得带出
{
    public void Value<T>(string key, T value);
    public void Choices<T>(string key, IReadOnlyList<UiChoice<T>> choices);
    public void Item(string source, string item, UiRegionSnapshot fields);
}
public sealed class UiRegionReadMap
{
    public UiRegionReadMap();
    public UiRegionReadMap Value<T>(string key, UiRegionUse use);
    public UiRegionReadMap ItemValue<T>(string source, string field, UiRegionUse use);
    public UiRegionReadMap Choices<T>(string key, UiRegionUse use);
    public UiRegionReadMap ItemChoices<T>(string source, string field, UiRegionUse use);
}
public static class UiRetainedRegions
{
    public static UiRegionAttachment Attach(UiHost host, string rootElementId,
        Func<UiRegionSnapshot?> captureCurrent, UiRegionReadMap reads);
}
public sealed class UiRegionAttachment : IDisposable
{
    public UiRegionStatus Status { get; } // detached；不捕获来源、不推进
    public void Retry();                // 下一合法点仅重试本区域失败的纯工作
    public void Dispose();             // 退出能力，不关 Host／不提交业务
}
public enum UiRegionState { Active, Unknown, Fallback, Failed, Detached, Closed }
public sealed class UiRegionStatus
{
    public string RootElementId { get; }
    public string? MountId { get; }       // opaque，只用于归因，不是操作地址
    public UiRegionState State { get; }
    public string Reason { get; }
    public long? SourceVersion { get; }
    public long? LayoutVersion { get; }
    public long? LastPaintPass { get; }
    public bool? InputsCurrent { get; }   // 截至已观察边界；未采样为 null
}

public interface IUiRegionParticipant // 与 IUiWidget 并存的可选能力
{
    UiRegionDisplay Build(UiWidgetContext context, UiRegionSnapshot inputs);
    void Operate(UiLayoutRect bounds, UiWidgetContext context, UiRegionDisplay display);
    void Paint(UiLayoutRect bounds, UiWidgetContext context, UiRegionDisplay display);
}
public abstract class UiRegionDisplay
{
    protected UiRegionDisplay(float naturalHeight, float? labelWidth,
        IReadOnlyList<UiPointerRegion> pointerRegions, UiLayoutRect? paintClip = null);
    public float NaturalHeight { get; }
    public float? LabelWidth { get; }
    public IReadOnlyList<UiPointerRegion> PointerRegions { get; }
    public UiLayoutRect? PaintClip { get; }
}
```

- `host` 是原实例；`rootElementId` 是其唯一已声明结构容器，可为 F09 的 Repeat `b-actions`，
  不限 Overlay，不要求全页或变成新 kind。尚未 materialize 可登记声明；模板内歧义 ID 拒绝。
  Attach 只做静态／元数据连接验证，不在此读取业务或创建候选节点；首次捕获在原合法边界。
- `captureCurrent` 每个合法同步边界、reentry/show 首同步及 Repaint 前必须真实调用，
  不以相同旧 token 跳过此调用。同一 Host 同一同步只取一次，独立 Host 各确认。
  函数只读当前源并返回完整 immutable 表示；`null` 是明确 Unknown，走原区域路径。
  抛错是当前 Failed，不转换成 Unknown／空成功；不自动写 source 或装 tracking。
- snapshot 以**同一不可变实例**表示同一完整业务表示；新实例保守失效，FL 不做任意对象
  深相等。源 owner 可核对覆盖全部实际来源的可信不变证据后返回原实例，不要求重读每个字段；
  证据须归真实 source owner、覆盖全部写边和有效期（包括失败 rowResults），不是只查 view/Bump。
  这是可检查的数据表示，不是仅有整数／hash／版本号的凭据。
- Create 复制封存键表，Choices 复制封存其外层及 caption／help 事实；Value 的集合及其他
  payload 必须由源提供 detached 不可变值，不承诺对任意 T 做反射深拷贝；字符串／值类型按值，
  不能把可变 view、普通对象字段或延迟枚举藏进去。同一通道重复 key／错型定位拒绝；缺项读取失败，
  合法 null value 与 source 返回 Unknown 分开。`Item` 按原 UiBindingAddress source/item
  命名，包内 field 为 key；可复用未变 item packet，不按 ordinal 绑定 UI 身份。
- Value 和 Choices 在 builder、read map、root／item snapshot 都是独立通道；同名 key 可各有一个，
  Get<T> 只取 Value，Choices<T> 只取独立 choices，不把 Value 中的列表转作 choices 或反向回退。
  Choices<T> 返回 defensive immutable 的 IReadOnlyList<UiChoice<T>>，保 Text／Value／HelpRef／TextKey，
  与当前 readonly struct UiChoice<T> 一致；T 的实际消费数据仍须不可变，不深克隆任意业务对象。
  缺目标通道／key／item 抛 KeyNotFoundException；目标通道存在但精确声明 T 不符抛
  InvalidOperationException；同通道重复登记抛 ArgumentException，choices 外层 null 抛 ArgumentNullException，
  明确空列表是成功空值。Build 的错误归原路径定位，不能借普通 Get／Choices 补资料。
  Operate 的实时 choices／资格／请求仍走原 endpoint，不从冻结列表取得执行授权。
- `reads` 是**新端口的明确采用表**，Attach 复制冻结。Layout 覆盖该 key 的结构／测量角色，
  Display 覆盖支持表示消费的纯展示角色；二者可并用。Items 用 Value 的有序 key 列表，
  Choices 是独立通道；ItemValue/ItemChoices 精确匹配 source/field，不是字符串通配符。
  map 不用于请求、即时资格、旧 custom Draw 或普通 Get。已明确映射但包中缺失是错误，
  未映射才是旧读取洞；不能把错型／失败伪装成洞掩盖错误。
  空 use／未定义 flag 拒绝；item packet 是否存在不代替 Items 向量，不另发明成员顺序。
- `Build` 无业务／UI mutation，只用传入表示、当前有效几何／字体／样式环境及已存在的
  UI 状态事实，不能 Get 普通业务、创建 editor、执行控件、发帮助／请求或保存 context。
  它产出一个自定义不可变 display 子类，包含自己的只读绘制资料；自然高、可选 label
  贡献及 widget-local pointer regions 同供测量、指针和绘制。数组被复制冻结，不能持候选容器。
  `labelWidth=null` 不证明 Auto／共享列，相关贡献沿原鲜验；非有限高度／区域定位失败。
  `inputs` 对非 Repeat 项是区域根包，对项内节点是当前最内层 source/item 的 packet；
  由原 Repeat 地址解析选择，不从行号／caption猜测。所需共享业务事实须显式放进该包，
  不隐式向外查普通 getter；更深嵌套可继续用 Item 分组，包不授予请求目标权威。
- effective clip 是实际裁剪栈的共同区域，不是 layout rect 与祖先 clip 的交集；
  无法证明全部输出时，不因布局框不相交而省略 Repaint。
- `PaintClip` 默认 null，表示没有额外绘制边界证明，不能用 layout rect 不相交省 Paint。
  非 null 是作者对**新纯 Paint**显式选择的局部绘制约束，不是一个“信任布局框”的标志。
  FL 必须借原 drawing／adapter 的 clip 强制限制该 Paint 的全部实际输出，并保持原逻辑坐标，
  才能把该约束转换为有效 footprint 证明；该 clip 不包住 Operate／native editor／旧 Draw。
  作者须给出包含其应展示内容（含文字外溢、线宽、装饰等）的有限局部范围；不得自动取 layout rect。
  PaintClip 以当前 widget bounds 左上为局部原点，可负偏移以包含外溢，宽高须有限非负；
  由原坐标 owner 映射到当前 scroll／window clip 空间，不能缓存过时的绝对位置。
  不得以过小约束或删内容缩减真实场景；只有新纯 Paint 受该显式边界约束。
  不能强制覆盖全部输出的后端不使用此证明并走明确原路径；没有证明时仍调用适用 Repaint。
- `Operate` 使用同一 display 给出的局部几何，但所有模型读／资格／native editor／命令走
  原通道，保每事件次数及原全局顺序。`Paint` 是新纯展示，只读 display 和当前 FL 外观／
  drawing，不 Get 模型、不创建控件／帮助／请求。没有把 IUiWidget.Draw 自动当成此接口。
  原 Draw/Measure/PointerRegions 仍是退出／未采用／不支持后端的完整合法实现。
- editor 的 draft／valid／modified／conflict／focus／composition 变化由原 FL edit owner
  推进局部表示失效；Build 可读已持有的 editor 事实，不在 Build 创建它。
  custom 其他本地状态由其真实写入 owner 调用原 `ctx.Invalidate()`；做不到的那段保旧路径，
  不要求未接入作者遵守新纯度。草稿变化不能靠业务 view 未变来压住。
- `Status` 的最小字段为区域路径／mount、状态（Active/Unknown/Fallback/Failed/Detached/Closed）、
  原因、最后完整输入／布局／paint 的 pass 与截至已观察边界的有效性；未公告外部变化不能
  由纯状态查询自动发现。显式既有仪器启用时，追加有界计数：
  BoundaryCaptures、Builds、ReusedRegions、ScheduleReuses、InteractiveCalls、UnknownCalls、
  PaintCalls、ClippedDisplays；未采样为 NotSampled，不把 PaintCalls 当 native/GPU/FPS。
  返回不含 live snapshot／node／source／callback。重试不重发操作，Dispose 幂等。

## 作者采用示例（待实现）

以下仅为实施接线示例，当前源码不可调用；领域 capture 由消费 owner 实现，不是 FL 的业务接口：

```csharp
// IUiBindingAccess、普通命令和原 source 公开入口全部保留。
var reads = new UiRegionReadMap()
    .Value<IReadOnlyList<string>>("b-action-keys", UiRegionUse.Layout)
    .ItemValue<bool>("b-action-keys", "field-visible", UiRegionUse.Layout)
    .ItemValue<bool>("b-action-keys", "has-row-result", UiRegionUse.Layout)
    .ItemValue<string>("b-action-keys", "row-result", UiRegionUse.Display);
using var region = UiRetainedRegions.Attach(host, "b-actions",
    source.CaptureActionDisplay, reads);
```

这里只展示一个采用技巧；生产 map 要补齐实际选择迁移的 title／effective／可见性等角色。
不在 map 的普通读继续旧路径，不能因例子短就声称整个区域已封闭。
`CaptureActionDisplay` 鲜核 BuildView 所用的 configuration／definitions／policy／session，
并另外核对真实 rowResults、展开／details／target 和贡献数据；未变分项复用 item packet。
失败结果的所有原 mutation 入口由 source owner 纳入 capture 的实际输入或内部完整修订，
**不向原调用者追加 Notify/Bump 条件**。原 source 无法证明的来源返回 Unknown 或留成洞。
生产 owner 已能提供完整不变证据时，未变 capture 只核这些来源域证据并返回原包；
只有变化才投影相应分项。不得在 CaptureCurrent 内逐字段 Get／重算 effective、格式化、
CompactLayout 或重建完整展示，再把后代 Build=0 冒称区域收益。

外部 sparse 作者保留原 kind／Validate／Draw，另外实现 participant：

```csharp
public UiRegionDisplay Build(UiWidgetContext ctx, UiRegionSnapshot row)
    => BuildFieldDisplay(row.Get<string>("field-title"),
        row.Get<string>("field-effective"), observedEditor?.Valid == false,
        ctx.ViewWidth, ctx.Properties, ctx.Metrics, ctx.Translation);

public void Operate(UiLayoutRect rect, UiWidgetContext ctx, UiRegionDisplay display)
    => DriveOriginalEditorAndHelp(rect, ctx, (FieldDisplay)display);

public void Paint(UiLayoutRect rect, UiWidgetContext ctx, UiRegionDisplay display)
    => PaintFieldLabels(rect, ctx, (FieldDisplay)display);
```

这些 BuildFieldDisplay/Drive/Paint 名称是作者拆出的函数，不是新 FL 公共方法。
FieldDisplay 继承 UiRegionDisplay 并持 readonly strings／局部 rects；业务值和 readModel／
request codec 不从 display 取得。原 Help 每 pass、下拉／编辑 native 调用不移入 Paint。
原编辑器的当前选区／caret／draft 和必须当次反映的下划线等反馈仍可留在 Operate 的交互片段；
不为强行把所有画法搬到 Paint 而引入第二个 editor 或等待业务 view。纯标签／effective文字／
已配对错误布局进入 FieldDisplay，自然高度变化在原下一合法排列采用。
目标是一次纯区域派生共享给 Measure／Pointer／Paint，而不是三个位置各查同一叶缓存。
不采用新接口时仍调用原 IUiWidget 的完整实现；仅增加接口而没有 Attach 也不生效。

独立 choices 的读取采用同一包，不补业务读：

```csharp
// root 或 inputs.Item(source, item) 都支持这两个独立通道。
string selected = inputs.Get<string>("mode");
IReadOnlyList<UiChoice<string>> choices = inputs.Choices<string>("mode");
```

源构造时分别 Value("mode", selected) 与 Choices("mode", copiedChoices)；read map 也分别声明。
该例只展示读取，实际候选值仍须由 Operate 当前资格／endpoint 决定。

# Design — 区域表示与实际操作分离

状态：**0.9 已采用，r3 已放行实施**，WO-FL09-RETAINED-DISPLAY-20261111 r2 选用 bb7da49 并补全独立 choices、
实际 paint footprint 证明和生产源捕获要求。本文件是采用时设计来源，不是另一份 current；
行为归 [现行入口](../../../specification.md)，接口唯一归 [region-api](../../specs/contracts/presentation-execution/region-api.md)。
本文代码保采用时记录；实际导出、执行支持和证据归实现账，不以采用文本充作产品验收。

## D1 两种输入、一个 Host

采用区域内有两个明确端口：

1. **表示端口**：边界 `captureCurrent` 取得不可变 `UiRegionSnapshot`；只供显式映射的结构／
   展示消费及新 participant 的纯 Build。它不是业务答案或执行资格。
2. **原 reader／endpoint**：`IUiBindingAccess.Get/Choices/Set/Invoke` 保原含义；原 widget、
   未映射需求、operate、资格及请求仍走它。不能用登记同名 key、ambient scope 或代理 reader
   偷换旧 Get 的结果。Attach 才明确开启新端口；登记快照或实现接口本身不改变旧调用。

没有第二棵 UI 树。snapshot 的 item packet 是业务表示分组，不拥有节点／mount／焦点；
成员仍来自原声明／Repeat keys。FL 在原 retained records 上编译区域纯工作片段、
interactive／unknown 操作片段及有效位置，复用原 journal、Session 与成功 publication。
UpdateScope 继续持记录，不成为独立 scheduler；不扩为通用跨 Host 缓存平台。

## D2 采用时接口记录与参数（非 current）

整个新增闭包已定 Public-unstable；原有 Stable 依赖不降级，不改旧接口 implements、构造默认或 XML。
以下保留采用时的接口设计；实施以唯一 current region-api 为准，不并存 snapshot／epoch 两套入口。
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

## D3 作者先采用一个现有区域

采用时的实施接线示例（尚未导出）；领域 capture 由消费 owner 实现，不是 FL 的业务接口：

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

## D4 区域边界做实际工作减法

FL 持一个已提交记录，包含表示的根／item packet、真实定义和 reader owner、约束／环境、
局部结果及纯／操作调度片段。相同源表示、engine-owned generation 和边界约束下：

- 每同步点只鲜验区域边界，不重新遍历纯后代 Build/Capture/Measure、解析其 context 或
  生成局部 pointer geometry；复用**区域**片段，而非进入每个叶再命中 cache。
- 仍顺序执行 interactive 与 unknown 操作列表；同一份局部 pointer facts 随新位置／clip
  变换并按原 pass 发布 hit。运行列表可复用，不能省掉必要操作。
- 源根变化时利用未变 item packet／分支证明跳过未变区域记录，只构造新／变成员及祖先
  布局依赖；不能先完整重建所有后代再标 sparse。算法不承诺任意图 O(变更节点)。
- 未映射的控制 getter 按实际先父后子顺序鲜读；父关闭后不能探读历史后代。Unknown 洞
  只破坏其实际依赖闭包；跨 Fill／共享列／Scroll 等不能证明局部传播时，报告并走原 full。
  动态成员、普通 getter、旧 Measure 的**存在本身**不再强制整个已证明区域退出。

纯记录的有效性不是一个业务 token。FL 的记录覆盖以下 owner：

| 输入／事件 | owner／新鲜度 | 相等时／变化时 |
| --- | --- | --- |
| 业务值、key向量、target、展开、贡献、row result | source；每合法边界 capture | 相同完整表示可跨边界；分支变化使相应纯工作／结构失效 |
| 定义、注册／reader owner、绑定类型 | 原编译／Host | 同目标可保状态；更换重验，不能给旧 endpoint 新权威 |
| bounds、width、padding、Fill／MatchContent、共享 label、scrollbar预留 | 原 layout | 平移不重测文字；尺寸／响应式域变化重建受影响闭包 |
| clip、scroll offset、window origin、popup anchor | Session／layout／adapter | 每事件用当前变换；滚入首个 Repaint 用当前表示实画 |
| fonts、style、语言、metrics、UI scale、图像 token环境 | 原属性／服务／adapter | 完整可靠环境可复用；未知尺规继续鲜算。颜色只影响其真实 paint 依赖 |
| draft、valid、conflict、focus、IME、capture、hover／pressed | edit／interaction owner | 即时操作不冻结；相关 UI state 使局部纯结果失效，不要求业务 bump |
| custom local state／未知外部服务 | 实际 custom owner | 明确 Invalidate 或保旧洞；不凭源表示推测无变化 |
| 外部 Invalidate／未分类 dirty、故障／Retry | 原失效／恢复 owner | 显式未知失效仍保守退路；区域登记不吞掉任何旧通知 |

Scroll 内容增减、clamp、后续兄弟、clip/hit/popup 位置必须同一完整事务发布。
外层 Scroll 不需要也被登记；若传播越过区域边界，原父 owner 接收真实新尺寸。
不要求把全页、所有自定义 widget、所有业务或 reactive graph 一次迁完。

## D5 操作、绘制和 phase 的先后

页面原 TieredEvents／Schema 决定何时同步；区域保留是同步内部的新选择，不创造第三种页面
时钟，也不取消两个合法同步边界。每边界廉价 capture 可相同，后代不再付两次派生成本。
逐 pass 页也可显式登记，仍每 pass 取源，未登记读的次数不变。

实际顺序仍是合法边界取源／事务布局 → 原全局序列操作（含 popup/input 消费）→ 该位置的
纯 Paint。不能把全页 native 操作移到另一批而改变 control IDs／事件消费；可以把纯资料
准备和可复用调度片段提前，不重排必要调用。输入在本回调产生的新业务结果按 U3 到下一
合法同步，较晚 sibling 不读一半新源；原 native editor 的当前稿／caret 当次正常显示。
操作导致局部 UI 装饰变化时更新本地显示事实；若自然高度变化，按原下一合法几何边界
完整采用，不在已发出的半个布局中插一次私自 arrangement。Repaint 前已有的稿／错误变化
在该 Repaint 同步采用，不能拖到第二个 Repaint。

手动 MeasureAndArrange→Draw 消费同一已提交 region epoch；间隙新公告使其待下次合法
配对，不递归插排或以新表示配旧几何。原无登记手动调用、BeforeDraw／Tools prepare 的
调用点不变。新区域登记不把手动 phase 改成可重复 draw 的替代对象。

1.6 IMGUI 每实际可见 Repaint 仍通过原 drawing 提交；隔离按原 owner finally 恢复。
仅当 effective clip 本身为空，或全部实际可能 Paint footprint 已由具体实现证明／原后端强制
限制且在当前 clip 外，才可延后纯 Paint；layout rect 不相交不是覆盖证明。未知 footprint、
未知 clip 或证明过期时仍调用 Repaint。原操作／资格／help／capture／editor 调度保留。
几何所需的 Build／首次自然测量不因屏外延期；同一有效 UiImage 的供应者只更新像素时，
Repaint 仍画当前资源，尺寸变化才参与真实测量失效，不把供应资源复制进表示或抢其寿命。
滚动、resize、隐藏后重显在首个适用 Repaint 实画当前内容，不能靠上次 framebuffer。
未提供所需隔离／字体／绘制能力的 adapter 明确报告回退至旧路径；不同 adapter 单独供证，
不是把1.6验收改名1.7。首版不增加 GPU 图像缓存或承诺省 scanout／全部 buffer 提交。

## D6 事务、失败与寿命

- 新捕获／Build／附件变更在 Host owner 上串行，不新增线程或时钟；未接入旧调用无新线程前提。
- Attach 验证 root／map／可静态判断的源连接后才生效；首版不允许两个 attachment 的范围重叠，
  原 UpdateScope 嵌套不受影响。失败登记不改原页；非重叠区域可独立采用。
- 候选表示、纯结果、成员、plan 和位置同成功 journal 发布。捕获／Build 期间发生已知
  变更不能确认其较早凭据；放弃候选，保待处理失效，到下一合法边界处理，不能无限现场重试。
- Build 的纯失败缓存其实际输入／环境和原因；相关输入变化或 Retry 才重试。
  源普通捕获的异常保原鲜读恢复条件，不拿 pure failure 缓存吞掉普通读的后续义务。
  几何失败保完整旧结果并暂停不可靠输入；纯 Paint 失败可用最后成功 immutable display
  作带 fault 的静态绘制，不运行 Operate／旧 Draw／业务读，保 RU3 的 current/last 区分。
- 同一 Host 的区域 capture／Build 不得重入推进；新能力在更改候选前定位拒绝。
  捕获／操作回调中 Close 后立即停止后续常规工作，丢候选，不通过 finally 复活状态。
- attachment Dispose 在下一安全边界退出新路径，丢本能力记录、重新验证旧路径，保相容
  稿／身份，不发 blur/save。Host Close、root移除／不兼容替换、mount结束退休对应记录；
  可靠折叠不等于销毁，重显核当前来源，不复活 capture。重挂是新 incarnation。
- FL 只释放自有记录／附件引用；借用 execution、source业务、共享服务／纹理由原 owner
  释放。幂等逐项清理、保聚合原因，不缓存每个历史源表示，不新增进程全局可变 cache。

## D7 必须拒收的反例

| 反例 | 所需结果 |
| --- | --- |
| view／configuration 未变，外部 TryCommitActionField 失败写 rowResults | 首个合法同步出现错误及真实高度；无额外 Bump 前提 |
| model未变，invalid draft／composition／focus发生变化 | 编辑原时序、即时 native 稿与局部表示同步；不靠业务 token 压掉 |
| 源仅给旧整数、闭包之后读可变对象，或遗漏实际贡献字段 | 不可作为完整证明；留原读取／明确拒绝该登记，不冻结整页 |
| Unknown widget/来源混在已证明区域，或缺度量环境 | 未知段继续原条件；仅使实际依赖闭包回退，不要求删控件或贡献 |
| layout rect 在 clip 外，但文字／装饰可能画进 clip，且无全输出证明 | Repaint 调用 Paint，不能由布局框推断屏外 |
| value/choices 同名或缺 choices／类型错误 | 独立通道精确读取；缺失／错型定位失败，不借普通 reader 补齐 |
| 未变 CaptureCurrent 仍逐字段派生，只让后代 Build=0 | 不满足区域更新，不能把成本搬家叫复用 |
| 父关、tab切换、Repeat移除后才读旧后代 getter | 拒收；按当前父条件停止，再由原成员事务协调 |
| scroll/resize/字体/语言/共享列变，旧packet相同 | 传播当前几何／环境，paint/hit/popup同位 |
| 已裁剪编辑器仍持capture、弹层覆盖滚轮、help grace经过 | 保逐事件操作及原顺序／结束／pass计数；不能整体跳过 |
| Build失败、源换backing失败、操作写后抛错、Close中途发生 | 不发布半结果、不回滚或重放业务；当前失败／最后展示分开 |
| stable区域仍逐项capture／contexts，或只改计数标签 | 不满足 RETAIN；必须有区域边界实际停止重复工作的证据 |
| 复用后不在实际Repaint提交可见内容 | 不满足呈现合同，即使模型／截图缓存正确也拒收 |

## D8 观察与接受

Status 和既有 Tools/R9 只读同一 owner 记录，标 source epoch／layout epoch／last Paint pass
及当前失效；“Build reused”不是“刚刚画过”。纯 capture 查询不运行 source callback、Build、
编辑或 Retry。Tools 操作仍通过原队列重核，MeasureBudget 只在原显式准备边界取新候选，
不发布 region cache／epoch、不clamp、不改变旧自然叶准入。

后续以固定0.9开／关配对观察：边界captures和跳过的区域工作、实际交互／未知调用、
首次／暖开合、展开／收起完整帧、Host父段、实际目标绘制调用时间、GC和分配。
保留实际分母及不同计时场，不相加 inclusive/self 或不同列中位数，不用方法次数换算FPS。
先验证完整功能路径，再裁定整帧6.060606ms目标。历史主扫144Hz与后续独立IMGUI账的60Hz
现场分开，不是新0.9对照；同代测量按实际节奏配对，不变更显示设置，也不签发165Hz物理显示保证。
SPEC采用阶段只有文本／源码证据，无产品性能结论。

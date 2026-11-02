# 属性来源、文字、图像与纯呈现

## Purpose
让作者按属性写意图，消费者按当前有效来源测量／绘制。外观不持业务值或输入状态，不以主题快照替代继承，也不要求采用页面引擎才能独立使用。

## Requirements

### Requirement: FL08-PROP 每个属性独立解析
适用环境属性 SHALL 按 runtime instance → inline instance → runtime local → named local → 最近祖先同样书写来源 → page style → host declaration → kind/library fallback 解析。每层一个 Scheme 与一个 Density；Density 对自身明确声明的度量生效，与同层 Scheme 同时声明该度量时 Density 优先，未声明项仍继续查找。稀疏缺项继续向下找；已解析父包不能升级为子声明。业务值、Bind、Tone、草稿、编辑政策、capture、最终 rect 不继承。

#### Scenario: Tiny override 与继承密度
- **WHEN** 子显式 Tiny 而密度未声明，父由 Small/常规改 Medium/紧凑
- **THEN** 子 Tiny/紧凑，来源分别保留
- **AND** 不把整包外观固定为旧快照

### Requirement: FL08-PROP-REMOVE 删除只揭示当前下一来源
Remove SHALL 只撤该层该属性，揭示当前 inline／local／ancestor／page／host／fallback，不恢复创建时快照、不重置其他属性。显式同值 SHALL 保留书写和后续跟随意图。Removing inline 走正常 immutable definition update，不与 runtime Remove 混淆。

#### Scenario: 实例与父同为 Medium
- **WHEN** 父改 Small，随后实例 Remove Font
- **THEN** Remove 前仍 Medium，后为当前 Small 并继续跟随
- **AND** 不因数值曾相等删除声明意图

### Requirement: P1 有限字体、颜色与五项度量
普通作者 SHALL 可直接配置字体、30个独立 semantic palette roles 与 Padding／Spacing／Gap／RowHeight／Hairline，不需私有部件或派生默认控件。单项 Metric 声明须有限非负；原 UiGeometry 构造对负输入仍夹到0，不新增旧调用拒绝条件。字体独立于密度；bare container Padding/Gap 是该容器分配，不是继承的 control metrics。默认主题可直接用；UiColour 是中性颜色，包括主题 indexer，不是 Unity Color。默认角色值相同不意味着声明联动，透明 alpha0 是明确颜色而非 null fallback。

#### Scenario: 只覆盖 WarningForeground
- **WHEN** 作者设一个语义角色又撤销
- **THEN** 只影响该角色及其当前来源，Danger／Success／Selected 等仍独立
- **AND** 不建立第二个调色存储或旧26位 bag

### Requirement: FL08-PALETTE 有限合法颜色与样式失败
颜色 SHALL 接受 #RGB/#RGBA/#RRGGBB/#RRGGBBAA 与 invariant r,g,b[,a]，通道有限且0..1；未知角色定位报告，不能映射旧名。当前 palette 是 Window/Panel/Control/Hover/Selected/Success/Warning/Danger 各 Surface/Border、WorkspaceSurface、SectionSurface、Foreground、SecondaryForeground、DisabledForeground、SelectedForeground、SuccessForeground、WarningForeground、DangerForeground、Border、StrongBorder、Divider、Accent、SwitchOff。独立 style 解析保原 fail-soft；inline manifest 非法声明创建失败。当前 font style 拼写 Metric Token="Font"，不是 retired Font element。

#### Scenario: 独立 style 候选无效
- **WHEN** 新 style 使用未知角色或退休 Font 元素
- **THEN** 给出实际 issue 并按既定 last-good 处理
- **AND** 不静默忽略为有效新样式或影响业务

### Requirement: FL08-LIVE-PROPERTIES 活视图与可变 Theme
ResolvedFont/Metrics/Colours SHALL 给当前有效值和来源；不同度量来源可显示 Mixed，细项保真实源／Scheme／Density／祖先。持有的只读字典仍随正常解析更新，强转写入拒绝。mutable Theme、ThemeFor 与未改变对象 identity 的合法条件 SHALL 保留；颜色只 dirty paint，字体／度量真实变化 dirty measure，Draw 使用当前外观，不每帧冻结整包。

#### Scenario: measure 后 pointer collection 前修改 tint
- **WHEN** 作者在正常条件改变 palette
- **THEN** 当前 paint 可采用新 tint，几何无需因此重排
- **AND** 持有来源视图继续更新，不变成可写 resolver store

### Requirement: FL08-TEXT-COLOUR 七个独立文字色槽
TextColor 与 TextColor.Muted/Active/Success/Warning/Danger/Disabled SHALL 是主题文字色，
不扩展 UiPaletteRole。各槽先独立按 FL08-PROP 解析，再由有效状态选择。
Disabled 只取 TextColor.Disabled 或 DisabledForeground，不取普通 TextColor；
Active/Success/Warning/Danger 依次取对应槽、TextColor、对应 semantic foreground；
中性 Muted 取 TextColor.Muted、TextColor、SecondaryForeground；其余取 TextColor、Foreground。
透明 RGBA 是明确值。只改色 SHALL 只推进 ColourRevision，不推进 LayoutRevision 或强制重排。
实例／局部 Remove 只揭示下一当前来源；resolved 视图保留每槽来源。
独立视觉使用同一 Theme/Styles.Resolve，无 Host 或 Session 前提。

#### Scenario: 近处普通色与远处状态色
- **WHEN** 实例只写 TextColor，祖先写 TextColor.Warning，当前有效 tone 为 Warning
- **THEN** 使用祖先的 Warning 槽；撤销该槽后才回落实例普通色
- **AND** 真的禁用时普通色不覆盖禁用提示，Disabled 槽不恢复任何输入资格

### Requirement: FL08-READONLY-TEXT 纯展示绑定不是禁用
text/wrapped SHALL 不把绑定可写性传入展示状态；只读 Bind 默认使用正常前景，
Tone/Emphasis/TextColor 仍可决定文字色。不做 opt-in。共享 Resolve(..., writable:false)、
StatusBadge/StatusTreatment 的显式 writable 语义与所有交互控件的禁用、输入禁止保持原合同。

#### Scenario: 只读状态文字
- **WHEN** text/wrapped 读取 BindReadOnly，且没有额外样式声明
- **THEN** 文字由禁用灰变为正常前景
- **AND** 同一只读绑定用于交互控件时仍按原输入和禁用政策处理

### Requirement: FL08-TEXT-FONT 数值字号的已采用合同
FontSize SHALL 是 1..96 的无符号十进制正整数逻辑 GUI 字号，不是物理像素。
零、负数、小数、符号、指数、空串、超范围或 int 溢出须拒绝；manifest 创建定位报错，
style 更新报告并保留 last-good，runtime 声明抛 ArgumentOutOfRangeException。
可在 Font 的所有来源声明。最近一个声明 Font 或 FontSize 的层决定描述：该层有 Font 时，
字号只取同层 FontSize；该层只有 FontSize 时，别名继续向外找。Remove 揭示下一来源。
旧 Font 读取器保留别名语义，完整描述／有效字号与来源另行读取；无数字保留原三档路径。
新测量／绘制能力 SHALL 可选，不给旧实现者增加前提；不近似三档、不用 rich-text size 或绘后缩放。
测量、绘制、Auto/SharedLabels、换行、自然行高、编辑／popup、fit、retained 和缓存一律使用
生效字体；不支持用途时回到别名预设，按元素路径／原因去重报告请求值、生效值和用途。
字体环境未知时仍撤销跨同步复用；未请求同步的原 idle-snapshot 条件对数值和旧路径一致保留，
不将无数字视为可靠环境。新入口 SHALL 通过 public-unstable 组合提供，保留旧 Stable 签名、
implements、singleton、旧 override 与合法使用条件；不得借新能力重解释旧 drawing 的显式参数。
标准 Window/Host、独立视觉及 Session/custom widget SHALL 有实际采用路线。
原生 editor glyph SHALL 消费生效描述和最终状态色，不以 placeholder/retained label 代替。
实现与证据边界见[开发映射](../../../implementation.md#appearance-freedom)。

### Requirement: FL08-PAGE-STYLE 页面应用不抹宿主意图
page style SHALL 分开其实际声明与 host 当前声明，撤页面应用揭示宿主现值；不能把一个 Gap 应用提升为整个 density override。显式 ApplyTo 的原 caller-owned-theme 应用条件保留。更新／排列失败 SHALL 恢复精确有效应用及声明来源，而不撤回作者 runtime override 或业务副作用。

#### Scenario: page 只设 Gap3，host 后改 Gap9/RowHeight45
- **WHEN** 页面移除自己的 density 应用
- **THEN** 揭示 host 当前 Gap9/RowHeight45 和当前字体／tint
- **AND** 不恢复 host 创建时全包值

### Requirement: FL08-DESCRIPTION 只描述真实可读事实
七个描述用途 SHALL 分别提供 Text、Heading、Status、Image/Decorative、Decoration、Progress 等实际角色，不为装饰发明可写语义。描述不 Draw／输入／capture／请求。缺绑定、读不可用、已知 null image 与未知来源 SHALL 分开；Progress 保留原值／Min/Max／Finite 及单独夹取 fraction，不改模型。测量与 paint SHALL 使用同一实际文字、字体、宽度、翻译环境。

#### Scenario: progress 值超范围或非有限
- **WHEN** 描述读到实际异常范围值
- **THEN** 原事实与可用显示 fraction 分开报告
- **AND** 不把夹取后的值回写或冒称真实业务值

### Requirement: FL08-IMAGE 图像布局不夺资源寿命
图像 SHALL 以 scope-owned 中性 token 提供，资源由 supplier 创建／更新／销毁；失效／异 scope token 不解析。Fit=Contain完整、Cover填满并clip、Stretch显式变形；inner alignment 是 picture 在槽内，不是 widget 位置。AspectHeight=true 用 allocated width × source height/source width，不加padding，显式 Height 优先。缺失／非法尺寸按[默认包装](../../policies/default-wrappers/spec.md)保槽及 unavailable，不生成 input region。

#### Scenario: RenderTexture 像素内容更新而尺寸不变
- **WHEN** supplier 更新同一有效资源
- **THEN** 可以继续原布局画当前内容
- **AND** 不为动画另建 UI 状态或销毁 supplier 纹理

### Requirement: FL08-VISUAL 独立视觉与局部原生状态
独立视觉 SHALL 可用显式 IUiDrawing、theme、metrics 和中性 geometry，无 Host/XML/registry/reactive 前提。Cover 等局部 clip 及 tint SHALL 在 finally 平衡／恢复，即使 native paint 抛错；合法焦点变化不被当作 paint 状态回滚。此处不提供 GPU retained pixels、整页比例缩放或 world-space renderer。

#### Scenario: Cover 绘制抛错
- **WHEN** 已进入局部 group 并设置 tint
- **THEN** group 与临时 tint 仍恢复，错误交适用外层
- **AND** 不把真实 paint 异常当可忽略 fit 报告异常

### Requirement: FL08-BACKGROUND manifest 与实例背景分权
未设实例 override 时标准页背景 SHALL 跟当前 committed definition；成功更新替换／移除它，拒绝／rollback 不生效。instance Background 可明确设置包含 null，null 恢复原 shell 而不是重新启用 manifest 背景；该 override 不改变定义或别窗。绘制顺序 SHALL 为 workspace base → image → accent/title/subtitle/close chrome → page → popup，内容自己的 surface 可覆盖自身区域；背景无 input target。

#### Scenario: 实例关闭背景后更新 manifest
- **WHEN** 原窗口仍持 explicit null override
- **THEN** 新 definition 不重新打开背景，其他无 override 实例仍跟定义
- **AND** swap/unbind/Close 不 Destroy supplier 资源

### Requirement: FL08-STYLE-SOURCE 样式来源冲突显式报告
Scheme/Density SHALL 是样式应用而非 widget kind；外部样式文档与manifest inline Styles同时供给时，
按既有选择／issue合同显式报告冲突，不静默混成另一权威。ThemeFor SHALL 保原located诊断；
fail-soft style issue 不改变必要measure/paint或业务错误的性质。

#### Scenario: 外部style覆盖同时存在的inline Styles
- **WHEN** Host收到这两个来源
- **THEN** 采用原有外部文档选择并报告被忽略的inline来源
- **AND** 不把未应用的inline声明显示成有效属性源

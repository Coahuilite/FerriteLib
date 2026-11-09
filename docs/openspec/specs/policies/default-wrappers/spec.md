# 默认包装与首版产品政策

## Purpose
规定普通页面可直接使用的完整包装默认，不把这些默认变成自定义／headless 的全部强制策略。业务能力、身份、资格、模型权威仍分别归核心规格；这里不创建新的运行时 owner。

## Requirements

### Requirement: FL08-INVENTORY 首版用途完整
FL SHALL 保留18个注册用途：input/button、input/slider、input/checkbox、input/mode-row、input/dropdown、container/tree、input/number-field、input/text-field、input/stepper-slider、chart/line、text/wrapped、section/header、chrome/banner、state/empty、display/image、chrome/rule、display/progress、Repeat；以及 Stack、Row、Column、Wrap、Overlay、Section、Surface、Scroll、Clip 九种容器。默认内部结构只读，作者不直接实例化内部实现类；真正独立用途可选能力组合，不强制采用标准窗口。

#### Scenario: 只需要一段独立视觉
- **WHEN** 作者不采用 manifest 或默认包装
- **THEN** 仍可通过显式 drawing/theme/metrics 使用视觉核心
- **AND** 库的完整用途表不变成“必须全部采用”清单

### Requirement: E1 默认编辑延迟发布
TextField、NumberField、StepperSlider SHALL 默认 Live=false，TextField Multiline=false；明确 Live=true 合法。有效合格 pending Confirm 只请求一次；未修改或已 Live 发布且无新依据时不重复。正常回调返回后结束该稿并显示实际模型，包括拒绝／归一化结果，不凭请求推断保存。

#### Scenario: 业务拒绝有效确认
- **WHEN** setter 正常返回但模型保持原值
- **THEN** 本次确认已结束，显示实际模型
- **AND** 不永久 pending 或在重绘中重试

### Requirement: E2 普通 blur 与不合格结束分开
普通有效且无 conflict 的 blur SHALL 确认；invalid/conflict SHALL 保稿并允许焦点离开。隐藏、失资格、popup takeover、移除、abort、Close 等 SHALL 不冒充普通 blur 补提交。明确参与组合的 capture／动作应先声明，再做普通 blur。自定义政策可以明确选择其他结束策略，不要求开放全部内部 flags。

#### Scenario: 未提交有效稿被可靠隐藏
- **WHEN** 同一目标仍保留
- **THEN** 保稿但不发 blur 请求；显示时核对实际模型

### Requirement: E3 无效文字不得变业务零值
数字编辑 SHALL 保留原始 invalid draft 和原因，不写0／旧值冒称成功；文本空串默认有效，除非作者明确 parser／约束拒绝。numeric codec 使用有限值、invariant parsing／0.## 默认格式，范围和 Step 明确，不因模型越界就自动修复模型。中性 UiNumericEdit 保原 reversed bounds 的 Clamp→min 条件，不追加 min≤max 的新采用前提。

#### Scenario: 数字稿为非有限或不能解析
- **WHEN** 用户确认
- **THEN** 不请求写值，保原文字／原因，仍可离焦或 Cancel

### Requirement: E4 conflict 保稿并停止自动发布
未修改稿 SHALL 跟随模型；修改稿遇不同模型 SHALL 标 conflict、暂停 Live 和普通 blur。明确 Confirm 可覆盖已知当前依据，不额外制造自动双确认；错误／conflict 使用现有 Danger／Selected 语义提示。live 10→20→10 是两次真实请求，不被“最终没变”吞掉；IME 中间态不 Live。

#### Scenario: 外部把模型由10改12，当前稿20
- **WHEN** 自动 blur 或绘制发生
- **THEN** 保20及 conflict，不自动写20
- **AND** 用户明确 Confirm／Cancel 仍各有自己的含义

### Requirement: E5 Cancel 与异常不倒退已生效业务
Cancel SHALL 只丢本地稿并读取最近模型，不 Undo 已 Live 的写入。失败 Live/Confirm/TakeOver SHALL 保错误、暂停自动 Live／blur，不标旧稿已发布；用户下一明确 Confirm 可是新请求。正常返回须先结束，再读展示，因此展示异常不能重放。

#### Scenario: Live 已发布20再 Cancel
- **WHEN** 用户取消本地编辑
- **THEN** 采用最近实际模型，不请求回写原10

### Requirement: F1 Button 松开、Slider 实际选值
Button SHALL 在合格主键按下后、仍合格且有效区域内释放时请求一次，移出释放／失资格零次。Slider SHALL 按真实 press 选值和改变的 drag sample 即时请求，不等 release、不借旁边 NumberField 草稿；Repaint 不重发，拖出按 owned capture 规则继续／结束。语义激活来源可独立接入，不伪造 pointer 状态。

#### Scenario: Button 按下后移出释放
- **WHEN** 原有 capture 合法结束
- **THEN** 零业务请求且 capture 释放

### Requirement: F2 离散部件释放一次且保持部件优先
Checkbox 两 look、ModeRow、Dropdown trigger/options、Tree disclosure/check/body SHALL 用合格主释放各成立一次。Checkbox 明确 ActionBind 才在 Set 后 Invoke，Set 抛错不 Invoke；普通 bool 不隐含 command。Tree 消费业务提供的 flat keyed rows，部件动作不再同时触发 body。typed choices 保真实值身份；同值明确选择仍请求一次。

#### Scenario: Tree disclosure 被点击
- **WHEN** 对应部件合格释放
- **THEN** 只发 expansion 请求，业务决定后续行结构
- **AND** 不同时选择 body 或由 UI 自行修改业务展开状态

### Requirement: F3 popup 外部按下关闭并消费
打开的菜单 SHALL 在合格外部主按下时关闭、消费该手势，不让其后 release 激活下层。菜单／content pass 之间 capture 归明确 menu option，不以 key 前缀猜测。option 成立后先 close 再请求，不自动回焦不存在的 trigger；普通非重叠 outside click 不一概变成 popup 式挂起。

#### Scenario: popup 盖着另一 Button
- **WHEN** 用户点击菜单之外
- **THEN** 只关闭并消费，不执行下层 Button

### Requirement: N1 组合 Step 基于有效稿接替
model10、valid modified draft20、Live=false、Step1 时，plus SHALL 只请求21并结束稿，不先写20。invalid draft 不能 Step；没有稿则用实际模型。每步仍按真实范围和资格，不把旧模型越界自动修复。

#### Scenario: 点击 plus
- **WHEN** 上述已确认组合状态成立
- **THEN** 一个请求21，没有20的中间请求

### Requirement: N2 真实范围值直接替代稿
同一参与组合的 Slider 明确选30 SHALL 只请求30，不先20；thumb capture 但无实际选值 SHALL 不清稿。

#### Scenario: 抓住 thumb 后未改变值就结束
- **WHEN** 只是 capture 变化
- **THEN** 原稿仍保留，不能将 capture 本身当 TakeOver

### Requirement: N3 接替不扩散至其他控件
独立 Button 或同 Bind 的另一 field SHALL 不清本编辑器稿；只有明确参与组合的 Step/Range/SuspendEditor 接缝可按既定转换接替。

#### Scenario: 相邻独立按钮执行
- **WHEN** 它并非本组合参与者
- **THEN** 不借 Bind 相同或视觉相邻取得清稿权

### Requirement: FL08-EDITOR-KEYS 基础文本采用原生编辑键
单行 Return SHALL Confirm；多行 Return 插入换行、Ctrl+Return Confirm，仍服从 composition 优先。基础多行 SHALL 真正换行并提供可达的滚动／导航／选择／剪贴板；只读保选择复制而禁止 mutation。新增库级结构 focus traversal、Button Enter/Space、Checkbox Space、Slider increment/Home/End 属 K1 已批准延后，不计已实现；原生 editing/IME 及现有 Accept/Cancel 未延后。

#### Scenario: 多行编辑按 Return
- **WHEN** 非 composition 处理且未按 Ctrl
- **THEN** 插入换行，不提交或关窗

### Requirement: FL08-CANCEL-LADDER 现有钩子逐层一次处理
现有 Cancel SHALL 先让 composition 保留其键，再按 popup → owned pointer capture → active editor discard → 明确／最后交互 subject 向上最近可执行 CancelBind → 未处理 shell/host native convention。每个已处理层消费一次；Accept 只到适用 active editor，无 editor 时 native owner 保原含义。业务 Save、close veto 不等于 UI Cancel；不新建全局 key router。

#### Scenario: popup 与编辑同时存在
- **WHEN** 非 composition Cancel 到达
- **THEN** 首次只关闭 popup；不同时丢稿／执行返回命令

### Requirement: FL08-COLLECTION-DISPLAY 缺失值、描述与图表保义
Dropdown/ModeRow SHALL 不修复 missing current；string fallback 按 exact value → legacy display → 原 current text，typed fallback 保空。七描述用途 SHALL 按[外观能力](../../core/appearance/spec.md)只读事实。chart 可只读／editable；点在 normalized 范围映射，press 命中6逻辑单位圆取得 capture但不请求，变化 drag 以0.0001坐标 tolerance 判变化再请求，Repaint／release不重发。

#### Scenario: 选中值不在 choices
- **WHEN** 菜单仅绘制或观察
- **THEN** 报 Missing，显示 fallback，不选第一项回写

### Requirement: FL08-POINT-IDENTITY 图表稳定点与 legacy 条件分开
PointKeysBind 可选；提供时 SHALL 与点数相同、唯一非空，以 Key+当前 Index 发请求，删／替换 key 结束 capture。同 sibling traversal 不取消 owner。未提供 key 的 legacy index 路径 SHALL 合法且无新注册前提：精确自身 echo／未改拒绝可保 capture，不明 count／坐标替换结束；不可区分的同长业务重排不承诺身份保持。

#### Scenario: keyed 点重排
- **WHEN** 原 key 仍存在且当前 index 改变
- **THEN** capture 跟 key，payload 给当前 index，而不是旧位置

### Requirement: FL08-DEFAULT-LOOK 默认度量和图像政策
默认 font SHALL 为 Small，banner 无真实 font 声明才 fallback Tiny；默认 Padding6、Spacing4、Gap6、RowHeight28、Hairline1 均为逻辑单位，非硬最小尺寸，字号不乘 density。Image 默认 Contain/Center/Center；未开 AspectHeight 保原正 source height + 两倍padding，开后缺失／非法尺寸用 row fallback；显式 Height 总保槽。窗口背景 BG1 默认 Contain居中、露原 base；Cover 只显式选用。

#### Scenario: 图片缺失
- **WHEN** 已明确槽高且 token 不可用
- **THEN** 保作者槽及 unavailable，原 chrome／base 可见，不发 input target

### Requirement: FL08-POPUP-GEOMETRY popup 是有限可滚动行而非无限窗口
共享 popup SHALL 用24逻辑单位整行、每 notch3行；有限 viewport 能放几行就画几行，最低一行，因此 viewport小于24时该行可能越边；未发布／非正高度视口保原向下退路。先尝试 trigger下，再能完整容纳的上方，否则顶齐viewport；右溢时左移但不缩宽，不承诺超宽／左越界通用 clamp。行绘制、hover、hit、offset clamp SHALL 用同一当前行窗口，只消费可见项。共享popup／下拉菜单目前没有 scrollbar/thumb/位置指示保证；筛选／分页属于消费者业务。

#### Scenario: 列表变短且偏移过大
- **WHEN** 取得新 count 并计算本次行窗口
- **THEN** 先夹取 Session offset，再使用同一索引画与命中
- **AND** 不因增加滚动提示而要求全量重建布局或逐项每GUI量宽

### Requirement: FL08-WINDOW-DEFAULTS window 选项不替代原生后端
默认 chrome SHALL 为 Margin0、title56、side/bottom20、accent3逻辑单位，可通过既有接缝定制；InitialSize 首次放置，NormalSize 在 PreOpen 重用尺寸，不提供持续 fit 或持久位置恢复。nullable pause/camera/absorb/drag/resize/close 选项未设时保原生值；InitialSize/NormalSize 默认null。AllowMultipleInstances=true、SetFocusOnActivate=true，FocusPolicy 默认FollowClicks，另有OpenOnly/Manual。false 多实例是原生 exact C# type 去重，不是 Catalog key 去重。标准 window 的尺寸不是 US 的864×640，也不保证任意缩放／旧矩形可达边界。

#### Scenario: 两种 Catalog key 共用 UiPageWindow 类
- **WHEN** 允许多实例且 key 不同
- **THEN** 不因 exact-class 去重意外驱逐另一窗口
- **AND** Catalog active 状态不冒充原生 z-order 或全屏输入孔能力

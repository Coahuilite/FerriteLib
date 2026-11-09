# 帮助主题与可选输出连接

## Purpose
将实际 hover 主题交给消费者自己的帮助面板；Session 主题、显式 Tooltip 文本、可观察每控件输出是不同连接。此连接不读取业务帮助目录、不决定 pinned topic，不增加统一面板采用前提。

## Requirements

### Requirement: FL08-HELP 主题是不透明引用
元素 HelpRef SHALL 表达一般主题，choice 的明确非空 HelpRef 可覆盖；空或省略 SHALL 不从 Value/Text 推断，仍可回一般元素主题。TextKey 只解析 label。Tooltip SHALL 消费调用者给的可展示文字，不能把 HelpRef 当同名 binding／翻译键。CurrentHelpRef/Source 与 PublishHelp 维持现有 advanced 采用条件，不提升整个 Session。

#### Scenario: hovered option 没有专属帮助
- **WHEN** 元素有一般 HelpRef，当前选项未给 HelpRef
- **THEN** 保留一般主题／消费者 overview fallback
- **AND** 不把 typed 值或 label 自动当帮助正文

### Requirement: FL08-HELP-GRACE 保留现有 pass 时序
HelpGracePasses SHALL 用既有 Session pass 算法：首个无 live claim 边界重置 live，随后在配置次数内呈现 held；新 live publisher 立即覆盖 held。库默认0，消费者15不是库默认。此处 pass SHALL 不改成秒、tick、render frame 或“只有新发布才延寿”的另一个算法。

#### Scenario: held 帮助期间出现新 live claim
- **WHEN** 新 publisher 提供有效主题
- **THEN** 立即以新 live 主题／来源为准，不等旧 held 期结束

### Requirement: FL08-HELP-OUTPUT 可选输出只撤最后仍归属的发布
HoveredOptionHelpOutputBind SHALL 在采用时指向 writable string endpoint，创建期拒绝缺失／只读；不采用仍可用 Session 帮助。写入 change-detected；owner-bound endpoint 须保留到旧连接清理，不因 proxy 换 backing 把清理写向新 owner。关闭 popup、空／无 hover、可靠隐藏、移除、dispose、成功 backing switch SHALL 仅撤本 publisher 最后仍拥有的发布；另一 publisher 即使写同 topic，或消费者 Set/announcement 后，不得被旧清理擦除。未公告 ordinary getter 变动仍保原通知条件。

#### Scenario: 同地址另一控件发布同字符串
- **WHEN** 旧 publisher 随后离开／关闭
- **THEN** 不清除新 publisher 的发布
- **AND** 共享输出不被声称为无条件当前 hover 镜像

### Requirement: FL08-HELP-RELEASE 清理失败不重放选择
输出清理 SHALL 保原 setter／通知／纯度和[逐项释放与 seal](../definition-update/spec.md)合同；已完成 selection 不因 withdrawal 失败重放，已提交定义不因输出清理失败被伪回滚。选择业务失败与清理失败应各保原因。Close 自身仍在逻辑关闭后聚合 cleanup。

#### Scenario: selection 成功后 withdrawal 失败
- **WHEN** 业务请求已返回且 popup 已关
- **THEN** 只报告 cleanup 问题，不在 paint／Close 再做选择

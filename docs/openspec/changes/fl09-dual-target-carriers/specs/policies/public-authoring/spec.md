# MODIFIED Requirements — 采用来源，非 current

### Requirement: FL08-CARRIER 单载体与版本轴明确
仅 coahuilite.ferritelib SHALL 分发 FerriteLib.UiKit.dll，消费者不捆绑第二份，并在自己的入口
Require 已测 [min,max)；游戏 modDependencies 不能表达FL API区间。
0.9 SHALL 支持 RimWorld1.6/net472 与 RimWorld1.7/net10.0 两个目标，保原1.6默认编译入口和显式1.7入口。
Neutral为独立非游戏目标。0.8的既有支持和维护线不由本条0.9变更删除或替换。

游戏包 SHALL 按实际目标放在 `1.6/Assemblies/FerriteLib.UiKit.dll` 或 `1.7/Assemblies/FerriteLib.UiKit.dll`；
双目标包可同时含两份，但同一游戏的有效 LoadFolders SHALL 只选自身目标，不能在共享根放另一份 DLL，
也不能把 net10 重标为1.6或让1.7静默载入net472。单目标包只公告实际包含的游戏版本；双目标包公告并准确选择两者。
staging／export SHALL 检查真实程序集 configuration、TFM、目标游戏引用及来源身份，不能仅信路径、XML标签或调用参数；
双目标载荷 SHALL 有相同源码身份。目标错误、缺项或来源不相容 SHALL 在替换已交付包之前拒绝。

Api、About modVersion、VersionPrefix major/minor SHALL 一致，本0.9线为0.9.0；
InformationalVersion的源码身份不是兼容轴。闭合包为 About/About.xml、LoadFolders.xml、LICENSE、version.txt
和所选目标的 DLL；版本收据区分各目标真实配置／身份。不带PublishedFileId、PDB、游戏程序集或游戏内容。
不加 Def/Patch/Languages/textures 内容。公开消费取实际发布资产；本地fixed input、工程分支、未来tag/mainSHA
与发布不能互冒，普通Release构建或归档不等于游戏／双版本原生验收。

#### Scenario: 运行环境有两份 DLL
- **WHEN** Require 枚举已加载载体
- **THEN** 给可读冲突诊断，不能依加载序默认为消费者选中正确版本

#### Scenario: 同一包按游戏版本选择
- **WHEN** 双目标包分别由1.6与1.7读取
- **THEN** 每次只选择匹配TFM及游戏目标的那一份DLL
- **AND** 单目标包不公告缺失版本，不靠根目录或跨版本回退补缺

#### Scenario: 构建产物与目标标签不符
- **WHEN** stager或export收到错误TFM、非匹配配置或不同源码身份的双目标载荷
- **THEN** 在替换交付前拒绝，原完整包保持
- **AND** 不修改标签、删除校验或复制游戏依赖以冒充支持

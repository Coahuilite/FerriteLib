# 手工维护规格与真实变更

从[现行入口](../specification.md)找唯一责任，再读本目录的[来源覆盖](source-coverage.md)和[实现现状](implementation.md)。
这里是纯 Markdown 规划资料，不需要安装／执行 OpenSpec；没有 apply、runtime owner 或 root agent 集成。

## 写现行要求

采用清晰中文及固定结构：Purpose、Requirements、Requirement、Scenario；要求使用可观察 SHALL/MUST，
场景用 WHEN/THEN，必要时 GIVEN/AND。每条写清条件、操作、结果及禁止结果，不以类名或方法清单代替语义。
Scenario 是规格 prose，不自动变成新增执行用例。

能力页持自身状态／转换，连接页持双方出入结果，adapter 页持平台事实，policy 持产品默认。
源码符号、版本、偏差和证据进 implementation／source-coverage，不从现有 bug 反写支持条件。
已有合同补录注明来源，纯恢复不造行为 delta；发现新保证只能放真正 ADDED 候选并等待合同决定。

## 从一个完整边界开始改变

proposal 应短而实：目标／不改什么、能力／连接／owner、真实 caller → entry → consumer、
改变的状态／publication／failure／release 边、公开／原生／跨库影响、可复用证据与缺口。
design 只解释决定性机制、反例、兼容／失败取舍；tasks 直到获得实施授权和实际完成才勾选。
MODIFIED 必须保完整要求及全部仍适用场景，不能只贴补丁片段。

可正式提出内部扩展、独立模块、新核心或责任迁移；以真实 need/gap、authority/lifetime/thread/clock、
最小完整切片、adoption/exit 对账。详见[正向路线](specs/policies/public-authoring/spec.md)。
不设新治理等级／文件数／双消费者门槛，也不把旧 owner 变成无限否决者。
责任迁移须同步实际 callers、恢复／清理与唯一 current，仍受真实兼容约束。

## 读取和证据

保存来源 ID、关键值／单位、合法条件和当前偏差；可复用证据须保原 candidate／layer。
文档／链接／结构检查只证明文本，不证明源码满足或 native 已接受。
本次没有 CLI 运行证据；早先隔离小样的工具结果不能转签本目录。

书写约定参考固定公开来源
[studyzy/OpenSpec-cn](https://github.com/studyzy/OpenSpec-cn/tree/b4a6bd99d6b7b12cfabb1dd0192091475107f69e)。
本文及规格为独立编写，未复制上游模板正文／脚本；不要求运行其工具或激活 agent skills。

# FerriteLib 0.5.x 团队执行与交付文档

本目录是 FerriteLib（下称 FL）0.5.x 的执行与交付面。台账规则不变：`AGENTS.md` 稳定、`MEMORY.md` 唯一易变账本、
`TODO.md` 行动面；本目录只承载**本轮的工作包、接口说明、消费者接入说明与证据表**，不另立竞争性状态源。

分支：`0.5.x`。基线：`c53bd37`（`0.4.x` / `origin/0.4.x` 同点）。契约轴本轮为 **0.5.0**。

## 阅读顺序

| 文件 | 内容 |
| --- | --- |
| [00-baseline.md](00-baseline.md) | 准确基线、取代关系、原版边界、API 窗口 |
| [10-work-packages.md](10-work-packages.md) | 工作包、依赖图、文件所有权、集成顺序、状态 |
| [20-api-and-xml.md](20-api-and-xml.md) | 公共 API / XML 使用说明与示例 |
| [30-consumer-handoff.md](30-consumer-handoff.md) | 面向消费者团队：版本区间、接入、迁移、已知限制 |
| [40-verification.md](40-verification.md) | 自动化验证结果、实机检查清单、未完成项 |
| [50-dev-package.md](50-dev-package.md) | 可用开发包位置、对应源码版本、构建命令 |

## 证据分级（全文强制）

任何一条陈述必须落在下列五类之一，禁止越过：

1. **已实现** —— 代码在树里，编译通过。
2. **已自动化验证** —— 本仓 harness/门禁在某个 revision 上跑绿，且写明该断言是"变更证明"还是"未来回归护栏"。
3. **已由真实消费者接入** —— 另一个仓库的代码在跑，需 `owner/repo@sha:path:line` 转录。
4. **已实机验证** —— 在真实游戏里被观察，需写明存档/路径/次数/观察者。
5. **尚待外部团队验证** —— 库侧已就绪，等待消费者或实机确认。

构建通过、打包成功、库内示例运行，**都不等于** 3 或 4。本仓自己的示例页永远不能抬高"已验证公共面"的计数
（`AGENTS.md` "Our own demo is not consumption"）。

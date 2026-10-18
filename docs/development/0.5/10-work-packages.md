# 0.5 工作包 — 已结束轮次的索引

原来的 worktree 分工、合并排程和中间任务状态已结束。本文件只保留 P 编号的含义和其合同/证据入口，
不为当前工作指派写者，也不恢复历史发布或提交权限。

| 包 | 交付内容 | 合同与证据 |
| --- | --- | --- |
| W0 | 基线、版本轴与跨包合同 | [00-baseline.md](00-baseline.md) |
| P1 | keyed 窗口、选项、生命周期和通用 XML 页壳 | [API/XML](20-api-and-xml.md)、[P1 验证](verification/p1-adversarial.md) |
| P2 / P2b / P2c | 按 key 通知、失效分类、命令资格、显隐和节点剪枝 | [API/XML](20-api-and-xml.md)、[P2 验证](verification/p2-adversarial.md) |
| P3 | keyed Repeat/Templates、checkbox、progress、tree | [API/XML §7](20-api-and-xml.md)、[最终整合](verification/final-integrated.md) |
| P4 / P4b / P4c | 文档来源、原子候选、last-known-good、监听和手动重载 | [API/XML](20-api-and-xml.md)、[验证索引](verification/README.md) |
| P5 | 每 host/session 诊断归属、有界事件和释放 | [最终整合](verification/final-integrated.md) |
| P6 | 独立 review、反例验证、开发包与实机清单 | [40-verification.md](40-verification.md)、[50-dev-package.md](50-dev-package.md) |

关键整合修正已经进入对应合同：删除元素要剪枝，隐藏不等于删除；查找按身份而非是否排布；通用页壳
通过 `PageHost` 暴露诊断接入；控件输入仍需覆盖禁用资格；文档候选的验证/回退不能丢草稿；壳的 fit
发现须归属 host。R1–R7 的后续修复见验证记录。

旧提交身份脚本和逐步合并指令不再分发为操作建议。当前协作及隐私要求分别由根 `AGENTS.md` 和
[push-privacy-gate.md](../../push-privacy-gate.md) 持有。

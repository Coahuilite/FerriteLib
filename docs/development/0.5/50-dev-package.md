# 50 开发包与源码版本

## 1. 构建与打包（本仓命令）

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev
```

- 产物目录：`dist/dev/FerriteLib/`（目录，不是压缩包）。
- `version.txt` 携带 `build=`、`commit=`、source 行——**这是判定"开发包对应哪次提交"的唯一依据**，
  不要凭文件名或时间戳推断。
- Dev 通道要求 Dev 配置字节（stager 从程式集读 `AssemblyConfigurationAttribute` 并拒绝不匹配的通道），
  因此 **Dev 产物不等于 Release 发布字节**。
- **本仓没有任何脚本会把 Mod 放进游戏目录**；安装是开发者自己的步骤。

## 2. 本轮的开发包注册表

| 包 | 内容 revision | 对应源码提交 | 含包 | 位置 |
| --- | --- | --- | --- | --- |
| 第一个开发包 0.5.0-dev | `0.5.0-dev` | 见 `dist/dev/FerriteLib/version.txt` 的 `commit=` 行（构建时树必须干净；带 `-dirty` 表示构建时存在未提交改动，则该包不对应任何提交） | P1 + P1h + P2 + P2b + P4 + P4b | `dist/dev/FerriteLib/` |
| 第二个开发包 | 待生成 | 待生成 | 加 P3 + P5 | `dist/dev/FerriteLib/` |

第一个开发包的实测 `version.txt`（构建于 `0ee55b6`，当时工作树因未提交的文档改动而带 `-dirty`，因此**不作为交付版本**，
只作流程记录）：

```
FerriteLib 0.5.0-dev
build=dev
commit=0ee55b665c39-dirty
source https://github.com/Coahuilite/FerriteLib
```

**交付用的开发包必须由干净树构建**：先提交全部改动，再运行 `-PackDev`，使 `commit=` 与 `git rev-parse HEAD` 完全相等。
本目录的注册行在该包生成后回填。

填写规则：`commit=` 必须与 `git rev-parse` 一致；同时记录 `version.txt` 的完整内容摘要，
不得只写"最新"。如果工作树是 dirty 的，dev 标签会带 `-dirty`，必须照实登记。

## 3. 给消费者团队的交付物

1. 开发包目录（或由消费者按上述命令自行构建同一提交）。
2. [30-consumer-handoff.md](30-consumer-handoff.md)：版本区间、接入步骤、迁移、已知限制。
3. [20-api-and-xml.md](20-api-and-xml.md)：API/XML 说明与示例。
4. `1.6/Assemblies/FerriteLib.UiKit.dll` 与 `tools/.../Stubs/`（后者是事实上的已发布面，
   重命名或移动会让消费者 harness 断裂而本仓门禁全绿）。
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
| **第二个开发包（当前交付版本）** | `0.5.0-dev` | `ef133b3067da9ce6e52f20c59f7c3863b26920b8`（`ef133b30 67da`） | P1+P1h+P2+P2b+P2c+P3+P4+P4b+P4c+P5+task-13+task-14（全部包） | `dist/dev/FerriteLib/` |

**当前交付版本（干净树构建，`commit=` 与 `HEAD` 逐字符相等）**：

| 项 | 值 |
| --- | --- |
| 标签 | `0.5.0-dev` |
| 源码提交 | `ef133b3067da9ce6e52f20c59f7c3863b26920b8` |
| 位置 | `dist/dev/FerriteLib/`（5 个文件：`About/About.xml`、`1.6/Assemblies/FerriteLib.UiKit.dll`、`LoadFolders.xml`、`LICENSE`、`version.txt`） |
| 构建 | `pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev`（10 门禁全绿后才 staging） |
| `version.txt` | `FerriteLib 0.5.0-dev` / `build=dev` / `commit=ef133b3067da` / `source https://github.com/Coahuilite/FerriteLib` |

**这是 Dev 配置字节，不是发布字节**：stager 从程序集读 `AssemblyConfigurationAttribute` 并拒绝通道不匹配的字节；
消费者若要与发布资产对齐，请按同一提交自行构建 Release，或以之后授权的 release 通道产物为准。

**交付用的开发包**（干净树构建；`commit=` 与 `git rev-parse HEAD` 逐字符相等）：

| 项 | 值 |
| --- | --- |
| 标签 | `0.5.0-dev` |
| 源码提交 | `de5a56536afcb7fac7bfd9e0518380bc61491e77`（`de5a56536afc`） |
| 位置 | `dist/dev/FerriteLib/`（目录，5 个文件：`About/About.xml`、`1.6/Assemblies/FerriteLib.UiKit.dll`、`LoadFolders.xml`、`LICENSE`、`version.txt`） |
| 含包 | P1 + P1h + P2 + P2b + P4 + P4b |
| 构建命令 | `pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev`（门禁全绿后才 staging） |
| `version.txt` | `FerriteLib 0.5.0-dev` / `build=dev` / `commit=de5a56536afc` / `source https://github.com/Coahuilite/FerriteLib` |

流程记录（**不是交付版本**）：第一次 staging 得到 `commit=0ee55b665c39-dirty`，原因是构建时工作树里还有未提交的文档改动；
这正是 dev 通道"脏树必须自曝"的行为，保留在此说明为什么不能拿它当交付包。

**交付用的开发包必须由干净树构建**：先提交全部改动，再运行 `-PackDev`

填写规则：`commit=` 必须与 `git rev-parse` 一致；同时记录 `version.txt` 的完整内容摘要，
不得只写"最新"。如果工作树是 dirty 的，dev 标签会带 `-dirty`，必须照实登记。

## 3. 给消费者团队的交付物

1. 开发包目录（或由消费者按上述命令自行构建同一提交）。
2. [30-consumer-handoff.md](30-consumer-handoff.md)：版本区间、接入步骤、迁移、已知限制。
3. [20-api-and-xml.md](20-api-and-xml.md)：API/XML 说明与示例。
4. `1.6/Assemblies/FerriteLib.UiKit.dll` 与 `tools/.../Stubs/`（后者是事实上的已发布面，
   重命名或移动会让消费者 harness 断裂而本仓门禁全绿）。
**关于标签与本记录的关系（照实说明）**：包由干净树 `ef133b3` 构建；其后提交的是**文档**（本目录与 `MEMORY.md`/`TODO.md`），
不改变任何源码或载荷字节。因此在 `ae68a0e` 上重新执行同一命令会得到同样内容的 DLL，而 `version.txt` 会写明 `ae68a0e`；
两个标签指向同一份载荷，差别只在文档。需要与消费者对齐时，请以 `version.txt` 的 `commit=` 为准并说明该差异。

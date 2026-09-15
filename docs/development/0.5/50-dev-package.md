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
| （第一个开发包） | 待生成 | 待生成 | P1+P2+P4 | `dist/dev/FerriteLib/` |
| （第二个开发包） | 待生成 | 待生成 | P1–P5 | `dist/dev/FerriteLib/` |

填写规则：`commit=` 必须与 `git rev-parse` 一致；同时记录 `version.txt` 的完整内容摘要，
不得只写"最新"。如果工作树是 dirty 的，dev 标签会带 `-dirty`，必须照实登记。

## 3. 给消费者团队的交付物

1. 开发包目录（或由消费者按上述命令自行构建同一提交）。
2. [30-consumer-handoff.md](30-consumer-handoff.md)：版本区间、接入步骤、迁移、已知限制。
3. [20-api-and-xml.md](20-api-and-xml.md)：API/XML 说明与示例。
4. `1.6/Assemblies/FerriteLib.UiKit.dll` 与 `tools/.../Stubs/`（后者是事实上的已发布面，
   重命名或移动会让消费者 harness 断裂而本仓门禁全绿）。

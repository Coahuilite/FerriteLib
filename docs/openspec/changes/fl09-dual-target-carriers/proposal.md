# 0.9 双目标载荷：正式采用

状态：用户于2026-11-14明确0.9起支持RimWorld1.6与1.7，并授权规格先行后实现本次最小接线。
采用提交 `5d12091` 仅含文稿，先于包／配置接线和构建。唯一 current 为 public-authoring 的 FL08-CARRIER
及 developer-tools 的 DEV-07；本目录只保变更来源，后续实际进度见[实现映射](../../implementation.md)。

## 实际缺口与选择

0.9已有普通 net472（默认）和显式 RimWorld17/net10.0 两个编译入口；但元数据只选1.7，
旧 stager/exporter 固定1.6且 stager拒绝1.7-only load map。仅增加 supportedVersions 会让包名与实际
载荷不符，不能交付双版本。选择保留两个编译入口，按真实 TFM／游戏引用检查后分目录选择和归档。

同一0.9包可包含两个目标载荷，游戏只加载自身版本目录中的一个 FerriteLib.UiKit.dll。
原单目标1.6脚本默认和闭合文件集保留；显式选择1.7或 Dual，单目标包只公告实际包含的版本。
staging／export须先查真实 configuration、TFM、游戏引用和来源身份，再复制并核完整文件集；
Dual须同一源码身份。错误在替换旧交付前拒绝。Neutral和游戏依赖不进游戏包。

原1.7源码／编译目标不降回1.6，不把 net10 DLL换标签放入1.6目录；0.8线不编辑。
本次只扩0.9包／脚本参数，无C# API、版本区间、业务／输入／关闭语义改动。
本地普通Release构建及试分发不创建tag、GitHub Release或Steam发布，不替代1.7游戏／165FPS验收。

## 采用与执行

[完整 MODIFIED delta](specs/policies/public-authoring/spec.md)先并入唯一 current，入口同步说明
0.9双目标及采用时未实施状态；然后才改 About／LoadFolders、原 stager／exporter和必要调用者。
使用既有检查、两个真实目标的普通Release及实际包检查，不新增单测／case／等价probe。
任务与证据归 [tasks](tasks.md)，不上传原生原件、私有路径／标识或游戏DLL。

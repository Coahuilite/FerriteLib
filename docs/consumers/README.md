# Consumers — choose the version you use

Start with the [0.8 RC guide](consume-from-0.8.0-dev.md) for the published 0.8.0-rc1 prerelease,
including shared behavior, RU1 sources, RU2 scoped stage reuse and RU3 managed failure/view lifetime.
Consumers pinned to 0.7 should use the [frozen 0.7 guide](consume-from-0.7.0.md).

| Need | Document |
| --- | --- |
| Package choice, version assertion, 0.8 migration | [Consume from 0.8.0-rc1](consume-from-0.8.0-dev.md) |
| A plain settings page without a custom widget | [Ordinary settings recipe](ordinary-settings.md) |
| Native window sizing, UI scale, compact layouts and menu entry points | [RimWorld window guide](rimworld-window-layout.md) |
| Compatibility by exported type | [API tiers](../api-tiers.md) |
| Supported author paths, lifecycle and windows | [0.8 public contract](../development/0.8/public-contract.md) |
| A consumer still pinned to 0.7 | [Versioned 0.7 guide](consume-from-0.7.0.md) |
| A consumer still pinned to 0.6 | [Versioned 0.6 guide](consume-from-0.6.0.md) |

Only `coahuilite.ferritelib` ships `FerriteLib.UiKit.dll`. Consumers compile against the selected package
with `Private=false`, assert their tested range, and do not bundle another DLL. Registry scope must match
`UiHost.Source`; a page identity is not necessarily a packageId.

The [v0.8.0-rc1 asset](https://github.com/Coahuilite/FerriteLib/releases/tag/v0.8.0-rc1) is the current
published runtime distribution. These author guides travel with the versioned source tree. A named
local rehearsal remains its own candidate, not a second identity for that release.

## Historical consumer-impact evidence

The [2026-09-25 NGS report](../handoff-ngs-uikit-delta-2026-09-25-zh.md) retains its comparison baseline,
artifact observations and limits. Its duplicated API lists have been replaced by the versioned contracts;
its old wording about palettes, line state and unpublished artifacts must not be used as current guidance.

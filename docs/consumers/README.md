# Consumers — choose the version you use

Start with the [0.7 consumer guide](consume-from-0.7.0.md) for the frozen implementation. The
[0.8 development guide](consume-from-0.8.0-dev.md) covers the isolated 0.8 candidate, including shared behavior, RU1 sources, RU2 scoped stage reuse and RU3 managed failure/view lifetime; it does not replace the frozen guide for 0.7 consumers.

| Need | Document |
| --- | --- |
| Package choice, version assertion, 0.7 migration | [Consume from 0.7.0](consume-from-0.7.0.md) |
| A plain settings page without a custom widget | [Ordinary settings recipe](ordinary-settings.md) |
| Compatibility by exported type | [API tiers](../api-tiers.md) |
| Manifest, style, binding, tabs, repeat and windows | [0.5 base usage](../development/0.5/20-api-and-xml.md) |
| Lifecycle, notifications, reload scheduling and catalog | [0.6 additions](../development/0.6/20-api-and-xml.md) |
| A consumer still pinned to 0.6 | [Versioned 0.6 guide](consume-from-0.6.0.md) |

Only `coahuilite.ferritelib` ships `FerriteLib.UiKit.dll`. Consumers compile against the selected package
with `Private=false`, assert their tested range, and do not bundle another DLL. Registry scope must match
`UiHost.Source`; a page identity is not necessarily a packageId.

The 2026-09-24 backup-only publication policy was superseded by a conditional RC decision on 2026-10-09;
see the [0.7 line record](../development/0.7/README.md). This is not a claim that an RC asset currently exists.
When a release is published, its asset is the supported public distribution. A named local rehearsal remains
its own candidate, not a second identity for that release.

## Historical consumer-impact evidence

The [2026-09-25 NGS report](../handoff-ngs-uikit-delta-2026-09-25-zh.md) retains its comparison baseline,
artifact observations and limits. Its duplicated API lists have been replaced by the versioned contracts;
its old wording about palettes, line state and unpublished artifacts must not be used as current guidance.

# Use FerriteLib 0.8.x

Begin with the [0.8 RC guide](consume-from-0.8.0-dev.md). Choose only the capabilities your UI needs:
ordinary page, custom composition, native no-Host controls, headless semantics or independent visuals.
The [current specification](../specification.md) gives each guarantee one home.

| Task | Document |
|---|---|
| Select a fixed package and assert its range | [0.8 consumer guide](consume-from-0.8.0-dev.md) |
| Ordinary page without a custom widget | [Settings recipe](ordinary-settings.md) |
| Window size, UI scale, compact arrangement and native entry | [Window guide](rimworld-window-layout.md) |
| Supported public entry and lifecycle | [S1–S7](../openspec/specs/policies/public-authoring/spec.md) |
| Exact type/member compatibility | [API tiers](../api-tiers.md) |
| Source status, known gaps and evidence | [Implementation map](../openspec/implementation.md) |

Only coahuilite.ferritelib ships the DLL. Reference the selected input with Private=false and assert its
tested API range. Registry scope matches UiHost.Source; a page identity need not be a packageId.
The published asset is [v0.8.0-rc1](https://github.com/Coahuilite/FerriteLib/releases/tag/v0.8.0-rc1).
A local preparation candidate is not that release or a future main SHA.

## Internal archive

[0.7](consume-from-0.7.0.md), [0.6](consume-from-0.6.0.md) and the
[2026-09-25 consumer-impact record](../handoff-ngs-uikit-delta-2026-09-25-zh.md) retain historical facts.
They are not public legacy obligations or an upgrade-first route for new authors.
# Consume FerriteLib 0.8.x

Select one fixed DLL, assert its API range, and retain the same Host for an open UI. Then choose ordinary
notifications or explicitly tracked sources; neither requires a model base class. Start with the
[settings recipe](ordinary-settings.md) for a page, or [S1–S7](../openspec/specs/policies/public-authoring/spec.md)
for partial/custom adoption. Current requirements are in the [specification](../specification.md).

## Select the actual input

The published asset is [v0.8.0-rc1](https://github.com/Coahuilite/FerriteLib/releases/tag/v0.8.0-rc1),
from a0b5b983c3c13a099c62d936305eaebaf1deb5a8, a non-latest prerelease.
**rc2 is prepared locally, not published**; see its [notes](../release-notes/v0.8.0-rc2.md).
Until a real cloud asset exists, rc2 integration uses the explicitly delivered immutable local Release input.
Record actual source/configuration/DLL identity; do not substitute a moving output, rc1 fallback or relabeled Dev.
After publication select the real repo/tag/full SHA/asset.

Only FerriteLib distributes the runtime DLL. Reference it with Private=false and install no second carrier.
Every consumer checks its own tested range; game dependency metadata cannot express a version:

    if (!FerriteLibVersion.Require(new Version(0, 8, 0), new Version(0, 9, 0),
        "your.package.id", out string diagnostic))
        throw new InvalidOperationException(diagnostic);

The first public author baseline is0.8.x. Prior internal ranges and vocabulary are historical references,
not prerequisites for learning current use. Source, compiled DLL, XML, meaning and legal conditions have
the [stable compatibility promise](../api-tiers.md); public-unstable/advanced exceptions remain explicit.

## Declare and connect once

Use XML Schema2 or immutable UiElementSpec/Create definitions; both use the same compiler and typed checks.
Default kinds cover ordinary controls, descriptions, images and keyed Repeat. A compatible UpdateDefinition
keeps the Host/session; deleting and recreating an editor is not equivalent to hiding a retained one.

For ordinary data use BindValue/BindReadOnly, BindCommand/BindAction and NotifyChanged at the model mutation
boundary. Set requests the owner's write; it neither guarantees acceptance nor sends the notification.
Notify the actual declared key with its real invalidation class. Mutable Theme and ordinary/legacy Measure
remain legal under their original conditions.

For automatic dependencies explicitly use ReactiveRuntime.Int/Text/Boolean and BindTracked.
Other Value/List types require caller equality. Structural lists and item properties are separate sources;
immutable choices project all mutable data while tracking, not later in Draw. A shared model owner can outlive
multiple Hosts. External services deliver safe results in the projection's owning context; FL does not supply
tasks, thread dispatch or freshness policy. See the [counter walkthrough](../development/0.8/counter-walkthrough-zh.md)
and [binding contract](../openspec/specs/core/binding-reactivity/spec.md).

Forwarding IUiBindingAccess names its actual BindingOwner and consistently forwards endpoints and operations.
Switch backing between passes; Host validates before replacing its projection. Reacquire minting-registry
endpoints after switching. Ordinary forwarding does not activate tracking or change target identity.
Use optional UiItemBindings for current-key fields/commands; authoritative membership may outlive a filtered
Repeat key view. Filtering/paging/business Move and object disposal remain consumer-owned.

## Use one UI lifecycle

For a standard shell construct UiPageWindow, optionally register it in UiWindowCatalog with a stable key.
For an existing native callback retain UiHost plus VerseUiExecution and call DrawFrame with the actual content
rect, execution.CaptureFrame(nativeInputAllowed), and that execution. Capture once per native callback.
For native controls without Host retain Session/CreateContext/SetFrame; for headless operations or independent
visuals no fabricated Host/node is required. See the [adapter](../openspec/specs/adapters/rimworld16/spec.md).

Host/Session borrow execution. Idle switching is legal, but an open pass/capture/active editor refuses a different
execution before changing frame or qualification. End or explicitly SuspendEditor on the old one first.
Close Session before execution, and dispose execution in finally. Image tokens are execution-scoped;
the supplier retains texture lifetime. Host close does not destroy business services.

SetDisplayDemand communicates reliable hidden demand, not input focus. Hidden drafts remain under the same
target; closing the real UI disposes its connections. Subscriptions attach/detach through the existing hooks,
whose explicit failure conditions still apply. No library-wide keyboard navigation or global window manager
is implied by the native shell.

## Edit first, publish deliberately

TextField, NumberField and StepperSlider default Live=false; explicitly set Live=true when wanted.
Valid, nonconflicting ordinary blur confirms; invalid/conflicting drafts remain. Cancel changes only the local
draft, not earlier Live writes. A normal request return ends that request even if business rejects/normalizes;
paint/readback failure cannot replay it. Setter failure preserves actual side effects without rollback.

Within StepperSlider, model10/draft20/Step1 requests only21 on plus; an actual Slider30 requests only30.
Capture alone preserves the draft. Only declared local participants can take it over, not every same-Bind field.
Shared ctx.Controls.Edit/Editor/SuspendEditor support custom composites.
Composition owns its key first; single-line Return confirms, multiline Return inserts newline and Ctrl+Return
confirms. Read-only preserves selection/copy while preventing mutation.
See [default policy](../openspec/specs/policies/default-wrappers/spec.md) and
[interaction/editing](../openspec/specs/core/interaction-editing/spec.md) for the exact outcomes.

## Retain valid work without retaining stale geometry

Meaningful UpdateScope=true roots can be nested; root/Section/Surface/Scroll/Clip/Repeat already participate.
Defaults capture their measurement inputs; new IUiMeasureParticipant declares complete pure inputs,
and optional IUiTextMetricsEnvironment supplies truthful per-font validity without changing ITextMetrics.
Old widgets/metrics receive no new purity condition. Stable-domain publication can be sparse, while unknown
or unsupported dependencies take the correct full path.

Positions, scroll ranges, clip/hit and popup anchors still change when their actual dependency changes.
Pure tint/hover does not require layout. This is not retained IMGUI pixels: required Repaint continues.
Use the [layout contract](../openspec/specs/core/layout/spec.md), not an every-frame rebuild or input throttle.

## Configure only the appearance you mean

A new UiTheme works without palette assignments. Override one neutral UiColour role or one Font/metric
declaration; Remove reveals the current remaining source, not a creation-time theme snapshot.
Five metrics are Padding/Spacing/Gap/RowHeight/Hairline; container Padding/Gap allocate that container rather
than becoming every child's density. Retint does not replace the Host or clear drafts/scroll.
Use scoped UiImage for images/backgrounds. See [appearance](../openspec/specs/core/appearance/spec.md).

## Publish help without owning a panel

Declare opaque HelpRef on elements/choices. Empty choice help does not infer Value/Text and can fall back to
the general element topic. Session CurrentHelpRef/CurrentHelpSource and HelpGracePasses retain their advanced
grade and pass-based timing (library default0; a consumer's15 is not the default).
Tooltip takes explicit translated text.

Optional HoveredOptionHelpOutputBind targets a writable string endpoint. It withdraws only the last publication
still owned by that control; a newer same-address publisher or consumer write survives cleanup, including
across proxy switches. A failed withdrawal does not replay selection or uncommit a sealed definition.
See the [help connection](../openspec/specs/contracts/help-output/spec.md).

## Diagnose failures without inventing acceptance

Pure faults use Fault/Retry; managed writable responses use IsPaused/Fault/Resume after their owner checks
the actual business result. Related inputs can retry pure work but cannot resume a paused callback.
Retained paint is display-only; invalid current geometry/input cannot execute. See
[lifetime/recovery](../openspec/specs/core/lifetime-recovery/spec.md) and [open deviations](../openspec/implementation.md):
popup wheel qualification, Warning/release/seal and unaccepted Trace/fit isolation remain visible.

S1–S7 and necessary dependencies are stable as listed in API tiers. Node/Session runtime tables and detailed
R9 are not blanket stable. Surrounding authors use ElementProperties, scroll/scope/invalidation and InputState;
custom authors use their supplied context, not private tables. The external AuthorExamples assembly demonstrates
public-only usage without an internals grant.

The accepted stable-FPS pair2f22bec/833c14f4 and overall internal trialea610fa/833c14f4 retain their identities.
Later fixed compilation uses US3e42cc1 with FL6ec0c31; this guide neither rebuilds nor re-identifies them.
Unrecorded font/IME/overlap/scaling/native details and known headless errors remain limits.
1.6 is delivered; compiled/native1.7 is not. See [platform evidence](../development/0.8/platform-and-real-integrations.md)
and the [window guide](rimworld-window-layout.md).

For maintainers comparing older internal code, preserved
[author-vocabulary](../archive/specs-before-20261029/refactor-migration.md) and
[theme](../archive/specs-before-20261029/theme-migration.md) migration tables are historical aids,
not a parallel API or an upgrade-first route.
# Consume the 0.8.0 development candidate

Use a fixed Dev package from this line, not a moving sibling build output. Only FerriteLib ships the
runtime DLL; a consumer references it with `Private=false` and does not bundle another copy.

```csharp
if (!FerriteLibVersion.Require(new Version(0, 8, 0), new Version(0, 9, 0),
    "your.package.id", out string diagnostic))
    throw new InvalidOperationException(diagnostic);
```

The old `[0.7.0,0.8.0)` range excludes this candidate. Explicitly adopt the new development input, compile,
then run the consumer's own harness and native UI checks; widening the range alone is not a migration.

This is the approved prepublication breaking refactor. Start with [the exact migration](../development/0.8/refactor-migration.md)
and [supported S1–S7 paths](../development/0.8/public-contract.md), including explicit frame/execution,
UiChoice<T>, typed declarations and structured item addresses. Existing Button/Slider kinds remain usable. Button now activates on valid
primary release rather than delegating timing to the native compound button. Slider selects immediately
through shared range behavior and does not edit a neighboring NumberField draft. Keyboard/edit defaults
include the confirmed editing migration below. Other 0.8 requirements are not implied by this candidate.

Checkbox (both looks), InputModeRow, Dropdown trigger/options and Tree parts now use qualified primary
release (F2). Explicit same-value choices still request once. Dropdown outside primary press closes
and consumes the gesture (F3), without executing the covered/underlying command on its later release.
Checkbox's declared ActionBind still runs after Set. String/typed choice identity and display fallback
remain; missing values are observed, never silently repaired. No keyboard/global Cancel or synthetic
focus-return behavior is added. See the [BSE contract](../development/0.8/bse-contract.md) for independent
composition and headless examples and the [complete inventory](../development/0.8/migration-status.md)
for the complete retained first-version scope.

See [S0/S1 contract](../development/0.8/s01-contract.md) for per-property remove/following, independent
composition, headless entry points, capture ownership and error handling. Supported S1–S7 paths and required dependencies are stable; detailed R9 stays experimental.
Keep the exact selected source/configuration/package identity with your development evidence.

## Edit first, publish once

TextField, NumberField and StepperSlider now default to `Live="false"`. Declare `Live="true"` if your
old page relied on implicit immediate writes (including the later US migration). Ordinary valid,
nonconflicting blur commits; invalid/conflicting drafts remain. Cancel discards only local input.
Normal callback return ends a confirmation even when business refuses or normalizes the value;
the next display reads the real model. Setter exceptions do not cause rollback or automatic retries.

```xml
<Widget Id="amount" Kind="input/number-field" Bind="amount" Min="0" Max="100" Live="true" />
```

Within StepperSlider, a draft20 over model10 plus Step1 requests only21. A selected Slider30 replaces
that draft with only30; a thumb capture alone keeps it. Each real drag sample remains a business
request. An unrelated Button or same-Bind field cannot clear the draft. Custom native composites
explicitly suspend their participating editor with `ctx.Controls.SuspendEditor(ctx, binding)` before
ordinary blur, and use the same shared `Step`/`TakeOver` transitions. See the
[editing contract](../development/0.8/editing-contract.md) for the full end/failure and IME rules.

## Explicit reactive binding and RU1 sources

Use `UiReactiveRuntime.Int/Text/Boolean` and `UiBindings.BindTracked` for explicitly connected
scalar inputs. `Value<T>(initial, equal)` requires an explicit policy for other values/references;
ordinary classes can wrap sources in C# properties without an FL base class. `List<T>(items, equal)`
controls structure; item properties are independently observed. Four actual operation entries share nested batches; Host owns the presentation read
view, not your business owner/service. Declare tracked bindings before constructing the Host.
Use `BindTrackedChoices` for a pure immutable choice projection with explicit item equality. Actual
Choice endpoint and bounded reads select the same synchronized inputs. Project mutable item
properties to immutable presentation data during tracking; an outer list copy alone does not freeze them.
Repeat consumes an immutable ordered key projection through the existing stable-node reconciliation.
`BindTracked(..., equal: ...)` can suppress same-result presentation announcements without retaining old
dependency edges. Existing source calls remain valid; rebuild against the fixed 0.8 Dev API.
Ordinary getters/options retain their existing conditions. Start with the
[counter walkthrough](../development/0.8/counter-walkthrough-zh.md) and the
[actual runtime/host contract](../development/0.8/reactive-foundation-contract.md); the
[complete inventory](../development/0.8/migration-status.md) distinguishes connected uses from
editor/tree/chart/custom integration and separate native acceptance. The compiled `InventoryPageExample` and
`OrderBoardProjectionExample` demonstrate local object/list composition and externally owned safe results.
Application code owns external delivery, tasks and result freshness; Batch does not marshal a result.

## Scoped layout reuse without changing business ownership

For an existing forwarding reader, return its current registry from `BindingOwner` as well as
forwarding reads/requests/endpoints. Host then preserves tracked display/DisplayOnly semantics and
refreshes its projection on a between-pass backing switch. Ordinary getters remain untracked.
See the [public contract](../development/0.8/public-contract.md) for missing/mismatched capability
diagnostics and borrowed Execution's legal idle-switch/active-refusal/cleanup conditions.

Declare `UpdateScope="true"` at meaningful composition roots, including a custom subtree or Repeat
row template. Roots and default Section/Surface/Scroll/Clip/Repeat are already connected; nesting does
not rebuild node/editor/capture identity. The library captures its default wrappers' real measurement
inputs and retains unchanged leaf calculations and local container plans. Actual parent size,
Scroll content/range and shared-column consumers still propagate changes across scopes. Effective
positions/clip/hit/popup anchors are republished when a subtree moves.

Ordinary getters keep their original notification conditions. Old custom `IUiWidget` is revalidated at
its original legal synchronization before dependent parent reuse; no new purity prerequisite is
inferred. New custom `IUiMeasureParticipant` supplies complete actual values and a calculation over
those captured values. Optional `IUiTextMetricsEnvironment` supplies truthful per-font metric validity,
without adding members to `ITextMetrics`. Unknown legacy metrics receive no new text-cache guarantee.

Use the [single reuse contract](../development/0.8/layout-reuse-contract.md) for a compilable custom
method and exact inputs/diagnostics, and the [inventory](../development/0.8/migration-status.md) for
all18 uses/nine containers and retained old conditions. The supported paths protect source/DLL/XML/behavior under the documented migration window; API grading does not establish native acceptance.

## Managed faults and retained views

Use `computed.Fault/Retry()` for pure R1 work and `owner.RespondManaged(...)` with `IsPaused/Fault/Resume()` for consumer R2 callbacks. Related inputs can retry pure work; source changes cannot resume a paused callback. Default wrappers retain successful control paint with fault feedback, while current derived reads still fail and dependent input stops. A first failure is unavailable, not zero/empty.

For reliably hidden retained views call `UiHost.SetDisplayDemand` or `UiWindowHost.SetDisplayDemand`; focus is not visibility. Keep shared computations under a shared owner. Custom tracked readers declare actual binding roles or IUiPresentationDemand. Ordinary untracked
getters retain their original conditions; no prepare-all compatibility path remains. The [failure/lifetime contract](../development/0.8/failure-lifetime-contract.md) has recovery, draft, two-host and captured-target confirmation examples and the actual fixture/native limits.

## Replace old implementation/table access

Before publication this development migration internalizes default widget implementations, the layout
engine and writable Session tables. Keep existing manifest kinds and bindings; use `UiKinds` instead of
implementation-class constants. Use `host.ElementProperties(id)`, `Get/SetScrollPosition(id)`,
`SetInteractionScope(id, scope)`, `Invalidate()` and `InputState`; custom widgets use the supplied context's
`State/Properties/InteractionScope/Invalidate` and shared `ctx.Controls` behavior. Do not move a draft into a
shared binding-key cache. S1–S7 and complete dependencies are supported; explicitly listed Session/Node
and detailed R9 members are experimental. The separate `tools/FerriteLib.UiKit.AuthorExamples` assembly
compiles these paths without an internals grant.

The old26 colour-property bag becomes `theme.Palette[UiPaletteRole.*]` (or the Color indexer on theme),
with the explicit mapping in [theme migration](../development/0.8/theme-migration.md). Existing fonts and
`Metric.Padding/Spacing/Gap/RowHeight/Hairline` are finite declarations; remove an override to reveal its
current source. Ordinary container Padding/Gap still allocate that container, not inherited density.
Retired `<Font Value="..."/>` is a located invalid style document. Use `<Metric Token="Font" Value="..."/>`; last-good style recovery is retained.

Use `UiItemBindings<T>` for optional current-key fields/commands over safe snapshots or active services;
plain existing bindings remain valid. Refresh explicitly at the real change boundary and dispose only
your UI owner. Business Move refusal retains the actual business order. For compatible definition refresh
use `UpdateDefinition`, not disposal/new Host and private-table copying. The isolated real-source cases
and 1.6/1.7 support limits are in [platform evidence](../development/0.8/platform-and-real-integrations.md).

After publication a breaking change to the supported stable contract requires a major release; the
two-minor retirement window remains an additional minimum, with no1.x shortcut. The0.12 example
is a time floor, not permission to delete a stable contract in a minor.

Published same-adapter source, DLL, XML and behavioral contracts retain the supported signatures and
legal conditions within the consumer's `[min,max)` range. A deprecated0.9 entry works through the two
complete subsequent published minors0.10/0.11; earliest removal is0.12. This development-line internal
migration is explicit, not a precedent for silently breaking published contracts. The0.7 temporary minor
exception does not apply. Added focus/key navigation is deferred; existing native editing/IME/Accept/Cancel
is not removed.

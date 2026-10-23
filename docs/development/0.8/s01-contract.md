# S0/S1 development contract (0.8.0)

This contract records the shared appearance/activation/range foundation, now used by the complete
0.8 retained inventory. The installed payload remains one `FerriteLib.UiKit.dll`. Editing, shared labels,
relative placement and subsequent wrappers have their own production contracts; [migration status](migration-status.md)
tracks all uses and their evidence. Native input/IME and real-font acceptance remain separate.

## Configure an instance, then remove only its override

Use the supported host entry after the first arrangement:

```csharp
UiElementProperties slider = host.ElementProperties("volume");
slider.Font.Set(UiFont.Tiny);
slider.Font.Remove(); // follows the current remaining source, not its creation-time value
```

`Font` and `Density` are independent instance declarations. `LocalFont` / `LocalDensity` describe an
explicit programmatic local style. Resolution uses instance, local style, nearest ancestor declaration,
host/theme declaration, then library fallback. `ResolvedFont` / `ResolvedDensity` expose value, origin
and source identity; reading a value does not create a declaration. Explicitly setting the current default
still records intent. Removing font does not reset density, request a business write, or end a capture.

The existing `UiTheme.DefaultFont` / `Geometry` setters remain host inputs. `HasFontOverride` /
`HasDensityOverride` distinguish declarations from library fallback; `RemoveFontOverride()` /
`RemoveDensityOverride()` withdraw only the matching host declaration. Value-changing assignments still
advance the existing layout clock; declaration-only changes are observed independently by the resolver.

Legacy XML `Scheme` / `Density` remains supported. Its named definitions are applied token by token,
outer to inner, so a local definition's missing font/metric/color does not erase an ancestor's declaration.
These legacy scoped themes are adapter defaults for the programmatic S0 declaration surface; their
aggregated origin is reported as host/theme, not as invented instance intent. The current finite
`Font`/`Metric.*` declarations and exact same-level precedence are in [property-contract.md](property-contract.md).

Slider measure and draw both call neutral `UiSliderLayout.Measure` using `ITextMetrics`, the effective
font, the density distances, and the assigned width. A label that leaves insufficient track space moves
above the track and contributes wrapped height. Explicit `Height` still constrains the outer allocation.
`Density` does not scale fonts. Recorded geometry/metrics are contract tests, not a real-font pixel claim.

## Use behavior without inheriting a default widget

An independent platform presentation can use the existing context and its own layout:

```csharp
var target = ctx.Controls.InteractionTarget(ctx, "Apply");
var operation = ctx.Controls.Activation(myHitRect, ctx, "Apply");
UiInteractionRequests.Activation(target, operation, () => ctx.Bindings.Invoke("Apply"));
// Paint my own appearance by reading operation.Captured; no second activation rule.
```

Range composition uses `ctx.Controls.Range(rect, ctx, binding, modelValue, min, max, writable)` and
`UiInteractionRequests.Range(target, operation, value => bindings.Set(binding, value))`. The default
`ButtonWidget` / `SliderWidget` call those same production entries. The native adapter translates input,
window qualification, clip and popup facts, then applies native capture/release and event consumption.
It does not implement another activation/range state machine.

For a non-rendering client, create one `UiInteractionSession` per host and call `Activate` / `Select` for
already-established semantic operations. Pointer clients call `PointerActivation` / `PointerRange` with
`UiPointerSample`, `UiHitGeometry`, and `UiInputEligibility`. Use `UiInteractionRequests` for the same
request/error boundary. No Draw call, invisible canvas, game type or alternate event loop is required.
Pointer input needs real effective geometry; semantic operations do not invent a hit test. `UiHitGeometry`
separates the hit area from horizontal travel, so a custom renderer can keep value/position mapping exact.

`UiInteractionResult.Request` means a request should be made, not that the business accepted or saved it.
Do not replay an old result. Invoke semantic operations once per real operation; a pointer `None` sample
(repaint/layout) never requests. A setter that changes the model then throws is reported with the original
exception and target context. It is neither retried nor rolled back. Subsequent model reads remain authoritative.

## F1 pointer policy and ownership

- Button: a qualified primary press captures; a still-qualified release inside the effective hit/clip
  activates once. Outside release or lost qualification does not activate.
- Slider: initial real selection and changed captured drag samples request immediately. Repaint and
  identical held positions do not re-request, even when the business has not echoed the requested value.
  Pointer travel outside the control clamps to the range and retains ownership until release or invalidation.
- Start eligibility and continuation differ. Leaving the control/clip is not itself capture loss. Disabled,
  unwritable, hidden, removed, replaced-target and closed states cannot replay an old operation.
- A consumed native primary release is read from `Event.rawType` even after `Event.Use()` changes `type` to Used. It remains consumed: release ends owned capture without activating Button or selecting another Slider value. Raw consumed presses and secondary-button releases do not acquire or end a primary operation.
- Native capture is owned by the logical session and node. A compatible reflow keeps the recorded native
  capture identifier rather than treating an allocation-order change as a new logical operation.
- `host.SetInteractionScope(id, scope)` / custom `ctx.InteractionScope` identifies a changed business object when a getter retargets. Change it before
  the next input. A different binding/command key is already a different target. Reordering stable nodes or
  replacing only presentation does not change target identity.
- A successful pass reconciles omitted capture owners. An aborted traversal releases unreliable capture
  without classifying unvisited nodes as removed. An aborted hit pass restores the previous complete hit list and
  suspends pointer input until a new complete pass establishes geometry. Callback close stops further traversal and normal frame
  cleanup; Dispose is idempotent. Pure presentation failure does not automatically clear reliable behavior.

`UiWindowHost` maps the native `WindowStack.GetsInput` answer. Embedded `UiHost` usage retains its existing
input scope; the explicit frame supplied to DrawFrame carries the native owner's current qualification.
No new window registration condition is added to old embedded usage. Added Button/Slider key navigation
is deferred; current Live/Cancel and edit takeover policies are implemented in [editing-contract.md](editing-contract.md).

## Compatibility and source boundaries

The approved prepublication refactor removes ambient/raw native compatibility entry points.
Use explicit ctx.Controls, or session.CreateContext for independent native C#. Four-question
composition and the complete author migration are in [refactor migration](refactor-migration.md).
Source/build/mod axes remain0.8.0. The new signature inventory and necessary dependencies protect
the supported contract; detailed R9 and runtime plumbing remain experimental.

`NeutralSources.props` is the production source inventory imported by both the runtime project and
`tools/FerriteLib.UiKit.Core.ContractTests`. The independent target uses .NET 10 standard libraries only,
with no game references, engine-named stubs, copied implementation, or behavior-changing compilation
symbols. It writes `production-sources.txt` and `references.txt` beside its executable. The installed mod
still contains only the one net472 runtime DLL; the contract executable is never packaged.

`verify-local.ps1` runs that no-platform target before the existing ten gates, including the Dev instrument.
`KernelSharedBehaviorTests` exercises real default wrappers, an independently composed presentation and a
non-rendering driver in isolated sessions, comparing request/value/capture/end trajectories. It also checks
P1–P6 against a real Slider, pointer loss/qualification, nonzero origin, hidden/removal, and callback close.

## Manual 1.6 acceptance

Use the isolated 0.8 Dev package, record its `version.txt`, target game build, UI scale and language.
In two windows at nonzero screen origins, verify occlusion/absorb-input qualification, nested scrolling,
Button abandon/release, Slider continuous drag beyond track/viewport then release, disable while held,
retarget/reorder, and a close callback. Inspect long-label font/density changes and actual wrapping.
The local target is 1.6.4871 / Unity 2022.3.35f1. Source guidance from remote 1.6.4850 and stub success do
not replace this pass. Nothing here installs the package, launches the game, or changes game configuration.

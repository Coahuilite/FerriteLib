# Reuse measured work without freezing effective geometry

RU2 A–E directions are approved. The current engineering surface connects explicit scopes, captured
measurement inputs and independently retained local arrangement plans to the production Host. New API
names below are **public-unstable**; this does not freeze XML or ABI.

## Declare a meaningful composition root

```xml
<Column Id="page" UpdateScope="true" Padding="0">
  <Column Id="counter" UpdateScope="true" Padding="0">
    <Widget Id="caption" Kind="text/wrapped" Bind="text" />
  </Column>
  <Surface Id="details" Padding="0">
    <Widget Id="help" Kind="text/wrapped" Text="Details" />
  </Surface>
</Column>
```

`UpdateScope="true"` starts an explicitly owned cache scope; it can be nested in an existing scope.
`false` makes no explicit declaration. Invalid boolean text is a located creation-time error. A root
always owns a scope; default Section, Surface, Scroll, Clip and Repeat composition roots participate
without page-side node tables. Ordinary Row/Column/Stack do not each become a mandatory island.
Programmatic `UiElementSpec` attributes use the same entry. `UiNode.UpdateScopeRoot` reports the actual
nearest scope owner. Neither the declaration nor cache replacement changes node/business identity.

A scope owns its stage records, not a new scheduler. Parent constraints, Scroll content and shared
columns still cross scope boundaries. The session and existing reactive runtime keep their original
owners and synchronization points.

## Measure, arrange locally, then publish effective positions

The production chain is `UiBindings → UiBindingPresentation → UiHost → UiLayoutEngine → UiLayout →
node/draw/hit`. Presentation publishes changed binding keys rather than bumping a page-wide cache key.
The content revision remains an observable batch clock and an explicit legacy invalidation entry; it
is not a key that clears every new stage cache.

- **Measurement:** enrolled widgets capture the values actually consumed by their calculation. Equal
  values and a valid matching metric environment reuse its result, even after a source notification.
  A changed source is synchronized first, so switching to an equal-valued source replaces subscriptions.
- **Local arrangement:** the shared neutral `UiLayoutReuse` records the actual constraints requested by
  `UiLayout.Stack`, Row, Wrap, Overlay and VerticalContent. It validates all child results at those
  constraints before accepting a parent plan. Equal child sizes retain the plan while new child content
  and derived geometry continue through it. Changed results rerun the real layout algorithm.
- **Position publication:** local plans contain no mutable page-space entries. Current child entries
  are assembled through the retained slots and offset into effective page space. Drawing, clipping,
  hit layers, popup anchors and scroll targets therefore see the new positions without remeasuring
  unchanged text. Natural flow without Fill does not consume an irrelevant remaining-height constraint.

All visible membership is reconciled through the existing keyed Repeat/session lifetime. Retained
private sub-nodes remain linked when their owner measurement is reused. Removed nodes and Close
suspend owned editors without committing; capture/popup cleanup remains the existing owner path.
Leaf/plan/text calculation records are staged and committed with the complete arrangement. A failed
candidate restores the prior complete geometry and caches, ends unreliable input and never replays a
business request. RU3 adds per-node retained display and current-invalid facts; required shared layout failure restores the complete prior arrangement and stops that Host input until a successful hit pass. See the [failure/lifetime contract](failure-lifetime-contract.md).

## Follow the output its consumer actually uses

The page does not enumerate an output schema:

| Existing role | Result consumed | Propagation |
| --- | --- | --- |
| Parent flow/placement | Validated child width/height and current direct-child identity/definition | Same dimensions retain the parent plan; changed dimensions reflow following positions |
| Scroll | Natural content range, viewport and scrollbar reservation width | Content can grow from80 to140 inside viewport100 while the parent size stays100; maximum scroll moves0 to40 |
| Shared-label group | Each participating wrapper's measured demand, then the actual maximum | Contribution60→70 beside90 keeps the group90 and its peer valid;120 reaches actual consumers |
| Draw/hit/clip/popup | Current effective placement and session scroll | A reused local plan never freezes an old page position or trigger anchor |

Shared labels keep their existing direct-child/opt-in group semantics. An explicit nested update scope
on a participating wrapper does not hide its contribution. Same-parent anchors remain finite placement
relations; there is no arbitrary cross-tree solver.

## Use the default wrappers, or opt a custom measurement into complete inputs

All18 registered defaults participate: Repeat uses its container path; the other17 own their ordinary
measurement dependencies. Changed declared source revisions select their capture even when the source
classifies the change as Paint; the actual consumer inputs decide whether calculation is needed.
Text/value/option changes that affect only paint do not invalidate their
measured band. Library wrappers capture their own label/text/draft/options-count/image-size or fixed-band
inputs; authors do not declare their private parts or mark each child dirty.

Tracked bindings announce their actual changed presentation results automatically. Ordinary getters
still require their original explicit `NotifyChanged`/`UiNotifyAdapter` or `MarkDirty` conditions.
RU2 neither polls arbitrary business objects nor changes business state ownership.

For a new custom widget, implement optional `IUiMeasureParticipant` alongside unchanged `IUiWidget`:

```csharp
public UiMeasurement CaptureMeasurement(UiWidgetContext ctx)
{
    string caption = ctx.Bindings.Get<string>("caption");
    float width = ctx.ViewWidth;
    UiFont font = ctx.Theme.DefaultFont;
    return new UiMeasurement(
        new UiMeasureInputs(new[] { caption }, new[] { width, (float)font }, usesTextMetrics: true),
        () => ctx.Metrics.MeasureText(caption, font, width));
}

public float Measure(UiWidgetContext ctx) => CaptureMeasurement(ctx).Calculate();
```

Capture is an effect-free description of **complete actual inputs**; Calculate uses those captured
values. Include every consumed width, effective property, shared result or local state value. Announce
changes through declared binding keys or `MarkDirty` for external inputs. No cache keys or dependency
lists over internal child nodes are required. `usesTextMetrics` consumes the effective-font environment;
custom use of other font/services must supply their actual environment facts among its inputs or stay
on the legacy entry. Do not claim reuse for a service whose mutable inputs cannot be represented.
Null captures, nonfinite input values and nonfinite calculated heights receive the existing located
node recovery diagnostic; omitted business notifications cannot be discovered by deep scanning.

An old `IUiWidget` gains no measurement-purity requirement. On its original legal synchronization
(including a changed tracked presentation it reads through an undeclared custom context), its Measure
is validated before any dependent parent plan is accepted. Repeated requests at identical constraints
within that synchronization use the validated result; no cross-synchronization purity is inferred.
Unchanged legacy snapshot conditions remain supported. Current migration levels are maintained in
[migration-status.md](migration-status.md), not a final RU4 retirement policy.

## Give a metric service a real environment identity

`ITextMetrics` still has exactly its two original methods. Optional `IUiTextMetricsEnvironment` returns
a detached `UiMeasureInputs` stamp for the requested font; change it when actual metric facts change,
and return null to revoke reuse. Equal stamps must mean equal results for identical text/font/width.
The engine also distinguishes metric service instances and Host ownership.

`VerseFerriteTextMetrics` captures its actual resolved Verse font (including Tiny fallback), resource
identity/names and known font/style metric facts. It normalizes and restores wrap/anchor/font around
measurement. Custom mutable font providers supply their own truthful stamp; this is not a new font or
density product capability. Unknown old services retain the old idle snapshot condition and are
revalidated at legal synchronization; they receive no new text-measure purity promise. The per-Host
text calculation memo is bounded per font, not invalidated by an unrelated font or a colour clock.

## Evidence and author examples

`KernelLayoutReuseTests` reports actual per-node measure/arrange counters with numeric geometry,
current scopes, Scroll range and shared columns. It covers the default counter, an independent safe
multi-item service projection, scope nesting, same-output changes, real growth/movement/hit, native multiline draft invalidation, supplier image/tree/option impacts, Scroll80→140,
shared60→70→120, width/font/colour distinctions, source replacement, Add/Remove/Move, popup anchors,
editor/capture identity and failed definition attempts. The existing purpose-specific wrapper,
assembly, standard/embedded Host, observation and transaction lanes remain applicable.

`LayoutReuseTests` in the no-platform target consumes the same production stage-validity, result
comparison and layout sources with both compilable business examples. It does not copy a layout
algorithm. Runtime sources and the neutral target share `NeutralSources.props`.

The [counter walkthrough](counter-walkthrough-zh.md) and compiled `CounterPageExample`,
`OrderBoardService`/`OrderBoardProjectionExample` show the two ownership models. The independent service
keeps ordinary C# state, concurrency and freshness policy; the projection receives its safe result in
the FL-owned context and batches UI changes. Scopes never stop or schedule that service.

These are retained calculation/layout results. The engine still validates the current composition and
publishes effective geometry; drawing is not a partial-GPU/subtree-texture cache. Native1.6 input,
font pixels, IME and compositor acceptance, 1.7 and RU4/RU5 remain separately scoped; RU3 managed failure/demand behavior is described in the linked contract.

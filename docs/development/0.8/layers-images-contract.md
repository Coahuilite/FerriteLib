# Layered input, images and window decoration

## Overlap works with ordinary default controls

Put a large ordinary button and a smaller button later in an Overlay. The later button is visually above
and receives a qualified primary gesture where its actual input region overlaps. No declaration-order
workaround, custom subclass or input registration is needed. Pure image/text decoration does not acquire an opaque hit box. L2 is approved: a visible configured operation keeps its actual region as an obstruction while disabled/unavailable, but cannot activate or begin capture. Removing/hiding it or omitting an operation does not add obstruction. Existing capture is never handed to an underlying
target; release outside its own region, lost eligibility, removal or Close follows the common end rules.

The engine projects current declared input geometry through the same arranged scope/scroll transform and
effective clip before native Draw. The neutral production UiPointerLayers decision resolves content
coverage; UiNative reads that result and the existing interaction session remains the sole capture owner.
IMGUI calls keep their original order on every event. There is no second Draw, fake Event replay or new
per-widget dispatch algorithm. Popup/F3 priority stays above content, and content coverage does not turn an
ordinary nonoverlapping outside click into popup-style edit suspension. A covered native editor cannot
steal that pointer input, but still paints on Repaint and retains its existing keyboard/draft contract.

## Precise custom overlap needs real geometry

IUiWidget still has only its existing required members. A custom that wants the new precise content
coverage guarantee implements optional IUiPointerLayout and returns its actual operation regions in
local coordinates. Reuse the same geometry in Draw; configured operation geometry remains present during temporary disable. Execution eligibility goes separately to the shared interaction, not into a second behavior state machine. For example, if a custom has one30px control100px from its left edge:

```csharp
private static UiLayoutRect ButtonBounds(UiLayoutRect outer)
    => new(outer.X + 100, outer.Y + 10, 30, 30);

public IEnumerable<UiPointerRegion> PointerRegions(UiWidgetContext ctx, UiLayoutRect bounds)
{
    yield return new UiPointerRegion(ButtonBounds(bounds));
}
```

A ctx.Child keeps its independent interaction/capture identity; layer queries resolve its owning widget through the real Parent chain rather than treating the child as a new paint layer. Draw uses ButtonBounds with its allocated origin and supplies current execution eligibility to the existing UiNative activation/shared
request path. It does not implement topmost selection or capture. An empty sequence means decoration;
UiRowBand.PointerRegions exposes the same part geometry used by the retained row visual for custom row
wrappers. All default interactive wrappers provide their real field/track/option/row/point geometry.

An old custom without the optional capability keeps its previous entry points, nonoverlap and popup
behavior, without extra registration. Its unknown private/native input cannot be inferred on a first
overlapping input frame: precise occlusion against it is not promised. The library neither guesses its
whole outer rectangle is opaque nor claims to intercept arbitrary raw GUI. This is a condition of the
new precise-overlap capability, not a new prerequisite for existing custom use.

## Verification boundary

The fixed baseline realHost test recorded the lower large button stealing the upper lock gesture. The
same realEvent route now selects the upper target. Tests also cover first-frame live eligibility,
decoration pass-through, shared custom geometry, release/cancel/removal/Close, nested clip and current
scroll offsets, native-editor coverage/Repaint and ordinary outside-click publication. Existing full
popup/F3 and all control regressions remain. Pure tests compile the actual neutral decision source.
Native1.6 paint/input acceptance remains separate; fixture geometry and event records do not certify pixels.

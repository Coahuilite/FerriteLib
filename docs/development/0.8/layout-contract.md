# Shared layout and effective geometry

`UiLayout` is the production, engine-independent flow/placement implementation. The actual runtime
engine and the engine-free contract target compile these same sources. Platform adapters parse
attributes, resolve the effective font/density, invoke the real width-dependent measurement service,
and publish the returned slots to the existing node/draw/hit geometry. They do not repeat the layout
algorithm. Stack/Column/Section/Surface/Clip share vertical flow; Row uses the cross-group maximum;
Wrap measures each actual line; Overlay uses the same resolved child sizes. Scroll and Repeat use
vertical content flow. The session remains the only scroll-position owner.

## Position direct children without wiring private parts

An Overlay child can declare `AnchorX="other.Right"` or `AnchorY="parent.Center"`. `parent` names the
Overlay's inner rectangle. Other names must be exact Ids of visible direct siblings, not paths or
bindings. Horizontal edges are Left/Center/Right; vertical edges Top/Center/Bottom. Use AlignX/AlignY
for the child's own pivot and OffsetX/OffsetY for pixels or a fraction of the parent's inner span:

```xml
<Overlay Id="positioned" Height="120" Padding="8">
  <Widget Id="caption" Kind="text/wrapped" Text="Details" Width="90" AlignX="Left" />
  <Widget Id="apply" Kind="input/button" Bind="apply" Label="Apply" Width="80"
          AlignX="Left" AnchorX="caption.Right" OffsetX="8" AnchorY="parent.Center" AlignY="Center" />
</Overlay>
```

Each axis has a finite direct-sibling dependency order. A forward declaration works; cycles, missing
visible siblings, ambiguous Ids and invalid axis edges are located failures. Explicit Stretch and an
anchor on the same axis compete and are refused. Flow-container main-axis ownership is unchanged.
The declared Overlay natural height is the maximum of the measured child heights plus chrome/padding;
position/offset dependencies do not recursively enlarge that natural height. Intentional overlap does
not displace siblings. Relations inside Repeat templates are qualified to the same item during
materialization; cross-item and cross-tree references are unavailable.

## Align a group's actual labels

Set `SharedLabels="true"` on the containing group. Direct participating `input/slider`,
`input/number-field`, `input/text-field`, `input/stepper-slider` and `input/dropdown` wrappers report their label demand
through `IUiLabelledWidget`. The neutral rule takes the maximum; each wrapper consumes that column in
its own common internal label layout. Local/translated text, effective font and density determine the
demand. The Dropdown retains its old80px fixed column outside an opted-in group. A narrow allocation moves the label above its control and reports the combined height; the
parent still owns the allocated outer rectangle. A fixed Height is not enlarged behind the author's
back. An empty label has no label band. Nested groups opt in independently; no private control Ids or
two-dimensional grid are required.

## Constraints and failures remain visible

L1 is a reversible Dev candidate pending final product decision: natural growth, fixed constraints, retained minima plus located diagnostics, and no automatic Scroll/Clip. Column allocation retains effective MinWidth/MaxWidth instead of reducing a valid minimum below its
declaration to fit the parent. An infeasible budget appears in the successful snapshot's bounded
`Diagnostics` (WidthInsufficient, HeightOverflow, ChildOverflow; at most128 facts plus Truncated).
`input/stepper-slider` also reports `InternalWidthInsufficient` at its own path when its private part bounds exceed the allocated outer width. At default part widths, an unlabelled80px allocation retains minus24, range1, field64, plus24 and three6px gaps: the enclosing extent is131px and the reported shortage is51px. The field remains x37..101 and plus x107..131; the diagnostic does not enlarge the parent, shrink explicit parts, or add scrolling/clipping. Existing viewport qualification still rejects pointer presses outside the effective parent viewport.

131px is the enclosing geometry under the existing1px range floor, not a validated usability minimum. That range still has positive travel (thumb0.5, travel0.5); no new response threshold is introduced. The label layout's minimumControlWidth (186px at default density) only decides label stacking and is not an enforced minimum width. Native usability remains unverified. Authors can allocate more space or explicitly choose part widths without a new MinWidth prerequisite.

These are author/tool runtime diagnostics, not automatic player popups, repeated log output, requests to change business values, or automatic scrolling/clipping.
Fixed dimensions constrain natural growth; existing container clipping/scrolling remains explicit.
Scrollbar reservation uses full natural content height, including padding/title, not just child height.

A failed fresh arrange does not publish a half snapshot, half node geometry, or an incomplete Repeat
membership. The previous complete derived node relationships/geometry and engine materialization are
restored. Actual model side effects, local drafts and ended interactions are not rolled back; input
against the failed geometry is suspended and owned capture ends. A later valid arrange can replace
the committed layout normally. Native scopes remain in the existing balanced draw path.

## Evidence and remaining work

The pure target tests production flow, width allocation, finite dependencies, cycles, label demand and
scroll bounds without engine references. Platform fixtures exercise all nine existing containers,
actual five-wrapper shared labels/narrow measurement/font invalidation, and real arrange cycle/missing
failure recovery. Native RimWorld1.6 font pixels, clipping, scroll coordinates and input remain pending.
Per-property sources, assembly/update and hosts/R9 are now connected; see their companion contracts
and the complete `migration-status.md`. This automated checkpoint does not replace native acceptance.

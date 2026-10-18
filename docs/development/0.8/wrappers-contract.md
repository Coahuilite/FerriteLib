# Descriptive wrappers and editable chart points

The existing kinds remain registered. The complete inventory is maintained in
[migration status](migration-status.md), including layout, assembly and host work still in progress.

## Read descriptions without drawing

The seven descriptive wrappers implement `IUiDescriptiveWidget.Describe(context)`. It samples their
actual declaration, translation and model inputs and returns neutral `UiDescription` data. It does not
paint, consume input, capture a pointer or issue a business request. It is the wrapper read seam for the
later bounded host/node observation API, not another retained business tree.

| Kind | Descriptive facts |
| --- | --- |
| `text/wrapped` | Text role and the same bound/translated text used by measure and paint |
| `section/header` | Heading role and actual title; width-aware natural title height |
| `chrome/banner` | Status role and actual bound/translated text |
| `state/empty` | Status role and actual empty-state text |
| `display/image` | Image or explicitly decorative role, description, and known texture availability |
| `chrome/rule` | Decoration, with no invented input state |
| `display/progress` | Actual model value/range separately from clamped display fraction |

An absent runtime binding is marked `MissingBinding`; a read that cannot be resolved under the retained
fail-soft binding contract is `BindingReadUnavailable`. A null texture is known absent, not an unknown
binding. Nonfinite progress values remain visible in the sampled value facts with `Finite=false`; the
display fraction is zero, without a repair write. Descriptive reads do not emit the paint fallback report.
Source getters and translation can still fail; the host observer must report that read failure rather than
inventing a value. This does not authorize a query to run Draw to obtain an answer.

Meaningful and decorative images share the existing texture entry:

```xml
<Widget Id="portrait" Kind="display/image" Bind="Portrait"
        DescriptionKey="Page.PortraitDescription" />
<Widget Id="ornament" Kind="display/image" Bind="Ornament" Decorative="true" />
```

Neither spelling adds text to Auto width or creates a click target. Existing images without a description
remain usable; the read surface reports an empty name rather than guessing their meaning.

## Font declarations reach measure and paint together

An undeclared banner keeps its historical Tiny fallback; the other descriptive text wrappers keep Small.
The previous hard pin is removed when a real instance/local/ancestor/host font is declared. That font now
reaches natural Auto width, width-sensitive height and actual text paint, including removal back to the
current ancestor or kind fallback. This is a visible 0.8 migration from the old banner/empty/header pin,
not a claim that the old wrapper already inherited every font declaration.

The registry's additive font-fallback overload supplies this kind-level default. Existing Register CLR
signatures remain. Row/Stack/Wrap/Overlay/Scroll/Repeat natural-width callers resolve the child's own
scope before measuring it; they no longer measure a child with only its parent's font. Named density
distances remain independent of typography. Seven descriptive uses need no artificial interaction machine.

## Chart capture uses the same pointer owner

`chart/line` retains normalized `IReadOnlyList<Vector2>` bindings, static `Points`, `EditablePoints` and
`UiChartPointChange.Index/X/Y`. A primary press within the existing six-pixel circular hit radius captures
without publishing. Actual drag samples map through the shared plot geometry to finite normalized X/Y,
retain the existing 0.0001 sample tolerance, and issue one callback per new selection. Repaint and release
do not republish. Used/raw MouseUp ends the owned capture without a request; loss, disable, hide, removal
and disposal use the same ownership cleanup as the other wrappers. Another native owner's capture is not
stolen or released.

`UiInteractionSession.PointerPoint` extends the existing pointer state owner rather than introducing a
chart drag bit/cursor authority. `UiNative.Point` adapts platform eligibility/capture and `UiHitGeometry`
provides the same circular hit and two-axis mapping facts to pure clients. `UiInteractionResult.Value` is
point X and `Y` is the second coordinate. The chart draws actual model points after dispatch; it does not
optimistically substitute a requested point for a model that rejected it.

### Optional keys make dynamic reorder unambiguous

```xml
<Widget Id="curve" Kind="chart/line" Bind="Points" PointKeysBind="PointKeys"
        ActionBind="MovePoint" EditablePoints="0,1,2" />
```

Bind `PointKeys` as `IReadOnlyList<string>` with the same length as Points and unique nonempty keys.
`UiChartPointChange.Key` is then the stable key; Index is its **current** index. Explicit key declarations
are validated and a missing/replaced key ends capture. Traversing nonowner points first cannot cancel
the real owner. An observed series change with the same keys may reorder without writing another point.

Without PointKeysBind, the original static/index-based entries remain usable without a new registration
prerequisite. The adapter retains exact self-drag echoes or unchanged/rejected samples. When it observes
an unexplained count/coordinate replacement it ends capture conservatively. An index-only list cannot
prove arbitrary same-length business reordering, including indistinguishable values; that guarantee is
available only with explicit stable keys. It does not infer identity from display labels or new indices.

### Established requests survive paint failure

Point callbacks run through `UiInteractionRequests` before surfaces, lines and point markers are painted.
A pure painting exception cannot swallow an established move. Callback Close stops further normal
painting, and partial callback effects retain their original contextual exception without replay or
rollback. The identity evidence tracks what was observed/requested; it is not business acknowledgement.

Automated evidence: `KernelSharedChartTests` compares default/native/headless paths, keyed reorder,
legacy ambiguity/echo, Used release, partial callback failure, actual paint failure and callback Close.
`KernelDescriptiveFactsTests` reads all seven wrappers without drawing, tests nonoperative paint, and
checks effective font/removal against actual Auto geometry in all nine containers. Existing chart
eligibility/popup/clip and prior interaction lanes remain applicable. Target RimWorld 1.6 appearance,
font and pointer acceptance remains pending; no game installation or run is implied.

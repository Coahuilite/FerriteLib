# Standard hosts, embedded hosts and bounded observation

Use `UiPageWindow` for an ordinary page. Use `UiHost.DrawFrame` inside an existing window. Both own
one real `UiSession` and run the same wrappers, neutral interactions, editing and arrangement; neither
requires an author to reproduce Begin/End, capture release or callback-close ordering.

## Open an ordinary page without a subclass

```csharp
var catalog = new UiWindowCatalog(Verse.Find.WindowStack);
catalog.Register("sample", "settings", new UiWindowOptions(), key =>
    new UiPageWindow(key, manifest, bindings, theme, translation,
        "Settings", "Close", notice => NoticeText(notice)));
catalog.Open(new UiWindowKey("sample", "settings", "profile-a"));
```

Keep the catalog for the owning consumer's lifetime. Its `(consumer, windowKind, contextKey)` identity
keeps two contexts distinct and reactivates an existing equal key. Explicit modality, initial size and
business close veto belong in `UiWindowOptions`; omitted nullable options preserve the game's values.
Direct construction of `UiPageWindow` remains legal and does not gain a catalog-registration prerequisite.
`UiWindowHost` remains the extension point for a genuinely custom shell, not a requirement for ordinary use.
`PageHost` is null before construction/after release and does not create a host when read.

The native stack determines current input qualification. Deactivation releases owned pointer capture;
it does not dispose another host or silently publish its draft. Existing Accept/Cancel hooks and native
IME ownership remain; this package adds no global keyboard or focus-management policy.

## Embed one host and give it one frame call

```csharp
// Create once for this surrounding window, not once per repaint.
var page = new UiHost("sample/settings", manifest, bindings, theme, metrics, translation);
// In the surrounding window's GUI callback:
page.DrawFrame(contentRect, this);
// When that surrounding owner really closes:
page.Dispose();
```

The surrounding native window supplies itself, the real content rectangle and lifecycle. The shared
owner overload obtains native stack qualification once for both standard and embedded routes. The
original `DrawFrame(Rect)` remains legal for non-window/test/custom embedding and preserves its existing
explicit Session qualification conditions. `DrawFrame` owns its frame
boundary, measurement, drawing, hit pass, popup pass and failure cleanup. It does not swallow an outer
failure or require the author to guess a `Dispose`/`EndFrame` ordering. A callback that closes the host
ends normal drawing/traversal. One host's Close leaves another host's session and shared business model
alone. Nested Scroll/Clip geometry and popup coverage continue through the same engine/native adapter.
F3 outside primary dismissal consumes that input and cannot replay its later release into underlying
business actions. Raw consumed MouseUp still ends only the library's own capture.

A custom widget's explicit `ctx.Node` is its state owner even outside an ambient `EnterNode` scope.
Editors and their wrapper compatibility projections use that node's existing named state slots;
same-Bind instances remain independent. Retargeting suspends the replaced neutral editor and releases
only its own native focus/active-editor record. Element-less legacy session/key primitives retain their
original session-level conditions.

## Read the current runtime without drawing it

```csharp
UiHostDiscovery discovery = UiHost.ObserveLiveHosts(maximum: 16);
foreach (UiHostObservation found in discovery.Hosts)
{
    UiHostObservation sample = UiHost.ObserveLiveHost(found.Host,
        new UiObservationBudget(nodes: 64, depth: 8, text: 512, items: 32));
    // Correlate sample.Host + node.Key; retain sample.SampledAtUtc and Definition.
}
// With an owned host and a previously observed stable node key:
UiHostObservation subtree = page.Observe(nodeKey: nodeKey);
```

Call on the UI thread. Discovery reads lifecycle metadata, not business getters; a detailed read may
call the same read-only binding/translation/parser seams used by the UI. It never invokes Set/Invoke,
opens an editor, runs Arrange/Draw, captures input or retries an operation. Returned records contain
no host/node/editor/model handles, callbacks or write methods. Captured records stay historical after
the model changes. Custom business getters retain responsibility for their own side effects and cost.

The default node view is the last committed arranged tree, including materialized keyed Repeat rows.
A stable key query selects its bounded subtree; a known retained/hidden node can report its retained
draft while geometry/current-declaration projection are explicitly unsampled. Never-materialized
conditional/template declarations are not invented as runtime nodes. Ordinary custom widgets can
expose the existing `IUiDescriptiveWidget` read seam; without it their capabilities are Unsupported,
not guessed from their kind name or pixels. An external binding provider without the optional typed
read seam remains fully usable for UI; observation reports that unsupported detail rather than adding
new registration or reflection requirements.

### Facts have independent sources and times

- Host/Session identity, parent key, path/kind, explicit wrapper role, command predicate, writability,
  pointer permission, capture part/item and last session interaction end come from the actual owners.
  The last interaction end is explicitly global session history, not fabricated per-node history.
- Scalar values come from current model getters; text drafts, validity, modified/conflict/focus facts
  come separately from the existing neutral editor. Reading does not call Observe on that editor.
- Tree row keys/text/check/selection/expansion, Dropdown and ModeRow options, and chart point/key
  prefixes are bounded reads. They do not select the first option or repair a missing current value.
  Static/string versus explicitly typed choices preserve their actual wrapper entry conditions.
- Seven descriptive wrappers use their actual description seam. Images distinguish known availability
  from unknown; decorative content has no invented input capability. Chart observation does not copy
  an entire series just to answer a bounded prefix read.
- Geometry is **last-arrangement page-local**, with scroll-local content/viewport ancestry labelled.
  Last layout frame/time and definition revision remain separate from query time/current definition.
  Declared pending invalidation makes the layout stale. Unannounced model changes cannot be inferred.
- Font/metric/colour sources are labelled **last-property-resolution**. These are not a pixel capture.
  A current window origin, native focus/selection, current point hit/occlusion or old screenshot pixels
  are NotSampled when unavailable; a page rectangle does not pretend to prove them.
- Popup owner and hover-help owner/text are actual session/last-frame facts. Current runtime reads do
  not claim to reproduce an earlier image; correlate identity, version, time and the caller's viewport.

`Sampled`, `NotFound`, `Unsupported`, `NotSampled`, `Stale` and `Failed` are distinct. Getter/parser
failures retain a cause class and unknown value rather than inventing a zero/empty business answer.
Node/depth truncation is aggregate; individual text and item-prefix truncation are explicit facts.
Identity keys retain their locating meaning; the text budget applies to text/value projections.

### Candidate budgets and the narrow discovery exception

The R9 surface/field vocabulary and budgets are **Dev candidates for A8 closeout**, not approved stable
API defaults. Defaults: 64 nodes, depth8, 512 characters per projected text/value and 32 items per
collection. Caller maxima are 512 nodes, depth32, 4096 characters and 128 collection items. Invalid
budgets are refused. Counts can be exact while their copied prefixes are truncated.

PM's engineering decision permits only a 512-entry **weak** live-host discovery index. It stores no
model/node copy, reuses Session identity, registers after successful construction, removes on
Close/Dispose and clears dead references on reads. The index does not keep abandoned hosts alive or
change `UiHostLedger`'s historical source meaning. Discovery reports caller truncation and historical
capacity refusals even for empty results; after a refusal, an absent id is unknown/NotSampled rather
than a false NotFound. Detailed reads through an already owned host do not depend on this index.

## Evidence and remaining native acceptance

`KernelObservationTests` exercises real Host reads without paint, detached snapshots, model/draft
separation, hidden retained state, exact subtree boundaries, stale layout, failed getter, bounded
options with a guarded list, static-option compatibility, ordinary `UiPageWindow` versus embedded
editor confirmation, shared real-owner modal qualification/restoration, nested two-scroll real-event
coordinates/capture/release, independent Close, weak GC and 513-live-host capacity refusal. SharedEditing adds
explicit-context same-Bind ownership and retarget cleanup. StyleScope checks page Panel blue against
actual Measure/Draw child override/removal/current ancestor facts; production already had the effective
overlay write in the preceding candidate.

Existing complete WindowHost/WindowCatalog, InputEligibility, Popup, Placement/Layout, Repeat and
failure lanes cover stack/dual-host/scroll/clip/callback-close/partial traversal routes;
the full local gate runs them with the changed candidate. These platform fixtures and the no-platform
production contract target are automated evidence, not RimWorld1.6 acceptance. The developer's target
1.6 pass remains pending for native stacking, font/IME/selection/clipboard, scrolling/popup geometry and
image-to-runtime correlation. No game install, execution, screenshot automation or release is implied.

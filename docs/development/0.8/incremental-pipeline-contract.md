# Incremental synchronous layout publication

Keep the same `UiHost` when a page remains open. The ordinary Release path now validates an unchanged
committed layout without first copying every scope, capturing every node or reconstructing its geometry.
No new author API, subscription prerequisite, async dispatcher or XML vocabulary is required.

## The first complete domain

The engine retains the existing definition, membership, per-key owners and RU2 measurement/plan records.
Known visible tracked demand is frozen before Draw, including an otherwise unchanged callback: an earlier
button action must not change the presentation a later control sees in that same callback. A pending
visibility/width/tab/Repeat synchronization returns to the complete traversal **before** preparing the
previous visible set, so hidden/removed exclusive data is not evaluated eagerly.

For a stable domain with completely enrolled inputs, changed revisions select the existing RU2 leaf
validation. Equal actual outputs retain the complete published geometry. Changed heights in natural
Stack/Column/Section/Surface/Clip ancestry reuse `UiLayoutReuse` and the real `UiLayout.Stack` over current
child results. Changed ancestors and actually displaced following siblings/descendants are published;
unrelated local plans and entry objects remain retained. Position changes do not freeze hit coordinates.

Cold/full traversal also prepares stable identity/index metadata for subsequent reuse; it is not promised
to be faster than the old cold path. Validation still visits committed entries and polls the actual reader's declared revisions. Draw and
current hit/clip/scroll/popup/native-control processing still run. This is a reduction in candidate copying
and publication, not a promise of O(changed-nodes) total callback cost or retained IMGUI pixels.

## Candidates own their writes

- Leaf-record lists are copied only on their first actual candidate write, within the existing scope.
  The existing complete path still owns its conservative full backup.
- A session-local first-touch journal captures only nodes whose derived arrangement/authoring facts
  change. Newly created private nodes are removed on abandonment. Runtime declarations and completed
  business operations are not rewound.
- Text memo candidates share read-only font records; an actual miss or environment replacement obtains
  writable storage. A font memo is bounded by its existing 512-entry policy; a write can copy that
  affected font's retained records, not every font on every callback.
- Stable ordered geometry shares its base table and replaces touched 32-entry pages. The small page
  directory is copied, but unchanged entries and stable id indexes are shared. A published snapshot
  contains only rectangle values and stable read-only metadata, not runtime entries, nodes or widgets.
  It never points at a mutable candidate patch map or wraps a previous snapshot on each publication.
  Previous snapshots remain readable after a later full update or Close; release replaces indexes
  rather than clearing indexes held by those snapshots.
- Caches, node geometry and the snapshot are committed synchronously after candidate calculation.
  Abandonment restores derived node facts and discards candidate cache records. A same-synchronization
  memo prevents a complete fallback from repeating already validated enrolled work at the same actual
  context/source/environment. Failure keeps the prior complete result and the existing input suspension
  and successful-hit-pass recovery rules.

The registry's tracked-descriptor inventory is retained until registration/removal changes it;
presentations share it read-only. Forget/Dispose remove only their own calculations and values, never
another view's descriptor inventory. There is no second reactive graph or process-wide queue/cache.

## Complete fallback protects existing legal conditions

| Boundary | Current treatment |
|---|---|
| New/replaced definition, roots, binding owner/reader, metric/translation service, viewport or layout/property generation | Complete path; immutable definitions and actual constraints remain authoritative. |
| Ordinary value/options getter or forwarding reader at a new synchronization | Complete path. Getter side effects and repeated old measurement requests retain their original conditions; no new pure/subscribe prerequisite is inferred. Idle snapshot conditions remain supported. |
| Legacy `IUiWidget.Measure` requires validation | Complete path before invoking it. New input purity is not retrofitted onto it. |
| Unknown legacy metrics | Original unchanged-snapshot rule remains; new synchronization revalidates normally. Optional effective-font stamps keep their real invalidation meaning. |
| Visibility/width/tab/Repeat demand or membership changes, non-leaf dirty domain, explicit content invalidation | Complete path, retaining keyed identity, hidden drafts, lifetime cleanup and declared getter conditions. |
| Shared-label/Auto-width roles at synchronization, changed MatchContent reference domain, changed-height propagation through Fill/unsupported ancestry, existing layout diagnostics in a changed-height domain | Complete path. Scroll range/scrollbar reservation and peer widths are not inferred from an unchanged outer rectangle. |
| Dynamic `IUiPresentationDemand`, or `IUiLayoutDiagnosticSource` callbacks | Complete path. Their existing traversal/callback schedule is not silently replaced by one flat-entry callback. |

Multiple roots remain supported. The complete traversal gives each later root the viewport height minus
the preceding roots' current heights. If a multi-root page contains a root/subtree that consumes this
remaining height (Fill="true", including descendant/template Fill), any changed measured height takes
the complete transaction rather than merely moving later roots. This conservative boundary also covers
shrinkage; unchanged outputs/idle retain the existing fast path. It does not add an XML or author
prerequisite. Existing multi-root placement and Fill allocation checks cover their original calls;
the combined tracked-root-growth followed by Fill sequence has no existing targeted regression case.

An idle MatchContent allocation retains its already committed independent reference. Pending demand,
dirty nodes or any newly validated measurement in that domain use the complete path before publishing
a changed reference; MatchContent is not an unconditional whole-page idle barrier.

Pure retint/hover does not become a measurement invalidation. Property resolution still checks the actual
mutable host/declarations/ancestors; live resolved views and mutable returned themes remain live.
`ThemeFor` still records its located diagnostics. Changed font/metric/layout facts enlarge the domain
instead of reusing an old placement merely because an outer band stayed the same.

## Read work evidence with the same Host identity

The existing opt-in arrange timing event adds `Reason = "work-total ..."`. These are cumulative
Host/session work totals, **not** the timing event's sampling-window deltas. Subtract successive totals
for the same Host/session. `passes` counts ArrangeRoots synchronizations, including an explicit extra
MeasureAndArrange call; it is not Session.Frame or a GUI/render-frame counter. Timing samples remain
Host stage samples, not FPS.

- `full`: complete candidate attempts; `reused`/`sparse`: successful retained/sparse commits. Other work
  totals include work already done by a candidate that later falls back or fails.
- `captured-nodes`: derived arrangement backup captures, not business-state snapshots.
- `copied-scopes`, `copied-leaf-records`: scope backup copies and references copied into leaf-record
  lists; these are not byte-allocation measurements.
- `geometry-visits`, `published-entries`: entries visited/written by geometry publication. They exclude
  the validation and Draw/hit walks, which still occur. Equal-output candidates add no geometry entries.

The existing `KernelLayoutReuseTests` scenarios print these totals alongside their existing traces; no
new scenario or runner is introduced. On the same Counter's cold/same-size-change/growth sequence,
full candidates move from 3 to 1, node captures from 17 to 7 and geometry publication visits from 21 to 13.
Its moved Button still captures/releases exactly one command. Existing Scroll and legacy idle scenarios
also retain geometry without full preparation; shared-column, ordinary changed-input and membership
scenarios explicitly retain their complete fallbacks. These are managed work/contract observations, not
native allocation bytes, frame timings or an acceptance of the new fixed FL/consumer pair.

The existing geometry-instrument page also combines MatchContent, Scroll and a real Button. Its
unchanged helper already performs DrawFrame followed by an explicit MeasureAndArrange. The stage
summary now shows two synchronizations with one full preparation and one retained result; repeated
existing calls retain the same six geometry entries. The Release half keeps its no-instrument behavior.
This observation adds no test scenario and does not treat those synchronizations as game frames.

## Reference mechanisms, not a backend transplant

The inspected Qz-UILib-Core reference is fixed at
[`32bfefae7e345c106d7ca129ba036225c74ee4a4`](https://github.com/QuanhuZeYu/Qz-UILib-Core/tree/32bfefae7e345c106d7ca129ba036225c74ee4a4),
under [MIT](https://github.com/QuanhuZeYu/Qz-UILib-Core/blob/32bfefae7e345c106d7ca129ba036225c74ee4a4/LICENSE).
This implementation is independent; no Java code or third-party runtime is incorporated.

Adopted lessons are separate geometry versus paint revisions, preparing input from current committed
geometry, and publishing a coherent presentation only after its facts settle. Actual reference points:
[`SceneRuntime.invalidate/preparePresentation`](https://github.com/QuanhuZeYu/Qz-UILib-Core/blob/32bfefae7e345c106d7ca129ba036225c74ee4a4/src/core/java/club/heiqi/uilib/ui/scene/runtime/SceneRuntime.java#L293),
[`SceneFramePipeline.prepareGeometry/publishPresentation`](https://github.com/QuanhuZeYu/Qz-UILib-Core/blob/32bfefae7e345c106d7ca129ba036225c74ee4a4/src/core/java/club/heiqi/uilib/ui/scene/host/SceneFramePipeline.java#L609),
and [`SceneHitTester`](https://github.com/QuanhuZeYu/Qz-UILib-Core/blob/32bfefae7e345c106d7ca129ba036225c74ee4a4/src/core/java/club/heiqi/uilib/ui/scene/input/SceneHitTester.java#L30).
Its layout still has complete mounted-tree/geometry work, so it is not evidence that FL's new candidate
storage or sparse publication was already implemented there.

Not adopted: its domain scheduling/input sampling, retained paint/GPU assumptions, or coordinate policy.
[Core design §9](https://github.com/QuanhuZeYu/Qz-UILib-Core/blob/32bfefae7e345c106d7ca129ba036225c74ee4a4/specs/2026-10-07-uilib-core-contract-design.md#L221)
uses framebuffer pixels and excludes Minecraft GUI Scale from Core. FL retains Verse logical coordinates,
existing UIScale/pointer/font semantics, synchronous native callbacks and every required Repaint/native
editor call. The complete platform-neutral dependency closure and adapter-specific execution authority
remain unchanged; no background Host or new 1.7 adapter is introduced.

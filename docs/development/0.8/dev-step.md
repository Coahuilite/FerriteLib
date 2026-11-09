# Use the real 1.6 Dev instrument

Switch the complete US + FL Dev pair together. Compile the consumer against the same fixed FL Dev DLL
that its separate FerriteLib mod root supplies at runtime, with Private=false. A Release FL carrier
cannot provide geometry by changing a label: its enable setters refuse and its snapshot reader returns
false. FL's existing Dev configuration defines FER_DEV; the consumer needs its own actual Dev wiring.
FL adds no settings window, developer-panel button or translation key.

The accurate 1.6 maintenance producer supplies separate fixed Release and FER_DEV roots from the
accepted R1 production source. The repaired Dev carrier removes the old DLL's personal CodeView PDB
path; the accepted Release carrier is reused unchanged. Exact inputs and evidence are in the
[current producer entry](../../openspec/implementation.md#fl08-dev-path-delivery); its USE.md selects
the matching DLL. After Coordinator release, US A002 r2 actually compiled and ran against that Dev
carrier and supplied the accepted complete pair. This local pair is not a published Release asset.
This does not replace native acceptance or relabel the old c3-dev carrier as containing R1.

## Keep cost sampling separate from geometry evidence

For a pure cost sample, leave geometry capture and outline off on the participating windows. Record the
actual logging/fit/timing state and keep it consistent. US Dev's existing Auto logging policy enables
detail unless explicitly Disabled; it may therefore change optional work compared with Release. FL's
logger detail setting does not itself enable fit, timing or geometry. If an output path needs detail,
keep that condition explicit rather than calling the sample diagnostics-free.

Use the consumer's existing current-Settings one-shot entry for its finite32 Host-pass sample. The r5
baseline does not offer that cost entry for Remix, the developer panel itself or independent diagnostics.
That restriction belongs to the consumer entry, not a limitation on the Host timing API. The US owner
documents and verifies the final paired UI entry; FL does not execute it on the user's computer.

FL TimingEnabled is independently opt-in, TimingSampleFrames defaults to60, and a timing event is an
aggregate over Host callbacks, not FPS or game ticks. It does not stop itself after32 passes: the finite
consumer sampler owns its request and closure/partial result. work-total retains its Host/session-lifetime
cumulative meaning. Inclusive arrange/draw/metrics or consumer scopes may overlap; do not add nested
times into a fabricated frame total. Do not treat Dev versus Release as optimization before/after data.

## Name one live Host before enabling an instrument

Read or opt in through that Host's existing Diagnostics subscription. Two Hosts with the same source
still have different SessionId values. Capture and overlay are both off initially and are independent.

| Capability | Existing public entry | Prerequisite and output |
| --- | --- | --- |
| Geometry capture | GeometryEnabled | Dev, live owning Host/subscription; records the latest target draw pass only |
| Independent outline | GeometryOverlay | Dev; screen drawing for that target, with no capture required |
| Structured report | TryGetGeometrySnapshot(out UiDevGeometrySnapshot?) | Capture plus an actual target draw pass; returns detached Host/Session/pass data |
| Text report | DumpGeometry() | Same capture, explicit string read; FL does not write a file or native log automatically |
| Phase timing | TimingEnabled / TimingSampleFrames | Separate opt-in; existing bounded diagnostic timing events |
| Clear | Clear() | Clears retained facts/capture samples, not the independent outline request |
| Close | Host.Close / Session.Dispose / subscription.Dispose | Existing owner unregisters and releases its capture; no sibling is closed |

The existing author sequence is:

```csharp
UiDiagnosticSubscription diagnostics = host.Diagnostics;
diagnostics.GeometryEnabled = true;
diagnostics.GeometryOverlay = false;
// Let the existing owner draw the actual target; do not invent a callback or force Arrange here.
// Read after that draw on the same UI thread:
if (diagnostics.IsActive && diagnostics.TryGetGeometrySnapshot(out UiDevGeometrySnapshot? snapshot))
{
    string dump = diagnostics.DumpGeometry();
    // The consumer owns display/export and its explicit output gate.
}
```

For outline-only, set GeometryEnabled=false and GeometryOverlay=true. It paints entries reaching the
existing draw walk without allocating a capture; snapshot is unavailable and dump is empty. Turning
capture off does not turn the outline off. When both are on, the element outline follows the retained
bounded capture, while the shell's own chrome remains its separate recorded/outlined bands.

No report read forces Draw or Arrange. Before the first captured pass, the snapshot reader returns false;
a capture's text header can still name pass=-1 with zero samples. Clear removes samples but preserves the
capture's pass number. A cleared or incomplete result is not a fresh screenshot. The consumer's explicit
one-shot report request must wait for its intended completed target pass and retain its original identity.

## Bounds and lifetime are part of the report

Each capture holds at most512 geometry samples and128 input samples per Host pass, with separate dropped
counts. A new pass replaces the previous capture. Snapshots/dumps share Host, SessionId, Pass, EntryEvent,
node/input records and drop counts; a standard window may also publish ShellChrome. Snapshot objects
are detached copies. An earlier snapshot remains historical after later draws or Close.

Geometry reports distinguish arranged/page-local, draw-local and window-space rects, their origin and
the effective clip. Input samples preserve before/after event consumption. Unobserved hover/focus/font
facts remain explicitly unknown rather than invented. Public snapshot types remain present in Release
for compile compatibility; their presence alone is not instrument availability.

After target closure, use IsActive and the consumer's current target lookup; do not reuse a closed
subscription or retarget an old snapshot by source name. Turning geometry off discards its buffer and
still leaves the independent outline request untouched. The consumer releases its target references,
pending explicit report and any extra timing/fit holds at its own detach/close seams. No new global owner
or collector is introduced by the Dev package.

## Target and evidence boundaries

The r5 consumer developer panel names Settings or its Remix dialog as separate geometry targets. Its
own page is not a target. Independent Kernel diagnostic windows use their own outline entry, not the
Settings panel's host. Final US_DEV gating, scope registration, report output location/budget and target
cleanup are the US writer's adoption responsibilities. FL only returns the selected Host's data/string
and paints through that Host's real draw scope.

Existing KernelDevGeometryTests cover capture/independent overlay, snapshot/dump agreement, bounds,
chrome/coordinates and copies/Host isolation/cleanup. Existing Diagnostics, WindowHost/Catalog and
native-event stub lanes cover adjacent attribution, lifecycle and input behavior.

The carried-forward r5 local-delivery record already accepts the complete US UiLogic run including the
N1 follow-on, B grouped-navigation479, and Close30 bilingual/positive fit-hit11 evidence on their valid
candidate or unchanged paths. These are closed managed checks, not unrun gates and not new A004 runs.

US A002 r2 completed the corrected O5 editor-injection entry and actual consumption of the repaired
Dev carrier. O5's32 assertions are not the original32-Host-pass sample. Other retained gaps are legacy
author popup/LongAuthor red gates without the same current contract; direct current source-list
search/selection/paging/long-text coverage; the new
Queued/32-pass/partial sampling branches; native glyphs, overlay alignment, editor/IME/input and lifecycle;
independent1.7 reference/JIT/native coverage; and the original release specification/migration-document
gates. No game was run here. Screenshot-review limits are unchanged.

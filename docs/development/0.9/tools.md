# Connect a tool to the real page

The normative contract is [developer-tools](../../openspec/specs/contracts/developer-tools/spec.md); this guide owns API usage. Source facts, fixed producer/consumer identities and unverified conditions live in [the implementation map](../../openspec/implementation.md#stage-evidence). New native work follows RimWorld 1.7 APIs rather than a fixed minor build; historical build identities below remain evidence, not a support ceiling.

API 0.9.0. The tool/preview surface is public-unstable. Existing Host, standalone controls,
visual-only, R9 reads and bounded diagnostics do not require Tools. Select a fixed carrier with the
capability actually needed: shell v1, selection-presentation v1, coordinate-hit v1 and budget-measurement
v1 are additive development capabilities, not a promise made by every API 0.9.0 DLL.

Use a [formal shell](#draw-the-formal-window-around-the-existing-preview-host) for a complete window,
[coordinate hit](#inspect-the-original-pointer-owner-at-a-coordinate) to inspect its original draw,
[budget measurement](#measure-a-proposed-budget-before-committing-layout) before final layout, and
[selection-ink suppression](#export-without-selection-ink-keeping-selection-facts) during a clean draw.
Old page-only providers and normal MeasureAndArrange callers need none of these new opt-ins.

## Adopted targets do not change these entry points

The [basic-capability change](../../openspec/changes/fl09-basic-ui-capabilities/proposal.md) adopts new dual-axis
scrolling, a limited pan/zoom viewport and later focus/platform targets; implementation has not started and no new signatures are fixed.
Use the existing v1 seams below. The [FQ1–FQ6 source/semantic map](../../openspec/implementation.md#fl09-tools-semantics)
records the carrier and the limits Tools can consume now.

Ordinary `NotifyChanged` allows redundant, same-value and off-page registered keys. The
[R1 repair and original-caller evidence](../../openspec/implementation.md#fl09-reliability) do not impose a current-layout notification rule.
Capture remains a read: it does not prepare/arrange/draw to repair Stale results. A subtree NotFound does not mean the Host closed;
retained draft facts, unmaterialized declarations, truncated prefixes and current geometry each keep their own status.

## Select, capture, queue, advance, read back

On the Host thread, select a concrete `UiHost.ObserveLiveHosts` identity and call
`UiHost.ConnectLiveTools(identity, new Version(0,9,0), new Version(0,10,0), UiToolAccess.Operate)`.
An owned Host also has `ConnectTools`. Only one connection may own that Host at a time.
A read-only connection can capture and inspect but cannot queue an operation.

`connection.Capture(budget, nodeKey)` reads the existing runtime entries and current bindings.
It does not arrange, draw, set a value or invoke a command. Capture first returns NotSampled until
an explicit owner advance has materialized the page. Use `snapshot.Observation.Nodes` for facts and
`snapshot.Targets` for opaque operation addresses. Names and labels are untrusted display data.
`IUiDescriptiveWidget` provides a custom read projection; absent projection is Unsupported.
`IUiSemanticWidget` optionally provides the custom wrapper's real operations, using its typed owner.
Its `ReadActions` must be pure and bounded; `ExecuteAction` returns whether the wrapper handled the intent.

Queue with `connection.Queue(requestId, target.Id, new UiToolValue(number: value))`.
Background protocol threads may only Queue, Result, Latest or Dispose. Live reads and instrumentation
belong to the Host thread. The next native `DrawFrame`, or explicit `host.AdvanceHeadless(size)`,
takes at most one queued intent, rechecks the original owner and attempts it at most once.
The headless entry uses the normal manifest, registry, bindings, layout and operations, without Draw/GPU.
It uses author-supplied measurement: arranged coordinates are structural evidence, not native glyph pixels.
Do not call the headless entry on an active native window; let its real callback own input qualification.

`Result(requestId)` distinguishes Queued / Handled / Rejected / Closed / Failed. A repeated ID within
this connection returns the original result and never repeats the operation. After a timeout, query it;
do not generate a new ID to replay an uncertain side effect. Handled means the original owner returned,
not that business state accepted the candidate or that anything was persisted. Capture again to read
actual value, draft, conflict and definition. A throwing owner is Failed, with no automatic retry.

## Identity and bounds

A target identifies the Host/Session identity, definition revision, node mount incarnation,
interaction scope, semantic part and item. Address IDs are capture-local opaque tokens, not names.
Queued targets survive a later capture, but are rejected after a definition change, incompatible
mount retirement, hidden/unarranged target, changed interaction scope or changed choice identity.
Same-key reconstruction cannot revive an old target. Original owners recheck visibility, command
predicates, writability, modal/capture state and current Host input eligibility at execution.
A newly selected target never replaces a closed target implicitly.

R9 budgets remain unchanged. Targets are limited to 512 total and Items per node;
`TargetsMayBeTruncated` reports a reached budget (including possible exact-boundary truncation).
Observation facts keep their own source/status/truncation. Snapshot Revision is a connection capture
sequence, not a claim of native frame/geometry equivalence. Geometry capture retains its own pass identity.
The queue holds 64 intents and the idempotency/result ledger holds 256 requests per connection.
The ledger is never evicted while connected; budget exhaustion refuses new requests. Disconnect,
retain/export the final results, and explicitly reconnect when a new development session is needed.

A new Capture replaces the latest unqueued addresses. Already queued requests retain their original capability and
are rechecked by the owner; they are not retargeted to a same-named node in the new capture. R9's defaults are
nodes64/depth8/text512/items32 and maxima512/32/4096/128; an external tool's default128 nodes does not change that contract.
For a known runtime subtree, use its node key and a finite budget rather than treating a truncated root prefix as a census.

## Inspect the original pointer owner at a coordinate

After the original owner completes its draw, call on that Host's thread:

```csharp
UiHostObservation hit = connection.HitTest(windowPoint);
if (hit.Status == UiObservationStatus.Sampled)
{
    string canonicalKey = hit.Nodes[0].Key;
    // Read/select explicitly using this key; HitTest itself changes neither selection nor input.
}
```

`UiToolConnection.CoordinateHitTestVersion` is 1. `HitTest(UiLayoutPoint windowPoint)` returns the
existing detached `UiHostObservation` shape, with at most one `UiObservedNode`. Both ReadOnly and
Operate connections may read it, without Capture or SetInstruments. Use the selected matching carrier;
API 0.9.0 alone does not identify this additive development capability.

The point is in the same **logical Host-window space as UiFrame.Pointer** and the draw's Window
geometry. It includes the original page/window origin; it is not page-local, scroll-content-local,
target physical pixels or Tools desktop pixels. The adapter/tool owns inversion of its existing
viewing transform and effective UI scale before calling. FL adds no second coordinate transform.

The read uses the original draw's contentInputLayers and popup hitLayers, not geometry rectangles
from an observation capture. Popup coverage wins; content uses the same region/clip predicate as
IsPointerOverHigherLayer. Configured disabled regions still obstruct and return their owner;
decoration and unconfigured regions pass through. Unknown custom private regions are not guessed
from an outer rectangle: the existing IUiPointerLayout support boundary is unchanged. Popup results
identify the original node, not an invented option/item operation.
This is a Host page-node query; window-shell chrome keeps its original shell owner and does not
acquire an invented page-node identity.

The result carries Host, Frame, Definition and LayoutDefinition; its node facts include mount-lifetime,
interaction-scope, pointer-region and clip (window-logical), popup, pointer-allowed, input-disabled,
capture-owner and native-capture-owner. These are geometry/qualification facts, **not activation
permission or a capture dispatch**. An existing capture remains with its original owner, even outside
this point; native capture availability, phase/consumption, writability and business predicates are
still rechecked only by the original operation path. To operate, Capture the desired node and Queue
its current opaque target normally. A key alone never revives an old definition/mount/part address.

Sampled means one declared topmost owner; NotFound/no-declared-pointer-region means none in the
supported declared regions. NotSampled means no successful arrangement. Stale reports a newer
invalidation, hidden Host, missing completed current draw, failed draw, intervening arrangement,
scroll write, frame/definition/mount change or retired owner. It returns no stale node and never
arranges/draws to make the answer available. Headless-only advancement supplies no pointer draw.
Non-finite points throw ArgumentOutOfRangeException; a closed connection/Host throws
ObjectDisposedException, and live reads off the owner thread throw InvalidOperationException.

Only this new read requires a completed draw. Existing drawing, observation, semantic capture,
selection, partial adoption and independent Session callers retain their entry points and conditions.

## Native instruments remain dormant until selected

The normal carrier includes `FER_TOOLS` instrumentation. It does not imply FER_DEV, DevMode, automatic
watchers, sampling, overlays or any network/protocol implementation. Call SetInstruments only for the
selected Host. Capture, overlay and timing are independent switches; disconnect restores their prior
settings on the owner thread and cancels queued intents. A background disconnect closes the queue
immediately and restores instruments on the next owner callback. It does not commit drafts or close
the business page. Host/Session disposal also closes the connection. Existing direct diagnostic
subscription use retains its conditions. Test-only document fault injection is excluded from normal builds.

## Register the same page as a preview

An author returns `UiPreviewRegistration(id, name, kind, create)` using explicit isolated sample data.
Create returns `UiPreviewInstance(host, sampleSource, measurementSource, resources)`.
It owns the sample Host and optional resources, and releases both on Dispose. Component/Window/Settings
is the requested wrapper kind, not a native-support label. The tool owns its registry and UI;
there is no new process-wide preview registry. See the separately compiled
`ToolPreviewExample.Register`, which reuses CounterPage rather than copying its page.

### Prepare author layout on that same owner

The original four-argument constructor (including its optional resources argument) is unchanged in
metadata, so already compiled providers remain loadable. Authors may instead use this additional overload:

```csharp
new UiPreviewInstance(host, sampleSource, measurementSource, resources,
    prepare: size => PreparePage(host, size));
```

The callback belongs to that preview's Host, not Tools. Tools explicitly calls the existing
`preview.Host.AdvanceHeadless(size)`. On the Host thread, after BeginFrame and before arrangement,
the Host calls prepare with that same available size. If a semantic attempt requires a second
arrangement, it calls prepare again first, so changed content/help gets its author budget in the same
round. Native previews use the same hook inside the actual `Host.DrawFrame` callback; Tools must not
headlessly advance a native-owned Host. A native author that already prepares in BeforeDraw should
keep that original owner path, rather than install a duplicate callback. No callback is required for
ordinary windows or old providers. Capture, Observe, diagnostics and cost reads never prepare.

Preparation must be repeat-safe: derive/set the current layout budget, without business requests,
sampling, recursive advancement or assumptions about once-per-render timing. It may run several times
at the same size. A Host accepts only one preparation attachment. Recursive advance is rejected.
An author exception propagates through the existing explicit/native owner failure path. An initial
prepare failure leaves queued work unattempted. A post-operation prepare failure does not undo or
replay the operation/result already produced; the caller must not generate a new request ID to retry it.
Closing during prepare stops subsequent arrange/pump/draw and closes pending work normally. Dispose
clears the callback with the Host, then releases the preview resources; it never recreates the Host.
The independently compiled ToolPreviewExample shows an actual container budget, not a copied page.

### Measure a proposed budget before committing layout

When an author's next container budget depends on current layout geometry, calculate it without
calling MeasureAndArrange for a temporary page allocation:

```csharp
UiLayoutSnapshot candidate = UiLayoutInput.MeasureBudget(host, available);
if (candidate.ScrollContents.TryGetValue("details", out UiLayoutRect content))
    host.SetLayoutInput("details", new UiLayoutInput(height: Math.Min(available.Y, content.Height)));
// The ordinary owner boundary now calls MeasureAndArrange with the final inputs.
```

`UiLayoutInput.BudgetMeasurementVersion` is 1. Its public-unstable signature is:

```csharp
public static UiLayoutSnapshot MeasureBudget(UiHost host, UiLayoutPoint available,
    Action<UiLayoutSnapshot>? readPrepared = null);
```

Call on the Host thread, outside Draw, an existing arrangement or document staging. The normal
preview prepare callback is a supported place. It requires finite positive current logical width
and height, and the actual borrowed metrics/font environment for that target. It needs neither a
Tools connection nor a previous successful arrangement. It does not run prepare or advance a frame.
The original Host/Session, manifest/templates, current binding reader/owner, layout inputs and
effective properties/styles supply the inputs; a frozen presentation from an earlier pass is not
substituted for current reads.

The same MeasureRoots/MeasureElement/container/text/scroll-width implementation calculates the
candidate, including current visibility, repeats, shared labels, responsive constraints, natural
scroll content and the scrollbar-width remeasurement. RectById remains page-local; Viewports and
ScrollContents use their original keys/spaces. Hidden/unmaterialized requested IDs are absent,
never filled from the last successful snapshot. Diagnostics describe this candidate allocation.
This detached snapshot is not a committed layout or hit/draw sample; do not pass it to Draw as a
replacement for the original owner's arrangement.

**Natural leaf measurement uses the existing pure IUiMeasureParticipant contract.** A leaf whose
height must be measured and lacks that capability throws NotSupportedException with its path/kind;
this entry never invokes an arbitrary legacy Measure and then tries to undo its effects. Fixed
heights retain the original rule that no natural height is requested. All participating factories,
layout readers, diagnostic callbacks and readPrepared must be effect-free; they may read existing
local inputs and prepare initial local read slots, but may not mutate existing state/cache objects,
business data, properties, resources, input or services. Calculate uses captured inputs only. A
custom widget can share one captured calculation between its normal Measure and this optional
contract; its normal owner remains responsible for any later draw-cache publication. Ordinary
MeasureAndArrange still accepts legacy widgets under their original conditions.

For first-use font/property inspection, readPrepared runs synchronously after current nodes have
been provisionally prepared. Within it, GetNodeByElementId plus the original CreateContext/WithNode
and UiTextFonts.Apply can read the current node's effective font/environment. Only detached values
or the candidate snapshot may leave this callback. Provisional nodes/contexts/state handles must
not escape. After return, first-use nodes are removed; formal arrangement creates their real
mounted incarnation. Existing nodes retain their identities, geometry, local state and dirty flags.

Each call owns a fresh private calculation workspace and text/layout caches; no cross-call or
committed-cache reuse is claimed. Within-call reuse still follows the real measure-input and
metric-environment rules. Changing width/height, bindings, manifest/styles or font environment is
therefore measured from the current inputs on the next call, not from a previous target's footer
or content. The workspace contains derived calculation records, not a second business/runtime tree.

The candidate does not publish node rectangles, acknowledge layout notifications, prune mounted
nodes, consume scroll targets, update last-successful/diagnostic arrangement, or create hit layers.
It never calls clamp or restores a clamped scroll value. A synchronous preparation scope discards
provisional nodes/slots and restores derived authoring/parent/geometry/dirty/measure-count facts;
it does not clone or roll back arbitrary business objects. Observe/HitTest report Stale within the
scope, and Capture/selection changes, recursive measurement, Host advancement, layout-input changes and
scroll writes are refused. Outside it, prior committed hit facts keep their original validity;
separate author invalidation still makes them stale normally.

Measurement errors propagate with their original cause and a located path, rather than publishing
a recovery-height budget or tripping/suspending the committed node. Binding pure-read faults also
fail the candidate before readPrepared. A throwing reader discards preparation; cleanup failures
are reported, together with the original failure when both occur. Host/Session Close wins and is
never undone. Calling a closed Host throws ObjectDisposedException; wrong thread, reentry/staging
or forbidden in-scope mutation throws InvalidOperationException; invalid constraints throw
ArgumentOutOfRangeException. The caller retains the existing owner failure/close policy.

The final ordinary MeasureAndArrange still publishes and immediately legally clamps on success.
This new capability's purity, thread and synchronous-borrow conditions apply only to this new use;
no old Host, widget, Session, shell, selection or Tools entry acquires an enrollment prerequisite.

## Build and targets

`dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release -p:UiKitTarget=Neutral`
builds the official platform-free net10.0 carrier from the same Kernel source, into
`dist/build/Release/Neutral`. Use one carrier in a process. Tools must reference, not redistribute,
the carrier. The legacy default net472/1.6 build is retained for existing maintenance callers.
The explicit RimWorld17 target and its local native-compilation evidence are described below.
No game DLL, font or shader is part of the tool/core distribution.

## Owner layout budgets and edit completion

Use `host.SetLayoutInput("help-drawer", new UiLayoutInput(height: availableHelpHeight))` for an
existing structural container (Stack/Column/Row/Wrap/Section/Surface/Scroll/Clip/Overlay/Repeat). This is a parent budget, not a replacement for a leaf control's appearance or native editor sizing. Runtime Height overrides its declared Height/Auto/Fill/MatchContent/VisibleRows.
`narrow: true` or `false` selects a container's existing responsive variant (it must declare Breakpoint); null retains its
Breakpoint decision. Pass null to remove the runtime input. No XML is replaced, and the definition
revision does not change. Inputs are Host-owned, apply before the next arrangement and use the
conservative full geometry transaction, including narrowed scroll width, hit, clip and popup geometry.
Compatible node identity, drafts, native focus and scroll remain with the same Session. Removing or
changing the kind of a declaration prunes its runtime input. Repeated rows keep their existing typed
binding/item routes; this first constraint API addresses named declarations, not an alternate row system.

`host.CompleteEdit(id)` and `host.CompleteEdit(id, cancel: true)` use the named wrapper's original
parser and editor. They need no Tools connection. They return whether that UI completion was handled,
not whether a setting was accepted or saved. Composition, unavailable input, modal/capture ownership,
readonly submission and another active editor can refuse completion. SetDraft changes only the local
draft; explicit commit makes at most one typed business request. Invalid drafts remain for correction.
Explicit confirmation retains the original conflict policy; cancel restores the current model without
requesting a write. Existing pointer blur, Live editing and native Accept/Cancel retain their policies.

At an owner boundary, `host.EndElementLifetime(id)` retires just that mounted subtree through the
Session's existing release path. It cancels local input/output/resources without completing an edit or
changing business state. Sibling drafts, focus, scroll and state survive. The next explicit/native advance
can remount the same declaration with a new incarnation; old queued targets return Closed. Layout
caches use the existing full fallback after retirement. This is the needed local lifetime seam, not a
new family of embedded native backends.

## Give a custom field draft, commit and cancel operations

A custom numeric or sparse field should keep its own parser and typed request, rather than pretend to
be a range or write the business value directly. Implement IUiSemanticWidget and forward the three
editing intents to the same Controls owner used by the native field:

```csharp
public bool ExecuteAction(UiWidgetContext ctx, UiSemanticAction action, UiToolValue value)
{
    return ctx.Controls.EditSemantic<string>(ctx, binding, action.Operation, value.Text,
        text => text, Parse, live: false,
        candidate => ctx.Bindings.Set(binding, candidate),
        () => ctx.Bindings.Get<string>(binding));
}
```

`Parse` is the custom field's existing `UiEditParser<string>`; for a numeric `T`, pass that field's
formatter/parser and typed callbacks instead. Advertise SetDraft, CommitEdit and CancelEdit as part
`editor` in ReadActions. The separately compiled `AuthorExamples.CustomControl` forwards its real
float codec and callbacks through this public entry; it has no internals grant or private focus calls.

Exact public-unstable signature:

```csharp
public bool EditSemantic<T>(UiWidgetContext ctx, string binding, UiToolOperation operation,
    string? text, Func<T, string> format, UiEditParser<T> parse, bool live,
    Action<T> request, Func<T> readModel);
```

Call it synchronously on the owning UI/headless thread, from the original owner-created context.
`live` matches the native Edit configuration; explicit SetDraft always remains local, even when that
native configuration is Live. Commit invokes the original typed request at most once for the draft;
cancel restores readModel without requesting a write. False means an unsupported/unavailable intent
or invalid completion, not saved data. Unsupported operations and null SetDraft text are refused.
The Host still validates opaque target lifetime, visibility and current command qualification.
Controls checks composition, readonly submission, disabled/input/reliable-transaction state, modal/
capture ownership, arranged ancestry and active-editor ownership itself. Drafts are not created by a
read. Invalid draft completion preserves focus; success/failure exits retain the original Session
focus and invalidation chain. Do not replace this with isolated UiEditSession.Input/Confirm calls.
No Tools connection is required for normal custom-owner use; queued agent operations retain the same
connection/result/idempotency contract.
## Read native instrument results through the connection

`connection.ReadDiagnostics()` returns the original bounded diagnostic events and optional completed
geometry snapshot. No subscription is created by the read. GeometryStatus is NotSampled until a real
instrumented Draw completes; a headless arrangement cannot create native geometry capture. Each
geometry sample retains its own Host/Session/pass identity. Export it separately from semantic capture
sequence Revision unless the actual source/pass correspondence is established. Normal Release enabling
of geometry/overlay needs the selected compatible connection; old FER_DEV direct instrument calls and
ordinary bounded logging/timing/R9 remain legal under their existing conditions.

## Optional original-window cost facts

The actual window may implement `IUiCostSampleProvider` and call
`host.SetCostSampleProvider(provider)` on its owner thread. It supplies two typed methods:

```csharp
bool RequestSample(UiCostSampleRequest request);
UiCostSampleSnapshot ReadSample();
```

This is a projection of the existing window sampler. It is not a new sampler, registry, timer, log
recorder or arbitrary-command endpoint. Ordinary sampler/logging use keeps its existing conditions
without Tools. The window retains the logging gate, permitted pass counts, begin/drain/end policy,
partial/close cleanup and resources. `SetCostSampleProvider(null)` detaches; Host close also releases
the attachment, without disposing or restarting the author's sampler. The original window must finish
its own sample on detach/close. Replacing the provider, including detach/re-attach of the same object,
retires queued work for the old attachment. Assigning the unchanged current provider is a no-op.

On the selected connection's owner thread, call `connection.ReadCostSample()` to select/read the
current optional attachment. No provider returns Unsupported with absent quantities. A provider with
no sample returns NotSampled, not a zero-valued measurement. The read must only project retained
facts: it does not enable, begin, drain, advance, complete or reset a sample. Ordinary provider read
exceptions return Failed and an explicit reason, never an empty successful result.

```csharp
var before = connection.ReadCostSample();
// The requested count is explicit author/client policy, not a library default.
var queued = connection.QueueCostSample(requestId, new UiCostSampleRequest(passes));
// Advance the selected headless sample, or wait for its actual native callback. Never do both.
// Query connection.Result(requestId), then owner-thread ReadCostSample for retained progress/results.
```

QueueCostSample is background-safe and shares the existing 64-entry queue, 256-ID result ledger and
one-entry-per-owner-advance budget with semantic operations. It requires Operate and a provider selected
by ReadCostSample; it cannot discover another window. Reusing any semantic/sample request ID returns
its original result, even with changed arguments. The provider receives one request on the original
Host thread. True gives Handled (accepted, not completed); false gives Rejected; an exception gives
Failed with Attempted=true, without retry. Disconnect/Host close cancels unattempted work; provider
replacement gives Closed/Attempted=false at the next owner boundary. No target is substituted.
An already accepted sample keeps the original window's lifetime and stopping policy, not the connection's.

Requests accept 1..256 owner passes, with no default. The provider may reject unsupported counts or
unavailable gates; the library does not reinterpret passes as frames or impose a consumer's count.
UiCostSampleSnapshot carries Status, original State (NotSampled/Requested/Sampling/Complete/Partial),
Source, Session, Sample, Page, Scene, Size, Definition, Revision, RequestedPasses, CompletedPasses,
FirstPass, LastPass, SceneChanged and EndReason. All unknown numerical/bool identities/counts are
nullable; State is also null when unavailable, rather than pretending a failed read is NotSampled.
Strings and reasons are author-sourced facts. Use Environment facts for start/end revisions,
fit/log/geometry flags and other original context, preserving each fact's Status/Source.

Metrics is a bounded list of UiCostMetric(Category, Source, Status, Calls?, TotalMilliseconds?,
MaximumMilliseconds?). An author can project its original text-height, text-width and environment
accumulators plus its own draw classifications, keeping distinct sources. Missing/unsampled groups
retain absent quantities and their status; actual measured zero is legal only when known from that
source. These are original pass/accumulator facts, not FL timing, render FPS or inferred native samples.
Each snapshot copies at most 64 metrics and 64 environment facts, limits all text to 512 characters,
and reports Truncated. The detached snapshot contains no provider, Host, node or callback handle.
Provider work itself remains the author's bounded pure projection responsibility.

The separately compiled WindowCostSamplePeer forwards existing window request/read callbacks with no
sample state. NativeSettingsPage's additional constructor attaches such a peer to its real Host; its
old constructor still works. The native window must return its own retained snapshot, rather than
inventing numbers in the adapter. Tools and authors adopt this fixed interface independently.

## Native 1.7 build

Build the same project with `-p:UiKitTarget=RimWorld17 -p:RimWorld17Dir=<local-game-directory>`.
This is net10.0 and references the chosen local Assembly-CSharp / UnityStubs / RimWorld / Waylith
assemblies with Private=false. It reuses the public Verse Window/Settings adapter source rather than
creating another renderer. The 1.7 build excludes the unavailable Unity fontNames environment body;
unknown native font revision disables optional metric reuse. Numeric Editor size retains the reported
preset fallback. The explicit legacy default target is maintenance compilation, not the 0.9 installable
1.7 carrier. Target outputs and restore intermediates are isolated.

The current selected local reference is 1.7.5000 rev1275. Earlier 4998 observations remain bound to
their original fixed input. Game execution, physical IME/input and wrapper appearance remain separate
acceptance work. The game's internal ImGui host/backend/dispatch/IME path is handled by the separately
authorized Tools bridge owner; standalone native evidence belongs to that owner. No reflection, copied game implementation, alternative renderer or game bootstrap
is supplied by this library.

## Author documents and same-screen selection

The additive `UiPreviewContext` registration overload supplies the explicit author directory, opt-in
watch and startup language together with the existing metrics/translation/source. The original three-
argument registration and four/five-argument preview constructors remain legal. The six-argument
preview constructor borrows the author's document service; resources still own its lifetime.

`UiDocumentService.AddProjectedLayout` validates one full author candidate and passes its pure
projection, including templates/styles, through the original last-good transaction. Accepted source
text, definition and version move together. `Snapshots` and `Locate` read retained accepted source,
including the original declaration of a repeated template, without rereading disk. `StatusRevision`
is the owner-thread status stamp for on-change inspectors; restoring identical last-good bytes clears
the active failure without rewriting the historical report ledger.

`UiToolConnection.SelectNode` selects one mounted node for the original draw walk's clipped outline.
`ReadSelection` reports current Window/Clip or the reason it is unavailable. Selection is independent
of whole-Host sampling. Observation facts distinguish declared attributes, author layout input,
resolved padding/gap and computed parent/measure/scroll facts. No CSS margin model is invented and
these full reads remain on demand, not per-frame dumps. API range and frozen Stable signatures stay
unchanged; these additions remain public-unstable.

### Part/item incarnation and tool exit work

After capture, the original owner follows only the captured/pending node capabilities at its explicit
advance/native callback. The bounded current part/item view retires absent or changed capabilities;
returning with the same key receives a new ItemLifetime. Leaving a bounded capability view revokes an
old address even if the business item still exists outside that view. This is not a claim that a business
object was destroyed. A new capture replaces the selected view, without replaying requests. No Tools
connection, or a connected Host with no captured/pending target, performs this semantic follow work.
Disconnect clears the interest/incarnation metadata; if Tools alone acquired the diagnostic subscription,
it is released. A subscription explicitly borrowed by a normal caller remains usable under its original
ownership, with the prior instrument switches restored. Background disposal closes the queue immediately;
remaining UI-side instrument teardown runs at the next real owner callback or Host/Session close.

### Compile the real Window and Settings call sites

The separate public AuthorExamples assembly contains both `CounterPage.Standard` (UiPageWindow)
and `NativeSettingsPage.DoSettingsWindowContents(Rect,bool)` (the normal embedded settings callback
route). The settings adapter forwards the actual native rectangle/input qualification into the same
CounterPage Host with VerseUiExecution and releases its Host before its execution owner on detach.
It owns sample UI data only, not ModSettings persistence or game state. These are compilable public
caller paths; their native rendering and lifecycle still need actual owner interaction evidence.

## Draw the formal window around the existing preview Host

Use the optional window capability when a preview represents a complete window, rather than a page.
`UiWindowShell` contract version 1 is the formal face/page-lifetime implementation used by
`UiWindowHost`. Its background, accent, title/subtitle, measured close button, content arithmetic,
fit scopes and Host release path now live in the neutral Kernel. The native adapter forwards its
existing virtuals and primitive hit tests; it has no second face renderer. Existing native subclasses
and compiled callers keep their signatures, defaults and input/close/catalog conditions.

The author derives `UiWindowShell`, supplying Theme, Metrics and DrawNotice and the original window's
vocabulary/policy through Title, Subtitle, CloseText, geometry, BeforeDraw and RequestClose as needed.
Share that author policy with the production window; do not paint another frame. DrawBackground is an
optional guarded underlay. RequestClose defaults to Dispose; an author veto must leave it alive when
refusing. The native bridge still invokes the original native Close/CanClose and stack lifecycle.

The author explicitly transfers its already-built page to the shell with the additive constructor:

```csharp
// page, documents and authorShell come from the author's actual isolated window composition.
var preview = new UiPreviewInstance(page, sampleSource, measurementSource,
    resources: documents, prepare: preparePage, documents: documents, windowShell: authorShell);
// preview.Host and preview.WindowShell.Host are the same object, with the same Session/doc attachment.
```

The original four-, five- and six-argument constructors remain page-only. The registration factory,
three-argument provider and UiPreviewContext overloads keep their conditions. Window/Settings kind
alone never promises a complete window; only a non-null WindowShell declares this capability.

On each actual owner callback, use the shell INSTEAD OF separately drawing/advancing the page:

```csharp
var shell = preview.WindowShell;
if (shell != null)
    shell.Draw(face, qualifiedFrame, textExecution, windowOrigin);
else
    preview.Host.DrawFrame(face, qualifiedFrame, textExecution);
```

`face` and the supplied pointer use the same logical, local coordinates. `windowOrigin` translates
that coordinate origin into window/screen space for completed chrome geometry; it is not a viewing
transform. Pass the owner's existing IUiExecution, including paired text capabilities. Native Window
still captures the page frame after its original chrome/BeforeDraw/attach boundary and retains native
keyboard first-refusal and close-veto hooks. Independent adapters own their equivalent input routing.
No backend is created or disposed by Draw. Do not also AdvanceHeadless on that native Host.

`ContentRect(face)` is the actual local page box. `GetChrome(outer)` reports current geometry in the
outer rectangle's space; it is not a completed frame record. Outside Draw, pass the actual text drawing
capability to `ContentRect(face, drawing)` or `GetChrome(outer, drawing)` for paired numeric font
measurement (or override TextDrawing with the owner's borrowed pair). Draw itself always uses its
supplied pair. Completed diagnostic chrome remains attached to the original completed Host pass.

Ownership and exits:

- Native factory usage remains lazy: BeforeDraw receives the actual content box before CreateHost;
  HostAttached runs before that Host's first draw. The preview constructor instead adopts its existing
  Host immediately and raises HostAttached there. It does not call CreateHost, arrange, draw, prepare
  or attach another document transaction. Wire author attach/detach callbacks before construction.
- The shell alone disposes the adopted Host. HostDetached sees a live Host on failure/close and the
  Host is disposed even if a handler throws. A close from HostAttached prevents that first draw.
  A failure retains LastFailure, releases Host and shows PageUnavailable on the next pass without retry.
- The preview owns resources; Documents is borrowed and disposed only if included in resources.
  Dispose releases shell/Host first and resources in finally. Closing the isolated shell also disposes
  its preview resources exactly once. A failed page releases per-Host attachments; whole-preview
  resources remain available to the terminal notice until close. Execution/metrics/business owners
  are borrowed, and are not silently moved into the shell. Keep them alive through that notice.
- Isolated Dispose is terminal. The native adapter preserves the old ability to draw the same Window
  instance after PreClose and create a fresh Host; failure notice/retry conditions remain unchanged.
  Closing never commits a draft or changes the author service's last-good transaction.

This interface is public-unstable in API [0.9.0,0.10.0). Select a carrier that advertises window-shell v1;
API 0.9.0 by itself does not identify which development candidate contains this additive interface.

## Export without selection ink, keeping selection facts

`UiToolConnection.SelectionOutlinePresentationVersion` is 1. On the connected Host's owner thread,
wrap the actual clean-target draw (not just the later texture readback) in the new scope:

```csharp
using (connection.SuppressSelectionOutline())
{
    // The original owner draw: shell.Draw(...) for a complete window, Host.DrawFrame(...) for a page.
    drawOriginalTarget();
}
// The next ordinary inspection draw keeps the original selected-node outline.
var currentSelection = connection.ReadSelection();
```

The method returns IDisposable, accepts ReadOnly or Operate connections and adds no diagnostic
subscription. Nesting is supported. Dispose once or repeatedly is safe, including background scope
cleanup; begin belongs to the owner thread. Always use using/finally so exceptions and cancellation
restore presentation. Connection close invalidates outstanding suppression immediately, even before
owner-side detach; Host/Session close follows the existing connection cleanup. Other Hosts/connections
are unaffected. An unclosed scope remains an explicit presentation choice until disposed or disconnected.

SelectNode/ReadSelection do not change. The original draw walk still updates the same selection's
mount qualification, frame/definition, Window/Clip and clipping/retirement status before deciding whether
to paint its four lines. No clear/reselect, invalidation, extra arrangement or tree is involved.
SetInstruments geometry/overlay/timing retains its independent diagnostic meaning. For a clean target,
the exporter must also manage any diagnostic overlay it previously enabled under that existing contract;
SuppressSelectionOutline only removes selected-node ink. Keep inspection and clean-target draws/frame
identities distinct in the consumer's export metadata.

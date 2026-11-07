# Connect a tool to the real page

The normative contract is [developer-tools](../../openspec/specs/contracts/developer-tools/spec.md); this guide owns current API usage and implementation evidence. New native work follows RimWorld 1.7 APIs rather than a fixed minor build; historical build identities below remain evidence, not a support ceiling.

API 0.9.0. The tool/preview surface is public-unstable. Existing Host, standalone controls,
visual-only, R9 reads and bounded diagnostics do not require Tools.

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

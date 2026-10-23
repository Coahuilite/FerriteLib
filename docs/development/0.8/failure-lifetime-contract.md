# Managed failures, retained display and view lifetime

RU3 A–C, R1/R2, H1–H3 and L1/L2 are confirmed directions and connected to the production runtime,
presentation, default wrappers and Host. Their supported author entries and necessary dependencies are
stable under [S1–S7](public-contract.md); detailed R9 stays experimental. Native/platform acceptance and
future backends remain separate. The product rules live in the
[specification](README.md); this page describes the usable implementation and its evidence.

## Retry a pure display, not the operation that changed business

```csharp
var runtime = new UiReactiveRuntime();
var count = runtime.Int(4);
var broken = runtime.Boolean(false);
using var owner = runtime.CreateOwner("counter");
var caption = owner.Compute("format", () =>
{
    int value = count.Value;
    if (broken.Value) throw new FormatException("format unavailable");
    return "Count " + value;
});
var bindings = new UiBindings(runtime);
bindings.BindTracked("caption", () => caption.Value);
bindings.BindCommand("reset", () => count.Value = 0);
bindings.BindCommand("retry-display", caption.Retry);
```

`caption.Value` either returns a current successful value or throws its cached `UiReactiveFault`.
`Fault`, `HasValue` and `TryGetLastValue(out value)` distinguish current failure from the last successful
value; a first failure has no value. The last value is a display fact, not a business answer.

The failed attempt keeps **its actual read dependencies**, including reads made before an exception
and failed-child reads. It replaces obsolete successful-branch dependencies. An unrelated source, old
branch change, repeated read, draw, timer or show cannot authorize another attempt. A relevant connected
input change or `Retry()` does. Retry follows known failed dependencies, not arbitrary fields or business
commands. It marks pure work pending; demand and the existing legal synchronization point decide when
it runs. Successful recovery replaces dependencies and clears the fault.

`UiReactiveFault.Identity` locates the origin, `Kind` distinguishes failure classes, `StoppedScope` includes
the demanded dependent path, and `Recovery` names the applicable entrance. A cached failure is not logged
or rebuilt on every draw. Exceptions outside a managed calculation retain the existing structural/adapter
protection; ordinary getters are not silently enrolled in a new purity or retry contract.

## Resume an automatic consumer callback explicitly

```csharp
UiReactiveResponse notification = owner.RespondManaged("completion-notification", () =>
{
    int current = completed.Value;
    ApplyConsumerNotification(current); // may already have written before an exception
});
// The owner checks its business result and repairs the integration first.
if (notification.IsPaused)
{
    Inspect(notification.Fault!.Identity, notification.Fault.StoppedScope);
    notification.Resume();
}
```

The handle exists even when the initial run fails. R2 pauses that callback, leaves applied writes intact,
and continues independent queued responses. Source changes do not resume it. `Resume()` runs current
state once through the existing batch/drain boundary; it does not replay historical notifications.
`Dispose()` releases the callback and its subscriptions. `Respond(key, callback)` retains its original
`IDisposable` signature and returns the same `UiReactiveResponse` handle; new callers can use the typed
`RespondManaged` entrance. Its failure behavior now follows R2 rather than throwing and discarding the
entire drain. Library-owned pure derived storage remains R1, not a consumer callback.

A normal Button failure ends that explicit request. Its next qualified click is a new request; the node
is not permanently tripped. Neither path rolls back, compensates, cancels orders or assumes business
acceptance. An external service's commands, concurrency, scheduling, idempotency and result freshness
remain with that service/application.

## Read validity and paint the last successful control separately

`host.Presentation.GetStatus(key, options: false)` reports `HasLastValue`, `Fault` and `IsCurrent` for that
view's synchronized input. `host.Presentation.Retry(key, options: false)` retries the associated pure
binding calculation and known failed dependencies. `UiBindingPresentation.Synchronize()` is the eager
explicit consumer entrance; failures remain queryable per key while independent complete values update.
A normal `bindings.Get<T>` inside `Enter()` throws for a failed current value: it never returns retained
old options, a fabricated zero or an empty successful list.

The default wrappers provide the display side without changing their definitions:

- Read-only descriptive wrappers can paint their last synchronized value with the existing theme's
  danger-colour marker and `!`. A first failure paints an unavailable band with its located identity.
- Checkbox, Slider, TextField, NumberField, StepperSlider, Dropdown, ModeRow, Tree and Chart retain explicit
  **pure paint facts** from a successful control display. Fault rendering calls dedicated paint-only
  helpers, not `Draw`, native editing, `Observe`, qualification, binding reads, popup callbacks or requests.
  Resolved labels/row text are captured too; a failed managed translation is not reread by retained paint.
- Editors paint the reliable same-target draft as static field content while input is unavailable. They
  retain the draft, suspend focus/capture, clear pending submission and close the failed control's popup;
  recovery does not submit a queued edit. Target/definition replacement cannot inherit an old snapshot.
- Content-sized retained editors, ModeRow and Tree remeasure known last-display content against current
  safe geometry/metrics. They do not publish a failed calculation as successful measurement. Independent
  controls with reliable geometry and current qualification remain usable.

`UiNode.ReactiveFault` identifies the affected node. Failed current eligibility is disabled, never allowed.
If a required shared width/visibility/items/layout read fails outside a locally complete control output,
the existing arrangement transaction restores its complete previous geometry. `UiHost.LayoutFault`
reports that actual Host layout/input boundary; the previous display remains marked, but the Host accepts
no input until a complete valid geometry/hit pass succeeds. It retains the session and reliable drafts
rather than reaching the standard shell's fatal release. Unclassified structural/adapter faults retain
that release protection. Cache scope and failure-validity scope are not assumed to be identical.

## Tell a retained view when it really has no display demand

```csharp
host.SetDisplayDemand(false);  // retained embedded view is reliably hidden
// Business sources and independent services may continue changing.
host.SetDisplayDemand(true);   // next DrawFrame adopts latest demanded input before accepting input
```

`UiWindowHost.SetDisplayDemand` provides the same ordinary standard-shell entrance, including before its
Host is created. This is explicit visibility information, not catalog focus, a clock or inferred occlusion.
Hidden frame calls do no presentation evaluation or measurement/arrangement. Hidden default subtrees
also do not prepare their exclusive tracked inputs; VisibleKey/Tab and other actual layout consumers
continue to be read. Shared lazy computations still serve any visible or explicit consumer. Source
invalidation and subscriptions survive hiding; no intermediate refresh or operation is replayed on show.
R1 failure and R2 pause survive hide/show. FL never disconnects an independent service because of visibility.
Whole-Host hiding suspends editing focus and owned popup input while retaining drafts, clears queued
submission and rejects its Accept/Cancel entry points. Showing does not submit that retained draft.

All defaults prepare their declared tracked consumer input before their own measurement and before Draw
can issue requests. Custom tracked readers declare their complete actual demand through descriptor
binding roles or IUiPresentationDemand.PresentationKeys(context); there is no prepare-every-key
compatibility path. This is an explicit tracked-author migration, not a purity prerequisite for ordinary
untracked getters. This declaration is over public binding keys, not internal nodes or recovery queues.
Ordinary getters/options still use their original notification and legal synchronization conditions.

## Keep shared ownership distinct from input focus and modal policy

Each Host disposes only its presentation and session. Put shared computations under an explicit owner
that outlives both views. B losing catalog/native input focus does not stop B's display updates. B truly
hidden does not stop A's shared computation; closing B does not dispose A, that shared owner or a service.
Cross-close/rebuild long drafts belong in an application pending-edit model, not in a destroyed UI session.

An owned nonblocking confirmation can be cleaned from B's existing `HostDetached`/close lifecycle. Capture
its original target (`A17`) when opening it; do not reread B's later selection (`B23`) on Confirm. The business
entry checks current eligibility and returns the real rejection/result. Repeated close and late UI callbacks
must see the disposed confirmation and issue no operation. UI ownership does not decide which other windows
are blocked. Existing Verse global modality/options remain; no B-local-modal backend, universal occlusion
analyzer, native window manager or 1.7 implementation is added.

## Distinguish a pure cycle from a work budget

Pure recursive evaluation reports `pure-cycle` on the actual computation path and caches that failure.
The production scheduler allows up to **256 executions of each same response per Drain**. Exceeding this
engineering budget pauses that response with `propagation-budget`; it means the permitted work did not
stabilize, not that a complete business cycle was proved. Other queued responses continue. A finite
0→1→2→3 adjustment completes; hundreds of independent one-shot responses do not share that repeat cap.
Recovery is R1 for pure work and owner Resume for a consumer callback. FL checks only when control returns;
it cannot interrupt a callback that never returns or discover every external asynchronous loop.

## Compile and inspect the actual paths

[`FailureLifetimeExamples.cs`](../../../tools/FerriteLib.UiKit.Core.ContractTests/FailureLifetimeExamples.cs) supplies `FailureCounterExample` and the independent
`FailureWorkshopExample`, including intentionally faulty notification wiring and captured-target confirmation.
Both the no-platform target and actual Host harness compile the same file. [`FailureLifetimeTests`](../../../tools/FerriteLib.UiKit.Core.ContractTests/FailureLifetimeTests.cs) verifies
production dependency/fault/queue semantics and real counters. [`KernelFailureLifetimeTests`](../../../tools/FerriteLib.UiKit.Tests/KernelFailureLifetimeTests.cs) exercises default
paint and request paths, first/branch/label failures, reliable drafts, selection/tree/chart, shared geometry,
real subtree/Host demand, conservative/explicit custom reads, two standard shells, owner cleanup and A17/B23.
The complete inventory remains [migration-status.md](migration-status.md), not these two examples.

The no-platform sources are the production `NeutralSources.props`; Host/Verse fixtures and native game
acceptance are distinct. These fixtures do not establish actual 1.6 font pixels, IME or window-stack
composition. The target remains RimWorld 1.6.4871 / Unity 2022.3.35f1; native acceptance remains pending.

# Reactive foundation: operations, presentation and ownership

The 0.8 Dev foundation connects the **same production** `UiBindings`, reactive graph and presentation
read view to both `NeutralSources.props` and the actual `UiHost`. It does not implement RU2 local layout
caching or complete the migration of every registered use. See the per-use [inventory](migration-status.md).

## Connect a scalar once

```csharp
var runtime = new UiReactiveRuntime();
var count = runtime.Int(0);
var bindings = new UiBindings(runtime);
bindings.BindTracked("caption", () => "Count: " + count.Value);
bindings.BindCommand("increment", () => count.Value++);
```

Declare tracked bindings before constructing the Host. A `text/wrapped` bound to `caption` follows the actual reads without `NotifyChanged`. Changing the
caption invalidates the existing full page arrangement before the next legal presentation opportunity.
No local-cache or per-node-layout performance claim follows from this implementation.

`Int`, `Text` and `Boolean` use integer equality, ordinal string equality and boolean equality. Equal
storage writes do not invalidate; command invocations and selection requests still happen separately,
even when their final stored values are equal. Ordinary objects can expose these sources through C#
properties; no base model class or automatic deep observation is required. There is no public generic
reference comparer, collection observer or automatic in-place mutation interception in this package.

`BindTracked<T>` is an explicit new getter contract: a pure calculation over explicitly connected
sources in **one runtime**. The runtime owns one graph and serial access context; do not compose graphs
by reading sources from another runtime. Cross-runtime reads/writes inside a tracked or pure-read call are diagnosed. One temporary
`[ThreadStatic]` current-runtime marker is saved/restored through nested calls and `finally`; no graph,
business data, subscriptions or scheduler live in static state. The outermost exit leaves no marker.
Ordinary `BindValue`, `BindReadOnly`, `BindOptions`, hand-written
`IUiBindings` and custom `IUiWidget` retain their original live-getter conditions, including serially handed-off untracked registry calls
without a new creation-thread prerequisite. Explicit runtime/BindTracked/BeginOperation use activates the
new serial-access contract. The platform-only audit
partial forwards existing binding type-mismatch diagnostics; the neutral source has no platform reference.

## Read now; present one consistent version

Assignment and dependency invalidation are immediate. `owner.Compute("caption", getter).Value` refreshes
known-stale derived data even inside a batch. Successful reevaluation replaces actual dependency edges;
changing a branch to an equal-valued source still updates subsequent following relationships.

`UiHost.MeasureAndArrange` synchronizes its own `UiBindingPresentation` before entering the existing
engine. `Draw` consumes that same published read view. A tracked binding read from this presentation
scope sees the synchronized input, including when an earlier input callback has just changed the model.
A failure while gathering inputs publishes no partial read view or layout. A later explicit frame can
attempt synchronization again; no operation is replayed. Direct split-phase users must keep the
`MeasureAndArrange` result and its corresponding `Draw` together, without another arrangement in between.

Business operations through `Set<T>`, `Invoke(string)`, `TryInvokeCommand`, `Invoke<T>` and the boxed
choice setter temporarily leave the presentation read scope. Their binding reads are immediate; their
completion restores the current presentation view. Outside a presentation scope `Get<T>` is also an
immediate business read. Direct source/derived reads are always business reads, not presentation reads.
Input handling in the same native Draw stack remains legal; hover, pressed and capture feedback are not
frozen. `CanExecute` and execution qualification temporarily leave only the presentation scope, so predicates
using `bindings.Get<T>` read latest business input too. This read boundary does not start/end a business
batch, drain responses or publish presentation input; success and predicate failure both restore the scope.
Execution rechecks qualification. Business code keeps the right to reject independently of the UI projection.

A custom new pure measurement/draw routine can use `using (runtime.ReadPhase()) { ... }` to enforce
no reactive writes/commands. Tracked getters enforce this themselves. The Host does **not** put old
custom measurements or ordinary getters into this new pure phase.

## A logical operation is not a transaction

```csharp
using (bindings.BeginOperation())
{
    bindings.Set("count", 1);
    // bindings.Get<int>("count") is 1 here, not the old presentation value.
    bindings.Invoke<int>("add", 1);
}
```

All four actual binding operation entrances create implicit nested batches; external projection application
can use `runtime.Batch()`. Automatic responses wait for the outer boundary. The shared
`UiValueBehavior.Request(..., UiBindings, request)` boundary is used by the real Checkbox for its compound
Set-before-Invoke operation, as well as the no-platform behavior E2E. Hand-written `IUiBindings` keeps the
existing callback route; other shared extensions must choose the new operation overload explicitly when
they compose multiple operations. Single-binding requests already enter the same runtime batch.

`UiNotifyAdapter` remains an explicit mapping/notification adapter. Its capacity-driven Flush is not the
new automatic-response boundary. Neither window repaint nor native callback count drives responses.

`owner.Respond("audit", callback)` schedules its initial dependency-discovery run through the same
drain as later invalidation. Outside a batch it runs before registration returns; inside a batch the initial
run waits for the outer boundary, never observing a half-applied operation. Responses may write, scheduling subsequent work rather than recursively executing a
subscription chain. At most 100 repeats of the **same response** are allowed in one drain; independent
responses are not subject to that total-count cap. A loop/fault reports the owner/key and discards the
remaining drain, retaining applied business writes. Detailed retry/recovery and hidden-node observation
policies remain RU3 decisions. Normal failures are not silently retried or turned into successful saves.
The binding entry points retain both business and drain failures if both fail.

## Close the view, not the service

`runtime.CreateOwner(name)` owns computations/reactions; `CreateChild(key)` creates an independently
closable child lifetime. Dispose removes its subscriptions and releases callback references, is idempotent,
and never saves, rolls back or destroys an external model. A Host owns its presentation calculations,
not the bindings' runtime, independent business owners or external services. Closing a Host twice leaves
those services running. Manually disposed calculations/reactions stop following their sources.

A failed initial evaluation leaves no temporary edges/context. A failed reevaluation retains the last
successful dependency set and no provisional additions. Calculation cycles and response cycles are
located and bounded. Layout/input cleanup continues to use the existing Host/session failure path.

## External business: deliver a safe result

The compilable `WorkshopService.cs` fixture contains no FL types. Its lock-protected snapshot is copied
by `WorkshopProjectionExample.Apply` onto the projection runtime's owning context, inside a batch.
Tracked getters read only the projection scalars, never the mutable service. Commands go back to the
service, which decides whether to accept; the projection is refreshed from its safe result. The caller
owns tasks, dispatch and result freshness. FL supplies no application work queue or latest-result policy.

The compilable examples are `tools/FerriteLib.UiKit.Core.ContractTests/ReactiveExamples.cs`,
`WorkshopService.cs` and `WorkshopProjectionExample.cs`. The adapter harness links the same files.

## Evidence and remaining scope

`ReactiveFoundationTests` runs counter/shared operation → value/invalidation/derived/batch/response →
presentation → production neutral geometry, and the separate safe business projection through the same
chain. `KernelReactiveFoundationTests` exercises actual `UiBindings → UiHost.DrawFrame →
MeasureAndArrange → Draw`, default Button press/release and wrapped-text paint/content-dependent geometry
for both examples, Checkbox release/compound batching, legacy/new purity boundaries, latest
qualification, failure and Close. Existing wrapper/editor/layout/document/observation regressions remain
separate evidence. No alternate test core or same-named platform stub implements the reactive graph.

These tests do not establish native input, real font pixels, IME, window-stack composition or 1.7 support.
RU1 reference/collection surfaces, RU2 local-cache outputs, RU3 detailed recovery/hidden policy, RU4
remaining old-entry migration and RU5 platform delivery commitments remain open in the main specification.

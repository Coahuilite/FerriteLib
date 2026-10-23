# Reactive sources: values, collections, presentation and ownership

The 0.8 Dev foundation and RU1 connect the **same production** `UiBindings`, reactive graph and presentation
read view to both `NeutralSources.props` and the actual `UiHost`. RU2 now retains measurement/local
arrangement work on this chain; its single contract is [layout reuse](layout-reuse-contract.md).
Full new-reactivity migration is still tracked per use in the [inventory](migration-status.md).

## Connect a scalar once

```csharp
var runtime = new UiReactiveRuntime();
var count = runtime.Int(0);
var bindings = new UiBindings(runtime);
bindings.BindTracked("caption", () => "Count: " + count.Value);
bindings.BindCommand("increment", () => count.Value++);
```

Declare tracked bindings before constructing the Host. A `text/wrapped` bound to `caption` follows the actual reads without `NotifyChanged`. Changing the
caption selects its measurement capture before the next legal presentation opportunity. RU2 compares
actual stage inputs/results, retaining unrelated calculations and local plans while publishing current
effective positions. Source synchronization still completes before layout reuse is considered.

`Int`, `Text` and `Boolean` use integer equality, ordinal string equality and boolean equality. Equal
storage writes do not invalidate; command invocations and selection requests still happen separately,
even when their final stored values are equal. Ordinary objects can expose these sources through C#
properties; no base model class or automatic deep observation is required. `Value<T>(initial, equal)`
connects other value/reference types with a required caller-selected equality function. A mutable-object
source normally explicitly chooses `(a, b) => ReferenceEquals(a, b)` so replacement changes following
relationships; connecting a reference does not observe ordinary fields inside it. No implicit reference,
`Default.Equals` or deep-comparison policy is introduced.

Equality functions are pure and define **stored state**, not business-operation identity. If equality
returns true the old stored value remains: do not compare two distinct observation targets only by their
current displayed count. Put replaceable target identity in its own reference source, then read its connected
properties inside the calculation. That reevaluation replaces dependencies even when the result is equal.
Same-final-result commands and valid same-value selections are still separate requests.

## Control structure; connect item properties separately

```csharp
var items = runtime.List(new[] { first, second }, (a, b) => ReferenceEquals(a, b));
var current = runtime.Value(items, (a, b) => ReferenceEquals(a, b));
var total = business.Compute("total", () => current.Value.Sum(item => item.Count));
items.Add(third);
items.Move(0, 2); // destination is the final index
items.Remove(second);
```

`UiReactiveList<T>` exposes `IReadOnlyList<T>` plus `Snapshot`, `Add`, `Remove`, `RemoveAt`, `Move`,
`Clear` and `ReplaceAll`. Reads observe **one structural source**; no per-index/Count optimization is
promised. Every accepted structural mutation publishes a new read-only outer snapshot, retaining the old
version for existing readers. No mutable backing list escapes. Element equality is required; equal ordered
replacement, empty Clear, absent Remove and Move-to-self do not invalidate. Element properties such as
`InventoryItem.Count` have their own sources: changing Count invalidates total/labels/options that read it,
not computations that read only the structure. Removing an item or replacing a collection disconnects
its old property/structure edges when the affected calculation next successfully evaluates.

Writes do not implicitly observe the output list: `owner.Respond("append", () => items.Add(item))`
runs once unless its callback actually reads a reactive input. Public `Snapshot`, `Count`, index and
enumeration reads still track, including an explicit read before a write, observable input enumeration
in `ReplaceAll`, and reactive reads inside caller equality functions. Reading and then changing the same
list can therefore form a real response cycle; the normal located cycle limit still applies.

Structural snapshots do **not** deep-copy items. A business reader can intentionally access current item
properties through that structural version. Presentation must instead project all used item properties to
immutable values during tracking; retaining a mutable item reference and reading it later is not a
consistent presentation projection.

`InventoryPageExample.cs` compiles this complete path without a model base class. Its Repeat key getter
reads structure, each scoped field/label resolves the current item by stable business key, and total/options
read connected Count properties. A new key gets its row bindings once via `RegisterKey`, before the next
synchronization. A key denotes the same logical business target; a different target must use a different key
or an explicit interaction scope. Collection replacement does not create a second Repeat/control tree.

`BindTracked<T>` is an explicit new getter contract: a pure calculation over explicitly connected
sources in **one runtime**. The runtime owns one graph and serial access context; do not compose graphs
by reading sources from another runtime. Cross-runtime reads/writes inside a tracked or pure-read call are diagnosed. One temporary
`[ThreadStatic]` current-runtime marker is saved/restored through nested calls and `finally`; no graph,
business data, subscriptions or scheduler live in static state. The outermost exit leaves no marker.
Ordinary `BindValue`, `BindReadOnly`, `BindOptions`, hand-written
`IUiBindingReader` and custom `IUiWidget` retain their original live-getter conditions, including serially handed-off untracked registry calls
without a new creation-thread prerequisite. Explicit runtime/BindTracked/BeginOperation use activates the
new serial-access contract. Binding diagnostics use the explicit neutral execution warning outlet when present; the complete
Kernel has no platform reference.

## Project actual options, not live item objects

```csharp
bindings.BindTrackedChoices("selected", () => current.Value
    .Select(item => new UiChoice<int>(item.Key + ": " + item.Count, item.Code))
    .ToList().AsReadOnly(), (a, b) => a.Text == b.Text && a.Value == b.Value);
```

`BindTrackedChoices<T>` is a pure choice getter contract with required item equality. It copies the outer
list **inside** the tracked calculation. Labels and typed values must themselves be immutable presentation
values (or explicitly frozen projections); no automatic copying of arbitrary business object graphs occurs.
The getter must read each mutable property needed by this presentation while tracking, not defer it to Draw.
Ordinary and tracked UiChoice<T> lists use the same choice endpoint. Full reads and bounded
observation prefixes select that synchronized channel without a type-probing companion.
Value and options can share the same key, as real typed mode rows require, without channel collision.
Outside a presentation, full options/choice reads project the current business values. A bounded observation
calls that same pure getter and copies only Count plus the requested indices, without a library-added full
list walk; getter-side projection work remains the author's responsibility. During presentation it reads
the already captured list. Ordinary options retain their original bounded-prefix path and conditions.

`BindTracked` also accepts an optional `equal:` function for presentation output. Int/string/bool reuse
their approved scalar policies; other output types without an explicit policy conservatively announce
successful reevaluation rather than silently choosing reference/deep equality. For Repeat keys, supply an
ordered ordinal-string comparison and return an immutable list. Comparisons run in the new pure phase.
Successful same-result reevaluation still updates graph edges but does not announce a data change when
the selected equality says equal. Computation invalidation and published value change are distinct.

## Read now; present one consistent version

Assignment and dependency invalidation are immediate. `owner.Compute("caption", getter).Value` refreshes
known-stale derived data even inside a batch. Successful reevaluation replaces actual dependency edges;
changing a branch to an equal-valued source still updates subsequent following relationships.

`UiHost.MeasureAndArrange` synchronizes its own `UiBindingPresentation` before entering the existing
engine. `Draw` consumes that same published read view. A tracked binding read from this presentation
scope sees the synchronized input, including when an earlier input callback has just changed the model.
RU3 synchronizes independent complete binding results and records invalid keys separately. Default/explicit consumer demand is prepared before that consumer measures and before Draw issues requests; custom tracked consumers declare complete descriptor roles or IUiPresentationDemand, without a prepare-all fallback. A failed current read throws; only dedicated display paths may use retained old paint. Geometry publication remains transactional. Retry requires related input or an explicit pure request; another frame is not a retry. Direct split-phase users must keep the
`MeasureAndArrange` result and its corresponding `Draw` together, without another arrangement in between.

Business operations through `Set<T>`, `Invoke(string)`, `TryInvokeCommand`, `Invoke<T>` and the boxed
choice setter temporarily leave the presentation read scope. Their binding reads are immediate; their
completion restores the current presentation view. Outside a presentation scope `Get<T>` is also an
immediate business read. Direct source/derived reads are always business reads, not presentation reads.
Reactive computations/responses also read current bindings/options, including when a safe external result
is applied during a Draw stack; the surrounding ordinary presentation consumer still sees its frozen input.
Input handling in the same native Draw stack remains legal; hover, pressed and capture feedback are not
frozen. This consistency covers collection structure and projected item properties, not just an outer list copy.
`CanExecute` and execution qualification temporarily leave only the presentation scope, so predicates
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
Set-before-Invoke operation, as well as the no-platform behavior E2E. Forwarding `IUiBindingReader`
names its current `BindingOwner`; the real Checkbox uses that owner's same operation boundary while
requests still pass through the reader. Ordinary registries keep the untracked RunOperation route.
Other shared extensions must choose the operation overload explicitly when
they compose multiple operations. Single-binding requests already enter the same runtime batch.

`UiNotifyAdapter` remains an explicit mapping/notification adapter. Its capacity-driven Flush is not the
new automatic-response boundary. Neither window repaint nor native callback count drives responses.

`owner.Respond("audit", callback)` schedules its initial dependency-discovery run through the same
drain as later invalidation. Outside a batch it runs before registration returns; inside a batch the initial
run waits for the outer boundary, never observing a half-applied operation. Responses may write, scheduling subsequent work rather than recursively executing a
subscription chain. RU3 allows 256 executions of each same response in one Drain. Budget exhaustion pauses that response with a located diagnostic, not a claim that the entire business cycle was proved. Independent queued responses continue. `RespondManaged` returns an inspectable `UiReactiveResponse`, including after initial failure; only owner `Resume()` runs it again from current state. Applied business writes remain applied. See the [failure/lifetime contract](failure-lifetime-contract.md) for exact R1/R2 entrances and scope.

## Close the view, not the service

`runtime.CreateOwner(name)` owns computations/reactions; `CreateChild(key)` creates an independently
closable child lifetime. Dispose removes its subscriptions and releases callback references, is idempotent,
and never saves, rolls back or destroys an external model. A Host owns its presentation calculations,
not the bindings' runtime, independent business owners or external services. Closing a Host twice leaves
those services running. Manually disposed calculations/reactions stop following their sources.

A failed attempt records its actual reads, including first/new branches and failed children, and removes obsolete successful-branch edges. Dynamic context is restored in finally. Its current result is invalid while any last successful value is retained separately for display. Pure cycles and repeated-response budget failures have distinct cached diagnostics. Managed Host failures remain below the fatal shell-release boundary; unclassified structure/adapter exceptions keep that protection.

## External business: deliver a safe result

The compilable `WorkshopService.cs` fixture contains no FL types. After application-owned delivery to the
projection runtime's context, `WorkshopProjectionExample.Apply` applies its lock-protected snapshot in a batch.
Tracked getters read only the projection scalars, never the mutable service. Commands go back to the
service, which decides whether to accept; the projection is refreshed from its safe result. The caller
owns tasks, dispatch and result freshness. FL supplies no application work queue or latest-result policy.

For multiple items, `OrderBoardService.cs` owns its own lock and returns immutable detached rows.
`OrderBoardProjectionExample.Apply` is called only after application-owned delivery to the page runtime.
It reuses projection objects by stable key, writes only changed Count/Accepting sources, and replaces the
ordered structure only when element identity/order changes. An unchanged full result does not invalidate
all computations; changing one row does not invalidate another row's independent property calculation.
Totals/options may legitimately read several rows again. Batch neither delivers across threads nor makes
application writes transactional. Old-result acceptance policy remains the caller's decision.

The compilable examples are under `tools/FerriteLib.UiKit.Core.ContractTests/`: `ReactiveExamples.cs`,
`WorkshopService.cs`, `WorkshopProjectionExample.cs`, `InventoryPageExample.cs`, `OrderBoardService.cs`
and `OrderBoardProjectionExample.cs`. The real Host adapter harness links those same author files.

## Evidence and remaining scope

`ReactiveFoundationTests` runs counter/shared operation → value/invalidation/derived/batch/response →
presentation → production neutral geometry, and the separate safe business projection through the same
chain. `KernelReactiveFoundationTests` exercises actual `UiBindings → UiHost.DrawFrame →
MeasureAndArrange → Draw`, default Button press/release and wrapped-text paint/content-dependent geometry
for both examples, Checkbox release/compound batching, legacy/new purity boundaries, latest
qualification, failure and Close. Existing wrapper/editor/layout/document/observation regressions remain
separate evidence. No alternate test core or same-named platform stub implements the reactive graph.

`ReactiveInventoryTests` adds object/reference equality, controlled structure versus Count dependencies,
same-result source replacement, item removal, safe multi-item results, failed options synchronization,
serial/pure access, actual typed/prefix options and production neutral per-item layout. Evaluation counters
and detached-result values are recorded in its log. `KernelReactiveInventoryTests` uses actual default
typed Dropdown/ModeRow, wrapped labels, numeric editors and keyed Repeat: Add/Remove/Move/source replacement,
same-round item/structure snapshots, stable drafts/capture, same-value requests, bounded real Host reads,
fault-before-layout and independent external service lifetime. Custom probes do not supply these proofs.

These tests do not establish native input, real font pixels, IME, window-stack composition or 1.7 support.
RU1 A–E use explicit equality and controlled collection policies; no implicit reference/deep default is added.
The supported entries/dependencies are stable under [S1–S7](public-contract.md). Approved RU2
is connected through the layout reuse contract and its neutral/Host evidence; RU3 recovery/demand/lifetime
behavior is connected through the [single contract](failure-lifetime-contract.md). The item helper reduces
registration/release work without changing that foundation. Fixed delivery and platform evidence remain separate.

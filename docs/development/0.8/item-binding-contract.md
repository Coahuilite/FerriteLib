# Connect a keyed list once

Describe the source, stable UI key and each field/command once. `UiItemBindings<T>` registers the
Repeat key projection and manages the corresponding item bindings:

```csharp
using var items = new UiItemBindings<Item>(bindings, "items", () => service.CurrentSnapshot,
    item => item.Key, fields =>
    {
        fields.Value("caption", item => item.Name);
        fields.Value("count", item => item.Count, (item, value) => service.SetCount(item, value),
            (a, b) => a == b);
        fields.Command("move", item => service.TryMove(item), item => service.CanMove(item));
    });
// On a real source change, in the runtime's owning serial context:
items.Refresh();
```

The source returns an `IReadOnlyList<T>` of reference objects; it need not implement any FL interface.
`Refresh` reads one source snapshot, validates all keys/addresses, then updates UI connections in one
existing runtime batch. It also reports ordinary-field changes on retained objects. It does not deep
observe business objects, dispatch across threads or make a transactional copy of business state.
Connected reactive properties continue to invalidate their own readers normally.

Use `Items="items"` on Repeat. Template-local `Bind="count"` and `ActionBind="move"` resolve to
`items.<key>.count` and `items.<key>.move` through the existing item scope. The complete, compiled
example is `tools/FerriteLib.UiKit.Core.ContractTests/ItemBindingExample.cs`; both the no-platform
target and actual Host harness compile it. `KernelItemBindingTests` supplies its real default template.

## Keys identify current targets, not row numbers

A key must be non-empty and unique in that source snapshot. Duplicate diagnostics name the source,
key and index before changing any connection. A new object with the same key updates the target
cell; reads and operations resolve that current object. An unchanged key does not transfer a draft
to another row when order changes. For a genuinely different business target, supply a different key.

The source's order remains authoritative. A move command calls the business entry and then refreshes
from its actual result. A refusal keeps the actual order; FL does not pre-move, roll back or retry it.
Normal command dispatch does not imply business acceptance.

Deleting a key removes its field/command registrations and releases all live presentation calculations
for those fields immediately, including their property subscriptions and retained UI values. The next
Repeat reconciliation removes that row's editor/capture through the existing node lifecycle. A later
same-key addition creates new connections; destroyed drafts are not restored from a permanent cache.

Filtering a view does not destroy business objects. Supply the authoritative membership to the helper
and a separate filtered key projection to Repeat if connections must outlive filtering. If the filtered
projection is the helper's source itself, leaving it releases that UI connection. Neither choice calls
Dispose or any deletion method on external objects.

## Active business and third-party bridges share the same owner

An active service can notify its UI adapter to call `Refresh` after a real change. A third-party
adapter can map its safe snapshots to the same keys and call `Refresh` at its explicit reconciliation
point. Objects lacking persistent IDs can receive session-lifetime keys in the consumer adapter;
this does not change their save format. New snapshots for a persistent business identity reuse its key.

The adapter owns event unsubscription, safe thread delivery, snapshot freshness and the business
operation. The helper owns UI registration/reuse/release. Close the owning Host and dispose the helper
when that UI connection lifetime ends; shared business owners and other Hosts remain independent.
Both disposals are idempotent. No ordinary binding, raw native primitive or independent visual client
acquires a helper-registration or pure-tracking prerequisite.

## Evidence and limits

`ItemBindingTests` runs the production implementation without engine/stub references: current-object
reads/commands, same-reference external changes, duplicate diagnostics, deletion, immediate property
unsubscribe and idempotent disposal. `KernelItemBindingTests` uses actual keyed Repeat/default fields
and Button native press/release to preserve draft identity through reorder/new snapshots, operate on
the current object, and prune deleted state in the same Host. Third-party integration evidence is
separate; these generic examples do not establish a real consumer or native game acceptance.

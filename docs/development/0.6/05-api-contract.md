# T0 — frozen public API for the open 0.6 window

Written by the Lead before any package started, and frozen: an implementer who needs a different signature
asks for it on the shared task board, and the Lead amends **this file and the tier document in one commit**
before the change lands. The reason is the round's own instruction — fix the cross-package contract first,
then parallelise — and the demo mod, which compiles against this surface from day one, cannot chase a moving
signature.

Every type below is listed in `docs/api-tiers.md` under `## Public-unstable`, in the same commit as the
type. Two are already fully implemented in the T0 commit; the rest are signatures with an explicit
`NotImplementedException` marker naming their owning package, which the delivery check turns into a
zero-match grep (see `10-work-packages.md`, T5).

## T1 — page lifecycle and the notification adapter

Owned by `mvvm`. Files: `Kernel/UiNotifyAdapter.cs`, `Kernel/IUiMainThread.cs`,
`Kernel/VerseFerriteMainThread.cs`, `Kernel/ReferenceEqualityComparer.cs`, `Kernel/UiWindowHost.cs`.

```csharp
public interface IUiMainThread { bool IsCurrent { get; } }
public sealed class VerseFerriteMainThread : IUiMainThread { /* UnityData.IsInMainThread */ }

public sealed class UiNotifyAdapter : IDisposable
{
    public const int DefaultMaxPendingKeys = 256;
    public UiNotifyAdapter(IUiBindings bindings, IUiMainThread? mainThread = null, int maxPendingKeys = DefaultMaxPendingKeys);
    public IUiBindings Bindings { get; }
    public IUiMainThread MainThread { get; }
    public int MaxPendingKeys { get; }
    public int AttachedSourceCount { get; }
    public int MappedPropertyCount { get; }
    public int PendingKeyCount { get; }
    public int OffThreadNotificationCount { get; }
    public int UnmappedNotificationCount { get; }
    public int FlushCount { get; }
    public event Action<string>? NotificationRejected;
    public UiNotifyAdapter Map(string propertyName, params string[] bindingKeys);
    public UiNotifyAdapter MapAll(params string[] bindingKeys);
    public IDisposable Attach(INotifyPropertyChanged source);
    public bool Detach(INotifyPropertyChanged source);
    public IDisposable BeginBatch();
    public void Flush();
    public void Dispose();
}

// On the existing UiWindowHost (inherited by UiPageWindow):
public event Action<UiHost>? HostAttached;
public event Action<UiHost>? HostDetached;
```

Decided semantics (the implementer documents and lanes them; the Lead does not relitigate them by stealth):

- **Mapping is explicit.** A named property with no mapping announces nothing, *even when `MapAll` was
  declared*; `MapAll` answers the empty-property-name convention only. A consumer that wants one property to
  announce everything writes `Map(name, those, keys)`.
- **Bounded batch.** Distinct pending keys are capped by `MaxPendingKeys`; reaching the cap flushes what is
  held even inside a batch. A batch is coalescing with a ceiling, not a promise of exactly one announcement.
- **Main thread only.** An off-thread notification is refused before any mapping is delivered, counted, and
  surfaced through `NotificationRejected`. It is never queued for later. `Detach` is the one member that
  does not throw after `Dispose` — a handle that outlives its page must be harmless.
- **Lifecycle.** `HostAttached` fires once per host, after `CreateHost()` and before that host's first draw.
  `HostDetached` fires before the window disposes the host it owns, on close and on a failed pass. Both
  handlers run inside the window's guarded pass, so a handler that throws is treated as that window's
  failure — deliberate, because a silent swallow would make broken lifecycle code look like a page that never
  loaded. The window owns the host; the consumer owns manifest, bindings, theme, model and VM, and nothing
  here disposes them.
- **Not in this round.** No base class, no DI, no reflection, no dependency inference, no collection
  framework (the keyed repeater and its per-key notification are the 0.5 mechanism and are reused), and no
  background delivery queue.

## T2 — reload scheduling

Owned by `reload`. Files: `Kernel/IUiTimeSource.cs`, `Kernel/VerseFerriteTimeSource.cs`,
`Kernel/UiReloadPolicy.cs`, `Kernel/UiReloadSchedulerState.cs`, `Kernel/UiDocumentService.cs`,
`Kernel/UiHost.cs` (only if the commit gate needs it).

```csharp
public interface IUiTimeSource { double NowSeconds { get; } }
public sealed class VerseFerriteTimeSource : IUiTimeSource { /* Time.realtimeSinceStartup */ }

public sealed class UiReloadPolicy
{
    public static readonly UiReloadPolicy Default;
    public UiReloadPolicy(double quietSeconds = 0.25, double retrySeconds = 0.5, int maxRetryAttempts = 3, double maxDeferSeconds = 2.0);
    public double QuietSeconds { get; }
    public double RetrySeconds { get; }
    public int MaxRetryAttempts { get; }
    public double MaxDeferSeconds { get; }
}

public readonly struct UiReloadSchedulerState
{
    public UiReloadSchedulerState(int pending, int deferred, int retrying, double oldestPendingSeconds);
    public int Pending { get; }
    public int Deferred { get; }
    public int Retrying { get; }
    public double OldestPendingSeconds { get; }
}

// On the existing UiDocumentService:
public UiDocumentService(bool? autoWatch = null, UiReloadPolicy? policy = null, IUiTimeSource? timeSource = null);
public UiReloadPolicy Policy { get; }
public IUiTimeSource TimeSource { get; }
public UiReloadSchedulerState SchedulerState { get; }
```

Worked out by `reload`, against the vanilla source (not against a method name): the quiet period that merges
a save burst, the bounded retry after a transient read failure, the deferral ceiling for the user's own
input, and the exact commit point in the game's update/GUI order. The Lead fixes only the shape above and
these invariants: time comes from `IUiTimeSource` and never from a frame/host/pump count; a paused game
still commits; a document with no attached host is not re-parsed on a timer; and no retry path can spin (the
attempt count is bounded and reported).

## T3 — registry description snapshot

Owned by `catalog`. Files: `Kernel/UiWidgetCatalog.cs`, `Kernel/UiWidgetDescriptor.cs`,
`Kernel/UiWidgetRegistry.cs` (an internal accessor only; the public registration surface does not change).

```csharp
public static class UiWidgetCatalog
{
    public static IReadOnlyList<UiWidgetDescriptor> Snapshot();   // scope, then kind, ordinal; a fresh copy
    public static bool TryGet(string scope, string kind, out UiWidgetDescriptor descriptor);
    public static IReadOnlyList<string> Scopes();                 // ordinal; a fresh copy
}

public readonly struct UiWidgetDescriptor
{
    public UiWidgetDescriptor(string scope, string kind, IReadOnlyCollection<string>? allowedAttributes, IReadOnlyCollection<string>? labelAttributes);
    public string Scope { get; }
    public string Kind { get; }
    public IReadOnlyCollection<string> AllowedAttributes { get; }
    public IReadOnlyCollection<string> LabelAttributes { get; }
    public bool HasAttributeSchema { get; }
    public bool HasLabelSet { get; }
}
```

Invariants the Lead fixes: identity is the `(scope, kind)` pair, because a kind name is not unique across
scopes; no factory is ever invoked (a catalogue that resolved in order to list would run every registered
consumer's constructor on open); no consumer code runs while the registry lock is held; the returned
collections are copies the caller owns; and `HasAttributeSchema == false` is rendered as "not declared",
never as "no attributes allowed".

## Not in this round

`KnownKinds` keeps its current merged-name behaviour: it is on the **stable** tier, and the round adds a new
type instead of changing a promised one. No second window system, no docking, no cross-save window state, no
C# hot reload, no cross-mod business bus, and no manifest expression language.

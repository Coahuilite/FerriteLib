# Using the 0.6 surface

Written for a consumer's author. It covers only what this round added or changed; the XML vocabulary itself
(manifest, styles, bindings, tabs, repeat/templates, window catalog) is documented in
`docs/development/0.5/20-api-and-xml.md` and did not change in 0.6.

The rule the round is built on: **ordinary layout and style belong in XML; business state, commands and
complex controls stay in C#.** Nothing here adds an expression language to the manifest, and there is still
no base class to inherit, no container to configure, no reflection and no code generation.

## 1. A plain C# object is a view model

```csharp
sealed class BrowserVm : INotifyPropertyChanged
{
    private string filter = "";
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Filter
    {
        get => filter;
        set { if (filter == value) return; filter = value; Recompute(); OnPropertyChanged(nameof(Filter)); }
    }

    public int VisibleCount => items.Count;
    public bool CanReset => filter.Length > 0;
    public void Reset() { Filter = ""; OnPropertyChanged(nameof(CanReset)); }

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

The library never reads the model, so it cannot infer that `Filter` also moves `VisibleCount` and
`CanReset`. You say so once, explicitly:

```csharp
var bindings = new UiBindings();
var notifier = new UiNotifyAdapter(bindings)
    .Map(nameof(BrowserVm.Filter), "browser.filter", "browser.count", "browser.reset")
    .Map(nameof(BrowserVm.CanReset), "browser.reset")
    .MapAll("browser.page");              // the empty-property-name "whole object moved" convention
notifier.Attach(vm);                      // returns a handle; hold it and Dispose it on close
```

Decided semantics, so you do not have to guess:

| Behaviour | Contract |
| --- | --- |
| A property with no mapping | announces **nothing** — even if `MapAll` was declared. Say `Map(name, keys)` if you want it to announce. It is counted in `UnmappedNotificationCount`. |
| `MapAll` | answers the empty/null property name only (the BCL "everything changed" convention). |
| One property, many keys | one announcement, keys coalesced; outside a batch it is one `NotifyChanged` call for the whole set. |
| `using (notifier.BeginBatch()) { ... }` | everything raised inside merges into one write at the outermost scope. Nesting is allowed; ending an inner scope closes the scopes opened inside it. |
| `MaxPendingKeys` | a hard ceiling on distinct pending keys. Reaching it flushes early **even inside a batch**: a batch is coalescing with a ceiling, not a promise of exactly one write. |
| Another thread | refused before anything is delivered, counted in `OffThreadNotificationCount`, surfaced through `NotificationRejected`. It is never queued and never delivered later — marshal to the main thread yourself. `VerseFerriteMainThread` is the game's own answer; a test can inject `IUiMainThread`. |
| Dispose | unsubscribes every source and disposes **none** of them. The handle `Attach` returns releases exactly one source and is safe to release twice or after the adapter is gone. |

## 2. The page lifecycle door

A window builds its page host lazily inside its first guarded draw, so before the first frame `PageHost` and
`Session` are null and there was no way to tell "not yet" from "failed" without counting frames. Subscribe
instead:

```csharp
var window = new UiPageWindow(key, manifest, bindings, theme, translation, title, closeText, noticeText);

window.HostAttached += host =>
{
    var diagnostics = host.Diagnostics;          // opt in per page
    reloadService.Attach(host, layoutId, styleId);
    subscription = notifier.Attach(vm);          // release it in HostDetached
};

window.HostDetached += host =>
{
    subscription?.Dispose();                     // your subscriptions, not your model
    subscription = null;
};
```

- `HostAttached` fires **once per host, before that host's first draw**.
- `HostDetached` fires **before the window disposes the host it owns** — on close and when a failed pass
  tears the host down. The host is still alive inside the handler.
- Both run inside the window's guarded pass. A handler that throws is treated as **that window's failure**
  and the window enters its failure notice: a silent swallow would make a broken lifecycle indistinguishable
  from a page that never loaded.
- Ownership is split, not shared: the window owns the host; you own the manifest, bindings, theme, model and
  view model, and nothing in the library disposes any of them. A reload rebuilds the tree and swaps the
  document — it does not rebuild your VM, re-subscribe it, run a command or clear an uncommitted draft.

## 3. Automatic reload, and the two channels

```csharp
var service = new UiDocumentService(
    autoWatch: null,                              // null = the build default (on in FER_DEV, off in release)
    policy: new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0),
    timeSource: null);                            // null = real time, never the simulation clock

service.Add(new UiDocumentSource("demo.layout", UiDocumentKind.Layout, layoutPath), embeddedXml);
service.Attach(host, "demo.layout", "demo.style");
```

| Member | Meaning |
| --- | --- |
| `Signal(id)` | the **watcher's** channel: a file changed. Debounced — the read happens after `QuietSeconds` of quiet (trailing edge: a later signal restarts the window), so the effective delay is `[QuietSeconds, QuietSeconds + one pump]`. It reads no clock and touches no game type, because it can be called from the `FileSystemWatcher` thread; the window is stamped on the main thread when the next `Pump` observes it. |
| `Reload(id)`, `ReloadAll()` | the **manual** channel: read and commit now, through the identical validation/commit path. Use this for a "reload" button; do not use `Signal` for it. |
| `Pump()` | called by `UiHost.BeginFrame`; the main-thread commit boundary. Nothing spins: a failing candidate is retried at most `MaxRetryAttempts` times and then reported. |
| `SchedulerState` | `Pending` / `Deferred` / `Retrying` / `OldestPendingSeconds` — what the scheduler is holding right now. |
| `Policy`, `TimeSource` | the injected policy and clock, readable. |

A commit may be deferred while the user is mid-interaction (a drag the session owns as its hot control) but
never past `MaxDeferSeconds`. **What that does not cover:** IME composition is outside the captured signal and
is bounded only by `MaxDeferSeconds`; the in-game timing of the commit point is still 尚待外部团队验证.

## 4. Listing registered kinds without instantiating them

```csharp
foreach (var scope in UiWidgetCatalog.Scopes())
{
    foreach (var kind in UiWidgetCatalog.Snapshot().Where(d => d.Scope == scope))
    {
        string schema = kind.HasAttributeSchema
            ? string.Join(", ", kind.AllowedAttributes)
            : "未声明";                                   // NOT "no attributes allowed"
        string labels = kind.HasLabelSet ? string.Join(", ", kind.LabelAttributes) : "未声明";
        UiThemeDraw.Label(rect, scope + "/" + kind.Kind + "  [" + schema + "]  {" + labels + "}", theme);
    }
}

if (UiWidgetCatalog.TryGet("my-mod", "details", out UiWidgetDescriptor descriptor)) { /* ... */ }
```

- Identity is the **pair** `(Scope, Kind)`: a kind name is not unique across scopes, which is why
  `UiWidgetRegistry.KnownKinds()` (which merges names) cannot answer this question. `Snapshot` is ordered by
  scope then kind, ordinal, and returns a fresh copy each call — a late registration appears in the next
  snapshot with no restart.
- **No factory is ever called.** Listing never runs a consumer's `Func<IUiWidget>`.
- `TryGet` matches the exact declared pair — it answers "declared where", so unlike
  `UiWidgetRegistry.GetAttributeSchema` it does **not** fall back to the core scope. `false` leaves
  `descriptor` as `default`: check the return value before reading it.
- A kind that declared no schema is not attribute-checked; render it as "not declared" and never as
  "no attributes allowed". The two are opposite claims.

## 5. Vocabulary traps a real integrator hit

Found by the demo mod while writing its pages against the packaged carrier — every one of these cost real
minutes, and none is new in 0.6. They are recorded here because the manifest vocabulary reference
(`docs/development/0.5/20-api-and-xml.md`) states the rules but not the failure shapes.

1. **`Tab` is a leaf attribute.** A container rejects it (`Unknown attribute 'Tab' on Column at 'root/body/core'`),
   so if your page uses containers the tab marker has to be repeated on every widget inside them. The failure
   text reads like a container-vocabulary gap; it is not.
2. **A dropdown needs `BindOptions`, not a read-only list binding.** `BindReadOnly<IReadOnlyList<T>>` compiles
   and passes value validation, then fails at creation with `Required options binding 'x' is missing` — which
   reads as "you never bound it" rather than "you bound it the wrong way".
3. **Declaring an action makes that binding required.** Adding `ActionBind` to `container/tree` (or any
   declared action) means the binding must exist; it is not an optional extra.
4. **`Scroll`'s `Height` is a number or `Auto`, never `Fill`**, and `container/tree` takes `RowHeight`, not
   `Height`.
5. **`<Templates>` has one `Id` namespace for the whole manifest**, not one per page or per section.
6. **The host's `Source` is the widget-registry scope** — see `30-consumer-handoff.md` item 8.

## 6. What is deliberately absent

Mandatory VM base class, DI container, code generation, reflection or deep-path binding, automatic dependency
inference, a collection framework (the keyed repeater and per-key notification from 0.5 are the mechanism),
background delivery of notifications, a cross-mod business bus, a plugin marketplace, C# hot reload, docking,
cross-save window state, and any second window system. Window placement, input dispatch and z-ordering stay
with `Verse.Window`/`WindowStack`; this library composes them.

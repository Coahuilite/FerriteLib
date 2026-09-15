# T1 — notification adapter and page lifecycle: lane record

Owner: `mvvm` (task-24). Branch `feat/0.6-mvvm`, implementation commit `8ff5b9f` (baseline `02a6aea`).
Evidence class reached: **已自动化验证** for every item below — the Release harness on the stub game surface,
153 assertions across two lane classes (`KernelNotifyAdapterTests` 89, `KernelPageLifecycleTests` 64).

Not reached, and not claimed anywhere in this record: 已由真实消费者接入 and 已实机验证. Nothing here ran in a
game, no second consumer compiled against the surface, and the window passes are driven through the harness's
`Verse.Window` double — the same surface `KernelWindowHostTests` already relies on.

## 1. What landed

| File | Change |
| --- | --- |
| `Source/FerriteLib.UiKit/Kernel/UiNotifyAdapter.cs` | the adapter's behaviour completed against the frozen signature |
| `Source/FerriteLib.UiKit/Kernel/UiWindowHost.cs` | `ReleaseHost` disposes in a `finally` and records a throwing `HostDetached` instead of letting it escape |
| `Source/FerriteLib.UiKit/Kernel/IUiMainThread.cs` | doc correction only (it claimed "three public entry points"; one takes it today) |
| `tools/FerriteLib.UiKit.Tests/KernelNotifyAdapterTests.cs` | new lane, 89 assertions |
| `tools/FerriteLib.UiKit.Tests/KernelPageLifecycleTests.cs` | new lane, 64 assertions |
| `tools/FerriteLib.UiKit.Tests/Program.cs` | one contiguous commented registration block for both lanes |

No public signature changed. The frozen contract in `docs/development/0.6/05-api-contract.md` is implemented
as written and no type was added to `docs/api-tiers.md`.

Behaviour the T0 skeleton did not have, all of it inside this package's own files:

1. **One notification is one write.** T0 flushed from inside the per-key queue, so one property mapped to
   three keys produced three separate `NotifyChanged` calls. The commit now happens once per notification
   (outside a batch) or once per outermost scope (inside one), with the ceiling still firing early.
2. **Ending a batch closes the scopes opened inside it.** T0's depth counter let a dropped inner handle keep
   `batchDepth > 0` forever, so the pending set could be stranded.
3. **A null or empty binding key, and a null key list, are refused** — and refused before the assignment, so a
   rejected `Map` cannot replace the mapping already in force.
4. **A subscription handle is bound to the subscription it was handed out for**, so a stale handle cannot
   release a newer subscription created after the old one was detached.
5. **`HostDetached` throwing cannot defeat host disposal or escape the guarded pass / close path.**

## 2. Items (1)–(7), lane by lane

For each item: the registered lane, what it asserts, the planted defect(s) that reddened it, and what it does
**not** pin. A "mutation" below means the defect was written into the production file, the Release harness was
run, the specific named assertions went red (recorded in §3), and the file was reverted with
`git checkout` — the mutations were never committed.

### (1) Explicit mapping

Lane `KernelNotifyAdapterTests`:
`Mapping: one property feeds many keys, and Map replaces what it mapped before`;
`MapAll answers the empty property name only, and an unmapped name is counted`;
`A null or empty binding key is refused without replacing the mapping in force`.

Asserts: one property writes every key it was mapped to in one flush; a repeated `Map` keeps one mapping and
drops the keys the replacement no longer names; `MapAll` answers only the empty property name, and a named
property with no mapping writes nothing and increments `UnmappedNotificationCount`; `Map("", keys)` is the
same convention; with no `MapAll` the empty name is counted too; a null key, an empty key and a null key list
are refused and the previous mapping survives the refusal.

**Mutation-proven.** M1 (MapAll as a fallback), M2 (a repeated Map no longer replaces), M3 (null key accepted),
M4 (null list accepted as empty), M6 (a flush writes every key twice).

**Does not pin:** mapping a property to zero keys (`Map("P")`), which is accepted and announces nothing and is
counted as unmapped; an internally empty key that reaches `Queue` is skipped (unreachable through the public
surface now that empty keys are refused).

### (2) Bounded batching

Lane `KernelNotifyAdapterTests`:
`Batch: nesting coalesces to one write and a repeated key is written once`;
`Batch: the ceiling fires inside a batch and a dropped inner scope strands nothing`.

Asserts: inside a batch the pending set is held; closing an inner scope does not commit; the outermost scope
writes each distinct key exactly once and the whole nested batch is one flush; a key raised repeatedly is one
pending key; `MaxPendingKeys` fires even inside a batch and empties the set; an inner scope that is **never
ended** does not strand the set — ending the outer scope delivers it, and releasing the dropped handle
afterwards is a harmless no-op that does not leave a batch open.

The contract phrase *"a key pending in a batch is still delivered even if the batch is never ended"* is
answered in the two shapes that actually deliver, and the residual is asserted rather than implied away:

- a **dropped inner** scope delivers at the outer scope's end (the shape the phrase most plausibly names, and
  the one T0's depth counter got wrong);
- the **ceiling** delivers a set that a leaked **outermost** scope is holding;
- below the ceiling, a leaked outermost scope holds the key — asserted as `PendingKeyCount == 1` with no
  revision moved, and `Flush()` deliberately does **not** punch through an open batch. The ceiling is what
  makes that state finite. This is a stated boundary, not a silent weakening.

**Mutation-proven.** M5 (no ceiling), M8 (depth decrement instead of closing nested scopes), M6 (writes each
key twice), M7 (a notification outside a batch is never committed).

**Does not pin:** any time-based release of a leaked outermost scope (there is none, and none is promised);
`MaxPendingKeys` behaviour under concurrent mutation (single-threaded by contract).

### (3) Deterministic unsubscribe

Lane `KernelNotifyAdapterTests`:
`Unsubscribe: attach twice is one subscription and a handle releases exactly once`;
`Dispose: releases every source, disposes none, and leaves handles and Detach harmless`.

Asserts: attaching the same source twice is one `PropertyChanged` subscription counted at the source, with
two handles and exactly one release; releasing the same handle twice releases nothing more; `Detach` reports
true once and false afterwards; a handle from a detached generation cannot release the subscription that
replaced it, and that subscription still delivers; two sources whose own `Equals` answer equal are two
subscriptions (reference identity, not the consumer's equality); `Dispose` unsubscribes every source, disposes
none of them (a disposable source's `Dispose` is never called); a handle, a batch scope and a second
`Dispose` after the adapter are harmless; `Detach` does not throw after `Dispose` while a null source still
throws; every other member throws `ObjectDisposedException` after `Dispose`.

**Mutation-proven.** M9 (a stale handle releases whatever is attached now), M10 (`Dispose` disposes sources),
M11 (`Detach` throws after `Dispose`), M12 (`Attach` twice makes two subscriptions), M19 (default equality
instead of reference identity), M20 (`Dispose` stops unsubscribing).

**Does not pin:** the adapter's own thread safety (single-threaded by contract); a source that raises
`PropertyChanged` from its remove accessor (it is refused after `disposed` is set).

### (4) Off-main-thread refusal

Lane `KernelNotifyAdapterTests`:
`Off-thread: refused before mapping, counted, surfaced, and never replayed`.

Asserts, with an injected `IUiMainThread` double (the process never moves a thread): an off-thread
notification delivers nothing and moves no binding revision; it increments `OffThreadNotificationCount` and
raises `NotificationRejected` with the property name (empty for the all-properties convention); it is not
queued; and the refusal happens **before** the mapping is resolved, so an unmapped off-thread name is not
counted as an unmapped delivery; the same notification on the main thread is then delivered, and nothing
refused was replayed.

**Mutation-proven.** M13 (the thread question moved after the mapping), M6/M7 (delivery assertions on the
control half).

**Does not pin:** that the real game process is single-threaded, or that a consumer's background producer
marshals to the frame thread — that is the boundary `docs/api-tiers.md` states, not a harness claim.

### (5) Lifecycle

Lane `KernelPageLifecycleTests`:
`Lifecycle: HostAttached fires once per host and before that host's first draw`;
`Lifecycle: HostDetached precedes disposal on close, and a reopened window gets a fresh host`;
`Lifecycle: a throwing handler is the window's failure and disposal still happens`.

Asserts: `HostAttached` fires exactly once per host, with the page's own draw counter still at zero, and the
host it names is the one the shell keeps and draws with; further passes neither re-announce nor rebuild it;
`HostDetached` fires once on close while the session is still active, then the session is disposed and the
shell holds no page; a reopened window builds a fresh host, announces it, and draws again; a `HostAttached`
throw is the window's failure — no notice inside the failing pass, `OnDrawFailure` once, the host torn down
through `HostDetached` and disposed, the notice on the next pass, no retry; a throwing `HostDetached` does
not escape the close path, the host is disposed anyway, and when both handlers throw on the same pass the
pass's own failure still wins, the host is torn down once, and the window still reaches its notice.

**Mutation-proven.** M14 (`HostAttached` after the draw and on every pass), M15 (a throwing `HostDetached`
unguarded and skipping disposal), M16 (`HostDetached` after disposal), M17 (`PreClose` no longer releases
the host).

**Does not pin:** how the real window stack orders its own hooks around these events; the vanilla
exact-type removal performed by `WindowStack.Add` (covered by `KernelWindowCatalogTests`); the case where
`CreateHost` itself throws — there is no host to detach, and that path stays with
`KernelWindowHostTests.VerifyFailingPassTripsTheNoticeOnTheNextPassOnly`.

### (6) Invariance across reload

Lane `KernelPageLifecycleTests`:
`Reload: a page with a VM attached survives layout and style reloads untouched`;
`Lifecycle: closing one window leaves another window's subscription live`.

Asserts, through the real `UiDocumentService` over a temporary layout and style file: after both documents
reload and the new tree is verifiably installed, the page host and its session are the same objects,
`HostAttached` is not fired again, the adapter's attached-source count and mapped-property count are
unchanged, `FlushCount` is still zero (the reload announced nothing and ran nothing), no command ran, the
stable element keeps its node object, and an uncommitted draft written before the reload is still there; the
view model's subscription then still delivers exactly one announcement. Separately: closing one window detaches
that window's own subscription and leaves the other window's host, session and subscription alive and
delivering.

**Mutation-proven:** the "the reload does not re-enter the page" half — M14 makes `HostAttached` fire on every
pass and reddens *HostAttached is not fired again by a reload* and *a subscription made in HostAttached is live
for the first draw*; M6/M7 redden the still-live-subscription assertions. The page/VM identity assertions
(same host, same session, constant `AttachedSourceCount`) are the observable form of that same M14 defect.

**Only a future-regression guard (reported as such):** the *uncommitted draft survives* and *a reload runs no
command* assertions. The code that could clear a draft or replay a command lives in
`UiHost`/`UiDocumentService`, which this package does not own, so no mutation inside this package's files can
redden them. Their mutation-proven owner is the document lane
(`KernelDocumentReloadTests.VerifyStateCarryOver`, `VerifyNoSideEffects`) at the host level; this lane is the
page-level regression guard that the same property holds with a view model attached. The
`closing one window does not detach another window's subscription` assertion is likewise a guard: the shell
has no shared registry to get wrong (each window owns its own `host` field), so there is no single-line
production defect for it to catch.

**Does not pin:** the two verifiers' territory — a reload arriving from a real file watcher on a real editor
save, and the in-game ordering of the commit.

### (7) The page-level door is usable

Lane `KernelPageLifecycleTests`:
`Door: HostAttached sees the first draw, and PageHost works after it`.

Asserts both halves in one lane: a consumer that subscribes in `HostAttached` sees the page's first draw — the
subscription is made while the draw counter is zero and a notification raised *from inside that first draw*
reaches the bindings; and a consumer that subscribes **after** the first draw still works through
`UiPageWindow.PageHost` — the host is reachable, its session is live, and the next draw's notification is
delivered. Both use a real `UiPageWindow`, not the lane's shell double.

**Which half is the new guarantee:** the first. `HostAttached`-before-first-draw is what this round adds (T0
commit `02a6aea`), and without it a consumer had to poll `PageHost`/`Session`. The `PageHost`-after-first-draw
path already shipped (0.5 task-14) and is asserted next to it so the new door cannot be mistaken for the only
one.

**Mutation-proven.** M14 (attach after the draw, reddening both halves), M18 (`PageHost` answers null).
M6/M7 redden the delivery assertions.

**Does not pin:** that `HostAttached`-in-the-first-draw composes with the game's real GUI event order — the
harness drives a synthetic pass.

## 3. Mutation campaign

Baseline: `8ff5b9f`, tree otherwise clean, Release harness `ALL PASS`. Each row: the defect planted in the
production file, then `git checkout -- <file>` before the next row. Every row fired at least one named
assertion; no row failed to compile.

| # | Planted defect (file) | Assertions that went red |
| --- | --- | --- |
| M1 | `Resolve` falls back to `allKeys` for a named property | *does not fall back to MapAll*; *it is counted as an unmapped notification* |
| M2 | `Map` skips when the property is already mapped | *keys it dropped are no longer announced* (+3 knock-ons) |
| M3 | `Copy` stores a null/empty key instead of refusing | *a null binding key is refused*; *a null key in the all-properties convention*; *an empty binding key is refused*; *the refused Map did not replace the mapping in force* |
| M4 | `Copy` treats a null array as empty | *a null key list is a caller error* (+2 knock-ons) |
| M5 | ceiling check removed from `Queue` | *reaching MaxPendingKeys writes what is held even inside a batch*; *that early write empties the set*; *a key raised twice afterwards is held once*; *the batch's own handle ending writes it exactly once more*; *the ceiling is the escape …* |
| M6 | `ForceFlush` calls `NotifyChanged` twice | 20 assertions across both lanes (every "written once" claim) |
| M7 | the outside-a-batch commit removed | 18 assertions across both lanes (every delivery claim) |
| M8 | `EndBatch` decrements instead of closing nested scopes | *ending the outer scope delivers it even though the inner scope was never ended*; *exactly once, nothing stranded*; *releasing the dropped inner handle … is a harmless no-op*; *and it did not leave a batch open* |
| M9 | `Release` ignores the captured handler | *the stale handle released nothing*; *so the subscription that replaced it is still live* (+2 knock-ons) |
| M10 | `Dispose` disposes each source | *Dispose never disposes a source* |
| M11 | `Detach` calls `EnsureAlive` | *Detach does not throw after Dispose* |
| M12 | `Attach` always subscribes afresh | *attaching the same source twice is one subscription* (+5 knock-ons) |
| M13 | thread check moved after `Resolve` | *the thread question is asked before the mapping*; *the rejection still names the property*; *the empty all-properties name is rejected*; *nothing refused was replayed later* |
| M14 | `HostAttached` after `DrawFrame`, on every pass | *HostAttached runs before that host has drawn a frame*; *further passes … do not re-announce it*; *HostAttached runs before the page's first draw*; *a subscription made in HostAttached is live for the first draw* |
| M15 | `HostDetached` unguarded, disposal skipped | *a throwing HostDetached does not escape the close path*; *the host is disposed anyway*; *a page failure whose teardown handler also throws stays inside the guarded pass*; *the pass's own failure is still reported once* |
| M16 | disposal before `HostDetached` | *HostDetached runs before the host is disposed: the session … is still active* |
| M17 | `PreClose` no longer releases the host | *closing announces the host once* + the close/reopen block, plus 12 assertions in other lanes that rely on a window releasing its host |
| M18 | `PageHost` answers null | *after the first draw the page host is reachable through PageHost*; *the generic page exposes the host it created* |
| M19 | default equality instead of reference identity | *two value-equal sources are two subscriptions, not one*; *each of them was subscribed once* |
| M20 | `Dispose` stops unsubscribing | *Dispose unsubscribes every source* (+2 knock-ons) |

## 4. What this record is not

- It is not in-game evidence, and no item above claims 实机.
- It is not evidence from a consumer: the one wired consumer tree was neither compiled against nor run.
- The reload-invariance draft/no-command assertions and the cross-window isolation assertion are future
  regression guards, as stated in item (6); the rest are mutation-proven.
- The harness's main-thread answer is injected; the real `UnityData.IsInMainThread` path is a compile-level
  delegation, not behaviour exercised here.

## 5. Reported to the Lead

1. **`scripts/privacy-audit.ps1` currently FAILS on a pre-existing, Lead-owned file.**
   `docs/development/0.6/10-work-packages.md` line 96 carries a machine-absolute drive path naming the demo
   mod's planned location. It landed with T0 commit `02a6aea`; vector1 reports exactly that one finding, and
   the working-tree scan is otherwise clean. The file is outside this package's write scope and was not
   touched, so the round's privacy gate cannot pass until the Lead removes the path there.
2. **The unended-batch reading.** §2 item (2) records which reading of "still delivered even if the batch is
   never ended" was implemented (a dropped inner scope delivers at the outer end; a leaked outermost scope is
   released by the ceiling). If the Lead intended `Flush()` to force a commit inside a batch, that is a
   one-line semantic change to `Flush` and a new assertion, not a contract amendment.

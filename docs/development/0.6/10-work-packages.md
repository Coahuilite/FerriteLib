# T0–T5 — work packages, owners, dependencies

**These packages are also on the session's shared task board**, which is the live status. This file is the
durable copy: it says what each package owes and how a reviewer falsifies it. When the two disagree, the
board is what the round ran on and this file is corrected in the same commit.

Round rules that apply to every package:

- One writer per file. A package that needs a file it does not own asks the Lead; the Lead changes the
  contract document and the tier list in one commit **before** the change lands.
- A public type, its `docs/api-tiers.md` entry, and a lane that exercises it land in the same commit.
- Every lane states which half is **mutation-proven** (the verifier planted the defect and watched the lane go
  red) and which is only a **future-regression guard**. An assertion on source text is never the whole lane.
- A package's report says which evidence class it reached: 已实现 / 已自动化验证 / 已由真实消费者接入 /
  已实机验证 / 尚待外部团队验证. Build green and "the fixture renders" are the first two, never more.

| Package | Owner | Owes | Depends on |
| --- | --- | --- | --- |
| **T0** baseline & contract | Lead | this directory, the frozen API, the three axes, tier entries, compiling skeletons, branch `0.6.x` | — |
| **T1** page lifecycle & MVVM | `mvvm` | the adapter's behaviour lanes, the lifecycle contract, and the invariance of both across an XML reload | T0 |
| **T2** reload scheduling | `reload` | scheduler semantics against a fake clock, the vanilla commit-point evidence, pause independence, bounded retry/defer, no rebuild while nothing is open | T0 |
| **T3** registry description | `catalog` | `UiWidgetCatalog`/`UiWidgetDescriptor`, scope-preserving identity, isolation, zero factory calls, late registration | T0 |
| **T4** demo mod | `demo` | `ferritelib_uikit_demo`, public-API-only, ModSettings entry, three tabs, core samples, consumer directory, dual window + reload | T0 (contract only); T1/T2/T3 for wiring |
| **T5** independent review & delivery | `verify`, `probe`, Lead | adversarial verification, the external compile probe, package inspection, in-game checklist, delivery record | all |

---

## T0 — baseline and contract (Lead) — complete

Delivered in the branch's first commit: `0.6.x` forked from `354d90a`; three axes at `0.6.0`;
`05-api-contract.md`; compiling skeletons for every new public type with an explicit
`NotImplementedException` marker naming the owning package; tier entries for all nine new types; the
`Verse.UnityData` harness stub the production main-thread seam delegates to; and all ten local gates green.

**The marker list is the delivery check.** `grep -rn "NotImplementedException" Source/` must be empty at
delivery; each hit is a package that did not land.

## T1 — page lifecycle and MVVM (`mvvm`)

Owes, each with a lane that can go red:

1. **Mapping.** One property to many keys; `MapAll` for the empty name and *not* a fallback for named
   properties; a named property with no mapping announces nothing and is counted; `Map` replaces an earlier
   mapping; a null key is refused.
2. **Bounded batching.** Nested `BeginBatch` flushes once at the outer scope; the pending cap forces an early
   flush inside a batch instead of growing without limit; a key announced twice in one batch is written once.
3. **Deterministic unsubscribe.** `Attach` twice is one subscription; the returned handle releases exactly
   once; `Dispose` unsubscribes every source and disposes none of them; a handle disposed after the adapter
   is harmless.
4. **Thread refusal.** An off-thread notification delivers nothing, moves no revision, counts, and raises
   `NotificationRejected` with the property name.
5. **Lifecycle.** `HostAttached` fires exactly once per host and before the first draw; `HostDetached` fires
   before the host's disposal on close and on a failed pass; a reopened window gets a fresh host and a fresh
   notification; a handler that throws is treated as that window's failure (asserted, not assumed).
6. **Invariance across reload.** Reloading the layout and the style document of a page that has a VM attached
   does not rebuild the VM, does not re-subscribe (source count constant), does not run a command, and does
   not clear an uncommitted draft. Closing one window does not detach another window's subscription.

Falsification: the reviewer plants a defect for each numbered item and records which lane went red; an item
whose lane stays green under its planted defect is reported as **not pinned**.

## T2 — reload scheduling (`reload`)

Owes:

1. **Vanilla timing evidence**, transcribed from the game's own source: where the update/GUI order puts a
   commit relative to `WindowStack`'s draw and input passes, with `file:line` and the excerpt. The plan's
   warning is explicit — do not conclude safety from a method name.
2. **Quiet period.** A burst of signals inside `QuietSeconds` produces one read and one commit; the clock is
   `IUiTimeSource`, and no lane advances a frame or a pump to make time pass.
3. **Retry.** A transient read failure is retried at most `MaxRetryAttempts` times at `RetrySeconds` and
   then reported; the previous valid version stays in force throughout; no path spins.
4. **Pause independence.** A commit happens with a clock that does not advance the simulation at all.
5. **Nothing open, nothing rebuilt.** With zero attached hosts the service does not re-parse on a timer; the
   next attach resolves the newest valid content.
6. **Bounded deferral.** A commit is deferred for the user's own interaction but never past
   `MaxDeferSeconds`, and the reason is visible in `SchedulerState`.
7. **Per-document atomicity survives**, including a layout and a style save in quick succession, and R1's
   rolled-back batch still loses no draft.

Evidence class: 已自动化验证 for 2–7; item 1 is a source reading, and the in-game item stays
尚待外部团队验证.

## T3 — registry description (`catalog`)

Owes: `Snapshot` returns every declared pair including same-named kinds in different scopes, in
`(scope, kind)` ordinal order; `TryGet` matches the exact declared pair and reports false for a kind that
only exists in another scope; `Scopes` is ordered and complete; the returned collections are copies (mutating
one does not change the registry or the next snapshot); **zero factory calls** are made while listing,
including for a kind whose factory throws. Plus a diagnostic-renderable schema half: a kind with no declared
schema answers `HasAttributeSchema == false` and still lists, and a kind registered after a snapshot appears
in the next one.

## T4 — the demo mod (`demo`)

Scope: `ferritelib_uikit_demo` — created by this round,
a local git repository with **no remote**, and never written by any other package.

Owes:

1. A RimWorld 1.6 mod that enters from vanilla `ModSettings` (`Mod.DoSettingsWindowContents`) and renders
   its content area through `UiHost`; the vanilla settings shell stays vanilla.
2. Three tabs in XML: core components, consumer components, and multi-window & hot reload. Consumer entries are
   grouped/filtered by scope, never one tab per consumer, and the list is the registry's own description —
   nothing is guessed, nothing unknown is attributed to a mod, and **no factory is called to list**.
3. Core-kind samples: every registered core kind has a catalogue entry; kinds that need a composition to be
   meaningful appear inside a working one, with the reason stated.
4. Two neutral demo windows sharing one model with separate view models, each holding its own search,
   selection, scroll and input state; a change in one refreshes the other; the settings page can open both.
5. Automatic reload demonstrated with the FL document service and a report readout, including the
   invalid-candidate path (the previous valid UI stays) and manual reload.
6. Its own package: `1.6/Assemblies/<demo>.dll` plus XML and translations, **no `FerriteLib.UiKit.dll`**,
   and a configurable FL artefact path with no machine-absolute path in any publishable file.

Evidence class reachable here: 已实现 + 已自动化验证 (build, package content, public-API-only compile).
实机 is 尚待外部团队验证.

## T5 — independent review and delivery

`verify` — adversarial verification of T1/T2/T3 in a separate worktree: re-run the gates from a clean
extraction, plant a defect per claim, and record for each item whether the lane fired. It owns
`verification/*-adversarial.md` and must report **what a lane does not pin**, not only what it does.

`probe` — the outside half: build a consumer-shaped project under `dist/` against the **packaged** DLL that
uses only public API (including the new surface), run it, and check the demo package's contents, the three
version axes, and that the demo package does not carry FL's DLL. It also owns the in-game checklist and the
shortest operator steps.

Lead — merges, the final gate run, the dev package and its hash, and the delivery record. Nothing is
published, pushed, tagged or installed into the game: all four are outside this round's authorization.

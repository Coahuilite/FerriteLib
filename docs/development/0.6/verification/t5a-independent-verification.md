# T5a — independent adversarial verification of T1/T2/T3

Owner: `verify` (shared task `task-28`). Bound to the merged tip `84537fc58d03e9b9a1d512cdd798bb21eaabe38b`
(branch `0.6.x`). Everything below was run in this session. Nothing here is 实机验证, and no PASS is written
that was not reproduced.

## 1. Revision, extraction, gates, privacy

| item | value |
| --- | --- |
| Tip | `84537fc` — the Lead's merge of T1 (`df243df`, `1613057`), T2 (`63c20e4`, `84537fc`) and T3 (`7d0116c`, `1c99dcf`) |
| Extraction | a unique temp directory of my own, `git archive 84537fc`; the path is deliberately not recorded in this tracked file |
| Restore | `dotnet restore tools/FerriteLib.UiKit.Tests/FerriteLib.UiKit.Tests.csproj` -> exit 0 |
| Second extraction | a separate unique directory for every planted defect; the baseline extraction was never written to |

Gates, raw:

```
[setup] restore the harness project graph (fresh-tree bootstrap) ... OK
[run] FerriteLib.UiKit harness (kernel + version + neutrality + boundary) ... OK
[run] library Dev build (warnings as errors) ... OK
[run] library Release build (warnings as errors) ... OK
[run] mod payload present at the path consumers bind to ... OK
[run] carries no game content (assemblies-only mod) ... OK
[run] LICENSE present and MPL-2.0 ... OK
[run] About.xml identity is present and well-formed ... OK
[run] runtime-resolvability traps: net472 surface + harness stub surface ... OK
[run] rule (c) consumer half is armed and tree-sensitive (dependency-reality) ... OK
[verify] all checks passed.
VERIFY_EXIT=0
```

Harness (run directly, not only through the gate): `exit 0`, `1840` `ok:` lines, `0` `FAIL:`, `ALL PASS`.
Lane coverage: `36` lane files declare `RunAll`, `Program.cs` has `36` registrations, all `36` headers appear
in the stdout, and splitting the stdout at those headers gives `36` blocks with `0` empty and a minimum of `3`
`ok:` lines per block; no block reports a failure at this tip. `grep -rn "NotImplementedException" Source/` is
empty, so the T0 markers are gone.

Privacy, run the only way it is meaningful — inside a git work tree (`scripts/privacy-audit.ps1` from the
worktree at this tip): vectors 1 and 2 `scanned`, `identity: account 19252128+Coahuilite`, vector 3
`SKIPPED` (`-FullHistory` is a first-upload/pre-push option), `PRIVACY AUDIT CLEAN`, exit 0.
**Method note:** running the same script against a non-git extraction reports
`identity: expected exactly one GitHub account, found 0` and exits 1. That is an artefact of the missing
repository, not a finding about the tip; the audit must be run inside the repo.

## 2. Method

Every defect below is **mine**, not a replay of the T1/T2/T3 tables. Each was written into a production file
inside my own scratch extraction, the Release harness was rebuilt (`--no-incremental`) and run, then all
touched files were restored and SHA256-verified. After the last mutation: `RESTORE_MISMATCHES=0`, restore run
`ok=1840 fail=0 ALL PASS`, and `verify-local.ps1` again `VERIFY_EXIT=0`. Anchor uniqueness is asserted
case-sensitively before each edit; a mutation whose anchor was not unique is reported as APPLY-FAILED rather
than counted.

- **mutation-proven** = my planted defect reddened a named assertion.
- **NOT PINNED** = my planted defect stayed green.

## 3. The five specific attacks

### 3.1 T1: the "once per host, before the first draw" door — one shape is NOT PINNED

- **Control (proves the create path is pinned):** raising `HostAttached` twice inside the same creation
  (`HostAttached?.Invoke(created);` duplicated in `UiWindowHost.DrawFrame`) -> red 7, including
  `the first pass announces the host exactly once`, `further passes reuse the host and do not re-announce it`,
  `the page's view model is subscribed exactly once, in HostAttached`.
- **Attack:** re-announce on the activation path — in `UiWindowHost.SetActiveTarget(bool)`, when the window
  becomes the active target and a host already exists, raise `HostAttached` again. This is the shape a
  consumer that re-subscribes its view model on activation would produce.
  Result: **build 0, harness 0, ok=1840, fail=0 — the whole harness stays green.**

**Verdict (NOT PINNED).** The once-per-host guarantee is pinned for create / failed-pass / close+reopen and
for a reload, but not against the window-activation path: no lane both subscribes to `HostAttached` and
re-activates a window after its first draw. `KernelPageLifecycleTests.VerifyAttachOrder` drives `WindowOnGUI()`
only, and the catalog lanes that do activate windows have no `HostAttached` handler. Their record's own
"does not pin" line is about the game's hook ordering; the sharper measured statement is that a re-announce on
activation passes every lane this repository has.

### 3.2 T1: draft-survival / no-command across reload — the escalation is correct, and sharper than written

Two defects in the files the T1 record names as the real owners:

1. **R1's shape** — `SealDocumentCommit();` run at stage time in `UiHost.CommitLayoutCandidate` (the
   destructive half at staging). Red **4**: `and its uncommitted draft survived the rollback`,
   `and its named state slots`, `and its scroll position` (all `KernelDocumentReloadTests`) and
   `and R1's draft survives the scheduler-driven rollback` (`KernelReloadSchedulingTests`).
   **Zero T1 assertions fired.**
2. **A blunter host-side defect** — `Session.Dispose();` at the same stage. Red **42**, all document /
   diagnostics / scheduler lanes, **and** finally
   `Reload: a page with a VM attached survives layout and style reloads untouched threw NullReferenceException`.
   That is T1's own lane failing as an **unnamed lane-level exception**, not at
   `and the uncommitted draft survived both reloads` (`KernelPageLifecycleTests.cs:299`) nor at
   `a reload never invokes a command` (`:296`).

**Verdict.** Their caveat is verified as written: the draft and no-command assertions are not reddened by the
document-package defect that owns them, and `KernelDocumentReloadTests` is the mutation-proven owner. The
sharper measured form: even a defect that destroys the session at commit time leaves those two *named*
assertions green (the lane holds the node object and never draws afterwards — `:279-303`); the lane only fails
as a whole, anonymously. Both belong in the "future-regression guard" column, and no more than that.

### 3.3 T2: "one read" is a report-count proxy — confirmed, with a measured blind spot

The record's `3 caveat says the "one read" claim is a report-count proxy, not a `File.ReadAllBytes` counter.

- **Two real reads, one report:** `bytes = File.ReadAllBytes(path);` duplicated in the read method (same bytes,
  second result used). Red **1**, and it is only
  `the document path reads one snapshot and no other file-reading entry point` — the R7 lexical guard. Every
  behavioural quiet-period / one-report assertion stayed green, so the proxy has no behavioural backstop.
- **The same defect in an unnamed shape:** the second read written as a bare `ReadAllBytes(path)` with
  `using static System.IO.File;`. Result: **build 0, harness 0, ok=1840, fail=0 — nothing catches it at all.**
  (This is the same evasion measured in `t5a-1-baseline-reproduction.md` `5b.)

**Verdict (NOT PINNED behaviourally).** The caveat is accurate; there is no lane that would notice a second
read except the lexical guard, and the lexical guard has the boundary T5a-1 measured.

### 3.4 T2: the adapted P4/P5 lanes — counts reproduced, nothing weakened

| file | before `02a6aea` | after `84537fc` | my count (both revisions) |
| --- | --- | --- | --- |
| `KernelDocumentReloadTests.cs` | 24 lanes / 175 `Check(` | 24 lanes / 175 `Check(` | `Run(` 24 = 24, `Check(` 175 = 175 |
| `KernelDiagnosticsTests.cs` | 15 lanes / 92 `Check(` | 15 lanes / 92 `Check(` | `Run(` 15 = 15, `Check(` 92 = 92 |

(the `Run(` count is every `Run(` occurrence; no `Run(` helper is declared in either file, so 24 and 15 are
the lane counts the record claims.)

Weakening check, by diff rather than by count:
- `KernelDocumentReloadTests.cs`: `96` insertions / `48` deletions, and exactly **four** `Check(`-line changes,
  all of one shape: `Check(service.Pump(), "…")` -> `Check(Settle(service), "…")` with the message strings
  **byte-identical**. **Zero** `Run(` lines changed.
- `KernelDiagnosticsTests.cs`: `45` insertions / `4` deletions, **zero** `Check(`/`Run(` line changes.

And the two assertions this round must not lose were re-planted at this tip, not just read: R1 -> red 4
(above), R7's single-read assertion -> red 1
(`the document path reads one snapshot and no other file-reading entry point`).

**Verdict.** Counts reproduced; no assertion removed, reworded or weakened; the only check-site changes are the
call form the new contract requires, with unchanged messages.

### 3.5 T3: the declared lock boundary — I agree, with named residuals

The code: `UiWidgetRegistry.Register` holds `Gate` and, inside it, builds the schema/label
`HashSet`s from the caller's collections (`:74-111`); `UiWidgetRegistry.Resolve` holds `Gate` and invokes the
factory, exact scope first and core scope as fallback (`:170-184`). T3's record declares these two paths a
boundary, not a defect, per the Lead's ruling, and lanes only the narrower invariant: no **listing** path
(`Snapshot`/`TryGet`/`Scopes`) runs consumer code while the lock is held.

**I agree with the boundary ruling**, for these reasons:

1. `Register` and `Resolve` are foreground calls on the consumer's own thread (registration at load,
   resolution during a window's pass), not the background/listing path the narrow invariant targets.
2. C# `lock` is reentrant on the same thread, so a factory that calls back into `Resolve`/`Snapshot` cannot
   self-deadlock; the hazard needs a second thread, which no lane here creates.
3. Both paths are pre-existing public behaviour, and changing lock scope would be a public-behaviour change
   the round's instruction does not authorize.

Residuals I would record with it (not fixed here, and none release-blocking):

- a factory that blocks extends a **process-wide static** lock across every mod's registration/resolution;
- `Register` enumerates a **caller-supplied** collection under the lock, so a foreign or slow enumerator runs
  consumer code there (the listing paths provably do not — my planted defect that aliased the caller's set,
  `3.8`, reddens `no listing call enumerates a caller-supplied collection…`);
- `GetAttributeSchema`/`GetLabelAttributes` return the registry's **live** `HashSet` as
  `IReadOnlyCollection`, which a caller can downcast and mutate — pre-existing 0.5 surface, outside T3's new
  types, but worth one line wherever the lock boundary is written down;
- no concurrency-stress lane exists; the lock discipline rests on reading plus the single-threaded
  observable, as their record already says.

## 4. My own defect per item

`ok`/`fail` are the harness totals against a `1840 ok / 0 fail` baseline; every row restored to green.

### T1 — `KernelNotifyAdapterTests` / `KernelPageLifecycleTests`

| item | my planted defect | result |
| --- | --- | --- |
| (1) mapping | `Map` stores the caller's array by reference instead of the validated copy | **red 4** — `a null binding key is refused`, `an empty binding key is refused…`, `a null key list is a caller error…`, + the lane threw `NullReferenceException` |
| (2) batching | (a) `pendingKeys.Count >= MaxPendingKeys` -> `>`; (b) always flush (`if (batchDepth == 0)` -> `if (true)` in `OnPropertyChanged`) | (a) **red 5** — `reaching MaxPendingKeys writes what is held even inside a batch`…; (b) **red 13** — `inside a batch the key is held, not written`… |
| (3) unsubscribe | `Remove(source, handler);` made a no-op | **red 8** — `and answers false when there is nothing left to remove`, `each release is exactly one unsubscription`… |
| (4) off-thread | thread gate weakened to `!IsCurrent && Environment.TickCount < 0` | **red 9** — `an off-thread notification delivers nothing`, `it is counted as refused`… |
| (5) lifecycle order/count | double `HostAttached?.Invoke(created);` | **red 7** — `the first pass announces the host exactly once`… |
| (5b) attack: activation path | re-announce in `SetActiveTarget(true)` | **GREEN 0/1840 — NOT PINNED** (`3.1) |
| (6) reload invariance | R1 shape in `UiHost` | **red 4, all document/scheduler; no T1 named assertion** (`3.2) |
| (6b) reload invariance | `Session.Dispose();` at stage | **red 42**; T1's lane fails anonymously, not at draft/no-command (`3.2) |
| (7) page-level door | `PageHost` answers null | **red 3** — `after the first draw the page host is reachable through PageHost`… |

### T2 — `KernelReloadSchedulingTests` + the adapted P4 lanes

| row | my planted defect | result |
| --- | --- | --- |
| 2 quiet / trailing edge | `now - LastObservedSeconds >= QuietSeconds` -> `<=` | **red 24** — `and nothing is read before the window opens (reports=1)`, `a later signal restarts the window…` |
| 3 bounded retry | `RetriesFor(id) < MaxRetryAttempts` -> `<=` | **red 4** — `the first attempt past the retry budget (attempt 3 = 1 + MaxRetryAttempts) is reported as a failure`… |
| 4 pause independence | not attempted | see `5 |
| 5 nothing open, nothing rebuilt | not attempted | see `5 (same report-count proxy as `3.3) |
| 6 bounded deferral | `IsInteracting` condition made unreachable (`&& SessionDisposed`) | **red 6** — `and SchedulerState names the reason - deferred, not pending (deferred=0)`… |
| 7 per-document atomicity | R1 at stage in `UiHost`; second read at the parse site | **red 4** and **red 1** (the R7 source-shape assertion) |
| 8 two channels | `Reload` routed through `Signal` and returning null | **red 51** — every manual-recovery lane, incl. `Reload commits the same bytes at once…` |
| 9 constant-zero clock | same mechanism as row 2 (quiet inverted) | covered by row 2's red — `with the constant-zero stub clock, pumping 25 times commits nothing (reports=1)` |
| `3.3` | two reads, one report (qualified) | **red 1**, lexical guard only |
| `3.3` | two reads, one report (bare, `using static`) | **GREEN 0/1840 — nothing catches it** |

### T3 — `KernelWidgetCatalogTests`

| item | my planted defect | result |
| --- | --- | --- |
| 1 Snapshot order | `descriptors.Reverse()` before returning | **red 3** — `every declared pair is listed once, ordered by scope then kind…` |
| 2 TryGet exact pair | declaration lookup forced to `CoreScope` | **red 7** — `an exact pair is found and reports the scope it was declared in`… |
| 3 Scopes | declared scope list rotated | **red 1** — `every scope with a registration, ordinal: cat-b, core, cat-a` |
| 4 copies | descriptor ctor keeps the caller's collection | **red 4** — `and fresh per-descriptor collections rather than one shared table view`… |
| 5 zero factory calls | `UiWidgetRegistry.Resolve(scope, kind)` inside `TryGet` | **red 2** — `Listing invokes no factory, even one that throws`… |
| 6 undeclared still lists | `HasAttributeSchema = allowedAttributes != null && Count > 0` | **red 1** — `a declared-but-empty schema is still a declaration…` |
| 7 late registration | not attempted | see `5 |
| 8 lock boundary / listing | registry aliases the caller's schema set (`schemas[kind] = allowedAttributes`) so listing enumerates it | **red 2** — `no listing call enumerates a caller-supplied collection, so no consumer code runs while the lock is held`, `nor the registry's own copy` |

## 5. NOT PINNED and not attempted

**Measured NOT PINNED (planted, stayed green):**

1. `HostAttached` re-announced on window activation — `3.1`.
2. A second document read that produces one report — nothing behavioural catches it (`3.3`); in the bare
   `using static` shape nothing catches it at all.
3. T1's draft/no-command assertions under a host-side state defect (`3.2`) — they are guards, as the T1 record
   says, and even a session-destroying defect leaves the *named* assertions green.

**Not attempted, with the reason:**

- T2 row 4 (pause) — the service has no simulation input to break; the only plantable seam is the clock, and
  breaking it is the same mechanism as row 2, which I did measure.
- T2 row 5 (nothing open) — same report-count-proxy class as `3.3, already measured with a stronger defect
  (two real reads); I did not duplicate it.
- T3 item 7 (late registration) — the teammate's M7 already plants a process-level cache; my time went to the
  items the Lead named. The late-registration lane is nonetheless exercised at baseline (its assertions appear
  in the harness output).
- T2 `1.6 items (sibling-window event atomicity, GUILayout Layout/Repaint, consumer close re-entrancy, watcher
  latency, real hot-control lifetime) — not plantable on this harness; their record already lists them as
  unpinned, and I agree from the code reading.
- T1 item (1)'s "mapping to zero keys" and item (2)'s leaked-outermost-scope residual — stated boundaries in
  their record; I did not plant them.

## 6. Limits of this record

- No 实机 and no second consumer; every claim here is harness-level.
- All my defects lived in a scratch extraction and were never committed; files were SHA256-restored and the
  restored tree re-ran `ALL PASS` and the ten gates.
- The privacy audit's historical-blob vector was skipped (`-FullHistory` not passed), so this record does not
  cover pre-push history.
- T4 (the demo mod) is in flight and is outside this task; the Lead re-runs the integrated gates after it lands.
- This record is bound to `84537fc`; a moved tip invalidates `1` and needs the campaign re-run.

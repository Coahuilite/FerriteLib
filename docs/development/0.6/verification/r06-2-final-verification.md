# R06-F — final independent verification of the R06 fixes and re-pack

Owner: `verify` (shared task `task-40`). Frozen tips verified: FerriteLib
**`7b62416fa623bd38869223e4639d556558c74185`** (`0.6.x`, clean) and the demo
**`587bf160fbf1e566475b8a80d1ee693ed3e9eca9`** (`master`, clean). **Complete: items 1-6.**

The FL fixes in this tip: `eff4f57` + `6b556dd` (risk-3 close-in-attach guard), `a48e808` (R06-2
Attach/pending), `9c7ce56` (risk-1 lane + record), `7b62416` (T5b-4 before-record).

## 1. Gates, harness, lanes, privacy at `7b62416`

Unique extraction of `git archive 7b62416` (path not recorded here); `dotnet restore` exit 0.

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

Harness run directly: `exit 0`, `1912` `ok:` lines, `0` `FAIL:`, `ALL PASS`. Lane coverage: `36` lane files
declare `RunAll`, `36` `Program.cs` registrations, all `36` headers present in the stdout
(`HEADERS_MISSING=0`), `36` blocks with `0` empty and `0` failing. `grep NotImplementedException Source/` = `0`.
Gates were run twice (baseline and after the mutation restores) with the same ten OK lines.

Privacy, run from the worktree at this tip: `vector1 (working tree): scanned`,
`vector2 (commit messages): scanned`, `identity: account 19252128+Coahuilite`,
`vector3 (historical blobs): SKIPPED` (needs `-FullHistory`), `PRIVACY AUDIT CLEAN`, exit 0.

## 2. The external reviewer's own probe — every signature gone (item 2)

Run exactly as instructed, from `ferritelib`, probe untouched:

```
dotnet run --project dist/review-0.6/Review.csproj -c Release
```

Raw output:

```
LIB-REOPEN before=new pending=True
LIB-REOPEN after=new pending=False
FILTER: no exception
ADD: before=8 model=9 alpha=9 beta=9
REOPEN: no exception
LATE: live=1 page=1
IDENTITY: review-a__kind == review~005Fa__kind : False
COLLISION: no exception
PROBE_EXIT=0
```

(The run also prints its own pre-existing `warning CS8602` at `Program.cs(37,4)`; the probe was not edited.
It remains a warning, not an error.)

**Verdict: all seven signatures gone.** No `FILTER` exception, `ADD` shows model and both VMs at 9, no
`REOPEN` exception, `LATE page=1`, `IDENTITY` false, no `COLLISION` exception, `LIB-REOPEN after=new`.

## 3. My own defects against the fixes (item 3, FL half)

Second scratch extraction of `7b62416`; every defect is mine, each planted into a production file, built
(`--no-incremental`), run, then byte-restored (SHA256 checked) with the restored tree green (`1912/0`) and the
gates `VERIFY_EXIT=0` again.

| fix | my planted defect | result | named assertion(s) |
| --- | --- | --- | --- |
| R06-2 | **M1** `RefreshBeforeAttach` reverted to the old gate (resolve only when a schedule existed) | **red 8** | `reopen shows the new content even though no Pump ever observed the signal`; `an unsignalled content change still resolves at attach (content-version freshness)`; `the late directory's file resolves at attach even though no watcher ever armed`; `and the break is reported once at attach (reports=0)`; `the same refusing version is not reported again` (+3 report-count assertions) |
| R06-2 | **M2** `pending.Remove(documentId);` dropped (state not consumed) | **red 1** | `and the signal was consumed, not thrown away` |
| R06-2 | **M4b** the non-retryable "broken" branch turned into a plain `return` instead of `ReloadCore(..., preRead)` | **red 2** | `and the break is reported once at attach (reports=0)`; `the same refusing version is not reported again` |
| R06-2 | **M5** the retryable "missing" branch treated as a change (calls `ReloadCore`) | **red 2** | `a missing file with a valid fallback is not a failure`; `and attach does not turn an absence into a report` |
| R06-2 | **M3** the `GoodVersion` freshness early-return made always-false (always treat as changed) | **GREEN** | see below |
| risk-3 | **M6** the post-announcement `if (host == null) return;` re-check removed | **red 2** | `CLOSE-IN-ATTACH-NO-FAILURE: closing the window from its own HostAttached is not a page failure`; `CLOSE-IN-ATTACH-NO-EXCEPTION: and no exception was recorded for it` |
| risk-1 | **M7** `applied[i].Host.SealDocumentCommit();` commented out (the destructive seal never runs in the batch commit) | **red 10** | `the running pass finishes on the pre-reload snapshot and the seal lands mid-pass … ([trigger:hot,tail:hot])`; `and the held capture was released by the mid-pass seal`; `the next pass draws the new tree only ([trigger:hot,head:hot])`; `a reload releases the hot control instead of leaving it held`; `a removed element's state is cleaned up`… |

The Lead's warning about `a48e808` was the right one to attack: **M1 is exactly the first-cut/old-gate
shape, and it does not leave the lanes green** — the six new R06-2 lanes redden with their own named
assertions (the (a) pending-signal lane and the (b) unsignalled-content lane both fire). The false-PASS is
closed.

**M3 is worth stating precisely, because it did *not* redden: the freshness *early-return* is a fast path,
not the behaviour guard.** With it disabled, the unchanged file still produces no report, because
`ReloadCore` is handed the same pre-read candidate and applies its own unchanged-version rule. So the
contract claim ("an unchanged file is not turned into a report") stays pinned — by the report-count assertion,
not by the early return. I do **not** record this as a NOT PINNED fix; I record it as a redundant guard.

**Independent cross-check with my own pre-fix fixture.** My R06 reproduction probe (own code, not the
reviewer's) reported `hasOld=True hasNew=False` at the pre-fix tip. Loaded against the re-packed DLL
(SHA-256 `2A7F9C9E…ECC0D`, i.e. the artefact of item 4) the same fixture now prints:

```
R062 firstAccepted=False attached1=True fileIsNew=true signalled=True pendingAfterSignal=True attached2=True hasOld=False hasNew=True pendingAfterAttach=False schedulerPending=0 reports=1
```

That is the fixed behaviour measured a second way: the reopened host holds the NEW layout and exactly one
report was written.

### Risk-1 lane: does it pin what the record claims?

`t2-reload-scheduling.md` `7` claims: nothing throws; the running pass finishes on the pre-reload snapshot;
the commit is synchronous; the destructive half lands mid-pass; the session stays coherent; the next pass
draws the new tree only. The lane `VerifyReloadInsideDrawPass` (`KernelReloadSchedulingTests.cs:844-915`)
asserts each of those by name, and `VerifyReloadAllInsideDrawPass` covers the two-document shape. **M7
confirms the lane is load-bearing**: skipping the batch seal reddens four of its assertions by name (plus the
P4 hot-control and prune lanes). Their own mutation table (`M-E`/`M-F`/`M-G`) covers the other two halves.

**What stays unpinned (I agree with the record's own list and add one):** the real IMGUI capture semantics of
a mid-pass release (the stub's `CaptureHotControl` is a session field); the appearance/geometry of the
running pass after a mid-pass theme swap; a consumer that reloads from `PreClose`/`PostClose`; the deferred
safe-boundary proposal (nothing measures it). I add: the lane drives one synthetic pass of one window, so the
event-level ordering across sibling windows in a real IMGUI event (Layout vs Repaint vs input) stays
unobserved. The `9c7ce56` commit is a lane plus documentation, not a behaviour change, and the record says so.

## 4. The re-packed dev package (item 4)

`dist/dev/FerriteLib/` holds exactly five files: `LICENSE`, `LoadFolders.xml`, `version.txt`,
`1.6/Assemblies/FerriteLib.UiKit.dll`, `About/About.xml`.

```
FerriteLib 0.6.0-dev
build=dev
commit=7b62416fa623
source https://github.com/Coahuilite/FerriteLib
```

DLL SHA-256 `2A7F9C9E9B1D060B32037A82E2989817A19B8317D2FB8B48A68F15B13DDECC0D` — matches the Lead's label.

**Tip reconciliation:** `0.6.x` is still exactly `7b62416fa623bd38869223e4639d556558c74185` at verification
time (checked `git rev-parse` and `git log`), so the package label names the verified tip **exactly**; no
documentation commit followed the content freeze, and none needs to be excepted. The packaged DLL is what my
own fixture loaded (hash copied above and re-read after the copy).

## 5. The demo half (items 3 and 5) — verified at `587bf160`, clean

Demo repository: branch `master`, tip **`587bf160fbf1e566475b8a80d1ee693ed3e9eca9`**, working tree clean;
read-only for me — every defect below was planted in a **scratch copy** of the repo, and that copy was clean
again after every run. Two commits landed since `b0fb161`: `f19ca9c` (validate the package before publishing
it; pin the new Attach contract) and `587bf16` (build without trusting the up-to-date check).

### 5.1 My own defects against the six demo fixes

Each row: my defect, written into one source file of the scratch copy, then the demo's probe rebuilt
(`--no-incremental`) and run. Baseline before each row and after every restore: `DEMO PROBE PASS`.

| item | my planted defect | probe result |
| --- | --- | --- |
| R06-3 | re-register `consumerKinds` inside `RefreshConsumerRows` (every scope change binds it again) | **FAIL** `consumer-filter-switch-repeatedly :: InvalidOperationException :: Duplicate value binding 'consumerKinds'.` (plus `catalogue-identity-is-injective` on the same exception) |
| R06-4 | `OnModelChanged` keyed on `DataProperty` instead of `ItemsProperty` (the structural announcement ignored) | **FAIL** `dual-window-add-under-different-filters` |
| R06-6 | `RowKey` made lossy again (`~` and `_` both collapse to `_`) | **FAIL** `catalogue-identity-is-injective` |
| R06-7 | `RefreshCatalogue` no longer calls `DemoCatalog.Read()` | **FAIL** `late-registration-is-visible` |
| R06-1 | tracked manifest `<li>1.6</li>` → `<li>1.60</li>` | `pack.ps1` **exit 1**: "LoadFolders.xml declares /, 1.60 but no declared folder + Assemblies/FerriteLibUiKitDemo.dll resolves the game-visible assembly in the candidate" |

Every row reddened its owning lane, and the restored scratch tree was clean (`git status --porcelain` empty)
with the probe green again.

### 5.2 The new consumer-side lane is not vacuous (measured against the real pre-fix behaviour)

`tools/DemoProbe/Program.cs` adds `attach-adopts-newest-content-without-pump`, asserting
`pendingBefore && before=="old" && after=="new" && !pending`. I built the **pre-fix carrier from source**
(`git archive a6e8885`, Release; SHA-256 `FF24197E27DCFBF4095183F27BF3B2516C8C16D720615C7617D27C0776203AD9`,
230912 B) and pointed a scratch demo copy's `FerriteLibArtifactDir` at it:

```
pre-fix carrier : no-pump attach: before=old signalPending=True after=old pending=False
                  FAIL attach-adopts-newest-content-without-pump        (probe exit 1)
fixed carrier   : no-pump attach: before=old signalPending=True after=new pending=False
                  PASS attach-adopts-newest-content-without-pump        (probe exit 0)
```

The lane cannot pass by constructing the host from the new text: it asserts the pre-state (stale `before`,
signal pending) and it detects the real pre-fix defect.

### 5.3 Attacking the artefact, not the tree (the class the demo owner fixed)

The pack path stages into `dist/.staging-*`, validates, and swaps only on success.

| attack | raw result |
| --- | --- |
| **A2** tracked manifest mutated (`<li>1.60</li>`), then `pack.ps1` | exit 1 with the reachability message; delivered-folder fingerprint **unchanged**; `dist/.staging-*` **absent** |
| **A1** delivered artefact drifted (`LoadFolders.xml` edited inside `dist/`, tracked tree correct), then `pack.ps1` | exit 0; the delivered manifest becomes byte-identical to the tracked one (`3FD398611C5E6013B590BB1E13A5FB328F88AE9A1C6608F6AE076DD0D63D5C49`) |

The "a refused pack leaves the previous artefact byte-identical" claim holds under my own attack, and a pack
from a correct tree repairs drift. Boundary worth one line: **drift is repaired, not detected** — a delivered
folder left stale by some other process reddens nothing until `pack.ps1` (or `verify-fixes.ps1`, which packs
at baseline) runs. That matches what the scripts claim; it is not a claim that a stale artefact would be
reported as such.

### 5.4 Item 5 — the delivered demo package

From my own scan of `dist/FerriteLibUiKitDemo/` at `587bf160`:

- **8 files** (FerriteLib's package is the five-file one; the demo also ships `Languages/` and `Xml/`).
- `LoadFolders.xml` 200 B, SHA-256 `3FD398611C5E6013B590BB1E13A5FB328F88AE9A1C6608F6AE076DD0D63D5C49`,
  declaring `/` and `1.6`.
- **My own reachability computation:** declared `/` → `dist/FerriteLibUiKitDemo/Assemblies/FerriteLibUiKitDemo.dll`
  `exists=False`; declared `1.6` → `dist/FerriteLibUiKitDemo/1.6/Assemblies/FerriteLibUiKitDemo.dll`
  `exists=True`. The demo's own `pack.ps1` prints the same lookup, the reachable path, `staged … (8 files)`,
  and `no FerriteLib.UiKit.dll; no machine-absolute path in any publishable file`.
- **No `FerriteLib.UiKit.dll`** anywhere in the package (0 hits) and no tracked file with that name.
- Demo DLL 49664 B, SHA-256 `53CBF59E3BDD8602CC0E43E45A3BF01412119BEB64339B3F2FAB6FB2EC4685E7`.
- **The package's DLL is the one its probe loaded:** the probe's build output copy
  `tools/DemoProbe/bin/Release/net472/FerriteLibUiKitDemo.dll` has exactly that SHA-256, and its
  `FerriteLib.UiKit.dll` copy is `2A7F9C9E…ECC0D` — the re-packed FL dev-package DLL.
- **No drive-letter path in tracked files:** 25 tracked files scanned for `[A-Za-z]:\` and `[A-Za-z]:/`
  → 0 hits. (A laxer first pass flagged `candidate":` inside a probe string literal; the strict scan above is
  the reported measurement.)

## 6. What I did NOT verify

- **In-game / real game load.** No game was launched: the LoadFolders reachability, the missing-settings-entry
  symptom, the real IMGUI pass, the real `WindowStack` ordering and the real text measurement are all
  unobserved here. The reviewer's probe runs against the harness's game stubs, and so does my own probe.
- **RISK-1's in-game safety**: the lane is a synthetic single-pass observation; the record's own
  "does not pin" list stands.
- **The demo half's stub surface.** The demo probe compiles against the harness's game doubles
  (`FerriteLibStubDir` = `tools/FerriteLib.UiKit.Tests/bin/Release/net472`), so R06-3/4/6/7 are observed
  through `Verse`/`UnityEngine` stand-ins; `pack.ps1`'s LoadFolders resolution is a filesystem computation,
  not an observation of a running `ModLister`, so R06-1 stays derived rather than seen in game.
- **The pre-fix carrier is a rebuild**, not the archived pre-fix package: its assembly informational version
  carries the a6e8885 commit, so its bytes are not the overwritten pre-fix package's bytes. The lane flip is a
  statement about the pre-fix code, which is what it has to be.
- **Anything bound to a moved tip.** This record is bound to `7b62416` (FL) and `587bf160` (demo). A move on
  either invalidates the corresponding sections and needs them re-run.

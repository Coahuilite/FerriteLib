# R06-F — final independent verification of the R06 fixes and re-pack

Owner: `verify` (shared task `task-40`). Frozen FL tip verified: **`7b62416fa623bd38869223e4639d556558c74185`**
(`0.6.x`, clean). Status of this record: **items 1, 2, 3 (FL half) and 4 are complete; item 3 (demo half) and
item 5 are pending the demo owner's confirmed re-commit** — see `7.

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

## 5. Demo package (item 5) — PENDING

Not reported yet, deliberately. At this moment the demo repository is at `b0fb161`
("fix(demo): close the six R06 demo defects with lanes and planted mutations") with a **dirty working tree**
(`README.md`, `pack.ps1`, `tools/DemoProbe/Program.cs` modified) — i.e. the owner's own re-probe/re-pack is in
progress. The Lead said the final demo sha and hash will be confirmed; until then I make no claim about the
package, its file set, its LoadFolders resolution or its DLL hash.

## 6. What I did NOT verify

- **In-game / real game load.** No game was launched: the LoadFolders reachability, the missing-settings-entry
  symptom, the real IMGUI pass, the real `WindowStack` ordering and the real text measurement are all
  unobserved here. The reviewer's probe runs against the harness's game stubs, and so does my own probe.
- **RISK-1's in-game safety**: the lane is a synthetic single-pass observation; the record's own
  "does not pin" list stands.
- **The demo fixes (R06-3/4/6/7, R06-1's packaged reachability) and the demo package** — `5 and `7.
- **Anything bound to a moved tip.** This record is bound to `7b62416` for FL. If `0.6.x` moves before the
  demo half is done, `1, `3 and `4 must be re-run.

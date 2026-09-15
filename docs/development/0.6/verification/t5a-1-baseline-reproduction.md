# T5a-1 — baseline reproduction at the 0.6.x tip `02a6aea`

Owner: `verify` (shared task `task-31`). Everything below was run in this session; no PASS is written that
was not reproduced here. Nothing in this record is 实机验证: a green gate, a fixture and a build are not
behaviour checks.

## 1. Revision and extraction

| item | value |
| --- | --- |
| Tip verified | `02a6aea` — "feat(0.6): fork 0.6.x, freeze the MVVM/reload/catalogue contract, and land the version bump" (branch `0.6.x`) |
| Extraction | `git archive 02a6aea` into a **unique** temp directory created for this task; the path is deliberately not recorded in this tracked file (the 0.5 round found a shared temp path tampered with) |
| Restore | `dotnet restore tools/FerriteLib.UiKit.Tests/FerriteLib.UiKit.Tests.csproj` -> exit 0 |
| Second extraction | a second, separate unique temp directory for the planted-defect runs (sections 4-5), so the baseline extraction was never written to |

This record is bound to `02a6aea`; if the tip moves, section 2 must be re-run.

## 2. `scripts/verify-local.ps1` — raw tail

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

The ten printed lines are the `[setup]` restore step plus the script's nine numbered checks (1 harness, 2 Dev
build, 3 Release build, 4 payload present, 5 payload content-free, 6 LICENSE, 7 About.xml identity,
8 runtime-resolvability traps, 9 dependency-reality rule (c)); every one is `OK`. Each line is printed only
after that check's body ran, so no check is skipped. The command was run twice in the extraction (baseline,
and again after the planted-defect runs were restored) and printed the same ten lines both times.

**Finding: none — all ten lines OK, `VERIFY_EXIT=0`.**

## 3. Lane execution evidence — a green gate that skipped a lane is not green

Gate 1 is reported as `OK` because the harness printed `ALL PASS`; that alone does not prove a lane ran.
Confirmed three ways from the harness's own captured stdout (and the harness was run directly, not only
through the gate):

1. **Lane count = 32.** `tools/FerriteLib.UiKit.Tests/Program.cs` has exactly 32 `*Tests.RunAll()`
   registrations, and exactly 32 `*Tests.cs` files declare `public static int RunAll`
   (counted with `Get-ChildItem ... | Where-Object { (Get-Content $_.FullName -Raw) -match 'static int RunAll' }`,
   which returns 32 at `02a6aea`).
2. **Every registration ran.** All 32 `Console.WriteLine("<header>")` literals in `Program.cs` appear in the
   harness stdout, in order (`HEADERS_MISSING=0`).
3. **No block ran empty.** Splitting the stdout at those 32 headers, every block holds at least one
   `  ok:` line and zero `  FAIL:` lines; the smallest block has 3, the total is 1572. Raw harness result:
   `HARNESS_EXIT=0`, `OK_COUNT=1572`, `FAIL_COUNT=0`, last line `ALL PASS`.

The four named lanes were confirmed by their own assertion text in that stdout:

| lane | assertion text found in stdout (each exactly once) |
| --- | --- |
| `FerriteLibApiTierTests` | `Every public payload type is classified in exactly one tier`; `No tier entry names a type that no longer exists`; `The stable tier matches the pinned promise`; `Tier comparison fires on a planted unclassified type` |
| `KernelLaneRegistrationTests` | `Every lane file that declares RunAll is invoked from Program.cs`; `Every Program.cs RunAll registration names a lane that exists`; `No lane is invoked more than once from Program.cs`; `The comparison fires on a planted unregistered lane and ignores a renamed file` |
| `FerriteLibNeutralityTests` | `No product vocabulary in the library or its harness`; `Gate fires on a planted literal in every scanned tree`; `Exemption is pinned to this one file` |
| `KernelContainmentTests` | `Backend contact stays inside the funnel files`; `Containment scan fires on a planted violation`; `Funnel allowlist has no dead entries`; `The context-free hit overload stays inside its allowance`; `Context-free hit allowance has no dead entries`; `The documented contract matches the measured call sites` |

`KernelLaneRegistrationTests` is the structural guard against the "lane file that never runs" failure, and it
reports 4 assertions green, including its own planted-unregistered-lane control — so its registration scan is
not vacuous. The api-tier, neutrality and containment lanes likewise each report their own planted control
green, so their green is not a scan of an empty tree.

**Finding: none — no lane was skipped.**

## 4. R1 — rolled-back batch keeps the draft: mutation-proven at 0.6

Lane: `KernelDocumentReloadTests` -> `Run("A rolled-back batch restores the old document AND the interaction
state it held", ...)` -> `VerifyRolledBackBatchKeepsInteractionState` (`KernelDocumentReloadTests.cs:58` and
`:832`). The lane fails host B's commit through the service's own `DocumentCommitFaultOverride` seam and
asserts the old document, the uncommitted draft, the named state slot and the scroll position all survive,
then that a fault-free re-run commits and does prune.

Planted defect (the 0.5-shaped regression, at 0.6's own anchor — `UiHost.CommitLayoutCandidate`, the staging
step): the destructive half is run during staging instead of after the whole batch committed.

```
before:  pendingPrune = previous;
after:   pendingPrune = previous; SealDocumentCommit();
```

Raw result (second scratch extraction of `02a6aea`; anchor count asserted 1 before the edit; the file restored
and SHA256-verified after):

```
R1 ANCHOR_COUNT=1
=== M-R1-seal-at-stage build=0 harness_exit=1 ok=1569 fail=3 ===
TOTALS: 3 test(s) failed.
FAIL> FAIL: and its uncommitted draft survived the rollback
FAIL> FAIL: and its named state slots
FAIL> FAIL: and its scroll position
RESTORED_OK=True
```

**Verdict: mutation-proven at `02a6aea`.** The 0.5 R1 regression lane still fires, on exactly the draft /
named-slot / scroll half the round must not lose.

## 5. R7 — single-read document pipeline: what the lane pins, and what it does not

Lanes under `KernelDocumentReloadTests`:

- `Run("The document path reads one snapshot and stays backend-free", VerifySourceWiring)` (`:65`,
  `:1242-1281`) — a **lexical** guard over the comment-stripped `UiDocumentService.cs`:
  `IsSingleReadService` requires exactly one `File.ReadAllBytes(` and exactly one `new StreamReader(`, and
  rejects the nine shapes in `ForbiddenReadShapes` (`File.ReadAllText(`, `File.ReadAllLines(`,
  `File.ReadLines(`, `File.OpenRead(`, `File.OpenText(`, `File.Open(`, `new FileStream(`, `ParseFile(`,
  `File.Exists(`). It carries its own planted controls: a second `Parse(ReadAllBytes(...))` read must redden
  it, and a read named only in a comment must not.
- `Run("An oversized document is refused and keeps the last known good", ...)` (`:62`) and
  `Run("A BOM-prefixed document parses exactly like its BOM-less equivalent", ...)` (`:63`) — the
  **behavioural** halves that pin what a snapshot decides (the size bound, and the BOM-aware decode with a
  byte-based content identity).

### 5a. The shape the lane does pin — mutation-proven

Planted defect: a second, real, executed read in the shape the 0.6 lane was widened to catch (the 0.5
false negative), at `UiDocumentService.cs:765`:

```
before:  return Candidate.LayoutVersion(version, UiLayoutManifest.Parse(Decode(bytes)));
after:   return Candidate.LayoutVersion(version, UiLayoutManifest.Parse(Decode(File.ReadAllBytes(path))));
```

Raw result:

```
R7a ANCHOR_COUNT=1
=== M-R7a-second-qualified-read build=0 harness_exit=1 ok=1571 fail=1 ===
TOTALS: 1 test(s) failed.
FAIL> FAIL: the document path reads one snapshot and no other file-reading entry point
RESTORED_OK=True
```

**Verdict: mutation-proven for that shape**, and no behavioural lane moved — which is what a source-shape
guard should do. The 0.5 record showed a real second read leaving this lane green; at `02a6aea` the
`Parse(ReadAllBytes(...))` form reddens it.

### 5b. What the lane does NOT pin — two things, one of them measured

1. **Run-time atomicity / the TOCTOU itself is not pinned.** No lane interleaves a read and a parse; the
   lane's own summary says so and gives the reason (there is no read seam to hook, and adding one would be
   test scaffolding in the product). The guard is a regression guard against re-introducing a second read; it
   is not proof that the bytes read are the bytes parsed.
2. **The lexical boundary is evadable, and the evasion is measured.** A second real read written as a bare
   `ReadAllBytes(path)` under `using static System.IO.File;` leaves `File.ReadAllBytes(` occurring exactly
   once, so `IsSingleReadService` still returns true. Planted on top of the same site:

```
using System.IO;
using static System.IO.File;                     // added
...
return Candidate.LayoutVersion(version, UiLayoutManifest.Parse(Decode(ReadAllBytes(path))));   // second read
```

Raw result:

```
R7b USING_ANCHOR_COUNT=1
=== M-R7b-bare-read-via-using-static build=0 harness_exit=0 ok=1572 fail=0 ===
TOTALS: ALL PASS
RESTORED_OK=True
```

This is **not a defect in the lane**: the lane states in its own summary that it is a lexical guard with an
enumerated boundary, and `ForbiddenReadShapes` is that boundary written down. The finding to carry is its
scope — the guard covers the read shapes it names, inside `UiDocumentService.cs` only. A regression routed
through an alias / `using static`, a helper in another file, or an async or `Stream`-family read outside the
listed tokens stays green, and the run-time TOCTOU stays unpinned in every shape.

Post-restore in the same scratch extraction: `RESTORE-GREEN build=0 harness_exit=0 ok=1572 fail=0 ALL PASS`,
and the full gate again `VERIFY_EXIT=0`.

## 6. T0 skeleton markers (context, not a finding)

`grep -rn "NotImplementedException" Source/` at `02a6aea` returns four hits, all the expected T0 markers:
`UiDocumentService.cs:123` (`T2: UiDocumentService.SchedulerState`) and `UiWidgetCatalog.cs:30/:36/:42`
(`T3: UiWidgetCatalog.Snapshot` / `TryGet` / `Scopes`). No lane reaches them (the harness is green), and the
delivery check is the zero-match grep at T5 per `10-work-packages.md` T0. Recorded so a later reader does not
mistake them for a defect at this revision.

## 7. Not verified here

- **T1 / T2 / T3 behaviour**: no package code has landed; the adversarial verification of those packages is
  `task-28` and is blocked until they land. Nothing in this record covers them.
- **The demo mod (T4)** and the **package / external-probe half (T5)**: untouched by this task.
- **In-game acceptance A1-A11**: outside this round's authorization; `02a6aea` is 已实现 + 已自动化验证 only.
- **R7 atomicity** (5b.1) and **the guard's uncovered read shapes** (5b.2): stated as gaps, not as PASS.
- **The nine new tier entries, entry by entry**: `FerriteLibApiTierTests` ran green with its own planted
  control, but I did not re-read `docs/api-tiers.md` against the nine new types; that is the tier lane's
  claim, not a second measurement here.
- **Gate 9 (dependency-reality rule (c))** was exercised only through `verify-local.ps1`; I did not plant a
  violation in it myself (the script header states it carries its own self-test and fixture).
- **Re-extraction on a moved tip**: this record is bound to `02a6aea`.

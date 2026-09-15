# Final integrated verification — tip `1769e37`

Verifier `verifier` (task-4). Date 2026-09-15. Delivery candidate `1769e37bd8a46e4bb62e244547261a22e77cacc0`.
All runs on clean `git archive` extractions in temp directories; the shared checkout was never mutated.

**Revision note.** `1769e37` is **docs-only** over `85c5fcc` (`git diff --stat 85c5fcc 1769e37` = 7 files
under `docs/`+`MEMORY`+`TODO`), and every code path named below is byte-identical between `23c5a33`,
`85c5fcc` and `1769e37` (`git diff --stat 23c5a33 1769e37 -- UiLayoutEngine.cs UiDiagnostics.cs
UiDocumentService.cs Widgets/ *.Tests lanes` is empty). The build gate was run on `85c5fcc`; the invariant
sweep and the git-level checks on `1769e37` itself.

## 1. Delivery gate

| run | sha | result |
| --- | --- | --- |
| `pwsh -NoProfile -File scripts/verify-local.ps1` | `85c5fcc` (code-identical) | **exit 0**, `[setup]` + 9 gates OK |
| harness | `85c5fcc` | **exit=0 ok=1426 fail=0 ALL PASS** |
| invariant sweep + privacy grep | `1769e37` | below |
| privacy-audit | repo | `PRIVACY AUDIT CLEAN`, exit 0 |

## 2. Invariants and merge resolutions at `1769e37`

| check | result |
| --- | --- |
| tiers vs public types | **TIERS=74 DECLS=74 NOT_TIERED=0 STALE_TIER=0** |
| lane registrations | **registered=32 declared=32 unregistered=0 phantom=0** (see finding F3: expected 33) |
| three axes | `Api = 0.5.0`, `<VersionPrefix>0.5.0`, `<modVersion>0.5.0` |
| conflict-marker residue | **none** (`^(<<<<<<<|>>>>>>>|\\|\\|\\|\\|\\|\\|\\|)` → no hits; the earlier `LICENSE:2:====` match is the MPL header underline) |
| privacy at the sha | no personal paths, no `<PublishedFileId>` value, no credential pattern |
| merge (1) diagnostics | `PlantedPrepareFailure|PreparePhase` in `Source` = **0**; `PublishToHosts|PublishToHost` = **10**; P4c page-level pre-check messages = **2**; only `DocumentCommitFaultOverride` remains |
| merge (2) collections/api-tiers | `UiTreeRow` tier bullet = **1**; `UiInvalidation` appears once as a tier bullet + prose (no stale duplicate); both `Program.cs` registration sets present |

Both hand-resolved merges did what the Lead intended.

## 3. P3 (`bb90907`) — never previously verified

**Lane counts (executed `ok:` lines) — all three claims REFUTED:** repeater **61** (claimed 60),
common controls **47** (claimed 46), fixture page **19** (claimed 14). Static `Check(` sites are 49/48/17,
so the claimed numbers match no metric.

**Five mutations (each: byte snapshot, one-anchor assertion, SHA256-verified restore; harness command
`dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release`):**

| mutation | measured | claimed | verdict |
| --- | --- | --- | --- |
| declared-identity set omitting rows | **exit=1 ok=1382 fail=18** (repeat 13 / fixture 5) | 14 | red, count **REFUTED** |
| positional row identity | **exit=1 ok=1371 fail=24** (repeat 17 / fixture 7) | 14 | red, count **REFUTED** |
| read-only checkbox hit-rule removed | **exit=1 ok=1415 fail=1** | (n/a) | red |
| engine disabled decision never fires | **exit=1 ok=1408 fail=8** | (n/a) | red |
| hidden-collection rows reaped | **exit=1 ok=1412 fail=2** | (n/a) | red |

All five turn the suite red, so the mutation list is real; only the two numbers fail.

**Hazards — all PASS, each with a named lane assertion:**
- (a) blank / duplicate / reserved item key refuses that row: `KernelRepeatTests.cs:246-261` and `:263-275`
  (keys `""`, `"a"`, `"b/c"`, `"d#e"`), engine contract `UiLayoutEngine.cs:1641-1665`; tree analogue
  `KernelControlKindTests.cs:335-367`.
- (b) reorder reuses state by item key: `KernelRepeatTests.cs:105-134` (per-key `ReferenceEquals` node, state follows).
- (c) removal releases node **and** sub-node **and** state: `:196-219` (`GetNodeByElementId == null`,
  sub-node `GetNode == null`, `GetValueStates.Count == 0`); reddened by mutations (i) and (v).
- (d) a hidden collection keeps its last materialization: `:221-244`; reddened by mutation (v).
- (e) fixture page: inline `FixturePageXml` `KernelFixturePageTests.cs:43-57` + model/bindings `:198-255` has
  **no** `Rect(...)`, no `BumpContentRevision`, no `UiNodeId`/index arithmetic, no page-level input rule;
  identity is the library's `entry#<key>`. The only `new Rect`/`NotifyChanged`/`Event` hits are the lane's
  test half (`:139,145,272,304,311` / `:127,159` / `:264-273`). Caveat: the comment at `:38-42` is literally
  false for the test methods below it (true of the page half only).

## 4. P5 (`a09d6e6`), task-13, task-14

**The tip does not enumerate "7 mutations".** `10-work-packages.md:63` claims "7 处突变转红、未订阅路径实测 0
分配" with no list; `plan-P5-diagnostics.md` lists exactly 6 claims; `40-verification.md` P5 row is blank.
Six representative mutations were reproduced, all red: C1 attribution removed **23 FAIL**; C2 dispose
releases another host **4 FAIL**; C3 dedup removed **4 FAIL**; C4 recovery kind dropped **5 FAIL**;
M5 allocation probe **1 FAIL**; M6 process-wide dedup **2 FAIL**.

**Allocation lane (judged, not trusted).** `KernelDiagnosticsTests.VerifyUnsubscribedPathAllocatesNothing`
(269-304) reproduces `subject 0 bytes vs control 483328 over 20000 passes`. It is a **real guard** for
per-pass allocation on the two calls it drives: planting one allocation in `UiDiagnosticHub.EnterHost`
reddens it (`483328 vs 483328`). Noise cannot make it pass vacuously (the `MonitoringTotalAllocatedMemorySize`
control varied 475136-483328, ~1.7%, well inside the ~60KB tolerance). **Two limits:** the subject is a
hand-written two-call loop, not `UiHost.DrawFrame`, so allocations in arrange/draw/context/frame-start on an
unsubscribed host are unmeasured; and nothing asserts the subject loop ran, so `subject=0` is
indistinguishable from an elided loop. It is narrower than the claim it is cited for.

**Interleaving is PARTIAL.** `VerifyTwoHostsAreIsolated` keeps two hosts live and checks A after B drew, and
the process-wide-dedup mutation reddens it; but no lane alternates frames A,B,A,B, so the plan's explicitly
required interleaved-hosts lane (`plan-P5-diagnostics.md:27-29`) is absent.

**task-13** (`00e0fd4`, `UiWindowHost.cs` +24): the shell pass is wrapped in
`UiDiagnosticHub.EnterHost(host?.CurrentDiagnostics)` with the old body extracted to `DrawShell`; the
failure-notice path (`noticeDueNextFrame`, `PageUnavailable`, `Prerequisite`, `DrawNoticeScoped`) is unchanged
and `KernelWindowHostTests` is green. **task-14** (`0629bd4`, `UiPageWindow.PageHost` public getter) adds
10 checks (1416 → 1426) and the window-catalog lane stays green.

## 5. Cross-package integration lanes (my own, run at the merged code)

Two lanes were designed and run through the turnkey harness (new lane file + one registration, in a
disposable extraction only):

- **LANE 2 (P4 × P3) — PASS.** A layout file containing a repeater, hosted through `UiDocumentService`
  (AutoWatch off): editing the XML and committing via `Signal`+`Pump` keeps every row's state and identity
  **by item key** and adds the new widget. Raw: `exit=1 ok=1440 fail=1` for the whole harness, and the single
  FAIL belongs to LANE 1 (below); LANE 2 contributed no FAIL.
- **LANE 1 (P3 × P2 × P2b) — PARTIAL.** It drove repeater rows through `NotifyChanged`/`UiInvalidation.Structure`
  and the removed-identity release, but aborted on its own probe defect:
  `FAIL: LANE 1 ... threw Exception: row c's probe widget minted no sub-node`. The probe looks up a
  widget-minted sub-node without drawing that row first, so this is a harness bug in the probe, not a red
  library assertion. The P3 per-package lane `KernelRepeatTests.cs:196-219` does cover node+sub-node+state
  release and is mutation-proven; the **cross-package** form of that one assertion remains unproven.

## 6. Findings

| id | finding | class |
| --- | --- | --- |
| F1 | P3's three lane counts (60/46/14) and two mutation counts (14/14) are wrong; measured 61/47/19 and 18/24 | **must-document** (record accuracy; no behaviour defect) |
| F2 | P5's "7 mutations" is unenumerated and `40-verification.md`'s P5 row is blank | **must-document** |
| F3 | lane registrations are **32**, not the expected 33; nothing is unregistered or phantom, so this is a miscount (task-14 added checks, not a lane) | **must-document** |
| F4 | the plan-required P5 interleaved-hosts lane is absent | **must-document**; if the plan line is contractual, decide before delivery |
| F5 | the allocation lane is narrower than its claim (synthetic two-call subject, no subject-liveness assertion) | **must-document** |
| F6 | my own dead-lane guard dedupes registered names, so a duplicated `Program.cs` registration is invisible (proven: planted duplicate → 4 tier FAILs, guard lane green) | **must-fix-in-0.5** (small, verifier-owned file) |
| F7 | the fixture lane's `:38-42` comment is false for its test half | **must-document** |

No correctness defect was found in P3, P5, task-13 or task-14; the delivery gate is green at the candidate.

## 7. Not verified

In-game behaviour for every package (unchanged); the cross-package sub-node-release assertion (F6/F5
probe limits); a consumer compiling against the surface.

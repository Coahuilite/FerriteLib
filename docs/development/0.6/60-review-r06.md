# R06 — disposition of the independent review

Date: 2026-09-16. Input: modding_documents/team-mode/ferritelib-0.6-independent-review-zh.md (**Request changes**).
Every one of the seven must-fix items was reproduced independently before being fixed — by the Lead on the
reviewer's own probe, and by this round's verifier with its own fixtures (never by re-running the reviewer's
probe): verification/r06-1-independent-reproduction.md.

## 1. How the review was validated

- The reviewer's probe was run as-is (dotnet run --project dist/review-0.6/Review.csproj -c Release): all
  seven signatures reproduced exactly.
- The verifier wrote its own probe and reproduced all seven with its own sequences (record §1-§7).
- R06-1 (the load-folder defect) was additionally confirmed from the game's own source by the Lead and the
  verifier: Verse.ModContentPack.InitLoadFolders AddFolders(list); return; when a version-specific
  list exists, and ModAssemblyHandler.ReloadAll loads only what
  GetAllFilesForModPreserveOrder(mod, "Assemblies/", .dll) walks over each declared load folder.
- Severities agreed with the reviewer on all seven; nothing was overstated, nothing was wrong.

## 2. Disposition per item

| # | Severity | Owner | Fix | Evidence |
| --- | --- | --- | --- | --- |
| R06-1 | P1 | demo | LoadFolders.xml now declares / + 1.6; pack.ps1 computes reachability the way the game does (declared folder + Assemblies/) and refuses a candidate whose DLL is unreachable | pack.ps1 refusal mutation; external probe reachability section FAIL→PASS (t5b-4); verifier artefact attacks A1/A2 |
| R06-2 | P1 | FL | UiDocumentService.Attach now consumes **both** pending and scheduled watcher state before the host joins the dependency table, then ONE read decides every shape by content version (a: signal while nothing attached, b: watcher never fired/late directory, c: file missing → last valid stays, d: broken → refused once, last valid stays). No quiet period on the explicit path; R7 single-read still green | reviewer's exact no-pump shape now after=new; 6 new lanes; FL fixes mutation-proven (old-gate revert red 8 incl. the false-PASS shape, pending-consumed red 1, broken red 2, missing red 2); outside probe (3) FAIL→PASS |
| R06-3 | P1 | demo | list bindings register **once**; getters read mutable filtered results; only new row identities get per-row bindings | consumer-filter-switch-repeatedly lane; verifier own defect red |
| R06-4 | P2 | demo | the VM owns the model's structural property; Sync() rebuilds visible list + item bindings + adapter value mapping **before** NotifyChanged; subscribe/unsubscribe as a pair | dual-window-add-under-different-filters lane; verifier defect red |
| R06-5 | P2 | demo | starting documents are compiled-in; UiDocumentService owns the file and the LKG; reopen goes through the service | starting-document-survives-broken-files lane; verifier re-check |
| R06-6 | P2 | demo | new DemoItemKeys: injective, reversible (scope, kind) → key encoding (literal -, reserved _, everything else ~XXXX); full identity preserved | catalogue-identity-is-injective lane; IDENTITY line now False; verifier defect red |
| R06-7 | P2 | demo | reachable re-read points: refresh-catalogue command + entering the consumer tab; only new identities are registered; no per-frame rebuild | late-registration-is-visible lane; LATE now live=1 page=1; verifier defect red |

### The risks the reviewer raised (not must-fix)

- **Manual Reload from inside a Draw pass.** No production change. A probe widget drives Reload/ReloadAll
  from its own Draw and the record pins what actually happens (running pass finishes on the pre-revision
  snapshot; the commit is synchronous; the destructive seal lands mid-pass; the session stays coherent). The
  synchronous contract is kept and documented; moving the commit to the next BeginFrame would break the
  frozen synchronous return the demo button reads. Lane VerifyReloadInsideDrawPass; verifier re-derived
  (seal removal red 10). What stays unpinned: real IMGUI re-capture semantics, the pixels of the mixed pass,
  sibling-window ordering inside one real IMGUI event.
- **Multi-host pumping ≠ a unified global IMGUI version boundary.** Still an in-game acceptance item.
- **HostAttached handler closing its own window.** Real pre-fix shape measured: an NRE caught by the shell's
  guarded pass produced a PageUnavailable notice on a page the consumer deliberately closed. Guard: after
  the announcement the pass re-reads the host field and returns before DrawFrame when the close released
  it. Mutation-proven both directions (guard removed red 2 named; guard broadened red the refused-close
  control + 6).

## 3. A real defect this review caught a layer down

pack.ps1 originally staged the candidate into dist/ and validated it **afterwards**; the R06-1 planted
mutation (which runs pack.ps1 with <li>1.6</li> deleted) left a **failed** candidate — the mutated 183 B
manifest — in the delivered folder while the source was restored and only the probe was re-run. The fix is
tool-level, not a re-run:

- pack.ps1 stages into dist/.staging-*, validates the candidate, and swaps in only on success (on
  refusal the scratch is deleted and the previous artefact stays byte-identical); reachability is reported and
  re-checked against the **delivered** folder.
- verify-fixes.ps1 fingerprints the delivered folder before the pack mutation, requires it byte-identical
  after the refusal, and ends by re-packing from the restored tree asserting the staged manifest SHA-256
  equals the tracked one.
- --no-incremental everywhere a carrier/reference path is involved, because a path-resolved reference does
  not invalidate MSBuild's up-to-date check.
- The verifier attacked the tool level, not the tree: A2 (mutated tracked manifest) → pack refused, delivered
  folder unchanged, no scratch left; A1 (drifted delivered folder) → pack repairs it byte-identically. Stated
  boundary: drift is repaired by a pack, not detected by one.

## 4. Verifier's independent checks

- Reviewer probe, run as-is: **all seven signatures gone** (LIB-REOPEN after=new, FILTER: no exception,
  ADD alpha=9 beta=9, REOPEN: no exception, LATE live=1 page=1, IDENTITY False,
  COLLISION: no exception, exit 0).
- The demo's attach-adopts-newest-content-without-pump lane is **not vacuous**: pointed at a pre-fix carrier
  rebuilt from a6e8885 it fails with after=old; the fixed carrier passes with after=new. The lane
  asserts the stale pre-state (before=old, signal pending).
- FL fixes: own defects planted for every fix; the false-PASS shape the implementer warned about (old gate
  revert) reddens 8 assertions including the reviewer's exact no-pump assertion.
- Gates at the verified tip: 1912 ok / 0 FAIL, 36 lanes, none skipped; ten gates OK; privacy CLEAN
  (vectors 1+2).

## 5. Honest status (unchanged classes)

- **Nothing here is 已实机验证.** All behaviour was proven against the packaged DLL with the harness's game
  stubs or the reviewer's probe, and against the staged folder layout — not a real game load. The in-game
  checklist (t5b-3) remains 尚待实机.
- The demo mod is still **not** a second real consumer, and does not lift the API freeze.
- The pre-fix carrier used for the non-vacuity check is a rebuild from the pre-fix commit, not the overwritten
  archived package; the flip is a statement about the pre-fix code.

## 6. Where the fixes live

- FL: 0.6.x from a6e8885 (e290eff tip): eff4f57 (risk-3 guard), a48e808 (R06-2),
  9c7ce56 (risk-1 lane + docs). Package re-staged from 7b62416: label commit=7b62416fa623,
  DLL SHA-256 2A7F9C9E9B1D060B32037A82E2989817A19B8317D2FB8B48A68F15B13DDECC0D.
- Demo: ferritelib_uikit_demo at 587bf160 (branch master, local only): f19ca9c/amended
  587bf16. Delivered package: 8 files, LoadFolders.xml 200 B
  3FD398611C5E6013B590BB1E13A5FB328F88AE9A1C6608F6AE076DD0D63D5C49, DLL 49664 B
  53CBF59E3BDD8602CC0E43E45A3BF01412119BEB64339B3F2FAB6FB2EC4685E7.
- Records: r06-1-independent-reproduction.md, t5b-4-...md, r06-2-final-verification.md,
  and the per-package lane records under verification/.

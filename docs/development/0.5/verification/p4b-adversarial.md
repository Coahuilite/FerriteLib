# P4b adversarial verification — merged tip `6e0781a`

Verifier `verifier` (task-4). Date 2026-09-15. Target: `merge(0.5): P4b watcher re-arm, atomic style
batches and gated theme baseline` (`079aa5e`; parents `fb2e5d7` + the P1 tip). Read from a clean
`git archive` extraction in a temp directory; the shared checkout was never mutated.

```
Source/FerriteLib.UiKit/Kernel/UiDocumentService.cs   | 210 ++--   (seam, style pre-check, rollback, re-arm)
Source/FerriteLib.UiKit/Kernel/UiHost.cs              | 185 ++--   (TryPrepareStyleCandidate, rollback, gated baseline)
tools/.../KernelDocumentReloadTests.cs                | 203 ++     (81 -> 103 checks)
docs/development/0.5/30-consumer-handoff.md           |   3 +
docs/development/0.5/40-verification.md               |   2 +-
```

## 1. Baseline

```
pwsh -NoProfile -File scripts/verify-local.ps1                       # exit 0, [setup] + 9 gates OK
dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release
```

`exit=0 ok=1072 fail=0 ALL PASS`. P4b adds 22 executed checks over the P1 tip (1072 − 1050), i.e. the
document lane is 81 → 103 as claimed. The run after every mutation restored to the same `1072/0`.

## 2. Item 1 — the four claimed mutations, reproduced

Runner note for honesty: my first two attempts were invalid — restoring several snapshots of one file in
forward order rewrites an intermediate (mutated) state, which leaked mutations into later runs. The numbers
below come from a fresh extraction with a reverse-order restore, a `BASELINE` assert before and a
`FINAL-RESTORED` assert after (`exit=0 ok=1072 fail=0` both).

| author claim | exact planted change | measured | verdict |
| --- | --- | --- | --- |
| old AutoWatch no-op → 3 FAIL | re-insert `if (autoWatch == value) return;` in the setter; comment the Pump `AttemptReArm()` and the Reload `RefreshWatchers()` calls | `exit=1 ok=1069 fail=3` | **CONFIRMED** |
| style pre-check branch removed → 7 FAIL | delete the whole `else` arm of the validation loop (planted prepare fault included) | `exit=1 ok=1065 fail=7` | **CONFIRMED** |
| rollback loop removed → 1 FAIL | comment the `for (int i = applied.Count - 1; …)` `RestoreDocumentRollback` loop | `exit=1 ok=1071 fail=1` | **CONFIRMED** |
| unconditional baseline restore → 1 FAIL | delete the `if (applied.DefaultScheme == null && applied.DefaultDensity == null) return;` gate | `exit=1 ok=1071 fail=1` | **CONFIRMED** |

Raw signatures:

```
### P4b-1 exit=1 ok=1069 fail=3
    FAIL: setting AutoWatch to its current value re-arms once the directory exists
    FAIL: the frame-boundary pump re-attempts the arming without the caller touching AutoWatch
    FAIL: the manual reload path re-attempts the arming as well
### P4b-2 exit=1 ok=1065 fail=7
    FAIL: a style batch is refused when one affected host fails its pre-check
    FAIL: and the refusal names the host that failed the pre-check
    FAIL: a pre-check failure leaves both hosts of the style batch on the old document
    FAIL: a style batch whose later commit fails is refused rather than half-applied
    FAIL: and the refusal says the batch was rolled back
    FAIL: the already-committed host is rolled back to the old document
    FAIL: once no host fails, the same style batch commits to both
### P4b-3 exit=1 ok=1071 fail=1
    FAIL: the already-committed host is rolled back to the old document
### P4b-4 exit=1 ok=1071 fail=1
    FAIL: a consumer re-tint survives a reload whose document applies no page level
```

(I additionally reproduced the *broad* re-arm break — disabling the setter's `RefreshWatchers` outright —
which gives 4 FAIL because it also breaks the ordinary "turning AutoWatch on arms a watcher" check. The
narrow change above is the one that matches the author's 3.)

## 3. Item 2 — the `DocumentFaultOverride` seam, judged

**Shape: correct.** `UiDocumentService.DocumentFaultOverride` is
`internal Func<UiHost, string, string>?` on the instance (UiDocumentService.cs:80), never static, never
public, with no initializer, so it is `null` in production; the phase strings are `internal const`
(:82,:84). It is reachable by the harness through the pre-existing
`[assembly: InternalsVisibleTo("FerriteLib.UiKit.Tests")]` (Properties/AssemblyInfo.cs:3). The lane sets it
for exactly one call and nulls it immediately after (`KernelDocumentReloadTests.cs:678-684, 693-699`), so
there is no cross-test leakage. No public type, so no tier entry is owed for it.

**Can a lane pass because the seam bypasses the real path? Split verdict:**

- *Commit phase — no vacuity.* The seam is consulted inside the existing commit loop, before each host's
  real commit; when it fires on the second host, the first host has genuinely committed and the rollback
  loop genuinely runs `CaptureDocumentRollback`/`RestoreDocumentRollback` and restores its manifest,
  document, resolver and theme. The lane asserts object identity of the restored document, not the report
  text alone. Removing the rollback loop reddens exactly that assertion (P4b-3). The failure is synthetic;
  the code under test is real.
- *Prepare phase — VACUOUS for the style pre-check itself.* For a style dependency the loop calls
  `PlantedPrepareFailure` first (:496) and only reaches
  `dependency.Host.TryPrepareStyleCandidate(style, …)` (:506) when the seam stays silent. The lane's only
  style pre-check failure is planted, so the real pre-check is never exercised. I proved it: replacing the
  whole body of `UiHost.TryPrepareStyleCandidate` with `return true;` leaves the harness **completely
  green** —

  ```
  ### VACUITY real TryPrepareStyleCandidate always true exit=0 ok=1072 fail=0
  ```

**How bad is that?** It is a real coverage gap, not a lie. A structurally valid style document cannot make
the real pre-check fail today: `UiStyleResolver`'s only `throw` sites are constructor null-argument
guards (`UiStyleResolver.cs`, four `throw` statements, all `ArgumentNullException`), and the candidate and
theme reaching it are non-null. So `TryPrepareStyleCandidate` is a defensive backstop for a future
resolver, and the author's lane comment says exactly that ("a valid style document has no per-host contract
to fail on"). The honest consequence is that the "whole batch validated first for style documents" claim is
**structurally implemented and structurally unreachable**, and the only evidence that the loop would refuse
is the seam. Recommendation (routing to the Lead): either drive `TryPrepareStyleCandidate` directly with a
candidate that can fail, or say in `40-verification.md` that the style pre-check is a backstop whose
refusal path is not exercised in production.

## 4. Item 3 — the residual baseline gate, probed

The gate restores only when the **outgoing** document applied a page-level scheme or density
(`UiHost.RestoreStyleBaseline`, :471-476). I built the specific probe: host on a style document that
declares a page scheme (`ice` → `Panel`), consumer re-tints `theme.Base` (a token the scheme never
declares), then reloads to a document with **no page level** (`<Styles Schema="1"></Styles>`).

```
### PROBE-RESIDUAL exit=1 ok=1073 fail=1
    FAIL: PROBE-RESIDUAL: a re-tint of a token the OUTGOING page level never declared survives
```

**What remains, plainly:** once any document has applied a page-level scheme or density, the next reload
that drops it calls `CopyThemeTokens(theme, styleBaseline)`, which resets **every** document-settable
token to the theme's construction-time values — not only the tokens the outgoing page level set. So a
consumer that re-tints *any* token after construction (here `Base`, never declared by the document) loses
that tint on that reload, even though the incoming document declares nothing. The code comment and the
handoff wording say "a token the outgoing page level **did** set is restored", which understates it.

**Classification: must-document** — correct the wording in `UiHost.RestoreStyleBaseline` and in
`30-consumer-handoff.md` §4 ("every document-settable token is reset whenever the outgoing document
applied any page level; re-apply after reload or declare the value in the document"). A precise fix would
require the resolver to record which tokens the page level touched; that is a design change, not a bug fix.

## 5. Item 4 — status of the previous P4/P1 findings

| previous finding | status at `6e0781a` | evidence |
| --- | --- | --- |
| P4 must-fix (c): watcher never arms when the directory appears later | **FIXED** | three entry points re-arm (setter / Pump / Reload); the P4b-1 narrow mutation reddens exactly those three |
| P4 must-fix (a): `UiSession.GetNodeByElementId` returns a pruned node after a reload removal | **NOT FIXED** | `UiSession.cs` is untouched by P4b and by the P1-fix commit; no `IsArranged` filter and the doc still promises null. PROBE-A: `exit=1 fail=1` (`a removed element node leaves the session table`) |
| P4 must-document (b): theme re-tint lost on reload | **PARTIALLY FIXED** — the no-page-level case is fixed (P4b-4's own check passes at baseline), the residual in §4 remains | PROBE-RESIDUAL |
| "whole batch validated first" for style documents with two hosts | **STRUCTURALLY FIXED, real refusal path unexercised** | P4b-2 red; VACUITY probe green |
| P1 must-document: `AllowMultipleInstances=false` eviction and `NormalSize` provisional | **DOCUMENTED** | `30-consumer-handoff.md` §4 gained both sentences |
| P1/P4 vocabulary regression (`40-verification.md:73`) | **FIXED** | A6 now says 面板 A; committed-tree scan for the consumer terms is clean |

## 6. Item 5 — shared-file invariants at `6e0781a`

| invariant | method | verdict |
| --- | --- | --- |
| public types vs tiers | parse `docs/api-tiers.md` vs every committed `public` type declaration under `Source/FerriteLib.UiKit` | **67 = 67** (12 stable / 42 public-unstable / 13 internalize-candidate), zero diff both directions; P4b adds no public type |
| lane registration | committed `Program.cs` `X.RunAll()` vs classes declaring `RunAll` in `*Tests.cs` | **25 = 25**, zero unregistered, zero phantom |
| version axes | `Api` / `<VersionPrefix>` / `<modVersion>` | **0.5.0 / 0.5.0 / 0.5.0** |
| neutrality | gate 1 neutrality lane (green) + committed-tree grep for the second consumer's terms | clean (the earlier regression is fixed) |
| privacy | `pwsh -NoProfile -File scripts/privacy-audit.ps1` (vectors 1-2; identity by email) | **PRIVACY AUDIT CLEAN**, exit 0 |

## 7. Not verified here

- in-game behaviour (A6/A7/A8 remain pending);
- the real filesystem watcher under an editor's multi-write storm;
- a style document that could actually fail the real pre-check (none is known to exist);
- consumer compilation against the new surface.

## 8. Branch note

`feat/0.5-verify` HEAD after this report; the file is the only change. P4b's owner is inactive; every
must-document/must-fix above is routed to the Lead.

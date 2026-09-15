# Post-fix addendum — R1-R7 lanes, adversarial probes, advisories (`0d1f38b`)

Verifier `verifier` (task-4), continuing `post-fix-verification.md`. Extraction
`%TEMP%\fl-postfix-...\0d1f38b`; baseline `exit=0 ok=1515 fail=0 ALL PASS`; every mutation below was
restored byte-for-byte and the restored run was `exit=0 ok=1515 ALL PASS`.

## 1. My own measurements

| item | planted OLD defect (exact) | measured | raw FAIL lines |
| --- | --- | --- | --- |
| R1 (seal half) | `UiDocumentService`: `applied[i].Host.SealDocumentCommit();` → commented | `exit=1 ok=1509 fail=6` | `a kind change under a stable identity cleans the old element's state` · `and the slots the old kind used` · `a removed element's state is cleaned up` · `and its named slots with it` · `a reload releases the hot control instead of leaving it held` · `and still cleans its state` |
| R7 | `UiDocumentService`: append a **comment** `// UiLayoutManifest.ParseFile(path)` (no behavioural change at all) | `exit=1 ok=1514 fail=1` | `the document path reads one snapshot and parses it, with no second file read` |

**R7 judgement (the Lead asked for this specifically).** The lane is
`KernelDocumentReloadTests.VerifySourceWiring` → `HasSingleReadPipeline(text)`, a raw source-text scan:
it requires `File.ReadAllBytes(`, `UiLayoutManifest.Parse(` and `UiStyleDocument.Parse(` to be present and
`ParseFile(` to be absent, with a planted control that only plants the *historical* double-read shape
(`ReadAllBytes` followed by `ParseFile`). It is therefore **not behavioural**, and my comment-only
mutation proves it can be driven red by text that changes nothing — and, symmetrically, it would stay
**green** if a second read were reintroduced in a shape the scan does not name (e.g. `File.ReadAllText` +
`Parse`). It catches the known regression form and nothing else; it cannot evidence that the TOCTOU race
is gone. That matches the owner's own framing (structural guard + source-wiring lane), but the record
should say "the historical double-read shape is guarded", not "the race is fixed".

## 2. Lane identification for R2-R6 (from the committed tree, not yet planted by me)

| fix | lane (0.5.x) | assertion that names it |
| --- | --- | --- |
| R2 | `KernelDocumentReloadTests.VerifyTransientlyMissingFileKeepsLastKnownGood` | "a transiently missing file is a failure, not a silent fallback"; "the file returning with the same bytes is already in force" |
| R3 | `KernelWindowCatalogTests` (R3 block, 14 assertions) | post-close survivor hand-off; refusal does not trigger it |
| R4 | `KernelDiagnosticsTests.VerifyRulersArePerHost` + `VerifySubscriptionDoesNotOwnTheLegacyRuler` | "Two hosts with distinct rulers each measure with their own ruler"; "Subscribing a host does not replace the legacy channel's ruler" |
| R5 | `KernelDocumentReloadTests.VerifyFirstStyleAttachValidatesLikeReload` | "First style attach runs the same validate-and-apply rule as a reload" |
| R6 | `KernelDocumentReloadTests.VerifyRebindingReleasesTheOldService` | "rebinding releases the dependency the first service held"; "re-attaching to the same service keeps exactly one dependency" |
| R1 (rollback half) | `KernelDocumentReloadTests.VerifyRolledBackBatchKeepsInteractionState` | "A rolled-back batch restores the old document AND the interaction state it held" |

**Named gap:** the old-defect plantings for R2-R6 (and the R1 rollback half) were delegated to a
separate verifier and had not returned when this addendum was committed. I have their lane names and the
author's recorded mutation counts (`40-verification.md` R1: 3 mutations, R2: 5, R3: 14 assertions + mutation,
R4: lane + mutation, R5: 2, R6: 4, R7: structural guard + mutation), but those counts are the **author's
record, not my measurement**, so I do not report them as verified.

## 3. Adversarial probes of the fixes (named gap)

The seven new-defect probes (R1 seal-vs-dispose, R2 recovery on return, R3 closing survivor, R4
unsubscribed legacy ruler, R5 first-attach with no style source, R6 same-service double attach, R7 size
bound + BOM) were delegated and had not returned when this addendum was committed. **They are unverified
here.** Nothing in this addendum should be read as covering them.

## 4. Advisories recorded, not dropped (verified at `0d1f38b`)

- pointer-space fix **done**: `UiWindowHost.cs:511-516` (`ToWindowSpace`) + `UiWindowCatalog.cs:392`;
  the vanilla stack-order overlap remains **in-game-only** — `UiWindowCatalog.cs:385-389`, `UiWindowHost.cs:517-522`, `40-verification.md:197-199`.
- two catalogs at once **still untested**: `40-verification.md:198`; the isolation claim is also `api-tiers.md:268`.
- diagnostics coverage gaps **recorded**: `40-verification.md:202` plus register rows `:13`, `:14`, `:29`.
- window double field defaults **fixed**: `Stubs/VerseStub/VerseStubs.cs:666-673` and `:682`; pinned by
  `KernelWindowCatalogTests.cs:349-358`, `:378`.
- `40-verification.md` has **no blank/placeholder evidence row**; the former P5 gap is filled at `:82`, and
  the formerly-blank register rows are classified (`待实机`/`待补`/`待文档`/`待关闭`/`修复中`).
- two doc-status nits (found by the independent sweep): `40-verification.md:197` still uses the pre-fix
  "spaces mixed" wording for a section that reviews `0117c01`; `:31` still marks item 19 `修复中` although
  its fix `b812dfc` is an ancestor of the tip. Both **must-document**.

## 5. Verdict

Delivery gate green; invariants green (74/74 tiers, 32/32 lanes, three axes, no markers, privacy clean);
R1's destructive half and R7's guard are planted and measured above. **No new defect found**, but two
items remain unverified by me at this commit — the R2-R6 old-defect plantings and the seven adversarial
probes — both named rather than omitted. R7's guard is a source-shape check and should be recorded that
way.

## 6. R1-R6 plantings (delegated verifier, controlled disposable tree)

Raw counts and FAIL lines from the delegated sweep, baseline 1515 ok / 0 fail, every mutation byte-restored
and re-verified with a forced `--no-incremental` rebuild.

| R | lane | planted old defect | raw | FAIL lines |
| --- | --- | --- | --- | --- |
| R1 (rollback half) | `VerifyRolledBackBatchKeepsInteractionState` | `SealDocumentCommit();` called inside `UiHost.CommitLayoutCandidate` (destructive half at stage time) | `1512 ok / 3 fail` | "and its uncommitted draft survived the rollback" · "and its named state slots" · "and its scroll position" |
| R2 | `VerifyTransientlyMissingFileKeepsLastKnownGood` | deleted the `GoodVersion.Length > 0 -> Candidate.Refused("missing:...")` block | `1510 / 5` | "a transiently missing file is a failure, not a silent fallback" · "the report says the file is gone" · "the last valid external version is kept" · "the same missing version is reported once" · "the file returning with the same bytes is already in force" |
| R3 | `VerifyClosingAWindowHandsOverTheActiveTarget` | reverted the condition to `ReferenceEquals(active, window)` | `1512 / 3` | "and it is the active target: a close never leaves windows open with no target" · "the survivor itself reports it as the target" · "and the target hands over to the survivor" |
| R4 | `VerifyRulersArePerHost` + `VerifySubscriptionDoesNotOwnTheLegacyRuler` | 3-file revert of the host-ruler routing | `1510 / 5` | "subscribing B with a different ruler did not change A verdict (got 1)" · "interleaved A/B draws leave A at zero (got 1)" · "closing B left A measurement unchanged (got 1)" · "the unsubscribed host still measures with the legacy ruler (got 1)" · "and its finding is not written to the legacy channel" |
| R5 | `VerifyFirstStyleAttachValidatesLikeReload` | `TryPrepareStyleCandidate` if/else replaced by the old `CommitStyleCandidate` | `1513 / 2` | "the first application is reported when its page level cannot resolve" · "and the host keeps the document it already had" |
| R6 | `VerifyRebindingReleasesTheOldService` | `AttachDocumentService` back to the plain assignment with no previous `Detach` | `1511 / 4` | "rebinding releases the dependency the first service held" · "and leaves the old one clean" · "re-attaching to the same service keeps exactly one dependency" · "and closing it releases that one" |

**R7, second independent falsification (stronger than my comment-only one).** The delegated sweep planted a
*real* second read — `...Parse(Decode(File.ReadAllBytes(path)))` at both parse sites, with size and version
still taken from the first `bytes` — and the lane stayed **GREEN** (`1515 ok / 0 fail ALL PASS`). So the
guard is lexical in both directions: a comment can redden it (measured above) and a genuine TOCTOU
reintroduction can pass it. No behavioural lane pins R7 either: there is no `MaxDocumentBytes`/oversized
test and no mid-attempt file mutation anywhere under `tools/`. The correct record is **"the historical
`ParseFile` double-read shape is guarded; single-read/TOCTOU semantics are not behaviourally pinned"** —
which is exactly the owner's "not reproducible, structural guard" caveat, now measured.

R1 is therefore fully covered (my seal-half planting plus this rollback-half planting), and R2-R6 each have a
measured red lane. The only remaining gap is section 3: the seven new-defect probes.


## 7. Follow-up: second independent R1-R7 sweep (corroboration)

A second, independently set-up disposable tree reproduced all seven reds with the same lanes: R1
`VerifyRolledBackBatchKeepsInteractionState` 3 FAIL (`SealDocumentCommit()` inserted before `RebuildTree()`);
R2 `VerifyTransientlyMissingFileKeepsLastKnownGood` 5 FAIL (`GoodVersion.Length > 0` inverted); R3
`VerifyClosingAWindowHandsOverTheActiveTarget` 3 FAIL (`closingWasActive` reduced to `ReferenceEquals(active, window)`);
R4 `VerifyRulersArePerHost` + `VerifySubscriptionDoesNotOwnTheLegacyRuler` 11 FAIL across the diagnostics lanes
(`textMetrics = UiDiagnosticHub.ActiveMetrics;` reverted to `metrics`); R5 `VerifyFirstStyleAttachValidatesLikeReload`
2 FAIL (attach pre-check bypassed); R6 `VerifyRebindingReleasesTheOldService` 4 FAIL (`previous?.Detach(this);`
dropped); R7 `VerifySourceWiring` 1 FAIL (`Parse(Decode(bytes))` -> `ParseFile(path)`). Every run: one unique
anchor, byte snapshot, SHA256-verified restore, then `dotnet build ... --no-incremental` exit 0 and a green
harness `ALL PASS`. Two independent sweeps now agree on the lane for every R.

**R7 verbatim (both sweeps).** `HasSingleReadPipeline(text)` is `File.ReadAllText(UiDocumentService.cs)` plus four
`IndexOf` clauses (three positive substrings, `ParseFile(` absent). It never executes the service, does not strip
comments/strings/dead code, and only ever looks at that one file. So the predicate is satisfiable by text that
executes nothing, and blind to a second read performed outside that file or in a shape that keeps the four
substrings. Together with the two falsifications already recorded (comment-only reddens it; a real second read
keeps it green), the honest record is: *the historical double-read shape is guarded; the TOCTOU fix is not
behaviourally pinned.*

**Process finding (not a tip defect).** One sweep's first extraction lived at a shared temp path another process
reused; `UiDocumentService.cs` there had been reverted to `ParseFile(path)` after extraction, giving a spurious
red. Re-extracting the same tip into a unique directory is green. Independent verifiers must use unique temp paths.

## 8. The seven new-defect probes

| probe | disposition | evidence / reason |
| --- | --- | --- |
| P1 R1: host disposed between stage and seal | **UNVERIFIED** | no lane reaches a disposed-host-between-phases shape; the delegated probe had not returned |
| P2 R2: recovery when the file returns | **EXERCISED-AND-PASSES** | lane R2 asserts "the file returning with the same bytes is already in force" and "and a changed file commits normally after recovery" |
| P3 R3: the survivor itself closing | **UNVERIFIED** | no lane closes the survivor during hand-off; the delegated probe had not returned |
| P4 R4: legacy ruler for an UNSUBSCRIBED host | **EXERCISED-AND-PASSES** | lane `VerifySubscriptionDoesNotOwnTheLegacyRuler` asserts "the legacy channel measures with the ruler it was handed"; the R4 mutation's FAIL list includes "the unsubscribed host still measures with the legacy ruler". **A stale comment**: the reviewer's report claims `UiWindowCatalog.cs:268` ... note: the two-catalogs claim lives in `api-tiers.md:268`. |
| P5 R5: first-attach refusal leaves no style source | **EXERCISED-AND-PASSES** | lane R5 asserts "and the host keeps the document it already had" - a refusal leaves the host with its existing source, not none |
| P6 R6: attaching the same service twice | **EXERCISED-AND-PASSES** | lane R6 asserts "re-attaching to the same service keeps exactly one dependency" and "and closing it releases that one" |
| P7 R7: size bound + BOM | **UNVERIFIED** | a search of `tools/` finds no test touching `MaxDocumentBytes`/oversized and none that feeds a BOM-prefixed file; the delegated probe had not returned |

P2/P4/P5/P6 are covered by the fix lanes themselves and are green at the tip; **P1, P3 and P7 have no lane and
remain named gaps** at this commit. Nothing here is asserted beyond what the cited lane text or the two sweeps
show.


## 9. The seven probes, run (follow-up) - one DEFECT found in R3

Scratch probe lanes in a unique extraction (3 temporary lane files + one scratch `Program.cs`, all restored
byte-exact by SHA256; `--no-incremental` build exit 0; post-restore `verify-local` exit 0, 9/9 gates).

| probe | verdict | raw evidence |
| --- | --- | --- |
| P1 R1: host disposed between stage and seal | EXERCISED-AND-PASSES | `disposedMidBatch=True outcome=no-throw accepted=True hostA.disposed=True hostB.added=True` |
| P2 R2: recovery when the file returns | EXERCISED-AND-PASSES | `missing.rejected=True keptV1=True back.accepted=True host.hasV2=True` (observation below) |
| P3 R3: the survivor itself closing / two closes in one frame | **DEFECT-FOUND** | `shapeA activeAfterClose=beta remainingAfterClose=1 activeAfterSecondClose=null remainingEnd=0` ; `shapeB activeAfterTwoCloses=null remaining=2` |
| P4 R4: legacy ruler for an UNSUBSCRIBED host | EXERCISED-AND-PASSES | `legacyControlCount=1 legacyUnsubscribedFirstDraw=0 legacyAfterSubscribe=0 subB.fit=1 unsubscribedRuleHeld=True` |
| P5 R5: first-attach refusal leaving no style source | EXERCISED-AND-PASSES | `attached=True reportsOnRefusal=1 keptOwnDocument=True depsAfterRefusal=1 recovered.accepted=True panelBlue=True` |
| P6 R6: attaching the same service twice | EXERCISED-AND-PASSES | `first=True secondSame=True badId=False depsAfterBad=1 deps=1 reloadOne.hosts=1` |
| P7 R7: size bound + BOM | EXERCISED-AND-PASSES | `atBoundLength=1048576 parsedAtBound=True overLength=1048577 over.rejected=True over.reason=the file is 1048577 bytes, above the 1048576-byte bound` |

`CANNOT-EXERCISE`: none.

### MUST-FIX: R3 pre-close target hand-off is a single slot

`UiWindowCatalog.NotifyPreClose` records the closing holder in one field
(`closingActiveWindow = ReferenceEquals(active, window) ? window : null`) that is unconditionally overwritten
by the next pre-close in the same frame. With two closes in one frame (alpha active, beta non-active, gamma
open): `PreClose(alpha)` records alpha, `PreClose(beta)` erases the record, `PostClose(alpha)` takes the false
branch and leaves **active=null while beta and gamma are still open** - exactly the invariant the R3 lane
states ("a close never leaves windows open with no target"). A second shape: `SelectSurvivor` can hand the
target to a survivor that has already been pre-closed but is still in `activationOrder`, even though the
PreClose contract says a window on its way out must receive no input or capture.

**Classification: must-fix-in-0.5** (or an explicitly recorded remaining item if the round closes first).
The fix surface is one concept: track the *set* of pre-closed windows (or skip windows with a pending close
in `SelectSurvivor`) instead of one slot. Reproduced deterministically through the catalog's own hooks; not a
scratch-harness limitation. The existing R3 lane passes because it only exercises the single-close shape.

### Observation (not counted a defect)

R2: after a successful recovery, a second disappearance is still refused but is **deduped against the first
missing version** (`state.LastFailedVersion` is keyed on `"missing:" + path` alone), so it never enters the
report ring. That is a reporting nuance worth a sentence in the R2 record, not a correctness failure.


## 10. Correction: second probe run and the R3 reachability disagreement

A second, independently built probe harness (scratch lane only, no Source edit; `Program.cs` restored
byte-identical; `--no-incremental` build 0/0; `verify-local` exit 0) ran the same seven probes and reached a
**different R3 verdict**. Its raw totals: 1588 ok / 5 FAIL, all five in the scratch lane.

| probe | second run |
| --- | --- |
| P1 R1 dispose between stage and seal | EXERCISED-AND-PASSES (15 ok / 0 fail) - `pendingPrune` non-null after stage and null after seal; a disposed mid-batch host leaks nothing; the captured hot control 4242 is released |
| P2 R2 recovery | EXERCISED-AND-PASSES; 4 FAILs are the stricter expectation: `LastFailedVersion` is never reset on accept/skip, so a re-failure of a seen version is `Duplicate=True` and not appended to `Reports`/`LastReport` (still returned by `Reload` and still published) - matches the documented "once per refusing version" |
| P3 R3 | reachable close path PASSES (gamma -> beta -> alpha); the out-of-order hook interleave is **CANNOT-EXERCISE** |
| P4 R4 unsubscribed legacy ruler | EXERCISED-AND-PASSES (6/0), including a draw nested inside another host's live subscription |
| P5 R5 first-attach refusal | EXERCISED-AND-PASSES (10/0) - non-null style source kept, draws, fixed file applies |
| P6 R6 same-service double attach | EXERCISED-AND-PASSES (19/0) |
| P7 R7 size bound + BOM | EXERCISED-AND-PASSES (12/0) - UTF-8 and UTF-16LE BOMs decoded, exactly-bound accepted, 1048577-byte refused with the bound message |

### R3: reachability decides the classification

The first run drove two PreCloses in one frame through the catalog hooks directly and saw the single
`closingActiveWindow` slot overwritten (`active=null` with windows still open). The second run checked whether
that sequence is reachable in production and argues it is not: `WindowStack.TryRemove` runs
`OnCloseRequest -> PreClose -> windows.Remove -> PostClose` adjacently, `List.Remove` uses reference equality,
and no user code (and no re-entrant path) runs between them, so a second `PreClose` cannot clobber the field
before the first `PostClose`. The catalog's own `CloseAll` likewise completes one `TryRemove` before starting
the next.

**Corrected classification: latent sharp edge, NOT a confirmed must-fix.** The failure requires an
out-of-order or re-entrant hook sequence that neither probe could reach through the production path; the
must-fix statement in section 9 is withdrawn as *confirmed*. What remains true and worth recording: the
pre-close record is a single slot, so any future path that pre-closes two windows before either post-closes
would strand the target; making it a set (or skipping pending-close windows in `SelectSurvivor`) is cheap
insurance. **Classify as must-document / optional hardening, not release-blocking.**

The R2 ledger observation is confirmed by both runs and classed by both as non-defect (documented "once per
refusing version"): a re-break after a successful recovery is refused and published but reports as
`Duplicate=True` and does not update `LastReport`. Worth one sentence in the R2 record.


## 11. Post-hardening verification at a0d2c89: both P3 shapes closed, both new assertions mutation-proven

The round hardened the R3 edge at `2a0379a` ("fix(kernel): keep the active target across interleaved
closes", merged as `a0d2c89`) after the section 10 correction: the single slot became
`HashSet<UiWindowHost> closingWindows` plus the subset `activeClosingWindows` that held the target when it
was pre-closed, and `SelectSurvivor` now walks `activationOrder` from the front and skips any candidate
whose close is pending.

Verified at tip `a0d2c89` in a clean extraction (`git archive a0d2c89` into a fresh temp tree;
`dotnet restore` exit 0):

- `pwsh -NoProfile -File scripts/verify-local.ps1` -> `VERIFY_EXIT=0`, all nine gates OK.
- `dotnet build tools/FerriteLib.UiKit.Tests/FerriteLib.UiKit.Tests.csproj -c Release --no-incremental --no-restore`
  then `dotnet run --no-build --no-restore --project tools/FerriteLib.UiKit.Tests -c Release` -> exit 0,
  `1544` `ok:` lines, `0` `FAIL:`, `ALL PASS`.
- The exact P3 driver from sections 9-10 (direct `PreClose()` on two windows, then direct `PostClose()` on
  both, with no `WindowStack.TryRemove`) now passes as a scratch method appended to the temp copy of
  `KernelWindowCatalogTests.cs`: shape A raw `active=p3 remaining=1` (was `active=null remaining=1`),
  shape B raw `active=p3 gammaActive=False` (was `active=gamma`); harness exit 0, `ALL PASS`, scratch file
  restored byte-identical. [product behaviour: mutation-proven below; this direct driver: scratch-only, not
  committed.]

Planted-failure controls in the same extraction. The anchor count is asserted case-sensitively before each
edit; the product file hash `B5476F1C78A05D8F04EB830F58393C14537FD0471E27F90E0D696F21C84556E7` is
identical before and after both runs, the post-restore harness is `ALL PASS`, and the post-restore full
gate is again `VERIFY_EXIT=0`.

| mutation | anchor | result | reading |
| --- | --- | --- | --- |
| M1 `activeClosingWindows.Clear()` on every pre-close | `closingWindows.Add(window);` in `NotifyPreClose` | build 0, harness exit 1, 2 FAIL, both in the new lane's one-frame shape, no other lane red | the two-closes-in-one-frame assertions are mutation-proven |
| M2 `closingWindows.Clear()` before the hand-over decision | `bool closingWasActive = activeClosingWindows.Remove(window)` | build 0, harness exit 1, 1 FAIL, `pending close: the surviving OPEN window takes the target`, no other lane red | the pending-close-survivor assertion is mutation-proven |

That both mutations leave every pre-existing lane green is the measured half of the neutrality claim: on the
adjacent single-close path the set holds one window when `SelectSurvivor` runs, so the skip cannot exclude
anything the old code would have chosen.

**Correction to section 10's reachability wording.** "no user code (and no re-entrant path) runs between them"
is too strong. What is adjacent is the library's own sequence; the interval between `NotifyPreClose` and
`NotifyPostClose` sits inside `UiWindowHost.PreClose`/`PostClose`, which are public overrides of the
vanilla hooks this library documents itself as sitting on (`docs/api-tiers.md:151`, `:269`), and instances
come from the consumer's own `Func<UiWindowKey, UiWindowHost>` factory (`UiWindowCatalog.Register`,
`:115-119`). A consumer override that closes a sibling after `base.PreClose()` therefore is a re-entrant
path, and `Instances`/`TryGet`/`ActiveWindow` hand the instance out, so driving the hooks directly (what
both probes do) needs no special access. The accurate claim is "not reachable by library-only control flow",
not "not reachable at all" - and with the hardening merged the distinction no longer decides a release
question.

**Residual precondition (inspection only, NOT measured).** Both sets are cleaned only in `NotifyPostClose`
(`:350`, `:360`). A `PreClose` that never reaches its `PostClose` - an exception thrown out of the hook
after `NotifyPreClose`, or a caller driving the hooks directly - leaves a stale entry that
`SelectSurvivor` then skips (`:380-389`), so a later active close can hand the target to a different
survivor, or to none, while the stale window is still in the stack. Not a regression (the pre-hardening code
was equally wrong there, in the other direction) and it needs the same aborted-close precondition as before;
worth one sentence in code or in the round record, not a fix in this round.

Neither the fix nor the lane touches the public surface: `2a0379a` adds only private fields and changes
internal methods, so no `docs/api-tiers.md` entry was due, and the lane is invoked from the existing
`KernelWindowCatalogTests.RunAll()` (registered at `tools/FerriteLib.UiKit.Tests/Program.cs:68`), so the
lane-registration invariant is untouched.


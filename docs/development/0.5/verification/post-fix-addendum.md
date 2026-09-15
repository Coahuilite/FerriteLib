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

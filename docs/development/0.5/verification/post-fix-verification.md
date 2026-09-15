# Post-fix verification — tip `0d1f38b` (external review R1-R7 + task-18/19/20)

Verifier `verifier` (task-4). Date 2026-09-15. `0d1f38bbdecfb9901559281def33b628dcddba23`. Clean
`git archive` extraction in a temp directory; the shared checkout was never mutated.

## 1. Delivery gate

```
dotnet restore tools/FerriteLib.UiKit.Tests/FerriteLib.UiKit.Tests.csproj
pwsh -NoProfile -File scripts/verify-local.ps1     # exit 0, [setup] + 9 gates OK
dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release
```

**`exit=0 ok=1515 fail=0 ALL PASS`** (the P4c/P3/P5 delivery candidate was 1426; the R fixes and
task-18/19/20 add 89 checks).

## 2. External probe (the gitignored `dist/review-0117c01` copied into the extraction)

```
dotnet run --project dist/review-0117c01/ReviewProbe.csproj -c Release    # exit 0
WINDOW after close active=a remaining=1
METRICS A before B subscription=0
METRICS A after B subscription=0
MISSING before=external-valid
MISSING accepted=False after=external-valid
STYLE initial attached=True reports=1
STYLE whitespace-only reload rejected=True reason=host 'review': the page level names scheme 'undefined', which the document does not declare; the page level could never take effect
ROLLBACK injected-failure rejected=True restoredRoot=one draft='uncommitted-draft'
REATTACH after close firstDeps=0 secondDeps=0
```

Every expected line is present in the expected shape; **no old-shape line remains**. (The `STYLE
whitespace-only` line is an additional probe check, not one of the six required, and shows the page-level
refusal firing on the reload path.)

## 3. R1-R7 in-repo lanes (delegated sweep)

A dedicated verifier is planting each old defect and naming the lane that reddens; its raw output follows in
an addendum to this report. Nothing is asserted here that has not been measured — the only R-specific
observation I can make from the tip diff so far is structural: the fix surface is
`UiDocumentService` (R1 stage/seal, R2 fallback gate, R7 single read), `UiWindowCatalog`/`UiWindowHost`
(R3 target hand-off, R3 pointer space), `UiFitAudit`/`UiDiagnostics` (R4 host ruler), `UiHost` (R5 first
attach, R6 rebind), and the new checks live in `KernelDocumentReloadTests`, `KernelWindowCatalogTests`,
`KernelDiagnosticsTests`, `KernelRepeatTests`.

## 4. Invariants at `0d1f38b`

| check | result |
| --- | --- |
| tiers vs public types | **TIERS=74 DECLS=74 NOT_TIERED=0 STALE=0** |
| lane registrations | **registered=32 declared=32 unreg=0 phantom=0** (32 `.RunAll()` call-sites; still not 33) |
| three axes | `Api = 0.5.0`, `<VersionPrefix>0.5.0`, `<modVersion>0.5.0` |
| conflict-marker residue | **none** |
| privacy at the sha | no personal paths, no `<PublishedFileId>` value, no credential pattern |
| neutrality vocabulary | clean (no second-consumer term) |

## 5. Adversarial probes of the fixes and advisory records (delegated sweep)

A second verifier is exercising the seven new-defect questions (R1 seal-vs-dispose leak, R2 recovery, R3
closing survivor, R4 unsubscribed ruler, R5 first-attach with no style source, R6 double attach, R7 BOM and
size bound) and a third is checking that the review's advisories are recorded, not dropped. Their raw results
follow in the addendum.

## 6. Not verified here

- in-game behaviour for every fix (unchanged);
- the vanilla stack-order overlap on the pointer-space fix (in-game only, as recorded);
- two catalogs in one process (still untested, as recorded).

# P4c adversarial verification — merged tip `9078812`

Verifier `verifier` (task-4). Date 2026-09-15. Target: `merge(0.5): P4c non-vacuous style pre-check and
documented baseline residual`. Clean `git archive` extraction in a temp directory; the shared checkout was
never mutated; mutations restored byte-for-byte with `BASELINE`/`FINAL-RESTORED` both at
`exit=0 ok=1182 fail=0`.

## 1. Baseline

```
dotnet restore tools/FerriteLib.UiKit.Tests/FerriteLib.UiKit.Tests.csproj
pwsh -NoProfile -File scripts/verify-local.ps1          # exit 0, [setup] + 9 gates OK
dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release
```

`exit=0 ok=1182 fail=0 ALL PASS` (P4c adds 9 checks over the P2 tip's 1173).

## 2. Item 1 — the two vacuity mutations, reproduced

| mutation | measured | author | verdict |
| --- | --- | --- | --- |
| hollow `TryPrepareStyleCandidate` (page-level rules + apply replaced by `return true;`) | `exit=1 ok=1176 fail=6` | 6 | **CONFIRMED** |
| hollow `TryPrepareLayoutCandidate` (`ValidateManifest` call + catches replaced by `return true;`) | `exit=1 ok=1174 fail=8` | 8 | **CONFIRMED** |

```
### P4c-1 hollow TryPrepareStyleCandidate exit=1 ok=1176 fail=6
    FAIL: the real style pre-check refuses a page level naming a scheme the document does not declare
    FAIL: and the same for a page-level density
    FAIL: a style batch whose candidate fails the real pre-check is refused before anything commits
    FAIL: and the refusal names the host and the unresolvable page level
    FAIL: a real pre-check failure leaves both hosts of the style batch on the old document
    FAIL: the already-committed host is rolled back to the old document
### P4c-2 hollow TryPrepareLayoutCandidate exit=1 ok=1174 fail=8
    FAIL: host B's real pre-check refuses the candidate that names an unbound command
    FAIL: and it names the element and the binding that stopped it
    FAIL: the shared batch is refused because one of its hosts cannot draw the candidate
    FAIL: the refusal names the element the contract stopped at
    FAIL: and names the host that refused it
    FAIL: host A - which could have drawn it - keeps the old version
    FAIL: host B keeps the old version
    FAIL: manual reload commits the same batch to both hosts once every host accepts it
```

The first two style FAILs and the first two layout FAILs name the pre-check call directly
(`hostA.TryPrepareStyleCandidate(...)`, `hostB.TryPrepareLayoutCandidate(...)`); the rest are the batch
consequences of the pre-check no longer refusing. The prepare-phase seam is gone: the service's only seam
is `DocumentCommitFaultOverride`, and the validation branches now contain only the host calls
(`UiDocumentService.cs`: no `PlantedPrepareFailure`, no `PreparePhase`).

## 3. Item 2 — the first-load fallback, measured (fills §6)

Seven probe assertions, all green on the untouched tip (`exit=0 ok=1190`). The result separates the two
paths, and it is not what §6's parenthetical suggests:

**First load is not guarded by the P4c rule at all.**
- A structurally **valid** style file whose page level names an undeclared scheme registers with no refusal
  report (`service.LastReport == null`), and `Attach` **adopts** it (`host.StyleResolver.Document.DefaultScheme == "nope"`).
- Nothing of that page level takes effect (the theme keeps its construction values) — the fallback is the
  host's previous values, i.e. a **soft no-op**, exactly the behaviour the pre-check now refuses on reload.
- So on a first load the "unresolvable page level" fallback is: *the document is adopted, the page level
  silently does not apply*. It is not refused.

**A first load can still be refused, but only by `ReadCandidate` (structurally invalid / unreadable /
oversized / absent with no usable embedded text).** With no last-known-good:
- the host keeps its **construction-time style source** — `manifest.Styles` (the empty document when the
  page declares no `<Styles>` section) or the style document handed to the host constructor. Measured:
  `host.StyleResolver.Document.IsEmpty` after a refused first load.
- **Not** the embedded layout fallback: for a `UiDocumentKind.Style` source the embedded text must itself
  be a style document, and it is consulted only when the external file is **absent**. Measured: with an
  absent file and `PageSchemeStyle("ice", "#0000ff")` passed as the embedded fallback, the page level
  applies (`Panel` becomes `#0000ff`).

**Later reload.** The pre-check refuses the candidate and the last-known-good style survives — but the LKG
can itself be the first-load unresolvable version (measured: after the first bad version is adopted, a
second bad version is refused and the first bad version remains the LKG).

`40-verification.md` §6 should therefore read: *first load is not covered by the page-level rule (a
structurally valid candidate with an unresolvable page level is adopted and applies softly); a first-load
refusal can only come from the parse/read refusal, and its fallback is the host's construction-time style
source — the manifest's own `<Styles>` section (empty when absent) or the style document handed to the
constructor. The embedded fallback is a style fallback used only when the external file is absent; there is
no embedded layout fallback for a style document.*

## 4. Item 3 — judging the adjudication

**The ladder as actually written** (`MEMORY.md` lines 1000-1011, `TODO.md` §3 lines 376-378): bucket 1 is
element-contained and bucket 2 is page-fatal; "**appearance must never take the page down** ... stage 3 fails
closed on **structure**, because a mis-named element means the page means something else, and soft on
**appearance values**: an unknown `Tone` falls back to the default treatment"; "fail-soft must not mean
silent".

**Verdict: the deviation is defensible against that text.** P4c's refusal is **version-scoped, not
page-fatal**: the page keeps rendering on the LKG, so the ladder's one absolute ("appearance must never take
the page down") is not violated. What the ladder's "soft on appearance values" prescribes is a *fallback*,
and the page-level name has no fallback that is not a silent no-op; the two rejected candidates for
"fail-soft here" are (a) leave the page on its previous values and log, which is exactly the invisible
no-op an author cannot diagnose, or (b) apply the previous version's class while dropping the new name,
which is a third semantics nobody specified. So a version refusal is the stricter but coherent reading, and
the vacuity control needs it. **Record it as a third bucket — version-fatal with last-known-good — rather
than as "structure fail-closed"**, because the ladder's bucket 2 is page-fatal and this is not.

**But three sentences in the record are false or unsupported as written:**

1. **`30-consumer-handoff.md` §4 (and §6's first rationale sentence): "an unresolvable page-level default
   makes the whole style document be refused ... otherwise the version is refused, last-known-good stays in
   effect".** False for the first load, measured above: the typo'd document is adopted and there is no LKG
   to fall back to. Consumer-facing, so it needs the qualifier ("on a reload of an already-loaded
   document").
2. **§6: "首次加载没有 last-known-good 时的回退目标（内嵌布局 / 空样式文档）由独立验证实测登记".** The
   deferral is fine, but both candidate values are wrong for a style document: no embedded **layout**
   fallback is involved, and the first-load refusal fallback is the host's construction-time style source
   (the empty document only when the manifest declares no `<Styles>` section). §3 above has the exact text.
3. **§6's recovery path: "把页面级名字解析改回软丢弃，并改用已存在的结构级失败
   （`UiStyleDocument.StructurallyInvalid`）来驱动 style 预校验 lane 的非空证明".** Unsupported: a
   structurally invalid style document is refused inside `UiDocumentService.ReadCandidate`
   (`ReadExternal`/`ReadEmbedded` check `StructurallyInvalid`), so it never reaches
   `TryPrepareStyleCandidate`; the pre-check's only refusal rules are the page-level ones, and the
   resolver's apply does not throw for a valid (or structurally invalid) document. Reverting the page-level
   rule to soft therefore **reopens the vacuity hole**, and the recorded recovery is not actually
   available. The honest record is "not cheaply reversible; a real recovery needs a direct
   `TryPrepareStyleCandidate` lane with a document the resolver can reject, which does not exist today".

The cost sentence in §6 ("该文档里其余合法的 scheme/density 在这一版里也不生效") is accurate: the refusal is
per version, so nothing in that version applies; the previous good version (LKG) keeps applying.

## 5. Item 4 — invariants and earlier findings

| invariant | method | verdict |
| --- | --- | --- |
| public types vs tiers | `docs/api-tiers.md` vs committed `public` types | **68 = 68**, zero diff both ways |
| lane registration | committed `Program.cs` vs `RunAll` declarations | **28 = 28**, zero unregistered, zero phantom |
| version axes | `Api` / `<VersionPrefix>` / `<modVersion>` | **0.5.0 / 0.5.0 / 0.5.0** |
| neutrality | gate 1 lane + committed-tree term grep | clean |
| privacy | `scripts/privacy-audit.ps1` (vectors 1-2) | **PRIVACY AUDIT CLEAN**, exit 0 |

No regression in the wave-A findings: F1's removed symbol is still absent from `Source` (grep exit 1), F3's
`ParseFile` wording now reads "no production caller" in both `api-tiers.md` places and `MEMORY.md`, and the
F4 vocabulary is still clean. The P4b must-fix on `GetNodeByElementId` is closed by P2b's
`PruneNodesExcept` plus the corrected doc comment (P2 pass), and the P4b residual wording was adopted
verbatim in `UiHost.RestoreStyleBaseline`.

## 6. Not verified here

- in-game behaviour (A6/A7/A8 and the first-load scenario in a real session);
- whether a real consumer ever authors an unresolvable page-level default (the failure mode is authored
  error, not a consumer-report).

## 7. Branch note

`feat/0.5-verify` HEAD after this report; the file is the only change. P4c's owner is inactive, so the
three record corrections above are routed to the Lead.

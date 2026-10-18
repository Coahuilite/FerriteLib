# 0.7.x — verification record

This file distinguishes the last recorded acceptance from earlier regression evidence. It does not
require access to a maintainer-local PM workspace or imply that the current checkout was rebuilt.

## Recorded candidate acceptance (2026-10-09)

The source candidate was `d5af4d8209823bb19f7c112805fcb1b8caf56bbc`. The contemporaneous ledger records
1 setup plus 10 local gates green, Release 2940 passing assertions and Dev 3208, with zero failures.
Those are that run's counts, not current test inventory. Its Dev rehearsal was named
`interaction-20261009-r1`; a later documentation commit did not rebuild it.

The maintainer accepted a bounded game pass covering:

- popup option selection where the menu covers its own trigger;
- a long menu reaching its first/last rows, with the wheel moving the menu rather than the page beneath;
- the open popup following its trigger through scrolling and closing when the owner leaves its effective clip;
- covered chart input writing nothing while an ordinary drag still captures, moves and releases;
- typed menu behavior alongside the string path;
- a deferred numeric draft committing after a click on a control drawn earlier, Enter committing without
  closing, and Escape dropping the draft before subsequent cancel layers;
- use of the library shell through the real game window stack.

This is recorded human acceptance of those scenarios and candidate, not a new run performed for this
2026-10-18 document cleanup. A formal window-report artifact was not captured. Independent close-sound,
full multilingual fit, every theme state, multiple-mod coexistence and arbitrary multi-window key attribution
are not established by this pass.

## Remaining evidence boundaries

- The harness window double does not faithfully model all native window/content coordinate transforms and
  key attribution. Origin-only stub results cannot settle non-zero-origin multi-window behavior.
- The R12 appearance and R3-A instrument surfaces ran in the later integrated harness. Their separate
  faithful-revert batches were recorded as tracked but unrun; the R3-A batch also had an anchor mismatch.
- Native IME composition, basic multiline editing and readonly-selection correctness are not established
  by the 0.7 pass. They are scoped in the [0.8 design](../0.8/README.md), not claimed here as implemented.
- Actual recovery-band behavior, absent/duplicate-carrier failure presentation, and broad real-font fit
  require their own relevant observations if those capabilities are being accepted. The old checklist is
  a reusable procedure, not an instruction to rerun the accepted candidate.
- Stub guards, mutation sensitivity, compilation, package identity and in-game behavior are distinct kinds
  of evidence. The source and batch names below make historical claims traceable without relying on raw logs.

## Earlier regression provenance (2026-09-18–23)

Each A lane was re-run against the reverted fix (`git stash` of the production file only, lane files kept)
and observed red on the baseline, then green with the fix. That half is mutation proof. The original C
default lane was written red-first against `new()` carrying the 0.6 palette (12 failing token assertions)
while the DarkGold literal lane stayed green — that pairing proved the split was real. **Amendment
2026-09-18:** `new UiTheme()` is an unpainted bag; `Vanilla` and `DarkGold` are named peers. At that stage the C lanes
pinned each factory's literals plus "the constructor selects neither palette". R2 later superseded that contract.

| Item | Baseline evidence (red assertions) | After fix | Commit |
| --- | --- | --- | --- |
| A1 Row Auto fallback | 5 of the lane's assertions red on `12d0dbb` (flex-share, empty-label, fixed-sibling split, MaxWidth, mixed row) | green | `21a81cd` |
| A2 Height validation | 7 red (malformed widget/container, NaN, ±Infinity, nested message, invalid reload candidate) | green | `e30af94` |
| A3 Wrap-only Cols | 4 red (Row, Column, Section-with-breakpoint, message) | green | `e30af94` |
| A4 dropdown precedence | required case red (`Option1=b/Value2=b` displayed `b`, not `Second`) | green | `c8fe06e` |
| A5 options diagnostic | category sentence red (baseline said only "missing") | green | `88f1c77` |
| C Vanilla / DarkGold peers | constructor-as-default framing superseded; bag must not equal either palette | green after amendment | original record did not name the SHA |
| B1 `SelectedKey` item scope | lane written and run red **first** on the pre-fix tree (3 active rows of 3 — the page-level decoy answered for every row); red again under the faithful revert of the table entry (exit 1, the three assertions named in the output) | green, `ALL PASS` 2600 ok | original record did not name the SHA |
| task-9 `Height="MatchContent"` | lane written and run red **first** (the value refused at creation, `invalid Height 'MatchContent'`); red again under the faithful revert of the engine half — the reference removed while the value stayed accepted: `the hit column takes the text column's measured height (50 vs 104)` | green, `ALL PASS` 2623 ok | original record did not name the SHA |
| task-9 unknown scope name attribution | lane written and run red **first** on the pre-fix tree (`attributed to the element that declared it, not to the document: 'style-scope#styles'`); red again under the faithful revert of the attribution half (exactly that assertion and nothing else) | green, `ALL PASS` 2623 ok | original record did not name the SHA |
| dev geometry instrument (task-12) | new surface, so the red evidence is a **mutation of the geometry producer**: gate 10 (Dev harness) reported `the band's recorded height is the sibling's measured height: 32 vs 30` with `ResolveHeight`'s `MatchContent` branch `+2f`, exit 1; and removing the tests project's `FER_DEV` constant reddened the same gate on the release-branch assertions instead - the "the lane was compiled out" defect | green: gate 10 `dev half ran: 7 dev-only assertion(s), 2660 'ok:', 6 dumped node line(s)`, exit 0; `verify-local` exit 0 | original record did not name the SHA |
| task-10 `section/header` divider switch | lane written and run red **first** (the attribute did not exist: `Unknown attribute 'Chrome' on section/header`); red again under the faithful revert of the widget half — the value accepted, the paint no longer suppressed: `Chrome=none paints nothing in the element's rect (5 surface call(s))` | green, `ALL PASS` 2633 ok | original record did not name the SHA |

B's eight lanes are a new reference over existing surface: they cannot be red on a "before" (nothing in B
changed production code); their weight is compile-check plus behavior pins — geometry-free authoring, the
shared-binding round trip through the real commit seams, paint/measure/structure invalidation classes,
show/hide without reopen, narrow stack + scroll viewport, and the close/reopen subscription arithmetic
through `UiWindowCatalog`. The A4 lane also exposed (and the same commit fixes) a null-`ValueN` crash in
the static-option path: baseline drew a recovery band instead of the option list.

The rows in this historical table are stub-harness evidence. The later game observations are listed separately above.

## Build and instrument checks

Gate 1 runs the Release harness; gate 10 runs the Dev half and requires the named instrumentation assertions,
not merely a successful build. The original defect was a harness configuration that never defined `FER_DEV`:
its supposed Dev run took Release assertions. The configuration and gate were corrected.

The geometry instrument was checked with a changed `MatchContent` producer and with its Dev compilation
flag removed; the named assertions failed and passed after restoration. Export fixtures separately cover
first delivery, replacement, stale-PDB removal and refusal of Dev bytes. These are scoped automated results,
not evidence about the game's font or native window stack.

Current command and input-selection rules live in [build-and-debug.md](../../build-and-debug.md). Build
outputs are configuration-isolated; verification and packers do not update the root compatibility carrier.
Historical package hashes and moving HEAD comparisons have been removed from this active summary: select
the candidate's own `version.txt` and retained delivery record when artifact identity is the question.

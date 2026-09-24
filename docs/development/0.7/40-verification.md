# 0.7.x — verification record

Commands are the repository's own (plan §D); no new framework. Harness:
`dotnet run --project tools/FerriteLib.UiKit.Tests -c Release` (full run each step; no filter CLI exists and
none was invented). Gates: `pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev`.

## Regression provenance (what is mutation-proven vs pinned-forward)

Each A lane was re-run against the reverted fix (`git stash` of the production file only, lane files kept)
and observed red on the baseline, then green with the fix. That half is mutation proof. The original C
default lane was written red-first against `new()` carrying the 0.6 palette (12 failing token assertions)
while the DarkGold literal lane stayed green — that pairing proved the split was real. **Amendment
2026-09-18:** `new UiTheme()` is an unpainted bag; `Vanilla` and `DarkGold` are named peers. The C lanes
now pin each factory's literals plus "the constructor selects neither palette".

| Item | Baseline evidence (red assertions) | After fix | Commit |
| --- | --- | --- | --- |
| A1 Row Auto fallback | 5 of the lane's assertions red on `12d0dbb` (flex-share, empty-label, fixed-sibling split, MaxWidth, mixed row) | green | `21a81cd` |
| A2 Height validation | 7 red (malformed widget/container, NaN, ±Infinity, nested message, invalid reload candidate) | green | `e30af94` |
| A3 Wrap-only Cols | 4 red (Row, Column, Section-with-breakpoint, message) | green | `e30af94` |
| A4 dropdown precedence | required case red (`Option1=b/Value2=b` displayed `b`, not `Second`) | green | `c8fe06e` |
| A5 options diagnostic | category sentence red (baseline said only "missing") | green | `88f1c77` |
| C Vanilla / DarkGold peers | constructor-as-default framing superseded; bag must not equal either palette | green after amendment | this commit |
| B1 `SelectedKey` item scope | lane written and run red **first** on the pre-fix tree (3 active rows of 3 — the page-level decoy answered for every row); red again under the faithful revert of the table entry (exit 1, the three assertions named in the output) | green, `ALL PASS` 2600 ok | this commit |
| task-9 `Height="MatchContent"` | lane written and run red **first** (the value refused at creation, `invalid Height 'MatchContent'`); red again under the faithful revert of the engine half — the reference removed while the value stayed accepted: `the hit column takes the text column's measured height (50 vs 104)` | green, `ALL PASS` 2623 ok | this commit |
| task-9 unknown scope name attribution | lane written and run red **first** on the pre-fix tree (`attributed to the element that declared it, not to the document: 'style-scope#styles'`); red again under the faithful revert of the attribution half (exactly that assertion and nothing else) | green, `ALL PASS` 2623 ok | this commit |
| dev geometry instrument (task-12) | new surface, so the red evidence is a **mutation of the geometry producer**: gate 10 (Dev harness) reported `the band's recorded height is the sibling's measured height: 32 vs 30` with `ResolveHeight`'s `MatchContent` branch `+2f`, exit 1; and removing the tests project's `FER_DEV` constant reddened the same gate on the release-branch assertions instead - the "the lane was compiled out" defect | green: gate 10 `dev half ran: 7 dev-only assertion(s), 2660 'ok:', 6 dumped node line(s)`, exit 0; `verify-local` exit 0 | this commit |
| task-10 `section/header` divider switch | lane written and run red **first** (the attribute did not exist: `Unknown attribute 'Chrome' on section/header`); red again under the faithful revert of the widget half — the value accepted, the paint no longer suppressed: `Chrome=none paints nothing in the element's rect (5 surface call(s))` | green, `ALL PASS` 2633 ok | this commit |

B's eight lanes are a new reference over existing surface: they cannot be red on a "before" (nothing in B
changed production code); their weight is compile-check plus behavior pins — geometry-free authoring, the
shared-binding round trip through the real commit seams, paint/measure/structure invalidation classes,
show/hide without reopen, narrow stack + scroll viewport, and the close/reopen subscription arithmetic
through `UiWindowCatalog`. The A4 lane also exposed (and the same commit fixes) a null-`ValueN` crash in
the static-option path: baseline drew a recovery band instead of the option list.

Everything here remains stub-harness evidence. None of it is 已由真实消费者接入 or 已实机验证.

## Integrated gates and staged package (measured, not assumed)

`verify-local.ps1` after the task-10 code commit (2026-09-22): all nine gates OK. It was run **without**
`-PackDev` in this round, so nothing was staged: the dev folder below is the C-round artifact and is labelled
as such rather than passed off as the current one.

- Single-DLL/content-free payload, API tiers, visual-core separation, three-axis version agreement,
  neutrality, containment: all re-asserted by that gate run.
- A delivery ends with the carrier updated **Release** by the one script that writes that path: a Release
  build (`dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release`, which writes
  `dist/build/Release` and **not** the carrier) followed by `scripts/export-carrier.ps1` — Release-only (it reads
  the configuration in a child process and refuses anything else), copied through a temporary candidate and
  swapped in with `[IO.File]::Replace`, and it removes the stale `1.6/Assemblies/FerriteLib.UiKit.pdb` (Release
  sets `DebugType=none`, so a build neither rewrites nor deletes an existing PDB). Its identity is then read in a
  **child process**: `Assembly.LoadFile` in the session that is doing the measuring holds the payload open until
  that process exits.
  *(Mechanism corrected 2026-09-24: this bullet read "the carrier rebuilt Release (`dotnet build -c Release
  --no-incremental`), then the stale PDB removed", which described building into the shared path. That was true
  before builds became configuration-isolated in `07f3c40`; the sentence is corrected rather than deleted so the
  contradiction with `docs/build-and-debug.md` is not left standing.)*
- **A committed byte identity cannot be current, which is why the pair is not written down here.** The payload
  embeds the committed SHA, so a commit whose content records the hash moves HEAD and moves the stamp with it;
  a frozen carrier's record is the pair *plus* its build command and clean/dirty state, taken at the freeze.
  History, superseded: the C-round measurement was `dist/dev/FerriteLib/` (5 files), `version.txt`
  `FerriteLib 0.7.0-dev / build=dev / commit=88095fb3cbed`, payload SHA-256
  `271128299A9CFF2C4CB5EBDFBB9246C3F6BEC2791A5D0117B894E6F96FC8F82F`. **That block was already stale before
  this round** — 35 commits landed after it, 11 of them touching `Source/`, so the sentence that used to stand
  here ("later commits on this line are documentation-only, so the payload's stamp stays true") had been false
  for a long time.
  **The rule this leaves behind:** a carrier's current identity — SHA-256 + stamp + the build command and the
  clean/dirty state it was taken in — belongs to the round's FREEZE NOTICE and delivery report, never to a
  tracked file, which can only ever pin a *past* build (recording the current pair is itself a commit, and the
  stamp moves with HEAD).

## Which gate guards which half of the harness (2026-09-23)

Not every lane is compiled in every configuration, and a half that no gate runs is a half that rots. The
arrangement, so the next reader does not have to derive it:

| Gate | Configuration | What it holds |
| --- | --- | --- |
| 1 | **Release** | The whole harness, therefore every lane's release half - including the dev instrument's
`#else` branch (no instrument, opt-in refused, dump empty). |
| 10 | **Dev** | The same harness in the Dev configuration, therefore every lane's `#if FER_DEV` half. It is
not enough that the project compiled: `scripts/verify-dev-instrument.ps1` requires the dev-only assertion
names, floors on the assertion and dumped-node counts, the absence of the release-only name, and the numbers
the instrument printed, and it control-tests its own checker on every run. |

**Why gate 10 exists at all, with the measurement that forced it.** The harness project did not define
`FER_DEV`, so the `#if FER_DEV` branch in `KernelDocumentReloadTests` had **never been compiled by any run**
- and a `-c Dev` run was consequently **red**: the lane took its release branch while the library was built
Dev, and `a release build leaves automatic watching off by default` failed. The tests project now mirrors the
library's Dev gate, which is what makes a lane able to hold the dev half, and gate 10 is what keeps it from
quietly going back to compiling nowhere. **No gate writes the compatibility carrier** (corrected 2026-09-24:
this sentence called gate 10 "a third writer of the shared carrier" and had gate 2 writing Dev bytes into it; both
were true before builds became configuration-isolated in `07f3c40`). Gates 1, 2, 3 and 10 build into
`dist/build/<Configuration>` — the library csproj pins `OutputPath` there and gate 4 asserts the evaluated
`TargetPath` equals it — and the chain reads the root `1.6/Assemblies/` payload **read-only**: it snapshots the
payload's hash **and** mtime before the first gate and refuses to finish if either moved
(`scripts/verify-local.ps1`). `scripts/export-carrier.ps1` is the **only** writer of that path, and no gate,
packer or workflow calls it, so a delivery is a deliberate step that ends the current freeze and needs a new
FREEZE NOTICE.

## Pending external checks

The four numbered items in `README.md` §External acceptance. Consumer/game evidence gates none of the
established contract (maintainer ruling 2026-09-17), and its absence is reported here rather than
converted into a new gate.

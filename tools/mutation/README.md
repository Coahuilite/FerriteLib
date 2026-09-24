# tools/mutation - the mutation battery

Evidence generators for "this assertion really reddens". They live here, tracked, because a generator kept
under `dist/` is a generator that any `dist/` cleanup deletes: the criterion is that someone else can find
the command that produced a log, not only the log (M8). The logs themselves still go to `dist/dev-work/`.

| File | What it is |
| --- | --- |
| `Invoke-Mutation.ps1` | one mutation, one run, one self-describing log. Everything in M1-M10/K3 below lives here. |
| `mutation-check.ps1` | the two halves of the battery (this repository, and the consumer's) as a driver over the engine. |
| `commands/*.cmd` | the one-token commands the battery runs (a `[string[]]` cannot cross a `pwsh -File` boundary as an array). |
| `Invoke-AnchoredEdits.ps1` | two-phase anchored edits for *migration* scripts: simulate every operation, then write. |

## Running it

```powershell
pwsh -NoProfile -File tools/mutation/mutation-check.ps1 -ValidateOnly   # resolve every anchor, write nothing
pwsh -NoProfile -File tools/mutation/mutation-check.ps1 -Half Fl        # this repository
pwsh -NoProfile -File tools/mutation/mutation-check.ps1 -Half Us        # the consumer checkout (cross-repo)
pwsh -NoProfile -File tools/mutation/Invoke-Mutation.ps1 -SelfTest      # the generator's own fixtures
pwsh -NoProfile -File tools/mutation/Invoke-AnchoredEdits.ps1 -SelfTest # the two-phase engine's fixtures
pwsh -NoProfile -File tools/mutation/batches/t2-2026-09-24.ps1 -ValidateOnly  # a batch, before it writes
pwsh -NoProfile -File tools/mutation/batches/t2-2026-09-24.ps1              # ... and for real
```

A batch is a tracked file too (`batches/t2-2026-09-24.ps1`): the mutations one round's evidence rests on,
with the assertion each must redden. Re-run it after any engine change, because a changed generator
invalidates every log it produced - which is exactly what happened to this batch's first run (polluted
fingerprints, K2, and one log red for the wrong reason, kept under `dist/dev-work/superseded/` as an
incident record).

**Cross-repo boundary.** `-Half Fl` is the default because this repository must stand alone: a public
checkout has no sibling, and nothing under `scripts/` may require one. `-Half Us` needs the consumer
checkout next to this one and refuses with a clear message when it is absent. The consumer half WRITES
files in the consumer repository (it mutates one source file and restores it), so it is the consumer owner's
step to run or authorize - the same one-writer rule the rest of this workspace runs on.

**The trap, recorded because it already cost a round.** The consumer half selects its carrier explicitly
(`-p:FerriteLibArtifactPath=..\ferritelib\dist\build\Dev\FerriteLib.UiKit.dll`). The Fl half must NOT do
the same: it relies on the harness's `ProjectReference`, which puts the library in `dist/build/Dev` for a
Dev run. Adding a carrier path there points at the repository-root Release carrier instead, the Dev half of
the instrument does not exist, and the lane reddens for a reason that has nothing to do with the mutation.

## Two carriers, two names (and why the consumer half is cross-repo)

The engine distinguishes two files that used to be one name:

- the **driver carrier** is what the command LINKS. The consumer half's command selects it explicitly
  (`-p:FerriteLibArtifactPath=..\ferritelib\dist\build\Dev\FerriteLib.UiKit.dll`), because a Dev run is
  where the development instrument exists. The Fl half must NOT pass that flag: its harness reaches the
  library through `ProjectReference`, which puts the Dev build in `dist/build/Dev`. Pointing it at the root
  payload would hand it a Release carrier, the Dev half of the instrument would not exist, and the lane
  would redden for a reason unrelated to the mutation;
- the **watched carrier** (`-WatchCarrier`, M10) is what must come out untouched: this repository's frozen
  root payload `1.6/Assemblies/FerriteLib.UiKit.dll`, compared by hash AND mtime.

They are different files on purpose, which is why they have different names - an earlier version called the
watched one `Carrier` while the command linked something else, and that reads as if they were the same file.

**Structural risk.** The consumer half WRITES a source file in another repository (it mutates one file and
restores it byte-for-byte) and then builds that repository. A script in this repository performing a
cross-repo write is exactly the shape that caused an earlier incident, so:

- run `-Half Us` only with the consumer owner's authorization, and never in the same window as a consumer
  build;
- the long-term home for the consumer half is the consumer repository, with this file keeping only the
  shared engine and the Fl half. That is a recommendation, not a plan for this round.

## What the battery guarantees (M1-M10, as implemented here)

| # | Guarantee | Where |
| --- | --- | --- |
| M1 | anchor must occur **exactly once**; missing or ambiguous refuses the run (a bare `.Replace` is a silent no-op) | `Invoke-Mutation.ps1` `Resolve-MutationTarget` |
| M2 | the run must exit non-zero **and print the named assertion** - the check reads the command's own output, never the log's header | `Invoke-Mutation.ps1` `$runOutputPath` |
| M3 | one mutation per invocation | parameter shape |
| M4 | restore by bytes, then re-read and assert byte-identity | `Invoke-Mutation.ps1` `finally` |
| M5 | after the restore, **rebuild** the mutated configuration, assert exit 0, and assert the rebuilt artifact is not the poisoned one | `Invoke-Mutation.ps1` `-RebuildArgs` |
| M6 | the log binds itself: command, rebuild command, expected assertion, each mutated file's sha256+mtime (before/mutated/restored), artifact fingerprint (before/poisoned/rebuilt), HEAD, dirty set, exit codes | log header |
| M7 | every assertion needs at least one red record; the ones without one are listed below | this file |
| M8 | the generator is tracked (this directory); logs stay in `dist/` | - |
| M9 | migration scripts go two-phase through `Invoke-AnchoredEdits.ps1` | that script + `-SelfTest` |
| M10 | the run may not touch the repository-root carrier: hash+mtime identical before and after | `-Carrier` |
| K3 | the artifact fingerprint is **derived** from the mutated file; a caller naming an unrelated artifact is refused instead of obeyed | `Get-ArtifactForPath` |
| - | the reference the artifact must return to is established by a **baseline build** with the same rebuild command, from the clean source, immediately before the mutation | `Invoke-Mutation.ps1` baseline step |
| - | every log carries an **outcome label**: `intended-red`, `instrument-refused-invalid-setting`, or `wrong-reason-red` (the last one is checked in the opposite direction: the named assertion must be ABSENT), and any non-intended label must carry `# why this label:` | `-Outcome`, `-OutcomeWhy` |
| M11 | M6 applied to the instrument: the log pins the **generator's own identity** (`# generator: <path> sha256=…`, `# batch: <path> sha256=…`), and the dirty set is a path PLUS per-file sha256 - so "the engine that ran is the committed engine" is a hash, not an inference | log header |

## The generator's own three acceptance checks

`Invoke-Mutation.ps1 -SelfTest` runs fixtures in a TEMP tree, with no build, and each of these must exit
non-zero for the reason named:

1. **a missing anchor** - the run refuses before executing anything (M1);
2. **a restore that cannot write** (read-only target) - the run fails loudly instead of leaving the tree
   mutated (M4/M5);
3. **an artifact the mutation cannot change** - refused, because a fingerprint of an unrelated artifact is a
   number with no signal that reads exactly like a signal (K3).

Five more fixtures guard the other shapes: an exit-0 non-mutation, a red that never names the assertion, a
rebuild that fails, a rebuild that consumes nothing, and a run that changes the carrier. One positive
control proves the checker does not simply reject everything.

## M7: which assertions have a red record

| Assertion | Red record |
| --- | --- |
| `covered` verdict (`KernelDevGeometryTests.VerifyCoveredVerdict`) | recording removed; rect-blind override |
| "records no hit sample in the same pass" | yield removed |
| "does not dispatch its command while the layer is above it" | yield removed |
| "the option row consumed the click and closed the popup, not the trigger" | row consumption removed (`t2-b11-row-consumption-removed`) |
| "with no popup layer left the same press hits it and dispatches once" | yield removed (fired twice) |
| stub tree's closed folder/DLL sets | `OutputPath` rename, warm tree and both-files |
| visual-core boundary rejects `UiPopup` | `typeof(UiPopup)` planted in `UiThemeDraw.cs` |
| gate 6 series-licence pin | one byte appended to `LICENSE` |
| gate 6 section-10.4 truncation probe | heading mangled |
| gate 10: a named dev-only assertion missing | the assertion deleted from the lane |
| gate 10: measured count below the list | the name printed outside an `ok:` line |

The mechanism assertion ("the option row consumed the click") had no dedicated red in its first round: the
mutations that reach that frame reddened the covered verdict first. It has one now - the batch's last case
removes the row's `ClosePopup()` and the assertion reddens - which is the difference between a guard and a
guard with evidence.

## Retired: the one-shot migration scripts

`edit-build.ps1`, `finish-build.ps1` and `update-docs.ps1` used to sit beside these scripts under `dist/`.
They are **not** tracked here, and that is deliberate:

- they are one-shot migrations whose batch already landed (the configuration-isolated build migration,
  `07f3c40`), so re-running them today is meaningless;
- they carry machine-specific absolute paths, which tracked files in this repository may not contain;
- their shape is exactly what M9 forbids - a chain of bare `.Replace` calls, writing file by file, with no
  transaction - and their bodies cannot be re-run to prove a port of them faithful.

What replaces them: `Invoke-AnchoredEdits.ps1`, which any future migration script must use. It collects
operations, simulates every one against the on-disk text, and writes only if all of them validated;
`-ValidateOnly` stops after the simulation. A migration written on it cannot half-apply, and cannot report
success for a batch that changed nothing.
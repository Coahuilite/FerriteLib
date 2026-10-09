# T5c — final integrated verification of the frozen 0.6 tip and the demo

Owner: `verify` (shared task `task-33`). Bound to the exact SHA extracted and verified:
**`e246aa478954f8e869ab2c8c6647d6f7c7d5684a`** on `0.6.x`. Nothing here is 实机验证; no PASS is written that
was not reproduced in this session.

**Frozen tip reconciliation (item 6, first).** The frozen content tip is `f3595a1`. At verification time `0.6.x`
is `e246aa4`, and `git diff --name-status f3595a1 e246aa4` is exactly one entry:
`A  docs/development/0.6/50-dev-package.md` (63 insertions, no source or harness file). That is the single
documentation commit the Lead said would follow, so the dev package label `commit=f3595a1b28f1` is
**reconciled, not a mismatch** — the payload bytes were staged from the frozen content tip and no code changed
after it.

## 1. Extraction, gates, harness, lanes

`git archive e246aa4` into a unique temp directory of my own (path deliberately not recorded here);
`dotnet restore tools/FerriteLib.UiKit.Tests/FerriteLib.UiKit.Tests.csproj` -> exit 0.

Gates, raw:

```
[setup] restore the harness project graph (fresh-tree bootstrap) ... OK
[run] FerriteLib.UiKit harness (kernel + version + neutrality + boundary) ... OK
[run] library Dev build (warnings as errors) ... OK
[run] library Release build (warnings as errors) ... OK
[run] mod payload present at the path consumers bind to ... OK
[run] carries no game content (assemblies-only mod) ... OK
[run] LICENSE present and MPL-2.0 ... OK
[run] About.xml identity is present and well-formed ... OK
[run] runtime-resolvability traps: net472 surface + harness stub surface ... OK
[run] rule (c) consumer half is armed and tree-sensitive (dependency-reality) ... OK
[verify] all checks passed.
VERIFY_EXIT=0
```

Harness run directly (not only through the gate): `exit 0`, `1853` `ok:` lines, `0` `FAIL:`, `ALL PASS`.
Lane coverage: `36` lane files declare `RunAll`, `Program.cs` has `36` registrations, all `36` headers appear
in the stdout in order (`HEADERS_MISSING=0`), and splitting the stdout at those headers gives `36` blocks with
`0` empty, `0` failing, and a minimum of `3` `ok:` lines per block. No lane was skipped.

## 2. Privacy

`scripts/privacy-audit.ps1` run inside the worktree at `e246aa4` (a real git work tree):
`vector1 (working tree): scanned`, `vector2 (commit messages): scanned`,
`identity: account 19252128+Coahuilite - 2 stamped display name(s)`,
`vector3 (historical blobs): SKIPPED (pass -FullHistory for first-upload / pre-push checks)`,
`PRIVACY AUDIT CLEAN`, exit 0.

**Method note (not a finding):** the same script run against a non-git extraction reports
`identity: expected exactly one GitHub account, found 0` and exits 1 — an artefact of the missing repository.
Vector 3 (history) was therefore **not** run here.

## 3. T1b — the activation-path hole is closed and named (item 3, the one that mattered)

The lane exists: `KernelPageLifecycleTests.VerifyActivationDoesNotReannounce`
(`tools/FerriteLib.UiKit.Tests/KernelPageLifecycleTests.cs:395-465`), registered through
`KernelPageLifecycleTests.RunAll`. It opens a second instance through the catalog to move the target away,
then activates the first window twice — so `SetActiveTarget(true)` runs with a host already built and drawn,
which is exactly where I planted the T5a attack. At baseline its assertions appear in the harness output
(`opening and drawing announces the host exactly once` … `the announcement count is still exactly one after two
full activation cycles`).

**My own attack, re-planted at this tip** — in `UiWindowHost.SetActiveTarget(bool)`, immediately after
`activeTarget = active;`:

```
if (active && host != null) HostAttached?.Invoke(host);
```

One anchor, asserted unique; scratch extraction; Release rebuild; file SHA256-restored afterwards.

```
ORIG_HASH=18D9120C53691897B7B1C59721A4EDB9C2F8C9A787B9D6C0236245995010448C
ANCHOR_COUNT=1
BUILD_EXIT=0
HARNESS_EXIT=1
OK=1850 FAIL=3
FAIL> FAIL: ACTIVATION-NO-SECOND-ANNOUNCEMENT: reactivating a window that already owns a host announces nothing
FAIL> FAIL: drawing the reactivated window announces nothing more
FAIL> FAIL: the announcement count is still exactly one after two full activation cycles
TOTALS: 3 test(s) failed.
RESTORED_OK=True
RESTORE_HARNESS_EXIT=0 OK=1853 FAIL=0
VERIFY_EXIT=0   (ten gates, all OK)
```

**Verdict: mutation-proven and named.** The identical attack left the whole harness green (`1840 ok / 0 FAIL`)
on `84537fc` — `t5a-independent-verification.md` `3.1 — and now fails by name in the T1b lane. The lane's own
summary correctly calls itself a regression guard, since the product was already correct; what it adds is a
named failure for the shape that was silent before.

## 4. Skeleton markers and the tier classification

- `grep -rn "NotImplementedException" Source/` at `e246aa4`: **0 hits** (measured in the extraction). The T0
  markers are gone.
- `docs/api-tiers.md`: I read the `Public-unstable` section (lines `340-369`) and it carries entries for every
  new public type of this round: `IUiMainThread`, `VerseFerriteMainThread`, `UiNotifyAdapter`, `IUiTimeSource`,
  `VerseFerriteTimeSource`, `UiReloadPolicy`, `UiReloadSchedulerState`, `UiWidgetDescriptor`,
  `UiWidgetCatalog`. `ReferenceEqualityComparer` is `internal sealed`
  (`Source/FerriteLib.UiKit/Kernel/ReferenceEqualityComparer.cs:10`), so no tier entry is due for it.
  This is a **reading of the document**, not a second measurement: the classification claim (every exported
  payload type classified exactly once) remains `FerriteLibApiTierTests`' own, which is green in `1` with its
  planted-unclassified-type control. I did not re-derive the exported-type set myself.

## 5. Demo repository (read-only)

`ferritelib_uikit_demo`, branch `master`, tip `d47b2216ab4555f39a0e0bc86c10edd463d3e53f`, working tree clean.
I wrote nothing there.

- **Package contents — exactly eight files** under `dist/FerriteLibUiKitDemo/`:
  `LoadFolders.xml`, `About/About.xml`, `1.6/Assemblies/FerriteLibUiKitDemo.dll` (SHA-256
  `8F30685E68A8D05CFA0EAA5B10D7586DA104F788AE88243DDDAB05674886DEFA`, matches the Lead's label),
  `Languages/ChineseSimplified/Keyed/FerriteLibUiKitDemo.xml`, `Languages/English/Keyed/FerriteLibUiKitDemo.xml`,
  `Xml/Panel.xml`, `Xml/Settings.xml`, `Xml/Style.xml`.
- **No `FerriteLib.UiKit.dll`**: recursive search of `dist/` finds zero; no tracked file has that name either.
- **No drive-letter path in any tracked file**: `git ls-files` gives `23` tracked files; reading each one and
  matching `[A-Za-z]:\\` gives `0` hits. (The `FerriteLib.local.props.example` that is meant to carry an
  absolute path is an example file with a placeholder, and the real `FerriteLib.local.props` is gitignored —
  consistent with the carrier rule.)
- Extra observations consistent with the "no carrier copy" claim, read but not re-measured: the packaged
  `About.xml` is `coahuilite.ferritelibuikit.demo` with `<modDependencies>` on `coahuilite.ferritelib` and a
  description saying it carries no FerriteLib assembly; the demo asserts its API range in code
  (`FerriteLibVersion.Require(new Version(0, 6, 0), new Version(0, 7, 0), …)`,
  `FerriteLibUiKitDemoMod.cs:25`) and references the carrier through the configurable
  `$(FerriteLibArtifactDir)` (csproj `:26-29`). I did not build or run the demo here.

## 6. The Lead's dev package

`dist/dev/FerriteLib/` holds **exactly five files**, matching the label:

| file | size | SHA-256 |
| --- | --- | --- |
| `LICENSE` | 15780 | `71B96808D967417BCE548B8159ED0409909763CC4371575CCF58455DE044F968` |
| `LoadFolders.xml` | 102 | `B99E238DAA898972E726527E8D19A21F6E5EF1E7457B9622032F9D5A343DC759` |
| `version.txt` | 103 | `0FD93D51920F254C45BB0D7DF91284E343185D785014B1E504DAA1B08248CB93` |
| `1.6/Assemblies/FerriteLib.UiKit.dll` | 249344 | `185C57601FB0D66781B6145B79711221FBB2096334F6355F18D8B7357388B365` (matches the Lead's label) |
| `About/About.xml` | 2198 | `1EBA1798C5124AF8DA1EEE83F727A128E92B74F2EF379D9E0B4481CABA293E99` |

`version.txt` reads: `FerriteLib 0.6.0-dev` / `build=dev` / `commit=f3595a1b28f1` /
`source https://github.com/Coahuilite/FerriteLib`. The label is reconciled with the verified tip per the
preamble; the gates and the privacy result above were both re-run at the verified revision.

**Observation (not a defect):** beside the folder sits `dist/dev/FerriteLib-0.4.0-dev.zip`
(2026-09-15 00:30), a previous round's gitignored artifact. The described package is the
`dist/dev/FerriteLib/` folder and it is exactly five files; the zip is neither part of it nor tracked, but a
reader listing `dist/dev` should not mistake it for this round's output.

## 7. What I did NOT verify

- **In-game behaviour** (checklist A1–A11): no game session was run; every claim here is harness or static.
- **A real second consumer**: the demo is our own mod, not the external-consumer evidence class; no other
  repository compiled against this surface in this session.
- **The demo's runtime behaviour and its package build**: I inspected the committed package read-only; I did
  not rebuild it or run it in the game, and `T5b-2`/`T5b-3`'s probe results were not re-derived here.
- **The packaged DLL's behaviour vs the source tree**: I verified the DLL's hash matches the label, not that
  the hash corresponds to `e246aa4` by rebuilding the payload independently.
- **Privacy vector 3 (history)**: skipped without `-FullHistory`.
- **Anything bound to a moved tip**: this record is bound to `e246aa4`; a new commit above it invalidates `1`,
  `3` and `6` and needs the checks re-run.
- **T1b's own mutation campaign numbers**: the Lead's `cff5506` record claims specific guard numbers; I
  re-derived the activation attack only, not their whole campaign.

## Findings

None. All ten gates, the direct harness run, the lane-coverage check, the privacy audit (vectors 1+2), the
T1b re-plant, the marker grep, the demo package inspection and the dev package hash/label checks are green or
reconciled. The only non-green item encountered was a methodological false FAIL of the privacy audit outside a
git work tree; the only leftover is the previous round's gitignored zip in `dist/dev`.

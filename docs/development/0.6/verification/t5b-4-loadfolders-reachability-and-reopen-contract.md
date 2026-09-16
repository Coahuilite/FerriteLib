# T5b-4 — LoadFolders reachability and the reopen contract (external probe, before the fixes)

Owner `probe`. Branch `feat/0.6-probe`, synced with `git reset --hard 0.6.x` to the round tip
`a6e8885c7cae7151b38d93566460fee0a3013fd9` (clean tree). Probe project: `dist/probe-0.6/` — gitignored,
never committed.

Task-39 extends the outside probe with the check the T5b record lacked: **"the file is in the package" is
not "the game will load it"**. A mod's `LoadFolders.xml` decides which content roots are loaded, and the
game looks for assemblies in `<declared folder>/Assemblies/`; the T5b package check listed files and proved
no foreign carrier was carried, which cannot see a manifest that fails to declare the folder the assembly
actually sits in.

Both halves are present: §1 is the run before the fixes (exit 1, both defects reproduced), §2 is the same
command after the fixes and the re-pack (exit 0, both defects closed by the probe and one delivered-versus-
tracked divergence caught on the way). The probe was not adjusted to make anything pass.

Run (from the worktree root; both delivered packages are read-only inputs):

```powershell
dotnet run --project dist/probe-0.6/Probe.csproj -c Release -- <main-checkout>/dist/dev/FerriteLib <demo-checkout>/dist/FerriteLibUiKitDemo
```

## 1. Before the fixes — exit 1, failure count 2

Raw output of the two new sections and the summary (only machine-absolute paths were replaced by
placeholders):

```text
-- reopen contract: close the only host, change the file, signal, attach a new host (no pump) --
  first window, from v1  : old  (deps=1)
  first window closed    : deps=0
  file rewritten to v2 and signalled; no Pump was called; scheduler=pending=0 deferred=0 retrying=0 oldest=0
  second window attached : old  (deps=1)
  FAIL a window reopened after a change shows the newest valid content  ::  before=old after=old (the new file says 'new')
-- delivered packages: folder reachability and the single carrier --
  [FL] manifest: <main-checkout>/dist/dev/FerriteLib/LoadFolders.xml
  [FL] declared folders for 1.6: [/,1.6]
    MISSING <main-checkout>/dist/dev/FerriteLib/Assemblies/FerriteLib.UiKit.dll
    FOUND   <main-checkout>/dist/dev/FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll
  OK   FL: FerriteLib.UiKit.dll is reachable under a declared folder  ::  <main-checkout>/dist/dev/FerriteLib/Assemblies/FerriteLib.UiKit.dll | <main-checkout>/dist/dev/FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll
  [demo] manifest: <demo-checkout>/dist/FerriteLibUiKitDemo/LoadFolders.xml
  [demo] declared folders for 1.6: [/]
    MISSING <demo-checkout>/dist/FerriteLibUiKitDemo/Assemblies/FerriteLibUiKitDemo.dll
  FAIL demo: FerriteLibUiKitDemo.dll is reachable under a declared folder  ::  <demo-checkout>/dist/FerriteLibUiKitDemo/Assemblies/FerriteLibUiKitDemo.dll
  OK   exactly one FerriteLib.UiKit.dll in the FL package  ::  count=1
  OK   no FerriteLib.UiKit.dll anywhere in the demo package  ::  count=0
  OK   the loaded carrier is the delivered package's DLL (same bytes)  ::  185C57601FB0D66781B6145B79711221FBB2096334F6355F18D8B7357388B365
  demo assembly: <demo-checkout>/dist/FerriteLibUiKitDemo/1.6/Assemblies/FerriteLibUiKitDemo.dll
  demo sha256  : 8F30685E68A8D05CFA0EAA5B10D7586DA104F788AE88243DDDAB05674886DEFA
  demo         : FerriteLibUiKitDemo 1.0.0.0
  OK   the demo references the carrier assembly by its packaged identity  ::  referenced FerriteLib.UiKit vs loaded 0.6.0.0
[probe-0.6] failure count: 2
```

The carrier line at the top of the run printed
`sha256 : 185C57601FB0D66781B6145B79711221FBB2096334F6355F18D8B7357388B365`, the delivered FL package's
bytes. The catalogue, adapter, policy, scheduler and demo-link sections all passed in the same execution and
are unchanged from T5b-2/T5b-3.

### (1) LoadFolders reachability — demo FAIL = review R06-1

- FL is the control and passes: `LoadFolders.xml` declares `[/,1.6]`, and `1.6/Assemblies/FerriteLib.UiKit.dll`
  is found.
- The demo declares only `[/]` while its assembly is at `1.6/Assemblies/FerriteLibUiKitDemo.dll`, so the
  only path the game would search is `<package>/Assemblies/FerriteLibUiKitDemo.dll`, which does not exist.
  The mod class is therefore never found and the ModSettings entry does not exist. **This is R06-1**, now
  reproduced by the outside probe.
- The probe resolves the manifest the way the game reads it: the `<li>` children of the matching `<v1.6>`
  element (plus any `<li>` directly under `<loadFolders>`), with `/` and `` meaning the package root.

### (2) Single carrier — PASS

Exactly one `FerriteLib.UiKit.dll` in the FL package (count=1) and none anywhere in the demo package
(count=0); the probe's loaded carrier is byte-identical to the delivered FL package's DLL
(`185C5760…`). The demo assembly references `FerriteLib.UiKit 0.6.0.0`, the packaged identity.

### (3) Reopen contract — FAIL = review R06-2

Sequence, exactly the reported shape with no `Pump` between: window one is opened from the file's v1
(`old`), it is the only host, it is closed (`deps=0`), the file is rewritten to v2 (`new`), `Signal` is
posted, a **new** host is constructed from the current file and `Attach`ed. The new window draws `old`:
the attach handed the fresh host the service's cached older tree instead of the newest valid content. **This
is R06-2.** After the library fix the same run must print `new`.

One detail worth recording for the fix's reviewer: the scheduler state printed
`pending=0 deferred=0 retrying=0` at the signal, exactly as documented (a signal is only observed by a
pump), so the defect is in what `Attach` resolves when nothing has pumped — not in the signal path.

### (4) Package hashes, labels and axes after the re-pack

- FL dev package: five files; `version.txt` = `FerriteLib 0.6.0-dev`, `build=dev`, `commit=f3595a1b28f1`
  (an ancestor of the current tip `a6e8885`), packaged DLL SHA-256
  `185C57601FB0D66781B6145B79711221FBB2096334F6355F18D8B7357388B365`; delivered `About.xml <modVersion>0.6.0`.
- Demo package: the same eight files as T5b-3; demo DLL SHA-256
  `8F30685E68A8D05CFA0EAA5B10D7586DA104F788AE88243DDDAB05674886DEFA` (unchanged — the demo fix had not
  landed).
- The three FL axes in the worktree read `0.6.0` (`FerriteLibVersion.cs` `new Version(0, 6, 0)`,
  `About/About.xml <modVersion>0.6.0`, csproj `<VersionPrefix>0.6.0`).

## 2. After the fixes — exit 0, failure count 0

Both fixes landed and the delivered artefacts were re-packed. Run conditions: worktree at the 0.6.x tip
`e689be7` (which contains this record's before half `b047d56`), clean tree; the identical command, FL package
directory first, demo package directory second.

Artefacts under test, hashed here:

| artefact | value |
| --- | --- |
| FL dev package label | `FerriteLib 0.6.0-dev`, `build=dev`, `commit=7b62416fa623` |
| FL packaged DLL | `2A7F9C9E9B1D060B32037A82E2989817A19B8317D2FB8B48A68F15B13DDECC0D` (249856 B; before: `185C5760…`) |
| demo repo | `587bf160fbf1e566475b8a80d1ee693ed3e9eca9`, clean, local only |
| demo package | eight files; `LoadFolders.xml` 200 B `3FD398611C5E6013B590BB1E13A5FB328F88AE9A1C6608F6AE076DD0D63D5C49`, declaring `/` and `1.6` |
| demo DLL | `53CBF59E3BDD8602CC0E43E45A3BF01412119BEB64339B3F2FAB6FB2EC4685E7` (49664 B) |

Raw output of the two new sections and the summary (machine paths replaced by placeholders):

```text
-- reopen contract: close the only host, change the file, signal, attach a new host (no pump) --
  first window, from v1  : old  (deps=1)
  first window closed    : deps=0
  file rewritten to v2 and signalled; no Pump was called; scheduler=pending=0 deferred=0 retrying=0 oldest=0
  second window attached : new  (deps=1)
  OK   a window reopened after a change shows the newest valid content  ::  before=old after=new (the new file says 'new')
-- delivered packages: folder reachability and the single carrier --
  [FL] declared folders for 1.6: [/,1.6]
    MISSING <main-checkout>/dist/dev/FerriteLib/Assemblies/FerriteLib.UiKit.dll
    FOUND   <main-checkout>/dist/dev/FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll
  OK   FL: FerriteLib.UiKit.dll is reachable under a declared folder
  [demo] declared folders for 1.6: [/,1.6]
    MISSING <demo-checkout>/dist/FerriteLibUiKitDemo/Assemblies/FerriteLibUiKitDemo.dll
    FOUND   <demo-checkout>/dist/FerriteLibUiKitDemo/1.6/Assemblies/FerriteLibUiKitDemo.dll
  OK   demo: FerriteLibUiKitDemo.dll is reachable under a declared folder
  OK   exactly one FerriteLib.UiKit.dll in the FL package  ::  count=1
  OK   no FerriteLib.UiKit.dll anywhere in the demo package  ::  count=0
  OK   the loaded carrier is the delivered package's DLL (same bytes)  ::  2A7F9C9E9B1D060B32037A82E2989817A19B8317D2FB8B48A68F15B13DDECC0D
  demo assembly: <demo-checkout>/dist/FerriteLibUiKitDemo/1.6/Assemblies/FerriteLibUiKitDemo.dll
  demo sha256  : 53CBF59E3BDD8602CC0E43E45A3BF01412119BEB64339B3F2FAB6FB2EC4685E7
  demo         : FerriteLibUiKitDemo 1.0.0.0
  OK   the demo references the carrier assembly by its packaged identity  ::  referenced FerriteLib.UiKit vs loaded 0.6.0.0
[probe-0.6] failure count: 0
```

### Per section, before → after

| section | before (tip `a6e8885`, FL `f3595a1`, demo `d47b221`) | after (tip `e689be7`, FL `7b62416`, demo `587bf16`) | verdict |
| --- | --- | --- | --- |
| (1) FL reachability, the control | declared `[/,1.6]`, FOUND under `1.6` | declared `[/,1.6]`, FOUND under `1.6` | PASS → PASS |
| (1) demo reachability | declared `[/]`, MISSING | declared `[/,1.6]`, FOUND under `1.6/Assemblies` | **FAIL → PASS — R06-1 closed by the probe** |
| (2) single carrier | 1 in FL, 0 in demo; loaded == `185C5760…` | 1 in FL, 0 in demo; loaded == `2A7F9C9E…` | PASS → PASS |
| (3) reopen contract | `before=old after=old` | `before=old after=new` | **FAIL → PASS — R06-2 closed by the probe** |
| run | exit 1, failure count 2 | exit 0, failure count 0 | — |

### The reachability check also caught a delivered folder one pack behind

The first after-run (FL `2A7F9C9E…`, demo repo `b0fb161`, demo package DLL `C7E6E022…`) still failed the
demo line, and the cause was not the source: the demo repo's tracked `LoadFolders.xml` was 200 B
`3FD39861…` declaring `[/,1.6]`, while the delivered `dist/FerriteLibUiKitDemo/LoadFolders.xml` was 183 B
`A266202A…` declaring only `[/]` — it carried the new comment but not the new `<li>1.6</li>`, with a rebuilt
DLL already in place. The demo owner traced it to `pack.ps1` staging the candidate into `dist/` and validating
afterwards, so a deliberately planted refusal left the mutated manifest in the delivered folder; the tool now
stages into a scratch folder, validates, and swaps in only on success, and a verify script fingerprints the
delivered folder across a refusal. The artefact was re-packed and the run above is against the corrected
folder. The same defect class, one layer below the source, was invisible to a file-listing check and visible
to a manifest-parsing one.

### (4) Axes and labels after the re-pack

- The three FL axes are unchanged at `0.6.0` (`FerriteLibVersion.cs` `new Version(0, 6, 0)`,
  `About/About.xml <modVersion>0.6.0`, csproj `<VersionPrefix>0.6.0`); the delivered FL package's
  `About.xml` also reads `0.6.0`, with `version.txt` at `commit=7b62416fa623`.
- Demo package: eight files, no `FerriteLib.UiKit.dll`, demo `About.xml <modVersion>0.1.0` (the demo's own
  axis, unchanged by this round).

## 3. What this record does and does not prove

- It proves the reachability rule was applied to the delivered artefacts, that the two defects were real and
  reproducible from outside with FL as the passing control, that both flip to PASS after the fixes, and that
  the same check catches a delivered folder that is one pack behind its source (the 183 B versus 200 B
  manifest episode recorded in §2).
- It does not run the game: the manifest resolution here mirrors the declared-folder + `Assemblies/` rule the
  review states; a loader corner the review did not mention (an implicit extra root, a version element
  spelling the game also accepts) is not modelled by this probe.
- The reopen section is public-API only, through the packaged DLL, with a `UiHost` constructed from the
  current file exactly as a consumer does; it is not a game session either.
- Nothing here is mutation-proven: the probe is the outside observer, and the planted-defect evidence for the
  two fixes belongs to their own lanes.

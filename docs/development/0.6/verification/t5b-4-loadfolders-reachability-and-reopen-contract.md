# T5b-4 — LoadFolders reachability and the reopen contract (external probe, before the fixes)

Owner `probe`. Branch `feat/0.6-probe`, synced with `git reset --hard 0.6.x` to the round tip
`a6e8885c7cae7151b38d93566460fee0a3013fd9` (clean tree). Probe project: `dist/probe-0.6/` — gitignored,
never committed.

Task-39 extends the outside probe with the check the T5b record lacked: **"the file is in the package" is
not "the game will load it"**. A mod's `LoadFolders.xml` decides which content roots are loaded, and the
game looks for assemblies in `<declared folder>/Assemblies/`; the T5b package check listed files and proved
no foreign carrier was carried, which cannot see a manifest that fails to declare the folder the assembly
actually sits in.

This record holds the **before** half. The demo fix (task-35) and the library fix (task-34) were still in
flight when it was written; the same command is to be re-run afterwards and the **after** half appended
below, when the demo line must read `FOUND` and the reopen line must print `new`.

Run (from the worktree root; both delivered packages are read-only inputs):

```powershell
dotnet run --project dist/probe-0.6/Probe.csproj -c Release -- <main-checkout>/dist/dev/FerriteLib <demo-checkout>/dist/FerriteLibUiKitDemo
```

## 1. Result at the time of writing — exit 1, failure count 2

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

## 2. After the fixes — to be appended

Not written yet: task-35 (demo manifest) and task-34 (library attach) were in flight. When they land this
record gets a second run of the same command with the new package labels/hashes, and the two lines above must
flip: the demo line to `FOUND <package>/1.6/Assemblies/FerriteLibUiKitDemo.dll`, and the reopen line to
`second window attached : new`. Until that run exists, the fixes are **not** confirmed by this probe.

## 3. What this record does and does not prove

- It proves the reachability rule was applied to the delivered artefacts and that the two defects are real
  and reproducible from outside, with FL as the passing control.
- It does not run the game: the manifest resolution here mirrors the declared-folder + `Assemblies/` rule the
  review states; a loader corner the review did not mention (an implicit extra root, a version element
  spelling the game also accepts) is not modelled by this probe.
- The reopen section is public-API only, through the packaged DLL, with a `UiHost` constructed from the
  current file exactly as a consumer does; it is not a game session either.
- Nothing here is mutation-proven: the probe is the outside observer, and the planted-defect evidence for the
  two fixes belongs to their own lanes.

# T5b-1 — probe baseline: version axes, dev-package content, outside probe skeleton

Owner `probe`. Branch `feat/0.6-probe`, forked from the 0.6 tip `02a6aead0f589ae10356e9cd253035aad8907b87`
(`02a6aea`, the T0 commit). Worktree-only run; the main checkout was not touched, nothing was pushed, tagged,
released or installed into a game `Mods/` folder.

Scope of this document: the *outside* half of the round's verification, at the T0 commit only — the three
version axes, the staged dev package's file set, and a consumer-shaped project that references **only the
packaged carrier** through public API. The full public-API probe of T1/T2/T3's landed surface is task-29 and
is not this document.

Evidence class of everything here: **已实现 + 已自动化验证** (a build, a staged package, and a probe that
compiled and ran). Nothing here is 实机验证, and none of it is a behaviour check of the rendered UI: a probe
that calls an API is not a game session.

## 1. Version axes

Commands (worktree root):

```powershell
Select-String -Path Source/FerriteLib.UiKit/Kernel/FerriteLibVersion.cs -Pattern 'new Version'
Select-String -Path About/About.xml -Pattern 'modVersion'
Select-String -Path Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -Pattern 'VersionPrefix|VersionSuffix'
git rev-parse HEAD
```

| axis | file | value |
| --- | --- | --- |
| contract `FerriteLibVersion.Api` | `Source/FerriteLib.UiKit/Kernel/FerriteLibVersion.cs:59` | `new Version(0, 6, 0)` |
| release `<modVersion>` | `About/About.xml:24` | `0.6.0` |
| build `<VersionPrefix>` | `Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj:15` | `0.6.0` (with `<VersionSuffix>dev`) |
| branch tip | `git rev-parse HEAD` | `02a6aead0f589ae10356e9cd253035aad8907b87` |

The staged package's `version.txt` (raw):

```text
FerriteLib 0.6.0-dev
build=dev
commit=02a6aead0f58
source https://github.com/Coahuilite/FerriteLib
```

Result: all three axes are `0.6.0`, the dev label is `0.6.0-dev`, and the recorded commit
`02a6aead0f58` is the branch tip's short SHA. **Nothing found in the shapes probed.**

## 2. Staged dev package content

Staged in this worktree with `pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev` on a clean tree, so the
label is not `-dirty`. Raw recursive listing of `dist/dev/FerriteLib` (relative path | bytes):

```text
LICENSE | 15780
LoadFolders.xml | 102
version.txt | 103
1.6/Assemblies/FerriteLib.UiKit.dll | 242176
About/About.xml | 2198
```

Directories present: `1.6/`, `About/`, `1.6/Assemblies/`. Assertions run against the tree:

- exactly five files, and the only `.dll` is `1.6/Assemblies/FerriteLib.UiKit.dll` (no second DLL);
- no `Defs`, `Patches`, `Languages`, `Sounds`, `Textures` or `ThingSets` directory anywhere under the package;
- `About/PublishedFileId.txt` is absent (the stager copies `About.xml` as a file, not the directory);
- SHA-256 of the packaged DLL: `F02B8F020C3BE5C8E73FA023CD2B90B4F2403D89F35DB5F0DA2772C9C85E0199`.

The probe below binds to those bytes: the SHA-256 the probe prints for its loaded assembly is the same
`F02B8F02…E0199`, which is the outside check that "the packaged carrier" is the DLL actually running.

**Nothing found in the shapes probed.**

## 3. The probe project

Location (gitignored, never committed): `dist/probe-0.6/Probe.csproj` + `dist/probe-0.6/Program.cs`.

Reference shape: one FerriteLib reference, `<HintPath>..\dev\FerriteLib\1.6\Assemblies\FerriteLib.UiKit.dll</HintPath>`
with `<Private>false</Private>` — the packaged file, never the project and never the harness's own copy. A
post-build target copies that same packaged file next to the probe executable so the CLR can bind to it at run
time, which is what a consumer install does; the probe prints the loaded location and the file's SHA-256 so the
binding is inspectable rather than assumed.

Game surface: the public API used here needs `UnityEngine`/`Verse` only because `IUiWidget`'s signatures and
`UiElementSpec` mention them; a real consumer references its installed game assemblies or `Krafs.Rimworld.Ref`.
This environment has no game install, so the harness stub assemblies under
`tools/FerriteLib.UiKit.Tests/bin/Release/net472/` stand in. They are **not** a FerriteLib reference; the only
FerriteLib reference is the packaged DLL above.

Run:

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev
dotnet run --project dist/probe-0.6/Probe.csproj -c Release
```

Raw output (absolute worktree paths redacted to `<repo-root>`; nothing else edited). Exit code 0, unexpected
throws 0:

```text
[probe-0.6] T5b-1 outside probe - public API only, packaged carrier only
-- loaded carrier --
  assembly   : FerriteLib.UiKit
  version    : 0.6.0.0
  location   : <repo-root>\dist\probe-0.6\bin\Release\net472\FerriteLib.UiKit.dll
  sha256     : F02B8F020C3BE5C8E73FA023CD2B90B4F2403D89F35DB5F0DA2772C9C85E0199
-- contract axis and Require --
  FerriteLibVersion.Api : 0.6.0
  Describe()            : 0.6.0
  OK   Require([0.6.0,0.7.0)) accepts the loaded carrier
  report: FerriteLib [coahuilite.probe] expects API in [0.6.0, 0.7.0), loaded API is 0.6.0. |   copy[0] assemblyVersion=0.6.0.0 api=0.6.0 from=<repo-root>\dist\probe-0.6\bin\Release\net472\FerriteLib.UiKit.dll |   page trees built in this process: none yet
  OK   Require([0.7.0,0.8.0)) reports a mismatch instead of throwing
  report: FerriteLib [coahuilite.probe] expects API in [0.7.0, 0.8.0), loaded API is 0.6.0. MISMATCH. |   copy[0] assemblyVersion=0.6.0.0 api=0.6.0 from=<repo-root>\dist\probe-0.6\bin\Release\net472\FerriteLib.UiKit.dll |   page trees built in this process: none yet
-- registry + UiWidgetCatalog (T3 owns the catalogue) --
  registered : coahuilite.probe/probe/badge (2 declared attributes, 1 label attribute)
  OK   UiWidgetRegistry.Resolve returns the registered kind  ::  probe/badge
  OK   UiWidgetRegistry.KnownKinds lists it (the stable surface still works)  ::  probe/badge
  NOT IMPLEMENTED  UiWidgetCatalog.Snapshot()  ::  T3: UiWidgetCatalog.Snapshot
  NOT IMPLEMENTED  UiWidgetCatalog.TryGet(scope, kind, out descriptor)  ::  T3: UiWidgetCatalog.TryGet
  NOT IMPLEMENTED  UiWidgetCatalog.Scopes()  ::  T3: UiWidgetCatalog.Scopes
-- UiNotifyAdapter (T1) with an injected IUiMainThread --
  OK   a mapped property bumps exactly its key's revision  ::  0 -> 1
  OK   outside a batch the announcement is delivered immediately  ::  FlushCount=1
  OK   the empty-property-name convention reaches the MapAll keys  ::  0 -> 1
  OK   a mapped announcement is not counted as unmapped  ::  unmapped=0
  OK   a named property with no mapping announces nothing and is counted  ::  unmapped=1
  OK   a batch flushes once at the outer scope  ::  2 -> 3
  OK   attaching the same source twice is one subscription  ::  sources=1
  OK   the released handles unsubscribe  ::  sources=0
  OK   an off-thread notification is refused and counted before delivery  ::  offThread=1
  OK   and it delivers nothing: no revision move, no flush  ::  revision 0 -> 0, flush=0
  OK   NotificationRejected names the property that was refused  ::  'Name'
  OK   the pending cap flushes early inside the batch instead of growing  ::  inside-batch flush=1, after=1, pending=0
-- UiReloadPolicy (T2 shape, implemented at T0) --
  OK   the shipped defaults are quiet=0.25 retry=0.5 attempts=3 defer=2  ::  0.25/0.5/3/2
  OK   custom values round-trip, and zero retries is legal  ::  1.5/0.25/0/4
  OK   quietSeconds = 0 is refused
  OK   retrySeconds = 0 is refused
  OK   maxRetryAttempts = -1 is refused
  OK   maxDeferSeconds = 0 is refused
-- UiReloadSchedulerState + UiDocumentService scheduler surface (T2) --
  OK   the scheduler-state value round-trips  ::  2/1/3/4.5
  OK   the service takes an injected policy and clock  ::  autoWatch=False now=12.5
  NOT IMPLEMENTED  UiDocumentService.SchedulerState  ::  T2: UiDocumentService.SchedulerState
[probe-0.6] unexpected-throw count: 0
```

## 4. Disposition at the T0 commit

| surface | state at T0 | evidence |
| --- | --- | --- |
| `FerriteLibVersion.Require` / `Api` / `Describe` | callable from outside; in-range true, out-of-range false with a MISMATCH report naming the loaded API | probe run above |
| `UiWidgetRegistry.Register` / `Resolve` / `KnownKinds` | callable; a probe-registered kind resolves and lists | probe run above |
| `UiWidgetCatalog.Snapshot` / `TryGet` / `Scopes` | **marker**: `NotImplementedException("T3: …")` — owned by `catalog` | probe run above |
| `UiNotifyAdapter` | no marker at T0; mapping, MapAll/empty-name, unmapped counting, batching, cap-early-flush, attach/release and off-thread refusal all behave as `05-api-contract.md` §T1 states | probe run above; these are API-level observations, not T1's lanes |
| `IUiMainThread` injection | works: `ProbeMainThread(false)` produces a refusal and a rejection event | probe run above |
| `UiReloadPolicy` | implemented; defaults and four validation refusals observed | probe run above |
| `UiReloadSchedulerState` | implemented value; fields round-trip | probe run above |
| `UiDocumentService` policy/time-source shape | ctor accepts an injected policy and clock; `Policy`/`TimeSource` echo them | probe run above |
| `UiDocumentService.SchedulerState` | **marker**: `NotImplementedException("T2: UiDocumentService.SchedulerState")` — owned by `reload` | probe run above |

The four markers (`UiWidgetCatalog` ×3, `UiDocumentService.SchedulerState` ×1) are exactly the T3/T2 debt the
T0 contract declares, and the probe reports them instead of working around them. When T1/T2/T3 land, this probe
is extended rather than replaced; that extension is task-29.

**Nothing found in the shapes probed.**

## 5. What this does not prove

- No in-game behaviour: no game session, no real fonts, no window stack, no IMGUI event pass. The probe is a
  build-and-run check of the packaged DLL from outside.
- The adapter observations are one call sequence each; they are not T1's lanes and carry no planted-defect
  evidence. Mutation-proven claims for T1/T2/T3 belong to those packages' lanes and to `verify`.
- The `UiReloadPolicy` refusals are boundary observations, not a proof that no invalid policy can be built
  (e.g. `double.NaN` behaves as "not > 0" and is refused, but that is an observation, not an exhaustive sweep).
- The package inspection is a file-set check on one staged build; it does not prove the DLL matches the source
  beyond the commit recorded in `version.txt`.

## 6. Process note (honest record)

The probe's first run reported six adapter FAILs. They were the probe's own bug — it raised notifications before
`Attach`, so nothing was subscribed and two adapters sharing one VM/bindings would have answered the same event.
The recorded output above is the corrected run. Recorded because a probe's assertions are only as good as its
call sequence: a green probe with a wrong call order would have been a false PASS in the other direction.

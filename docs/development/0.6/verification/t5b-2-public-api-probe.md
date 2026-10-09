# T5b-2 — external public-API probe at the merged 0.6 tip

Owner `probe`. Branch `feat/0.6-probe`. This record was written at the merged `0.6.x` tip
`84537fc58d03e9b9a1d512cdd798bb21eaabe38b` (reached with `git reset --hard 0.6.x`; T5b-1 is in it as
`eaae287`). It was later merged forward with `git merge 0.6.x` for the demo round, and the carrier block
below carries the fresh staging the probe was re-run against. Same worktree-only rules as T5b-1: nothing
pushed, tagged, released or installed into a game `Mods/` folder, and the probe project lives in gitignored
`dist/probe-0.6/` and is never committed.

Scope of this record: task-29 items (1)–(4) — the outside public-API compile/run probe over the landed 0.6
surface, and the package/version checks at the merged tip. Item (5) (demo package + in-game operator
checklist) is **not done** and stays open; see §7.

Evidence class: **已实现 + 已自动化验证** (a consumer-shaped project compiled and ran against the packaged
carrier). Nothing here is 实机验证, and nothing here is mutation-proven: planting a defect and watching a lane
go red is the T1/T2/T3 lanes' evidence, not this probe's. Zero findings below are reported as **nothing found
in the shapes probed**.

## 1. Release package at the merged tip

> **Update after the demo round.** The carrier was re-staged at the lead's current tip: `version.txt` records
> `commit=0d48d25ed1cc`, and the packaged DLL's SHA-256 is
> `F1CB344DDFA73982A79F266AB1BB838F72B7BF6DD319CB6EC09919244942A18D` (the `84537fc58d03` staging written
> below was `AC13A8C0…`). The probe was re-run against the fresh bytes, exit 0; that run, the demo package
> checks and the in-game checklist live in `t5b-3-demo-package-link-and-in-game-checklist.md`. The closed
> five-file set and the absence of content directories are unchanged (dll 249344 B, same listing).

Staged in this worktree with `pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev` (exit 0, all ten gates,
clean tree, so not `-dirty`):

```text
[stage-package] flavor=dev label=0.6.0-dev commit=84537fc58d03
[stage-package] staged 5 files -> <repo-root>\dist\dev\FerriteLib
```

Raw recursive listing of `dist/dev/FerriteLib` (relative path | bytes):

```text
LICENSE | 15780
LoadFolders.xml | 102
version.txt | 103
1.6/Assemblies/FerriteLib.UiKit.dll | 249344
About/About.xml | 2198
```

Directories present: `1.6/`, `About/`, `1.6/Assemblies/`. `version.txt` raw:

```text
FerriteLib 0.6.0-dev
build=dev
commit=84537fc58d03
source https://github.com/Coahuilite/FerriteLib
```

Assertions run against that tree:

- five files; the only `.dll` is `1.6/Assemblies/FerriteLib.UiKit.dll`;
- no `Defs`, `Patches`, `Languages`, `Sounds`, `Textures` or `ThingSets` directory;
- no `About/PublishedFileId.txt`;
- packaged DLL SHA-256 `AC13A8C060ADEE82867BB9E3665BB205F0F4858A4437409ADE5856C22DF8F567`, which is the same
  hash the probe prints for the assembly it loaded (below) — the outside check that the DLL under test is the
  packaged one;
- the three version axes read `0.6.0` in source (`FerriteLibVersion.cs` `new Version(0, 6, 0)`,
  `About/About.xml <modVersion>0.6.0`, csproj `<VersionPrefix>0.6.0`) and `version.txt` records the same
  `0.6.0` label plus the branch tip `84537fc58d03`.

**Nothing found in the shapes probed.**

## 2. The probe, and its raw output

`dist/probe-0.6/Probe.csproj` + `Program.cs` (net472 console app). One FerriteLib reference:
`<HintPath>..\dev\FerriteLib\1.6\Assemblies\FerriteLib.UiKit.dll</HintPath>` with `<Private>false</Private>`;
a post-build copy of that same packaged file sits next to the executable so the CLR can bind at run time, and
the probe prints the loaded location plus the file's SHA-256 so the binding is inspectable. The game surface
(`UnityEngine`/`Verse`) is needed only because `IUiWidget`'s signatures and `UiElementSpec` mention them; it
comes from the harness stub assemblies under `tools/FerriteLib.UiKit.Tests/bin/Release/net472/` because this
environment has no game install. Those stubs are not a FerriteLib reference.

Run: `dotnet run --project dist/probe-0.6/Probe.csproj -c Release` → exit code 0. Raw output (only the
absolute worktree paths were replaced by `<repo-root>`):

```text
[probe-0.6] T5b outside probe - public API only, packaged carrier only
-- loaded carrier --
  assembly   : FerriteLib.UiKit
  version    : 0.6.0.0
  location   : <repo-root>\dist\probe-0.6\bin\Release\net472\FerriteLib.UiKit.dll
  sha256     : AC13A8C060ADEE82867BB9E3665BB205F0F4858A4437409ADE5856C22DF8F567
-- contract axis and Require --
  FerriteLibVersion.Api : 0.6.0
  Describe()            : 0.6.0
  OK   Require([0.6.0,0.7.0)) accepts the loaded carrier  ::  FerriteLib [coahuilite.probe] expects API in [0.6.0, 0.7.0), loaded API is 0.6.0. |   copy[0] assemblyVersion=0.6.0.0 api=0.6.0 from=<repo-root>\dist\probe-0.6\bin\Release\net472\FerriteLib.UiKit.dll |   page trees built in this process: none yet
  OK   Require([0.7.0,0.8.0)) reports a mismatch instead of throwing  ::  FerriteLib [coahuilite.probe] expects API in [0.7.0, 0.8.0), loaded API is 0.6.0. MISMATCH. |   copy[0] assemblyVersion=0.6.0.0 api=0.6.0 from=<repo-root>\dist\probe-0.6\bin\Release\net472\FerriteLib.UiKit.dll |   page trees built in this process: none yet
-- UiWidgetCatalog + UiWidgetRegistry (T3) --
  registered : probe.alpha/shared, probe.beta/shared, probe.beta/only-in-beta, probe.gamma/counted (no schema), probe.gamma/explodes
  snapshot   : 5 descriptors
    [0] probe.alpha/shared  schema=True labels=True
    [1] probe.beta/only-in-beta  schema=False labels=False
    [2] probe.beta/shared  schema=True labels=True
    [3] probe.gamma/counted  schema=False labels=False
    [4] probe.gamma/explodes  schema=True labels=False
  OK   listing made zero factory calls (a throwing factory answers, a counting one stays at 0)  ::  counting.Calls=0
  OK   a kind name shared by two scopes appears once per scope  ::  alpha@0 beta@2
  OK   scope is part of the identity, and the ordering is scope-then-kind ordinal  ::  probe.alpha/shared,probe.beta/only-in-beta,probe.beta/shared,probe.gamma/counted,probe.gamma/explodes
  OK   TryGet answers the exact declared pair  ::  probe.alpha/shared / probe.beta/shared
  OK   TryGet reports false for a kind that only exists in another scope
  OK   TryGet has no core-scope fallback for an undeclared pair
  OK   a kind without a declared schema lists and answers HasAttributeSchema=false  ::  shared=True counted=False
  scopes     : probe.alpha,probe.beta,probe.gamma
  OK   Scopes lists each declaring scope once, ordinal  ::  probe.alpha,probe.beta,probe.gamma
  OK   a snapshot is a copy: the earlier one does not grow, the next one sees the late registration  ::  before=5 after=6
  OK   listing still made no factory call after late registration  ::  counting.Calls=0
-- UiNotifyAdapter (T1) with an injected IUiMainThread --
  OK   a mapped property bumps exactly its key's revision  ::  0 -> 1
  OK   outside a batch the announcement is delivered immediately  ::  FlushCount=1
  OK   the empty-property-name convention reaches the MapAll keys  ::  0 -> 1
  OK   a mapped announcement is not counted as unmapped  ::  unmapped=0
  OK   a named property with no mapping announces nothing and is counted  ::  unmapped=1
  OK   a batch flushes once at the outer scope  ::  2 -> 3
  OK   attaching the same source twice is one subscription  ::  sources=1
  OK   the released handles unsubscribe  ::  sources=0
  OK   one notification to a 3-key mapping writes ONCE outside a batch  ::  flush 0 -> 1, revisions 1/1/1
  OK   a stale subscription handle cannot release a newer subscription  ::  after stale dispose: 1
  OK   the live handle still releases the newer subscription  ::  sources=0
  OK   an off-thread notification is refused and counted before delivery  ::  offThread=1
  OK   and it delivers nothing: no revision move, no flush  ::  revision 0 -> 0, flush=0
  OK   NotificationRejected names the property that was refused  ::  'Name'
  OK   the pending cap flushes early inside the batch instead of growing  ::  inside-batch flush=1, after=1, pending=0
-- UiReloadPolicy (T2) --
  OK   the shipped defaults are quiet=0.25 retry=0.5 attempts=3 defer=2  ::  0.25/0.5/3/2
  OK   custom values round-trip, and zero retries is legal  ::  0.25/0.5/3/2
  OK   quietSeconds = 0 is refused
  OK   retrySeconds = 0 is refused
  OK   maxRetryAttempts = -1 is refused
  OK   maxDeferSeconds = 0 is refused
-- UiReloadSchedulerState value (T2) --
  OK   the scheduler-state value round-trips  ::  pending=2 deferred=1 retrying=3 oldest=4.5
-- UiDocumentService scheduler driven from outside --
  OK   the service echoes the injected policy and clock  ::  now=100
  OK   nothing is scheduled before any signal  ::  pending=0 deferred=0 retrying=0 oldest=0
  OK   attach applied the registered version without a signal
  OK   a signalled document is Pending until its quiet window elapses  ::  pending=1 deferred=0 retrying=0 oldest=0
  OK   OldestPendingSeconds is measured by the injected clock, not by frames  ::  pending=1 deferred=0 retrying=0 oldest=0.0999999999999943
  OK   the quiet window elapsing commits the batch and clears Pending  ::  pending=0 deferred=0 retrying=0 oldest=0
  OK   an interacting attached host turns a ready commit into Deferred  ::  pending=0 deferred=1 retrying=0 oldest=0.400000000000006
  OK   and the deferred commit did not happen yet
  OK   releasing the capture lets the deferred commit through  ::  pending=0 deferred=0 retrying=0 oldest=0
  OK   a transient read failure shows up as Retrying, not as a report  ::  pending=0 deferred=0 retrying=1 oldest=0.299999999999997, reports 2 -> 2
  OK   and the previous valid version stays in force while retrying
  OK   the retry is bounded and then reported  ::  attempts=4, pending=0 deferred=0 retrying=0 oldest=0, reports=3
  OK   the exhausted retry reports the file as unreadable or missing, not as a parse failure  ::  the file '<repo-root>\dist\probe-0.6\bin\Release\net472\scratch\scheduler\page.xml' does not exist; the last valid version is kept
  OK   the file returning commits on the next due pump  ::  pending=0 deferred=0 retrying=0 oldest=0, element=two
[probe-0.6] unexpected-throw count: 0
```

## 3. The scheduler as an outside observer sees it (item 2, detail)

Drive (fake `IUiTimeSource` starting at 100.0; `UiReloadPolicy(0.25, 0.5, 3, 2.0)`; a real file on disk; a
`UiHost` built from public API and attached, because the service deliberately reads nothing while no window is
open):

| step | clock | observed `SchedulerState` |
| --- | --- | --- |
| signal, then pump (observe) | 100.0 | pending=1 deferred=0 retrying=0 oldest=0 |
| clock advanced, no pump | 100.1 | pending=1 oldest≈0.1 |
| pump after the quiet window | 100.3 | pending=0 — the batch committed |
| signal, host holds a capture, pump | 100.7 | deferred=1 |
| release the capture, pump | 100.8 | deferred=0 — committed |
| delete the file, signal, observe, pump past quiet | 101.3 | retrying=1, no report added, previous version still in force |
| retry interval pumps | 101.8 / 102.3 / 102.8 | retrying stays 1 for 3 retries then the 4th attempt reports |
| file restored, signal, observe, pump | 102.9→102.91 | pending=0, host tree follows the new element |

What an outside observer **can** see: all four counters; `OldestPendingSeconds` measured against the injected
clock; the reload reports. What it **cannot** see: the private per-document schedule table, and therefore *why*
two documents would land in the same counter — the public shape is counts plus the oldest age, not per-document
detail. It also cannot observe a commit with nothing attached: `Pump()` observes signals and returns without
reading when there is no dependency, which is the documented contract, not a probe limitation. `Deferred` is
reachable from outside because `UiSession.CaptureHotControl` is public API — the same state an active drag
produces. The retry path is reachable by deleting the file, which is the service's own retryable failure class.

Process note: my first attempt advanced the clock before the pump that observes the signal and read
`pending=1` after the quiet window. That is the documented semantics — the quiet window opens when the pump
first observes a signal, so the effective delay is `[QuietSeconds, QuietSeconds + one pump interval]` — and
the probe now pumps once to observe, advances the clock, then pumps again.

**Nothing found in the shapes probed.**

## 4. Catalogue, scope-preserving identity and zero factories (item 1)

Two scopes declare the same kind name (`probe.alpha/shared`, `probe.beta/shared`); a third scope holds a kind
with no declared schema, a kind whose factory counts calls, and a kind whose factory throws. From outside:

- `Snapshot()` lists both `shared` pairs separately, ordered scope-then-kind ordinal, and each descriptor
  carries its own scope;
- `TryGet` answers the exact pair, returns false for a kind that exists only in another scope, and applies no
  core-scope fallback;
- a kind registered without a schema lists and reports `HasAttributeSchema == false` (with an empty attribute
  collection);
- the throwing factory and the counting factory were never invoked by `Snapshot`/`TryGet`/`Scopes`
  (`counting.Calls == 0` before and after a late registration);
- a snapshot is a copy: the earlier list does not grow when a kind is registered afterwards, and the next
  snapshot sees it.

**Nothing found in the shapes probed.**

## 5. Notification adapter (item 3)

The T5b-1 checks were re-run unchanged (mapping bumps its key's revision, immediate delivery outside a batch,
the empty-name convention reaching `MapAll`, unmapped counting, one flush per outer batch, idempotent double
`Attach`, off-thread refusal with no delivery and a `NotificationRejected` name, and the pending-cap early
flush). The two shapes T1 landed were added:

- one notification to a mapping with three keys writes **once** outside a batch (flush 0 → 1, and all three key
  revisions 0 → 1);
- a stale subscription handle cannot release a newer subscription (attach → release → attach again → the old
  handle's second dispose leaves the newer subscription attached; the live handle still releases it).

**Nothing found in the shapes probed.**

## 6. What this does not prove

- It is not mutation-proven: no defect was planted here. The planted-defect evidence for T1/T2/T3 belongs to
  their lanes and to the adversarial verification record.
- It is not a game session: no window stack, no real fonts, no IMGUI event pass, no input. The probe proves the
  surface is callable from outside and that the packaged bytes answer as documented at this tip.
- The scheduler drive is one scripted timeline; it is not an exhaustive timing sweep and it says nothing about
  frame-rate-dependent behaviour in a real game.
- The catalogue's zero-factory claim is asserted for the listed call paths only; it does not sweep every public
  registry entry point.
- The package checks are file-set and hash checks on one staged build, not a source-to-DLL equivalence proof
  beyond the commit recorded in `version.txt`.

## 7. OPEN — item (5), and one task-29 item not reachable from outside

1. **Demo package check (blocked on T4).** When `ferritelib_uikit_demo` lands: build it; assert its package
   does not carry `FerriteLib.UiKit.dll`; assert no machine-absolute path in any publishable file; then record
   it here. Not attempted — the demo package does not exist at this tip.
2. **In-game operator checklist (blocked on T4).** The shortest operator steps (ModSettings entry + three tabs;
   core samples; consumer directory; dual-window click/map/input/close; pause then save; invalid XML recovery;
   IME/drag; close and reopen) are to be written for a human with no context, every item marked 尚待实机. Not
   written yet, because the steps must be written against the demo that will be run.
3. **`UiWindowHost.HostAttached`/`HostDetached` are not exercised by this probe.** They are part of task-29's
   item (1), but firing them requires entering a window's guarded pass, which needs the window shell plus a
   game-side draw/event pass — not something this outside process can do with public API alone. Recorded as an
   open sub-item to be covered by the in-game checklist work in (2), not silently dropped.

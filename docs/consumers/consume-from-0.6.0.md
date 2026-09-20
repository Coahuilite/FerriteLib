# Consume FerriteLib from another mod — 0.6.0

**Read this first if you are wiring another project against FerriteLib.** It is the permanent, always-updated
home for consumer integration (kept current inside the FL repository). It works together with two docs that
describe the surface itself:

- `docs/api-tiers.md` — the compat promise (stable / public-unstable / internalize-candidate).
- `docs/development/0.6/20-api-and-xml.md` — how each capability is used (XML + C#), incl. vocabulary traps.
- `docs/development/0.6/30-consumer-handoff.md` — the round's handoff and known limits (kept in sync).

## 1. Which artefact to take

| | |
| --- | --- |
| Dev package folder | `dist/dev/FerriteLib/` (5 files) — staged by `scripts/verify-local.ps1 -PackDev` |
| Dev package zip | `dist/dev/FerriteLib-0.6.0-dev.zip` — same folder, compressed for transfer |
| version.txt (history) | `FerriteLib 0.6.0-dev / build=dev / commit=7b62416fa623` — recorded when this line was cut |
| DLL (history) | `1.6/Assemblies/FerriteLib.UiKit.dll`, SHA-256 `2A7F9C9E…DECC0D` (the R06-fixed **Dev-channel** rehearsal of that commit) |

**Which payload is authoritative, because more than one exists (FL-11).** The **GitHub Release asset** published
from `coahuilite.ferritelib` is the only payload identity a consumer can verify — each release body names the
commit it was built from and the asset's SHA-256. A repo dev folder, a sibling checkout's copy and whatever sits
in `1.6/Assemblies/` after a build are **rehearsals**, and the reason two of them can carry the same Api with
different bytes is that they are different *builds*: this table's row is a Dev-channel build (larger, `build=dev`)
of one commit, while a consumer's `<HintPath>` in the same period bound a Release build of another commit. Two
hash identities for one Api is not a contradiction to resolve by picking one — it is why you verify what you
actually bound (the assembly's `AssemblyConfiguration` and embedded commit, plus the `Require` verdict) and take
the artifact from the Release page.

**Do not copy the DLL into your package.** Shim: reference it with `<HintPath>` + `<Private>false</Private>`
(see §3). Only the `coahuilite.ferritelib` mod may ship `FerriteLib.UiKit.dll`.

## 2. Assert the version range

```csharp
// Mod constructor, before touching any UiKit type:
FerriteLibVersion.Require(new Version(0, 6, 0), new Version(0, 7, 0));
```

The game cannot express a prerequisite version, so the range is yours to assert; it is also the only detector
of a second carrier. Axes: `FerriteLibVersion.Api` = `About.xml <modVersion>` = `<VersionPrefix>` = 0.6.0.

## 3. Reference it (avoid stale-build and privacy traps)

```xml
<!-- YourMod.csproj -->
<ItemGroup>
  <Reference Include="FerriteLib.UiKit">
    <HintPath>$(FerriteLibArtifactDir)\FerriteLib.UiKit.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

Configure `FerriteLibArtifactDir` once, machine-locally, with a committed example and a relative-sibling
fallback — never a machine-absolute path in a publishable file:

```powershell
# committed file: FerriteLib.local.props.example    (a copy named FerriteLib.local.props is gitignored)
#   <FerriteLibArtifactDir>..\ferritelib\dist\dev\FerriteLib\1.6\Assemblies</FerriteLibArtifactDir>
# gitignore: FerriteLib.local.props
```

**Stale-build trap:** the reference resolves by path, so MSBuild's up-to-date check does not notice the carrier
being swapped. After fetching a new FL package, rebuild with `--no-incremental` (and run your probe with
`--no-build` after forcing it).

## 4. The registry scope is the host's Source — the trap that costs real time

`UiWidgetRegistry.Resolve` is called with the page's source, so a kind is only found by a page whose source
**equals the scope you registered the kind under**; otherwise it falls back to `core` and then throws
`UiUnknownWidgetKindException`. For a `UiPageWindow` the source is `Consumer + "/" + WindowKind` (page
identity, not packageId).

Register under the exact page identity you open, or wrap the page in a window whose source is your scope.

## 5. The round you should also read

- Lifecycle door: subscribe `UiWindowHost.HostAttached` / `HostDetached` (fires before first draw / before
  host disposal). A handler may close its own window — the guard exists and is lane-pinned.
- Notifications: `UiNotifyAdapter` over `INotifyPropertyChanged` — explicit `Map`/MapAll, bounded batch,
  main-thread refusal. A plain C# object is a VM (no base class, no DI, no reflection).
- Auto reload: `UiDocumentService` — `Signal` is the debounced watcher channel, `Reload` is the immediate
  manual channel (same validation path). Construct with `UiReloadPolicy`/`IUiTimeSource` and read
  `SchedulerState`. Reopen contract: a signal that arrived while nothing was attached is consumed at the next
  `Attach` and the newest valid content is resolved.
- Directory: `UiWidgetCatalog.Snapshot()/TryGet/Scopes` — read-only, scope-preserving, zero factory calls.

## 6. What is still open (do not over-claim)

- **In-game acceptance is 尚待实机** — a human with a game session must walk the operator checklist
  (`docs/development/0.6/verification/t5b-3-demo-package-link-and-in-game-checklist.md` §4).
- **The recorded 0.6.0 evidence contains no second-real-consumer integration.** This is an evidence
  limitation, not an API-stabilization prerequisite: the maintainer removed that prerequisite on
  2026-09-17. Existing type tiers remain as listed in `docs/api-tiers.md` until explicitly changed;
  the ruling does not itself claim new integration or promote types.
- Known, documented limits: main-thread-only notification delivery; IME composition is not part of the input
  deferral; no cross-sibling-window atomicity for consumer hooks; listing is a snapshot, not a subscription.
- If you are forced to hand-roll something the public surface cannot express, that is a finding, not a
  workaround — report it (FL's growth rule: a cited consumer need is what earns a new capability).

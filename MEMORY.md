# MEMORY

## Current durable state

- Repository split out of the Universal Squeaker tree on 2026-09-03. FerriteLib is a prerequisite mod,
  `coahuilite.ferritelib`, display name FerriteLib, `modVersion` 0.2.0. No remote, nothing published.
- Provenance: `Source/FerriteLib.UiKit/**` and `tools/FerriteLib.UiKit.Tests/**` were copied out of the
  US repo at US commit `0fe60b0` after a file-for-file `diff -r` check, then amended there. US retains
  its own history; this repo's history starts at the split.
- **Cross-mod assembly binding is proven in a running game (2026-09-04).** US, shipping no FerriteLib
  payload of its own, loaded and ran its whole settings page and camera overlay with
  `FerriteLib.UiKit.dll` present only in the carrier mod, with no red text and no type-load failure. The
  sibling-`HintPath` + `<Private>False` design is therefore correct, and "ship a copy / go NuGet with
  Private=true" is rejected on evidence rather than preference. This was the top open risk and it is gone.
- **`UiPopup` is the single popup geometry and input primitive (2026-09-04, `44b00c5`).** `RectFor`
  (below / flip above / pin to viewport top / horizontal clamp / unpublished-viewport pass-through)
  and `DrawOptionList` (panel, single-line option rows, `UiSession.SetPopupRect` publication, row
  hit-test, consume-close-callback) live in one place; `DropdownWidget` and the US composite
  `UsKernelDraw.Dropdown` both delegate to it. The publication is what makes
  `UiNative.DropdownButtonCore`'s yield guard able to fire at all — a consumer that redraws popup rows
  by hand and skips it silently reintroduces "covered trigger steals the option click", which is
  exactly the defect the US side reported and fixed the same day. Any new dropdown-shaped surface must
  go through `UiPopup`; a second copy of either rule is a defect, not a style choice.
- **Contract axis is 0.2.0 (2026-09-04, `b2006a0`) and the bump rule is tightened**: pre-1.0, any
  change to the public surface — additions included — bumps `Api.Minor`, and `About/About.xml
  <modVersion>` moves with it. The 0.1.0 → 0.2.0 bump exists because an additive type (UiPopup)
  shipped without one and a consumer desynced into a TypeLoadException inside UiHost.Draw; the
  tightened rule is what makes Require's readable-report promise hold in both directions.
- **Pointer-space contract (2026-09-04, `9a197a2`)**: inside scroll/group scopes `Event.current.mousePosition`
  arrives in the container's draw space, while popup rects are published in Host window space. Any
  comparison between the two must convert through the caller's `ctx` origin (`UiNative.PointerPositionIn`);
  comparing raw is the defect that stole every covered option click in game. The stub harness now models
  IMGUI group origins, hot-control capture/activation and per-pass control ids, and two pump lanes drive
  real event passes (flat and scrolled+offset); restoring the raw comparison fails the scrolled lane with
  the same trace signature as the in-game log.
- Still unverified in game, both cheap, both in `TODO.md` §1: the guard's deliberate duplicate-DLL
  branch, and the failure shape when the carrier is absent. Everything else in this file remains
  compile-time, stub-harness or reference-assembly evidence — say so rather than implying a game run.
- **Two hard rules inherited from the series, both easy to violate by accident.** Nothing in this library
  may persist data into a save - a prerequisite must survive being uninstalled, and a save-written flag is
  the one side effect a player cannot undo. And `../squeaky_ratkin` is never written: SR is a separate
  product with its own brand and `SR_` prefix, and this repo's neutrality lane exists to keep even its
  vocabulary out of here.
- **A `Require` desync is now a named verdict, and that is the durable part** (2026-09-04, `b2006a0`):
  the report must carry `MISMATCH`, the loaded `Api`, and the consumer's compiled floor, so a stale
  carrier is a readable prerequisite error rather than a `TypeLoadException` at first draw. Harness
  coverage for it is `FerriteLibVersionTests`' "A consumer compiled above the loaded carrier gets a named
  MISMATCH, not a pass". Lesson worth keeping even if the version numbers move again: **`Require` only
  protects a consumer if additive changes also move the axis** - pinning the compiled minor is what makes
  "the carrier is older than the DLL I built against" observable. So the axis is not only a breaking-change
  counter; it is a "does this carrier contain what I compiled against" counter, which pre-1.0 is the same
  bump.
- **Publication form decided (2026-09-05, maintainer): two repositories, two release pages, linked
  rather than copied.** `coahuilite.ferritelib` publishes its own GitHub Release and remains the **only**
  publisher of `FerriteLib.UiKit.dll`; Universal Squeaker publishes its own release carrying **only its own
  package**, and every US release body links to the specific lib release its `PrerequisiteApiMin` was
  compiled against. Deliberately rejected: rebuilding the lib artifact inside US's pipeline (two builders
  for one DLL, so the bytes a player holds have no single provenance) and re-attaching lib's zip to US's
  page (two pages that both look canonical, and a split download counter - the count is the demand signal
  during early testing). `actions/download-artifact` cannot cross repositories anyway, so Releases, not
  artifacts, is the sanctioned carrier between them.
- **There are three version axes, not two, and only two were locked.** Contract axis
  `FerriteLibVersion.Api`, release axis `About/About.xml <modVersion>`, and - the one no document named -
  the **build axis**, `Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj <VersionPrefix>`, which is what
  `pack-dev.ps1:45` derives the artifact name and `version.txt` from. The 0.2.0 move updated the first two
  and left the third at 0.1.0, so seven gates stayed green while the packaging script was preparing a zip
  labelled 0.1.0 around a 0.2.0 DLL. FerriteLibVersionTests now asserts all three agree
  ("Build axis in the csproj matches the other two axes", 66 named assertions total as of `fc59b60` + this
  change), and the assertion is **mutation-proven in one direction only**: reverting `<VersionPrefix>` to
  0.1.0 fails exactly that one assertion and nothing else. The reverse case - a future axis living in a
  fourth file - is guarded by no test, because no such file exists yet.
- **A release asset must be built after the gates run, not during them.** `verify-local.ps1`'s Dev and
  Release build gates leave `1.6/Assemblies/FerriteLib.UiKit.dll` carrying `0.2.0-dev+<sha>` in its version
  resource, and that is the correct identity for a dev rehearsal - so `pack-release.ps1` refuses it (tested:
  `Payload is a dev build ...`) and the release workflow rebuilds with `-p:VersionSuffix=` to get the bare
  `0.2.0+<sha>`. Anyone who wires these steps in the other order gets a red pack step, not a mislabelled
  package.
- **The two zip shapes differ on purpose, and the difference is user-visible.** `pack-dev.ps1` archives
  `(Join-Path $stageDir '*')` - contents at the zip root - because a dev rehearsal copies into a folder that
  already exists. `pack-release.ps1` archives `$stageDir` itself, so the archive contains a top-level
  `FerriteLib/` and a player unzipping straight into `Mods/` gets `Mods/FerriteLib/About/About.xml` instead
  of a `LoadFolders.xml` loose in `Mods/`. Verified by listing the produced zip
  (`FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll`, `FerriteLib/LICENSE`, `FerriteLib/version.txt`).
- **GitHub policy does not constrain this shape of distribution** (read from `github/site-policy` and
  `github/docs`, 2026-09-05): Releases are documented as packaging software "for other people to download
  and use", with a stated limit of 1000 assets per release, 2 GiB per file, and **"no limit on the total
  size of a release, nor bandwidth usage"**. The AUP's excessive-bandwidth clause (§9) is a
  relative-to-similar-features test, and its 10 GiB/month figures are Git **LFS** quotas - which this repo
  does not use, because no binary is tracked at all. What the policies actually prohibit is using raw/file
  hosting as a hotlinked CDN and shipping malware; a 45 KiB mod zip is neither. Cross-repo consumption of
  a public release needs no special permission: `GITHUB_TOKEN` with `contents: read`, or a plain
  `browser_download_url`.

## What was verified, and how

- **RimWorld 1.6 UI surface**, read from `Krafs.Rimworld.Ref 1.6.4871` with dnlib (that package mirrors
  the game's `Data/Managed`, so the presence of an assembly there means it ships with the game):
  `Assembly-CSharp` has 16,158 types, 32 declaring `OnGUI`, 76 `Verse.Window` subclasses, 116
  `DoWindowContents` overrides, 123 `Dialog_*`, 261 `*FloatMenu*` types, and `Verse.Widgets` with 174
  methods / 152 public static. `Verse.Window` carries a `UnityEngine.GUI/WindowFunction` field, i.e. the
  game's window manager is an adapter over IMGUI's `GUI.Window`. It references `IMGUIModule` and
  `TextRenderingModule`, never `UnityEngine.UI` or `UIModule`.
- **uGUI / UIElements assemblies do ship** (`UnityEngine.UI.dll`, `UnityEngine.UIModule.dll`,
  `Unity.TextMeshPro.dll`, `UnityEngine.UIElementsModule.dll`), so they are referenceable by a mod. The
  constraint that actually matters is compositing, not availability: IMGUI draws above every Canvas.
- **The game's own UI palette** is `Verse.Widgets`' 18 Color fields — five (fill, border) pairs plus
  normal/mouseover/inactive, separator, highlight, range-control text. **No accent slot exists**, and
  vanilla chrome is carried by `ButtonBGAtlas` + mouseover/click variants and nine `AtlasUV_*` 9-slice
  rects. Consequence for theming: the default DarkGold palette is a theme over the game's *content*
  palette (dark neutral, warm highlight, gold objects), not a reproduction of its widget chrome, and the
  token bag currently cannot express the game's per-surface fill/border pairing at all because
  `UiTheme` has one global `Border`.
- **Prerequisite mechanics.** `Verse.ModRequirement` = {packageId, alternativePackageIds, displayName}
  and `ModDependency` adds only download URLs, so no version can be declared. `RimWorld.VersionControl`
  offers `TryParseVersionString` / `IsCompatible` / `VersionFromString`; `ModMetaData.ModVersion` is
  readable at runtime; `Verse.ModLister.GetModWithIdentifier` / `GetActiveModWithIdentifier` enumerate
  mods. `Verse.ModAssemblyHandler` holds `List<Assembly> loadedAssemblies` and a
  `globalResolverIsSet` flag, which is why duplicate carriers resolve by load order silently.
- **NuGet behaviour, measured with a throwaway probe** (not asserted from memory): a package republished
  under the same version is NOT re-fetched — the consumer's global folder kept the first copy after
  `obj`/`bin` deletion and a fresh `dotnet restore`. A `1.0.0-dev+<sha>` version is worse than useless:
  the nuspec keeps the metadata, the filename and the resolved cache path both drop it, so every hash
  shares one cache entry. Only a prerelease label (`1.0.0-dev.<sha>`) produces a distinct package, which
  then requires a floating version, which the consumer gates defeat by running with restore disabled.
  Hence sibling relative paths.
- **net472 reference-assembly traps** hit while writing this repo's own code, all compile-verified:
  `string.IsNullOrEmpty` carries no `[NotNullWhen(false)]` (CS8602 on the ternary that returns it);
  `string.Split(char, StringSplitOptions)` is advertised but throws `MissingMethodException` at
  runtime; `string.Contains(string, StringComparison)` likewise does not exist at runtime.

## Cross-repo couplings that no gate in this repo can see

- **The consumer's harness compiles this repo's stub projects.**
  `../UniversalSqueaker/tools/UniversalSqueakerKernelHostTests/UniversalSqueakerKernelHostTests.csproj`
  sets `FerriteLibHarness` to `../../../ferritelib/tools/FerriteLib.UiKit.Tests` and, in a post-build
  target, runs `dotnet build` on all four projects under `tools/FerriteLib.UiKit.Tests/Stubs/` and copies
  their output from `bin/stubs/<name>/`. So the Stubs tree - directory names, project names, assembly
  names (`Assembly-CSharp.dll`, `UnityEngine.CoreModule.dll`, ...) and that `bin/stubs/` output shape -
  is a de-facto published surface. Renaming or relocating any of it breaks the consumer's gate 13 while
  **all seven gates in this repo stay green**, because nothing here builds or references the sibling.
  Same shape one level up: US's gate 6 asserts this repo's payload DLL exists at
  `../ferritelib/1.6/Assemblies/FerriteLib.UiKit.dll`, so a broken build here goes red over there first.
  Evidence: read from both csproj/scripts on 2026-09-04, source-level only.

## Consumer coverage, measured 2026-09-04

- **The library ships 7 widget kinds; 3 are reachable from outside, 4 have no consumer at all.** US's two
  Schema=2 manifests reference exactly one library kind, `chrome/banner`, out of 17 kinds total (16
  consumer `us/*` + 1 here). Two more library kinds are used, but *not* through a manifest:
  `UsFilterBarWidget` instantiates `DropdownWidget` and `UsAttenuationEditorWidget` instantiates
  `LineChartWidget` directly as composition parts. `section/header`, `state/empty`, `input/mode-row` and
  `input/stepper-slider` are registered here and appear nowhere in US's `Source/**` (grep, zero hits) -
  `input/mode-row` most pointedly, because US ships its own `us/mode-row`. Consequence for the freeze and
  for the invited-vs-unsupported call: "validated against one consumer" is narrower than it sounds. Those
  four kinds are unvalidated in a running game, not merely unused - and a third party compiling against
  them would be the first real test of them. Evidence: source-level grep across both repos; no new game
  run.
- **Two text paths bypass the fit audit today, so `UiThemeDraw.Label` is not "the single text outlet".**
  `UiFitAudit.Check` is called from exactly one place, `UiThemeDraw.cs:24`, but `VerseWidgets.Label` is
  called from three: `UiThemeDraw.cs:34` (audited), `UiLayoutEngine.cs:1049` (reached by
  `DrawContainer`'s `Title`/`TitleKey` handling for `Section`/`Surface` containers, `UiLayoutEngine.cs:1028`)
  and `StepperSliderWidget.cs:164` (the widget's own `Label`/`LabelKey` and its `-`/`+` glyphs). So
  container titles and stepper labels are invisible to the text-fit audit: `BeginElement(entry.Path)`
  attributes findings, but with no `Check` on those paths there is nothing to attribute. This is a
  coverage fact, not a proven defect - no overflow on those two paths has been observed anywhere, in-game
  or in the harness, and the harness's own audit tests drive `UiThemeDraw.Label` directly, so they cannot
  see the gap either.

## Structure and where things live

Paths and roles only; any line/file count here would be false within a day (see the predicate rule in
"Enduring corrections"). Re-derive with `git ls-files`.

- `Source/FerriteLib.UiKit/Kernel/` - the whole payload surface. `net472`, `TreatWarningsAsErrors`,
  `Nullable` on, output pinned to `1.6/Assemblies/` because that path is what consumers bind to.
  - Page model: `UiHost` (per-window façade, `IDisposable`), `UiSession`, `UiLayoutEngine` (the largest
    file by far), `UiLayoutManifest`, `UiLayoutSnapshot`, `UiBindings`/`IUiBindings`, `UiWidgetRegistry`,
    `KernelCoreWidgetRegistrar`, `UiWidgetContext`, `UiElementSpec`, `UiSessionGuard`, `UiValueState`,
    `UiNative` (the IMGUI interop seam), `UiPopup`, `UiChartPointChange`, the contract exceptions, and
    `Widgets/` (7 kinds).
  - Visual core: `UiTheme`, `UiThemeDraw`, `UiFitAudit`, `UiKitFonts`, `UiFont`, `ITextMetrics`,
    `VerseFerriteTextMetrics`.
  - `FerriteLibVersion` is BCL-only on purpose: no Verse, no UnityEngine, so a consumer can call
    `Require` from its earliest constructor and the harness can test it with no stubs at all.
- `tools/FerriteLib.UiKit.Tests/` - a plain `Main()` runner (no test framework), 11 lane files plus
  `Program` and `StubTextWidth`, with 4 standalone stub projects under `Stubs/`.
- `scripts/verify-local.ps1` (7 gates, `-PackDev` adds packaging) and `scripts/pack-dev.ps1`.
- `About/About.xml`, `LoadFolders.xml`, `LICENSE`, `1.6/Assemblies/` (the DLL and PDB are gitignored; only
  `.gitkeep` is tracked, so a fresh clone has no payload until it builds).

## Consumer contract, in the direction that hurts

- **`UiHost` is per-window and disposable; the consumer disposes it.** US does so from its own window's
  `PreClose`, a Verse hook that does not exist here. Nothing in this library may hold process-wide
  interaction state - that is what keeps two hosts in one game session from touching each other.
- **Schema=2 is the only shipped manifest schema**, and the consumer embeds its manifests as assembly
  resources and loads them with `GetManifestResourceStream` - a call that lives in US, not here.
  `UiLayoutManifest.ParseFile` has no production caller.
- **Popups are session-owned and window-spaced.** A surface that opens a popup publishes its covered rect
  through `UiSession.SetPopupRect`, which only `UiPopup.DrawOptionList` does; `UiNative`'s yield guard
  cannot fire without it. Geometry comes from `UiPopup.RectFor`, never from a local copy.
- **Naming hazard from the consumer side**: five substrings are banned in US's own `UI/**` (see
  "Enduring corrections"); all are legal here, but a public type carrying one becomes the consumer's red
  gate the moment it appears in a US file.
- Three symbols people keep assuming live here are **not** from this repo: `SessionRevisionBumper` (a US
  private class), `PreClose` (Verse), `GetManifestResourceStream` (BCL, called in US). Searching this tree
  for them returns nothing, by design.

## How verification is described here

- A PASS is recorded with its scope and its evidence class. The classes in this repo are, weakest to
  strongest: reference-assembly read, stub harness, compile-time, and in-game observation - and only the
  last is written as "proven in a running game".
- **Scope an assertion to the region that can legitimately carry the defect.** Gate 6's first version
  searched the whole `LICENSE` for the Exhibit B sentence and went red on a *correct* licence, because the
  reproduced MPL body always contains that sample notice.
- **Assertion order is part of an assertion.** A `Copy-Item LICENSE` placed before the staging directory's
  `Remove-Item` would have shipped a package with no licence while every gate stayed green; any claim
  about package contents must run after the tree is final.
- Mutation-test a new gate, not only the new feature. Half of `VerifyThemeColorsDoNotAffectLayout` is a
  future-regression guard rather than present evidence, and it is labelled that way for exactly this
  reason.

## Gates and what each actually proves

Seven gates in `scripts/verify-local.ps1`: 1 harness, 2 Dev build (`FER_DEV`, warnings-as-errors), 3
Release build (warnings-as-errors), 4 payload present, 5 content-free (named-path probe for `Defs`,
`Patches`, `Languages`, `Sounds`, `Textures`, `ThingSets` under `1.6/` - probed as names, not filtered
from an enumeration, so an empty tree cannot pass vacuously), 6 LICENSE, 7 About.xml identity. Gate 6
proves only what is visible from inside this repo: the file exists, carries the MPL-2.0 title, still
contains Exhibit B and section 10.4, and does not apply the incompatibility notice in its header block.
It does **not** compare the text against a consumer's copy, and no script in this repo refers to a
sibling repo at all (checked: no `Get-FileHash`, no `..\` path in `scripts/`). The byte-parity assertion
is consumer-side - US's own gate 10 hashes both copies.

Dated correction, 2026-09-04: earlier text here credited gate 6 (as "the seventh") with a SHA-256
comparison to the consumer's copy. It never ran one. The consequence is not cosmetic: **a `LICENSE`
edited or truncated in this repo cannot turn any gate here red**; only a US gate run notices, and only
when the sibling tree is present. Anything that repeats "byte-identical to the consumer's copy" as a
property of this repo's gates is wrong in the same way.

The harness is 13 test files carrying 63 named assertions. (The older wording here was "13 lanes",
which counted files and read like an assertion count - prefer the explicit numbers.) Three assertion
groups are worth naming because they are the reason this repo can be trusted across a boundary:

- Version contract lane (11 assertions): range accept/reject, the pre-1.0 bump rule, inverted range as
  a caller error, exact-API acceptance, duplicate-carrier detection driven through the internal
  decision function, range-vs-duplicate report separation, the named `MISMATCH` desync report,
  empty-copy-list safety, and the `Api` ↔ `modVersion` major/minor lock. Mutation-checked: breaking the
  duplicate condition and desyncing `modVersion` each fail exactly one lane.
- Neutrality lane (3 assertions): scans both trees case-insensitively for product words and
  case-sensitively for prefixes, **throws if a scanned tree is missing** rather than passing on an
  empty enumeration, plants literals into both trees as a positive control, and pins the single
  self-exemption to one full path. The guard on the guard exists because the previous arrangement —
  US scanning this library from over the fence — passed vacuously the moment the trees moved.
- Visual-core boundary lane: the seven visual-core files may not name any page-model type.
  Mutation-checked by planting a `UiSession` reference.
- The guard is a **name-list check, not a transitive one**: it reads the seven visual-core files and
  rejects any line naming one of fourteen page-model symbols. So it catches `UiThemeDraw` → `UiSession`
  directly, but not `UiThemeDraw` → `UiPopup` → `UiSession`, because `UiPopup` is in neither list — it
  joined the tree on `44b00c5`, after the guard was written, and appears in neither array of
  `KernelContractTests.VerifyVisualCoreIsPageModelFree`. The two-layer claim is true of the code as it
  stands and unenforced along that one new path. Read from both arrays; no mutation test of this hole has
  been run, and the hole is currently hypothetical — nothing in the visual core calls `UiPopup`.

Honest limit on the theme lane: `VerifyThemeColorsDoNotAffectLayout` has two halves. The shared-instance
half is mutation-proven. The rect-equality half has **no available failing mutation** today, because no
colour token currently feeds layout — it is a future-regression guard, not present evidence.

## Enduring corrections

- A library **can** assert its own neutrality. The note in the US harness claiming otherwise was written
  before the blocklist self-exemption was pinned to a single path with a positive control.
- `01-product-and-architecture-decisions-zh.md:208` (US repo) says the kernel's fallback text uses
  Ferrite-owned translation keys. Not implemented and, as it stands, not needed: `UiSessionGuard` logs
  only an English line and every visible fallback string comes from the consumer's `fallback` delegate.
  If a future theme or fallback wants its own key, that key belongs here and needs both language files.
- `UiTheme.DarkGold` is a **template**: each access returns a fresh instance. It used to be a shared
  mutable singleton, which is harmless with one consumer and cross-talk with two.
- **A count without its predicate is not a measurement.** A `find -not -path '*/obj/*'` never matches on
  this platform (paths are printed with backslashes), so any count taken that way silently includes
  MSBuild's generated `AssemblyInfo`/`AssemblyAttributes` files. Count with `git ls-files` + `wc -l`
  instead: generated output is untracked, so it cannot leak in. Derived at `3b549d6`: library 35 files /
  5,381 lines (Kernel top level 27 / 4,180, `Kernel/Widgets/` 7 / 1,198, `Properties/AssemblyInfo.cs`
  1 / 3), harness 13 files / 4,152 lines / 63 named assertions, stub sources 4 files / 672 lines in 4
  assemblies. **These move within hours, not days** - two landed while this bullet was being written.
  Treat them as a snapshot of a command, never as a fact to quote.
- **The consumer's banned-substring list is five names, not six.** `UiSourceInvariantTests` forbids
  `UiInteract`, `Palette`, `SurfaceFrame`, `UiText`, `UiValueStore`
  (`../UniversalSqueaker/tools/UniversalSqueakerUiLogicTests/UiSourceInvariantTests.cs:103`). `UiPanel`
  appears in prose in both repos and in no source list anywhere. The error is in the safe direction - it
  over-bans, never under-bans - but it sends a session hunting for a rule that does not exist, and the
  phantom still lives in the consumer's own `MEMORY.md`.
- **Documentation describes a moment, not a state.** A commit anchor in a prose doc rots the next time
  code lands; during this tidy the tree moved `958ac7d → 44b00c5 → c2aca8e → b2006a0 → 3b549d6` in about
  thirty minutes, retiring a "measured at <sha>" claim twice. Prefer a re-derivable command and a date
  over an anchor nobody re-checks.

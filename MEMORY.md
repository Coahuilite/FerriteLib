# MEMORY

## Current durable state

- Repository split out of the Universal Squeaker tree on 2026-09-03. FerriteLib is a prerequisite mod,
  `coahuilite.ferritelib`, display name FerriteLib, `modVersion` 0.1.0. No remote, nothing published.
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
- Still unverified in game, both cheap, both in `TODO.md` §1: the guard's deliberate duplicate-DLL
  branch, and the failure shape when the carrier is absent. Everything else in this file remains
  compile-time, stub-harness or reference-assembly evidence — say so rather than implying a game run.
- **The pre-1.0 minor rule was widened, and it was not a documentation-only edit.** Observed in the
  working tree on 2026-09-04 while this file was being tidied, uncommitted: `FerriteLibVersion.Api`
  `0.1.0 → 0.2.0`, `About/About.xml <modVersion>` moving with it, and the comment's rule changed from "a
  minor bump IS breaking" to "**any** change to the public surface a consumer compiles against - addition
  or break - bumps minor". The trigger recorded in the code was an additive type shipped without an Api
  bump: an older installed carrier passed `Require`, and the desync detonated later as a
  `TypeLoadException` inside `UiHost.Draw` instead of as a readable prerequisite error. Recorded here as
  **in flight, not verified** - confirm against `Kernel/FerriteLibVersion.cs` and gate 1 before repeating
  either version, and note that every doc in this repo that says "0.1.0" or states the narrow rule
  (`AGENTS.md`'s two-version-axes invariant, `HANDOFF.md` §4) is describing the old one.
  The wider lesson is durable even if that commit never lands: **`Require`'s range assertion only protects
  a consumer if additive changes also move the axis.** Pinning minor is what turns "the carrier is older
  than the DLL I compiled against" from a draw-time crash into a reportable message.

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

## Gates and what each actually proves

Seven gates in `scripts/verify-local.ps1`: 1 harness, 2 Dev build, 3 Release build, 4 payload present,
5 content-free, 6 LICENSE, 7 About.xml identity. Gate 6 proves only what is visible from inside this
repo: the file exists, carries the MPL-2.0 title, still contains Exhibit B and section 10.4, and does
not apply the incompatibility notice in its header block. It does **not** compare the text against a
consumer's copy, and no script in this repo refers to a sibling repo at all (checked: no
`Get-FileHash`, no `..\` path in `scripts/`). The byte-parity assertion is consumer-side - US's own
gate 10 hashes both copies.

Dated correction, 2026-09-04: earlier text here credited gate 6 (as "the seventh") with a SHA-256
comparison to the consumer's copy. It never ran one. The consequence is not cosmetic: **a `LICENSE`
edited or truncated in this repo cannot turn any gate here red**; only a US gate run notices, and only
when the sibling tree is present. Anything that repeats "byte-identical to the consumer's copy" as a
property of this repo's gates is wrong in the same way.

The harness is 13 test files carrying 62 named assertions. (The older wording here was "13 lanes",
which counted files and read like an assertion count - prefer the explicit numbers.) Three assertion
groups are worth naming because they are the reason this repo can be trusted across a boundary:

- Version contract lane (10 assertions): range accept/reject, pre-1.0 minor-is-breaking, inverted range
  as a caller error, exact-API acceptance, duplicate-carrier detection driven through the internal
  decision function, range-vs-duplicate report separation, empty-copy-list safety, and the
  Api↔`modVersion` major/minor lock. Mutation-checked: breaking the duplicate condition and desyncing
  `modVersion` each fail exactly one lane.
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
- **A count without its predicate is not a measurement.** The figures in `HANDOFF.md` §3/§9 were written
  as "excl. obj/bin" while being produced by a `find`-and-sum that matched nothing (`find` prints these
  paths with backslashes, so `-not -path '*/obj/*'` never fires). The obj-inclusive and hand-authored
  numbers differ, and only one pair was labelled honestly. Restated 2026-09-04 with the predicate
  attached: hand-authored, git-tracked - library **35 files / 5,376 lines** (Kernel top level 27 / 4,175,
  `Kernel/Widgets/` 7 / 1,198, `Properties/AssemblyInfo.cs` 1 / 3), harness **13 files / 4,115 lines /
  62 named assertions**, stub sources **4 files / 672 lines** in 4 assemblies. Reproduce with
  `git ls-files` + `wc -l`, never with a `-path` filter.
- **The `UiSourceInvariantTests` list in `HANDOFF.md` §6 has a phantom entry.** The consumer's code bans
  five substrings (`UiInteract`, `Palette`, `SurfaceFrame`, `UiText`, `UiValueStore`); `UiPanel` is named
  in both repos' prose but is in no source list anywhere
  (`../UniversalSqueaker/tools/UniversalSqueakerUiLogicTests/UiSourceInvariantTests.cs:103`). Harmless
  direction of error - it over-bans, never under-bans - but it sent a session looking for a rule that
  does not exist.
- **Documentation describes a commit, and code keeps moving.** `HANDOFF.md` §9 anchored itself to
  `958ac7d` as "the last code commit before this handoff"; `44b00c5` landed roughly twenty minutes into
  this session's read and made three of its rows stale, including the assertion count in the file's own
  status line. A SHA anchor in a prose doc is not a guard against staleness, it is a claim about a moment.
  Prefer re-measuring to re-anchoring.

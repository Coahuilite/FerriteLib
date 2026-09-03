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
- Still unverified in game, both cheap, both in `TODO.md` §1: the guard's deliberate duplicate-DLL
  branch, and the failure shape when the carrier is absent. Everything else in this file remains
  compile-time, stub-harness or reference-assembly evidence — say so rather than implying a game run.

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

## Gates and what each actually proves

Seven gates in `scripts/verify-local.ps1` (the seventh asserts the series licence is present, un-truncated,
and byte-identical to the consumer's copy); the harness is 13 test files carrying 61 named assertions.
(The older wording here was "13 lanes", which counted files and read like an assertion count — prefer the
explicit numbers.) Three assertions groups are worth naming because they are the reason this repo can be
trusted across a boundary:

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

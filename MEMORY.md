# MEMORY

## Current durable state

- Repository split out of the Universal Squeaker tree on 2026-09-03. FerriteLib is a prerequisite mod,
  `coahuilite.ferritelib`, display name FerriteLib, `modVersion` 0.2.0. **Published 2026-09-07 in the
  rc-only window:** GitHub repo `Coahuilite/FerriteLib` (public), sole release `v0.2.0-rc1` (prerelease),
  its platform digest matched a local pack byte-for-byte; `/releases/latest` 404s, which is the correct
  state while only rc iterations exist. A bare `v0.2.0` was tagged the same day and **withdrawn the same
  day by maintainer ruling** - cutting the stable tag was outside the push authorization: the rc scheme
  exists precisely so "the trial is over" stays a deliberate decision (see `TODO.md` §5). Workshop still
  pending. History was rewritten once before the push (HANDOFF.md removed from all revisions; the buffer
  is now gitignored and local-only), so pre-rewrite lib hashes cited anywhere are stale. Maintainer
  ruling 2026-09-07 closes the follow-up duty: no repo in the series will be rewritten again to fix a
  hash citation and no more re-pointing - a dangling hash is archaeology, not damage. The one-time
  ledger stays at `.git/filter-repo/commit-map` for anyone who cares.
- Provenance: `Source/FerriteLib.UiKit/**` and `tools/FerriteLib.UiKit.Tests/**` were copied out of the
  US repo at US commit `6c7053a` after a file-for-file `diff -r` check, then amended there. US retains
  its own history; this repo's history starts at the split. `6c7053a` is the post-rewrite hash of what was
  `0fe60b0` until US's 2026-09-06 targeted blob rewrite (verified: same message byte-for-byte, reachable
  from US `main`; the old object is dangling locally only).
- **Cross-mod assembly binding is proven in a running game (2026-09-04).** US, shipping no FerriteLib
  payload of its own, loaded and ran its whole settings page and camera overlay with
  `FerriteLib.UiKit.dll` present only in the carrier mod, with no red text and no type-load failure. The
  sibling-`HintPath` + `<Private>False` design is therefore correct, and "ship a copy / go NuGet with
  Private=true" is rejected on evidence rather than preference. This was the top open risk and it is gone.
- **`UiPopup` is the single popup geometry and input primitive (2026-09-04, `4dd97bf`).** `RectFor`
  (below / flip above / pin to viewport top / horizontal clamp / unpublished-viewport pass-through)
  and `DrawOptionList` (panel, single-line option rows, `UiSession.SetPopupRect` publication, row
  hit-test, consume-close-callback) live in one place; `DropdownWidget` and the US composite
  `UsKernelDraw.Dropdown` both delegate to it. The publication is what makes
  `UiNative.DropdownButtonCore`'s yield guard able to fire at all — a consumer that redraws popup rows
  by hand and skips it silently reintroduces "covered trigger steals the option click", which is
  exactly the defect the US side reported and fixed the same day. Any new dropdown-shaped surface must
  go through `UiPopup`; a second copy of either rule is a defect, not a style choice.
- **Contract axis is 0.3.0 (2026-09-07, branch `feat/round-1-0.3.0`) and the bump rule is tightened**:
  pre-1.0, any change to the public surface — additions included — bumps `Api.Minor`, and
  `About/About.xml <modVersion>` moves with it. The 0.1.0 → 0.2.0 bump exists because an additive type
  (UiPopup) shipped without one and a consumer desynced into a TypeLoadException inside UiHost.Draw; the
  tightened rule is what makes Require's readable-report promise hold in both directions. The 0.2.0 →
  0.3.0 move is the entire US→FL round-1 surface (P1–P6 plus FL-side A–E) as **one** bump: a bump per
  item would make the axis measure churn instead of contract, and the release coupling the review resolved
  is one consumer migration against one carrier. The branch is not tagged and not released — cutting a
  stable stays a maintainer decision.
- **Pointer-space contract (2026-09-04, `72afa23`)**: inside scroll/group scopes `Event.current.mousePosition`
  arrives in the container's draw space, while popup rects are published in Host window space. Any
  comparison between the two must convert through the caller's `ctx` origin (`UiNative.PointerPositionIn`);
  comparing raw is the defect that stole every covered option click in game. The stub harness now models
  IMGUI group origins, hot-control capture/activation and per-pass control ids, and two pump lanes drive
  real event passes (flat and scrolled+offset); restoring the raw comparison fails the scrolled lane with
  the same trace signature as the in-game log.
- Still unverified in game, all cheap, all in `TODO.md` §1: the guard's deliberate duplicate-DLL branch,
  the failure shape when the carrier is absent, and — new with round 1 — the window shell against the real
  window stack, the two text paths that just came into the fit audit, and an engine-side recovery trip.
  Everything else in this file remains compile-time, stub-harness or reference-assembly evidence — say so
  rather than implying a game run.
- **Two hard rules inherited from the series, both easy to violate by accident.** Nothing in this library
  may persist data into a save - a prerequisite must survive being uninstalled, and a save-written flag is
  the one side effect a player cannot undo. And `../squeaky_ratkin` is never written: SR is a separate
  product with its own brand and `SR_` prefix, and this repo's neutrality lane exists to keep even its
  vocabulary out of here.
- **A `Require` desync is now a named verdict, and that is the durable part** (2026-09-04, `a05fddf`):
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
  ("Build axis in the csproj matches the other two axes", 66 named assertions total as of `382e862` + this
  change), and the assertion is **mutation-proven in one direction only**: reverting `<VersionPrefix>` to
  0.1.0 fails exactly that one assertion and nothing else. The reverse case - a future axis living in a
  fourth file - is guarded by no test, because no such file exists yet.
- **A release asset must be built after the gates run, not during them.** The Dev and Release build
  gates leave `1.6/Assemblies/FerriteLib.UiKit.dll` carrying a `-dev` version suffix, and that is the
  correct identity for a rehearsal — so the github and steam channels refuse it (`Payload is a dev
  build ...`, asserted in `stage-package.ps1` so the two cannot diverge on it) and the release workflow
  rebuilds with `-p:VersionSuffix=` between the gates and the pack to get the bare `0.3.0+<sha>`.
  Anyone wiring these steps in the other order gets a red pack step, not a mislabelled package.
- **Three channels, one staging engine — the consolidation of two copies of the same rules.**
  `stage-package.ps1` owns what a package *is*: a closed set of five allowed files, the content probe on
  named paths, the licence copy, `version.txt` (`build=` / `commit=` / source), and the optional
  deterministic archive. `pack-dev` / `pack-release` / `pack-steam` own identity and nothing else. The
  split they replaced had already drifted: the dev folder's `version.txt` carried no `build=` or
  `commit=` line, so an installed dev folder could not say which configuration built its DLL, and that
  had to be read out of the assembly's metadata by hand. The closed set is mutation-proved both ways —
  copying `1.6/` recursively instead of the one DLL trips "missing 1.6/Assemblies/FerriteLib.UiKit.dll",
  and a deliberately copied `.gitkeep` trips "carries files it must not". Deliberate asymmetries, each
  with a reason: dev tolerates a dirty tree and writes `-dirty` into its label; github has
  `-AllowDirtyTree` as a rehearsal hatch only; steam has none, because it is the last step.
- **Only the GitHub channel archives, and its archive is a function of the commit.** A dev rehearsal and
  a Workshop upload are folders used in place, so the entry-timestamp problem is confined to the one
  artifact whose digest is public. `Compress-Archive` stamps entries from staged mtimes, which staging
  itself rewrites — two packs of one commit gave different SHA-256s over identical content (measured
  2026-09-07) — and pinning mtimes first does not fix it, because NTFS re-dirties a directory during the
  compressor's own walk. So the timestamps are written into the archive: sorted enumeration, every entry
  at the tagged commit's author date, top-level `FerriteLib/` inside so a player unzipping into `Mods/`
  does not get a loose `LoadFolders.xml`. Re-verified after the consolidation by packing one commit twice
  across a clock tick and getting the same SHA-256 both times. No digest value is ever quoted in these
  files: it is a function of the commit, and a stale one reads like a live contract. The dev channel can
  still emit a zip on request (`-Zip`) for when a folder has to travel as one file; that one is not
  deterministic and nothing quotes its digest.
- **One OutputPath for two configurations is a silent wrong-artifact hazard, and it bit packaging
  directly (measured 2026-09-07).** Dev and Release both write `1.6/Assemblies/FerriteLib.UiKit.dll`.
  Right after a full `verify-local -PackDev` run the file at that path was a **Dev-configuration
  assembly of 98,304 bytes**, where a forced Release rebuild of the same commit is **91,136**: an
  incremental `dotnet build -c Release` reported "up to date" against `obj\Release` and never touched the
  payload, because up-to-dateness is judged per-configuration while the output path is shared. The
  packager copies *that path*, so without a forced rebuild it stages the wrong bytes under a
  `version.txt` that still reads dev+sha — indistinguishable downstream, and the staged folder is exactly
  what an in-game verification pass installs. Fixed by `--no-incremental`, verified end to end: the staged
  DLL was byte-identical to the payload, with `AssemblyConfigurationAttribute = Release` and
  `AssemblyInformationalVersion = 0.3.0-dev+<branch tip>`. General rule: **when two configurations share
  one output path, "the build said it was current" is not evidence about the bytes on disk** — compare the
  artifact, not the build log. The structural cure is the sibling repos' shape (one configuration, flavor
  by property, as SR's `SqueakyBuildFlavor`), but switching this csproj to it is a cross-repo change —
  the consumer's `build-dev.ps1` drives `-c Dev` on this project — so it belongs to a round, not to a
  packaging cleanup. The same sharing explains a stale `.pdb` beside the payload (Release sets
  `DebugType=none`, so it neither rewrites nor removes the Dev gate's pdb); the closed set now keeps it
  out of packages, which is why no strip step is needed for it.
- **GitHub policy does not constrain this shape of distribution** (read from `github/site-policy` and
  `github/docs`, 2026-09-05): Releases are documented as packaging software "for other people to download
  and use", with a stated limit of 1000 assets per release, 2 GiB per file, and **"no limit on the total
  size of a release, nor bandwidth usage"**. The AUP's excessive-bandwidth clause (§9) is a
  relative-to-similar-features test, and its 10 GiB/month figures are Git **LFS** quotas - which this repo
  does not use, because no binary is tracked at all. What the policies actually prohibit is using raw/file
  hosting as a hotlinked CDN and shipping malware; a 45 KiB mod zip is neither. Cross-repo consumption of
  a public release needs no special permission: `GITHUB_TOKEN` with `contents: read`, or a plain
  `browser_download_url`.

- **Licence shape across the series** (measured 2026-09-07): MPL-2.0 everywhere; `LICENSE` is
  byte-identical between this repo and Universal Squeaker (same SHA-256), while SqueakyRatkin carries
  the bare MPL text without the repo-level Exhibit A notice header — so "byte-identical in each repo"
  is true of the lib/consumer pair, not of the whole series. The notice deliberately carries no
  "Incompatible With Secondary Licenses" statement, so the assembly may still be combined with
  GPL-family mods. A distributed mod package is an *Executable Form*, so MPL 3.2's source-availability
  duty is met by `pack-dev.ps1` copying `LICENSE` into the package; gate 6 rejects a truncated paste
  or an applied incompatibility notice (and its limits are stated in "Gates and what each actually
  proves" — parity against a consumer's copy is consumer-side).

## What was verified, and how

- **RimWorld 1.6 UI surface**, read from `Krafs.Rimworld.Ref 1.6.4871` with dnlib (that package mirrors
  the game's `Data/Managed`, so the presence of an assembly there means it ships with the game):
  `Assembly-CSharp` has 16,158 types, 32 declaring `OnGUI`, 76 `Verse.Window` subclasses, 116
  `DoWindowContents` overrides, 123 `Dialog_*`, 261 `*FloatMenu*` types, and `Verse.Widgets` with 174
  methods / 152 public static. `Verse.Window` carries a `UnityEngine.GUI/WindowFunction` field, i.e. the
  game's window manager is an adapter over IMGUI's `GUI.Window`. It references `IMGUIModule` and
  `TextRenderingModule`, never `UnityEngine.UI` or `UIModule`.
- **`Verse.Window`'s overridable surface, read member-by-member from the same 1.6.4871 reference
  assembly while writing `UiWindowHost` — these four facts each cost a failed run when guessed.** The type
  is `public abstract`; `DoWindowContents(Rect)` is **public abstract** (so `Margin`-style protected
  assumptions do not transfer to it); `Margin` is **protected virtual, getter only** (a `sealed override
  float Margin => 0f` is how the shell takes the content inset away from the game); `InitialSize` is
  **public virtual, getter only**. The constructor is `Window(IWindowDrawing customWindowDrawing = null)`
  — a derived parameterless `base()` compiles onto *that* slot, so a runtime stub that declares only an
  implicit parameterless constructor dies with `MissingMethodException: Void
  Verse.Window..ctor(Verse.IWindowDrawing)`, which is exactly what the first shell-lane run reported.
  And `Close` is `Close(bool doCloseSound = true)`, not `Close()`: same failure class, same discovery.
  `WindowLayer` is `{GameUI, Dialog, SubSuper, Super}`. Read the signatures from the reference assembly
  with `System.Reflection.Metadata` (`PEReader` → `GetMetadataReader`; in PowerShell that is the
  extension method `[System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)`, not an
  instance method) before writing either side of a stub.
- **GitHub's prerelease state is a stored boolean, never parsed from the tag name** (verified via
  `gh api` against the live SqueakyRatkin data and the REST docs, 2026-09-05): `POST /releases`
  defaults `prerelease:false`, and SR's `v0.1.0-rc1`/`v0.3.2-pre1` carry `true` only because their
  workflow set it. `/releases/latest` is "most recent non-prerelease, non-draft, ordered by
  **created_at of the tagged commit**" (SR's later-created `v0.3.2-pre1` did not dethrone `v0.3.0`),
  and returns **404 when only prereleases exist** - which is the normal state during a bare-repo
  rc-only window, not an error. Release assets additionally carry a **server-computed
  `assets[].digest`** (`sha256:<hex>`), verified byte-equal against a local `sha256sum` of SR's own
  zip: the platform can attest to artifact identity, so a self-published hash is not the only line
  of evidence. Consequence encoded in code: `release.yml` sets the flag from the tag dialect AND
  `scripts/verify-release.ps1` re-checks the platform state after publishing (draft/flag/asset-name/
  digest/latest-pointer/tags-without-release), because a manual web-UI release that forgot the flag
  would otherwise promote an rc to "Latest" silently. Exit codes: 0 verified, 1 mismatch, throw =
  tag shape outside the rc scheme.
- Two consequences of the stored-flag fact, both confirmed against docs and the gate logic:
  (a) the web UI's pre-release checkbox is "Optionally … select" - never auto-ticked from the tag
  name - so a hand-clicked rc release defaults to STABLE and steals "Latest"; that is precisely
  the state `verify-release.ps1` step 2 catches. (b) A bad rc is fixed by deleting release **and**
  tag, then re-pushing the same rc number: the ordering gate recomputes maxRc from tags surviving
  on the runner's checkout, so re-cutting rc2 after deleting rc2 passes while rc3-after-rc1 fails.
  Immutable releases (SR's live releases show `immutable:false`, the default) would forbid exactly
  that retag path, so do not enable the setting while rc churn is the workflow.
- **The release zip's digest was a function of the build clock, not the commit** (measured 2026-09-07,
  fixed in `f6347d4`). `Compress-Archive` stamps each entry from the staged file's mtime, and staging
  rewrites those mtimes to now: two packs of one commit gave different SHA-256s over identical content.
  Pinning the tree's mtimes first does not fix it — NTFS bumps a directory's mtime whenever anything
  under it is touched, and the compressor's own traversal re-dirties directories mid-run (measured:
  files held the pinned date, `1.6/` still carried the wall clock). The fix writes timestamps into the
  archive directly (`ZipArchive.CreateEntry` + explicit `LastWriteTime` = the commit's author date,
  sorted enumeration). Verified: two packs 5s apart, `cmp` clean, `1B7D57E0…`. The DLL was never the
  problem — same-path and clone-path Release builds hash identically (`0c62df…`), and the earlier
  "clone inserts CRs" vector is closed by `.gitattributes`. Consequence for the rehearsal: the
  "CI zip SHA-256 matches a local pack of the same commit" check in `TODO.md` §5 is now meaningful;
  before this fix it could only pass by luck of timing.
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
  runtime; `string.Contains(string, StringComparison)` likewise does not exist at runtime. Two more
  found writing the containment lane, same class and same asymmetry (compile green, runtime red):
  **`Path.GetRelativePath(string, string)`** and **`string.TrimStart()`** with no arguments. The scan
  now walks leading whitespace by hand and computes the relative path itself. The rule that generalises
  these: a member the reference assembly advertises is a *claim*, and the runtime this harness actually
  executes against is `net472`, so only a run proves it.

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
  Evidence upgraded 2026-09-07 from source-level to **runner-proven**: US's `ci.yml` checks this repo out
  by canonical name and stages the WHOLE tree at the sibling path - its first run died on gate 13 with
  DLL-only staging while gates 1-12 passed, proving the stub sources and `LICENSE` are as load-bearing
  as the payload. The checkout is deliberately unpinned (tracks this repo's default branch, so lib-side
  breakage goes red in US's CI early); the release-body link is the pinned half - see `TODO.md` §5.
  **Round 1 grew that published surface twice, in both directions.** `VerseStubs` gained
  `Verse.Window`/`Verse.WindowLayer`/`Verse.IWindowDrawing` so `UiWindowHost` is drivable at all (P2
  condition b), and `UiWidgetRegistry.Clear` went **internal** (item D), so a consumer harness that had
  ever called it would stop compiling against the payload while every gate here stayed green. Neither is
  visible to a gate in this repo; both are exactly the shape `TODO.md` §3's "give the harness `Stubs/**`
  a local guard" item exists to catch, and that item is now load-bearing rather than tidy.

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
- **`UiThemeDraw.Label` is the single text outlet as of 2026-09-07; it was not before.** The two bypasses
  (`UiLayoutEngine`'s container `Title`/`TitleKey` band and `StepperSliderWidget`'s label and `−`/`+`
  glyphs) were deleted by round-1 item A, so `UiFitAudit.Check` now sees those strings and
  `BeginElement(entry.Path)` has something to attribute. Still a coverage fact, not a fixed bug: no
  overflow on either path has ever been observed, in game or in the harness. Two consequences of routing
  rather than re-implementing: those paths now set `Text.Anchor` explicitly (`MiddleLeft`, the outlet's
  default) where the deleted copies inherited the ambient anchor, and the harness audit tests still drive
  the outlet directly, so they cover the outlet and not the routes into it.
- **`UiWindowHost` owns window chrome; a consumer's window is a subclass, not a re-implementation.**
  Round-1 P2, and the only round-1 item that *shrinks* the escape surface instead of adding capability:
  a consumer forced to write its own `Window` keeps being tempted to write widgets there too, and the
  proven result was a settings window whose close button reached past the kernel. What the shell owns:
  exactly one `UiHost` (built inside the guarded pass, disposed on `PreClose`), the chrome paint (one
  theme, no second palette), the close affordance, and `Margin => 0f` so it insets its own content. What
  it never owns: the window's **size** — `InitialSizePolicy` is a provider whose unset default is the
  game's own `Window.InitialSize`, because freezing the first consumer's screen-fraction clamp would put
  its taste into the freeze surface. Modality (`forcePause`, `absorbInputAroundWindow`,
  `preventCameraMotion`, `layer`) is likewise left to the consumer to set.
  The failure contract is the part that looks wrong and is not: a pass that throws reports once,
  disposes its host and draws **nothing**; the notice appears on the *next* pass, drawn from a clean
  IMGUI state, and is terminal for the instance (no retry, no second host). A synchronous switch would
  repaint the notice inside the pass that already claimed layout for the page. Do not "improve" it.
  Chrome geometry virtuals (title band 56, padding 20, accent rule 3, close 110×30) are the shell's own
  layout, generalised from the first proven implementation and overridable — a different kind of thing
  from window size, and worth keeping distinct when a second consumer disagrees with them.
- **The hover-claim machine is the session's (P3), and its clock is IMGUI passes.** `Session.Frame`
  increments once per `BeginFrame`, i.e. once per IMGUI pass — **not** once per rendered frame. Say the
  unit out loud before sizing a grace window: the settings window opens with `forcePause`, where game
  time freezes and any seconds-based grace would never expire, which is why the consumer's hand-rolled
  machine was already frame-count based and why the proposal's `graceSeconds` was dropped during review.
  `HoverGraceFrames` is a session property the consumer sets and defaults to **0** (the plain per-pass
  clear, which is what the library did before), so no consumer's number became a library default.
  Two rules the tests pin, because both are easy to lose in a rewrite: a claim the boundary *restored*
  does not re-arm the grace window (that is what stops a superseded claim pinning the panel forever),
  and the pass immediately after a live claim is blank by design — a still-hovered widget re-claims
  inside it.
- **`UiThemeDraw.Solid` and `UiThemeDraw.RecoveryBand` joined the visual core in 0.3.0.** `Solid` is the
  library's only fill primitive, and widgets call it instead of `Verse.Widgets.DrawBoxSolid` — that is
  what makes the funnel short enough to gate. `RecoveryBand` is the fixed paint of a tripped element.
  Neither takes a session, so both stay on the visual-core side of the boundary.
- **Backend containment is a gate, not a convention, and the funnel is five files.**
  `KernelContainmentTests` scans `Source/**` for qualified calls to `GUI.` / `GUIUtility` / `Event.current`
  / `Mouse.` / `Text.` / `VerseWidgets` (plus the `Verse.`/`UnityEngine.`-qualified forms and
  `Verse.Widgets` spelled out) and requires each hit to be a name its file is allowed to reach. The list
  after item A is `UiNative.cs`, `UiThemeDraw.cs`, `UiLayoutEngine.cs` (its four structural scopes only),
  `UiSessionGuard.cs`, `VerseFerriteTextMetrics.cs`. Two rules keep the list honest: an allowlisted file
  that no longer reaches one of its granted names is a failure, and `UiWindowHost.cs` is deliberately
  **absent**, which is what makes the shell's "zero raw backend calls" acceptance line enforceable
  (mutation-proved: a planted `Verse.Mouse.IsOver` there reddens the lane with file and name).
  Two corrections against the review's own prose, both measured after A: the sketched list named
  `UiKitFonts.cs`, which has no backend call at all (it maps an enum), and item A's text named three
  violations where its acceptance line implied nine — `Widgets/` also held four `DrawBoxSolid` fills in
  the chart and one in the stepper, and the engine held a five-rect copy of `UiThemeDraw.Surface`. The
  acceptance line was the requirement; the enumeration was not.
- **Recovery belongs to the tree now (item C).** `UiLayoutEngine` wraps every element's Measure and Draw
  through `UiSessionGuard`: a throwing control records into its session slot (one log line per slot, with
  kind, path and stack), restores font/colour, and paints `UiThemeDraw.RecoveryBand` — a warning-toned
  band carrying its layout path, no strings the library does not own. Before this the guard existed and
  nothing in the library called it, so the documented contract was consumer opt-in fiction. Two details
  that cost a cycle each: the recovery key is the engine's arranged **entry path**, not
  `ctx.ElementPath` (the draw pass reuses the host context with only width and origin adjusted, so the
  context path is the page root for every element and would collapse every recovery into one slot), and
  the engine-facing entry points take the widget rather than an `Action`/`Func`, because the engine draws
  every element on every IMGUI pass and a delegate argument allocates a closure per call on the hottest
  path in the library. Consumer-facing delegate forms remain for controls whose fallback paint is the
  consumer's design.

## Ecosystem protocol — full form, provenance, and the metric

The AGENTS section carries the operational rules; this is why they exist and how they were derived, so
an edge case can be judged without re-running the audit that produced them.

- **Derived from measurement, not taste.** The FL-side adversarial audit (2026-09-07) graded the
  library retained-B / boundary-B / ecosystem-C; the same day's US compliance round produced the
  P1–P5 proposals. US's 16 `us/*` kinds are the *success* case — a consumer weighed the options,
  built, and stayed in the tree. The failures are exactly the raw-backend call sites: 4 in US
  (`Mouse.IsOver` at the settings-window close button, `UsKernelDraw` help-hover ×2, the Mod-settings
  shell button) plus one in FL's own `UiNative.IsFocusLost` — raw `Mouse.IsOver` beside its own
  private wrapper. Every one of the five traces to a missing primitive, which is why the ladder is
  "make the tree the cheap, reliable, visible path" and not "forbid the platform's only GUI API"
  (that one cannot be enforced by anything in .NET or in RimWorld).
- **The metric is tree-membership, not built-in usage.** The library can never ship every control —
  one manifest reference out of 17 shipped kinds (US's own pages, "Consumer coverage" above) is the
  proof that speculative coverage misses. What the library can own is identity, state, invalidation,
  recovery, and the audit surface. A bespoke kind inside a manifest page feeds all of those; a raw
  call replacing even a correct library kind feeds none. Baseline was 5 raw sites across both trees
  (2026-09-07); FL's own one (`UiNative.IsFocusLost`) is gone and FL's remainder is gated, so the
  count standing today is **US's 4**, measured in its tree, not claimed from this one. Target: 0
  outside registered exemptions, the first registered exemption being world-space rendering
  (`GenMapUI` class of calls — no session, no hit-test, permanent by ruling). The count is what the
  consumer's gate should report against `tools/dependency-reality.ps1` rule (c); this repo cannot
  measure it without depending on a consumer tree, which is the vacuous-guard shape already recorded
  twice in the neutrality lane.
- **Why exemptions carry rent.** An allowlist without a named closing item drifts back into permanent
  undocumented self-implementation; the live specimen is US's diagnostics panel — 704 lines of
  hand-rolled immediate UI borrowing only the theme vocabulary. Written ruling + capability gap + TODO
  reference turns each bypass into debt with a due date instead of a precedent.
- **Promotion-gate provenance, by item.** P1 (hover primitive), P2 (window shell), P3 (hover-claim
  machine) each cite consumer code that was forced to hand-roll them — that is the provenance test
  passing. The P2 ruling that US's 60–75 % / 800×600 clamp is consumer policy (size arrives as a
  provider, never as a library default) is the no-consumer-numbers clause applied before anything
  shipped. The gate's inverse also binds: `input/dropdown`, `input/stepper-slider`, `input/mode-row`
  and friends carry zero manifest consumption and are dealt with by the P5/§3 fix-or-delete, not kept
  "for symmetry".
- **Harvest loop — run once end to end, template set (2026-09-07).** Consumer compliance audit →
  HANDOFF round → FL review against code, per item a verdict / correction / refusal, line-cited → the
  accepted set lands as ONE pre-1.0 minor bump, shipped in lockstep with the consumer's migration
  commit → FL answers with the items it owes the consumer (self-containment fixes, the containment
  gate, engine-owned recovery, static retirement, the D-1 checker). The corrections are the point: two
  of five proposals misstated the very code they cited (`Dragging` is live, read by `LineChartWidget`;
  P3's grace machine is frame-count, not seconds, and there is no "frame counter the layout cache
  uses" to reuse — the cache is revision-keyed). Review discipline: verdict against code, not intent —
  and the same discipline applied to the review itself found two more (see the containment fact: an
  allowlisted file with nothing in it, and an enumeration narrower than its own acceptance line).
  Round lifecycle and section kinds are pinned in each repo's HANDOFF header; round numbers
  count on the library side and name the initiating consumer in full (`US→FL round 1`) —
  attribution is deliberate incentive, since this library grows only from consumer feedback.
  **Round 1 is CLOSED on the library side**: landed on `feat/round-1-0.3.0`, its bodies trimmed from
  the buffer, the permanent record here and in `TODO.md` §0. Closing was contingent on the work
  existing, not on the prose being filed — a trimmed round whose items are still open would be the
  triage failure the buffer header names. Not shipped: the tag waits for US's migration commit and for
  a maintainer decision.
- **What a closed round costs the next one.** Round 1 answered a consumer audit with six public-surface
  items and five library-owed items in one bump; that is the largest single change this repo has made
  and it stayed reviewable only because every item cited consumer code that already existed. The
  template's constraint is therefore not effort per item but evidence per item: an ask with no citation
  into a consumer tree does not enter a round at all, and this repo's owed items are owed items, not
  speculative features — the containment gate, engine recovery and the static retirement all existed as
  defects before anyone proposed them.
- **One-assembly stance reaffirmed under this protocol.** The visual-core/page-model split already
  passes a name-level gate; a second assembly would double the version axes to keep in step for zero
  new boundary. The re-open trigger stays exactly one: a consumer needs the visual core without the
  whole DLL.

## Structure and where things live

Paths and roles only; any line/file count here would be false within a day (see the predicate rule in
"Enduring corrections"). Re-derive with `git ls-files`.

- `Source/FerriteLib.UiKit/Kernel/` - the whole payload surface. `net472`, `TreatWarningsAsErrors`,
  `Nullable` on, output pinned to `1.6/Assemblies/` because that path is what consumers bind to.
  - Page model: `UiHost` (per-window façade, `IDisposable`), `UiWindowHost` (the window shell: chrome,
    one owned host, the deferred-notice failure contract), `UiSession`, `UiLayoutEngine` (the largest
    file by far), `UiLayoutManifest`, `UiLayoutSnapshot`, `UiBindings`/`IUiBindings`, `UiWidgetRegistry`,
    `KernelCoreWidgetRegistrar`, `UiWidgetContext`, `UiElementSpec`, `UiSessionGuard`, `UiValueState`,
    `UiNative` (the IMGUI interop seam), `UiPopup`, `UiChartPointChange`, the contract exceptions, and
    `Widgets/` (7 kinds).
  - Visual core: `UiTheme`, `UiThemeDraw`, `UiFitAudit`, `UiKitFonts`, `UiFont`, `ITextMetrics`,
    `VerseFerriteTextMetrics`.
  - `FerriteLibVersion` is BCL-only on purpose: no Verse, no UnityEngine, so a consumer can call
    `Require` from its earliest constructor and the harness can test it with no stubs at all. That claim
    now has one bounded exception worth knowing before it is repeated: since item E, `Require` reads
    `UiHostLedger`, which is itself BCL-only (a `List<string>` behind a lock, append-only, no reset), so
    the version lane still runs without loading a single stub — but it is no longer a *pure* function of
    its arguments. The pure decision stays in `Evaluate`, which is what the tests drive directly; the
    ledger line is added in `Require` only.
- `tools/FerriteLib.UiKit.Tests/` - a plain `Main()` runner (no test framework), lane files plus
  `Program` and `StubTextWidth`, with 4 standalone stub projects under `Stubs/`. Two of the lanes are
  gates over source text rather than over behaviour: `FerriteLibNeutralityTests` (product vocabulary)
  and `KernelContainmentTests` (backend contact, per-file and per-symbol allowlist). Both carry positive
  controls, because a scan over files is exactly the check that passes vacuously when a path moves.
- `tools/dependency-reality.ps1` - the D-1 reference checker FL owns the rule text for: AssemblyRef,
  page-model MemberRef contact, and declared-chrome allowlisting over a consumer tree, with `-SelfTest`
  proving the source scan can fire.
- `scripts/verify-local.ps1` (7 gates, `-PackDev` adds packaging) and `scripts/pack-dev.ps1`. The three
  source-text gates and the version axes all run *inside* gate 1; the gate count is not the check count.
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
- **Byte-comparison against this repo only means something if the consumer pins LF too.** US's licence
  gate compares its `LICENSE` against this repo's byte-for-byte; with only a corpus rule in its own
  `.gitattributes`, a windows-latest checkout (`core.autocrlf=true`) gave US 16127 B against this repo's
  15780 B - every CI run would have failed on line endings alone. US adopted `* text=auto eol=lf`
  (`eadb931`, 2026-09-07). Read back as a lib-side constraint: this repo's own `* text=auto eol=lf` line
  is part of the consumer contract - dropping it would silently break any consumer that hashes or
  byte-compares carrier files on a Windows runner.
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
  joined the tree on `4dd97bf`, after the guard was written, and appears in neither array of
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
  instead: generated output is untracked, so it cannot leak in. Derived at `12dacb4`: library 35 files /
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
  code lands; during this tidy the tree moved `958ac7d → 4dd97bf → 632a9a3 → a05fddf → 12dacb4` in about
  thirty minutes, retiring a "measured at <sha>" claim twice. Prefer a re-derivable command and a date
  over an anchor nobody re-checks.

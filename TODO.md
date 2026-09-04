# TODO

## 1. In-game verification — status after the 2026-09-04 session

The blocking risk is cleared. What remains is two branches that only change code if they surprise us.

- [x] **Cross-mod assembly binding.** PROVEN. US installed without any FerriteLib payload of its own
      resolved its `FerriteLib.UiKit` reference from the carrier mod, opened the settings page and the
      camera overlay, and logged no error. Consequence: the sibling-`HintPath` + `<Private>False` design
      stands, and shipping a copy / `Private=true` is rejected on evidence rather than on preference.
- [x] **`Require` passes silently** when exactly one carrier is loaded — observed as no red text.
- [x] FerriteLib appears in the mod list with no content side effects.
- [ ] **The carrier guard's duplicate branch.** Copy `FerriteLib.UiKit.dll` into the *installed*
      `Mods/UniversalSqueaker/1.6/Assemblies` (not the repo — three gates refuse it there, deliberately)
      and restart. Record **which** of three outcomes occurs; all three are valid results, so this is not
      a pass/fail test:
      1. `DUPLICATE CARRIER` naming both paths → the guard works as designed.
      2. No error and US runs → RimWorld deduped by assembly identity, so enumerating loaded assemblies
         cannot see the second copy. The guard is then dead weight in exactly the case it was written
         for, and it must compare file location/identity instead of counting names.
      3. The game errors before US's constructor runs → the engine rejects duplicate assembly names
         itself; the guard's value drops to build time, which the gates already cover.
      `Verse.ModAssemblyHandler` installs a global AssemblyResolve (measured), so outcome 2 is the
      likely one. **Do not read "no error" as a pass.**
- [ ] **Failure shape with the carrier absent.** Disable or remove FerriteLib and report what the player
      actually sees: a readable unmet-dependency message, a silent absence, or an exception wall. This is
      the path a real user hits first and the only remaining item about a user-visible outcome.
- [ ] **Bilingual overflow across two launches** (replaces the withdrawn "language switch mid-window").
      That item is unreachable: switching language requires a restart, the restart destroys the settings
      window, and `UiHost` with its cached snapshot is per-window, so a new window always sees a new
      language. `TranslationRevision` in the cache key is harness-proven insurance against a future
      in-place switch, **not** a fix for an observable defect, and must not be described louder than that.
      What only the game can still tell us is real glyph advance: launch once in Chinese and once in
      English, walk all five workspaces plus the overlay with detailed logging on, and confirm
      `usdiag evt=ui.text.overflow` stays silent.
      Optional 30-second edge: if `activeLanguage.folderName` changes when a language is *selected* while
      Keyed tables only take effect after restart, then opening Options from the main menu, switching and
      returning without restarting is a half-switched state. Look for visible misalignment; if none,
      record it unreachable and add no mechanism.

## 2. Second consumer: NivarianGrandStructure

- [ ] Before touching NGS: it builds against a machine-local game path, so decide whether it moves to
      the same RimRef + relative-reference scheme US and this repo use.
- [ ] First NGS slice should be a **dialog-shaped** surface (the operator-console family), not the
      tactical table. The table is a full-screen world-overlay window with hand-flowed rects and
      per-window private interaction state; it is the worst available first test of a page model.
- [ ] Settled by the maintainer, and it narrows the earlier separation proposal: the colonist-bar
      navigation affordance **stays in `MegastructureFramework`**, because the framework must at least
      let a player find a pawn inside a megastructure. Only the **window** moves to the consumer.
      Recorded consequence, so the rule is not written down as something it no longer is: "the
      framework owns capability and zero UI" is now false, and "zero translation keys" is
      unreachable - `MegastructureRecords.cs:87-88` owns `MSF_Navigation_Open` and
      `MSF_Navigation_OpenDesc`, the affordance's label and description. The boundary that survives is:
      **the framework may own the affordance that reaches an in-world thing, including its own two
      strings; it may not own a window, a layout, or a session.** Dependency direction is unaffected -
      `MegastructureFramework` still takes no reference to this library, because an affordance label
      needs no UiKit. Verify that holds when the work is actually done rather than assuming it.
- [ ] Record what the table needs from the library. If it needs only theme + drawing helpers + text
      metrics, that is the visual-core seam paying for itself; if it needs the session and the engine,
      the boundary claim in AGENTS.md is too narrow and should be revised from evidence.

## 3. Retained-mode roadmap (this library's own, after the split is proven)

Each item is expected to delete a workaround, not add a layer.

- [ ] **Element/identity layer.** Today widget instances are keyed by *path*, the tree is carried as a
      flat list plus `SubtreeCount` index arithmetic, and components hold no state. Introduce a real
      node object owning identity, state and dirty flags. Removes the path-aliasing hazard where two
      unnamed same-kind siblings share a path (`UiLayoutEngine` falls back to `Kind` when `Id` is empty,
      and `UiLayoutManifest` only enforces uniqueness for ids that are present).
- [ ] **A third operation on bindings: announce.** `IUiBindings` has get and set but no notification, so
      invalidation is a single global `ContentRevision` counter and its call sites are policed by
      reading source files and asserting on substrings. Give every key a revision, then delete
      `SessionRevisionBumper`, the help-panel's hand-written before/after text diff, and those
      source-text assertions.
- [ ] **Owned hit stack.** Produce a z-ordered `(rect, element)` list during arrange and dispatch input
      topmost-first. **Half done by `44b00c5`:** `UiPopup.RectFor` is now the only popup rect rule and
      `UiPopup.DrawOptionList` the only publisher of the covered rect, so the popup-specific coordinate
      conversions are gone. What is left is `YieldsToCoveringPopup` (`UiNative.cs:259`, still called at
      `:115`) and the previous-frame `OpenPopupRect` reasoning, which still model one popup rather than a
      stack of overlapping surfaces.
- [ ] **Close the fit-audit's two blind spots, or record them as exemptions.** `UiFitAudit.Check` runs
      only inside `UiThemeDraw.Label`, but `VerseWidgets.Label` is also called from
      `UiLayoutEngine.cs:1049` (the `Title`/`TitleKey` band on `Section`/`Surface` containers, reached at
      `:1028`) and from `StepperSliderWidget.cs:164` (its `Label`/`LabelKey` and the `−`/`+` glyphs).
      Overflow there is unreportable by construction, and `KernelTextAuditTests` cannot see it either
      because it drives `UiThemeDraw.Label` directly. Either route both through `UiThemeDraw.Label`
      (preferred - it deletes a duplicate rather than adding a layer) or state the exemption in the gate's
      own comment and stop calling `Label` the single text outlet. No evidence yet that a real string
      overflows on either path; do not describe this as a fixed bug.
- [ ] **Decide what to do with the four widget kinds nobody consumes.** `section/header`, `state/empty`,
      `input/mode-row` and `input/stepper-slider` are registered unconditionally by
      `KernelCoreWidgetRegistrar` and have zero references in the consumer's `Source/**` (grep, 2026-09-04).
      Before any freeze or public invitation, either give them a real consumer, delete them, or say in the
      page text that they are unproven. Right now the honest statement of this library's validated surface
      is "3 of 7 kinds", not 7.
- [ ] **Make the licence-parity guard two-sided, or stop calling it ours.** Gate 6 is credited, in prose
      that has since circulated between both repos, with comparing `LICENSE` against the consumer's copy.
      It does not - only US's gate 10 does
      (`../UniversalSqueaker/scripts/verify-local.ps1:141-146`), and it skips silently when our file is
      absent. A truncated or edited licence here cannot redden anything in this repo. Two clean options:
      pin the expected SHA-256 of the series licence as a literal in this repo and assert it locally
      (self-contained, no sibling dependency, and mutation-testable by editing `LICENSE`), or record the
      asymmetry as accepted policy. A sibling path check is not a third option: it would make this
      library's gates depend on a consumer tree, which is the vacuous-guard shape `MEMORY.md` ("Neutrality
      lane") records as the reason the neutrality scan was moved inside this repo in the first place.
- [ ] **Give the harness `Stubs/**` a local guard.** The consumer's `UniversalSqueakerKernelHostTests`
      builds our four stub projects by relative path and copies them out of `bin/stubs/<name>/`, so that
      tree is a published surface. Renaming a stub project or its output folder breaks the consumer while
      all seven gates here stay green. A five-line assertion that the four project paths and their expected
      assembly names exist would close the hole without introducing a cross-repo dependency.
- [ ] **Keep the boundary guard's symbol list alive, or make it transitive.**
      `VerifyVisualCoreIsPageModelFree` rejects lines naming fourteen hand-maintained page-model symbols in
      seven visual-core files. `UiPopup` (added on `44b00c5`) is in neither list, so
      `UiThemeDraw → UiPopup → UiSession` would pass while breaking the "usable without a Host" claim the
      lane exists to hold. Cheapest fix: add `UiPopup` to the page-model array and mutate-test it by
      planting a `UiPopup` call in a visual-core file. Real fix, later: derive the page-model set from the
      types instead of a list nobody remembers to update.
- [ ] Clean up `UiLayoutManifest.ParseFile`: it has no production caller. Either wire a real
      load-from-disk path (which is the only thing that would make XML authoring worth its cost) or
      delete it and the `Schema="2"` version slot with it.

## 4. Deferred by decision, with the upgrade path written down

- [ ] **Package feed / registry — deliberately not now.** Measured reason in `MEMORY.md`: same-version
      republish is not re-fetched, and build metadata is stripped from the cache identity, so a
      hash-suffixed dev version provides false freshness. Revisit when any of these is true: a remote
      exists, a second machine builds consumers, or an outside party consumes the library. Upgrade path
      if revisited: `0.1.0-dev.<sha>` prerelease label + floating consumer version + drop
      `--no-restore` from the cross-repo gates (otherwise they go green against a stale graph).
      GitHub Packages was checked and rejected for a different reason: it requires a token to *install*
      even public packages.
- [ ] Theme token restructure to per-surface (fill, border) pairs, so a consumer can express a chrome
      family other than DarkGold's — including the game's own, which today is not representable. Do
      this before freezing, since it is breaking.
- [ ] `UiTheme` public-surface freeze, per its own rule: when a second real theme exists.
- [ ] Optional experiment: whether RimWorld tolerates an unknown tag in `About.xml`. The parsed tag set
      is closed (`ModMetaDataInternal`, 24 names) and no tolerance could be proven from stripped
      metadata, so nothing here depends on it; if someone wants it, it is a two-minute in-game test.
- [x] License settled: MPL-2.0 across the series, `LICENSE` verbatim and without the Exhibit B
      incompatibility notice, copied into the distributed package by `pack-dev.ps1`.

## 5. Publication — form decided 2026-09-05: two repos, two release pages, linked not copied

**The decision.** FerriteLib publishes its own GitHub Release and stays the single carrier of
`FerriteLib.UiKit.dll`. Universal Squeaker publishes its own release containing only its own package, and
**every US release body links to the specific lib release it was compiled against**. Players therefore
install two mods from two pages; no DLL is ever duplicated, rebuilt by a foreign pipeline, or re-attached.

Workshop is still the later step, unchanged from the 2026-09-04 decision: FerriteLib gets its own page and
US is released in lockstep with it. GitHub-first early testing does not pre-empt that, and it does not make
the library referenceable-by-strangers any more than a stable packageId will — see the invited/unsupported
item below, which remains the one irreversible call.

- [ ] **Pre-push privacy scan on the full reachable history** (this repo has no remote yet, so the scan
      runs *before* the first push rather than as a cleanup after it). Measured clean on 2026-09-05 at
      `fc59b60`: zero absolute paths in any reachable blob, zero emails/tokens/15-digit ids, no binary ever
      added on any commit (`git log --all --diff-filter=A`), single commit identity
      `Coahuilite <19252128+Coahuilite@users.noreply.github.com>`. Repeat the scan if anything lands between
      then and the push. The sibling repository paid for this lesson already: its published tags still reach
      a deleted `.slim/codemap.json`, and cleaning it needs a force-push plus tag rebuild.
      `git grep -I -E "[A-Za-z]:[\\\\/](Users|WorkSpace)" $(git rev-list --all)`.
- [ ] **Name the remote repository `ferritelib`, lowercase.** Three places in US pin that exact name and
      layout: `UniversalSqueaker.csproj:49` (`..\..\..\ferritelib\1.6\Assemblies\FerriteLib.UiKit.dll`),
      `UniversalSqueakerKernelHostTests.csproj:19` (`FerriteLibHarness`), and its `verify-local.ps1:37`
      (`$carrierDll`) plus the LICENSE comparison at `:141`. A `FerriteLib`-cased remote works by accident
      on Windows and macOS and breaks on a Linux runner; keeping it lowercase also matches the packageId's
      last segment. Display name stays "FerriteLib".
- [ ] **Create the repository and push** - **external action, needs maintainer authorization.** Suggested
      shape, one line each:
      `gh repo create Coahuilite/ferritelib --public --description "RimWorld 1.6 prerequisite mod: the FerriteLib.UiKit shared UI library. Ships no game content."` →
      `git remote add origin <ssh url>` → `git push -u origin main`.
      Then the first rehearsal release: tag on `main`'s tip only (`release.yml` rejects an off-trunk tag),
      `git tag v0.2.0 && git push origin v0.2.0`, and confirm the Actions run produces
      `FerriteLib-v0.2.0.zip` whose SHA-256 matches a local `pack-release.ps1` run of the same commit.
      Note the token in use has scopes `gist, read:org, repo` and **no `workflow` scope**, so the first push
      that includes `.github/workflows/*` may be rejected; grant the scope or add the workflows through the
      web UI before pushing them.
- [ ] **Fill `About/About.xml <url>`** (currently empty at `:25`). It is the only pointer a player or
      modder gets inside the game, and there is no Workshop id to put there yet. Do not add a
      `<steamAppId>` before any Workshop upload.
- [ ] **US side: the link itself** - **cross-repo write, needs maintainer authorization.** The mechanism is
      one derived line in US's release body plus one pin, and it must be derived from a single source so the
      two cannot drift:
      `PrerequisiteApiMin` / `PrerequisiteApiMax` in `UniversalSqueaker/Source/UniversalSqueaker/Mod.cs:22-23`
      (currently `0.2.0` / `0.3.0`) is the truth for which lib range US was compiled against, so the link must
      be **resolved from that range, not string-built from the floor**: `v0.2.0` is the minimum, but if lib
      has since shipped `v0.2.3`, linking to `tag/v0.2.0` sends players to an older carrier than the one US
      was tested against. US's `release.yml` should list lib's releases, take the highest whose base version
      satisfies `>= min` and `< max`, and then fail the release if (a) no release satisfies the range, (b) the
      chosen release is a prerelease while US is being released as stable, or (c) a lib payload exists in US's
      own stage directory. (c) already exists as `stage-package.ps1:41-45` and US gate 9; both stay valid
      under this decision unchanged. Pin the resolved tag into the release body, so the pairing is a published
      fact rather than something a reader has to recompute.
- [ ] **US's CI needs the sibling checkout pinned to a path, not just a repo.** Two repos in one runner
      workspace: `actions/checkout@v4` with `repository: Coahuilite/ferritelib` and
      **`path: ../ferritelib`** (default path would be `ferritelib` *inside* the workspace and every pinned
      relative reference above would miss). Add `dotnet restore` for each project before
      `verify-local.ps1` - see the `--no-restore` note in `MEMORY.md`.
- [ ] **The four widget kinds nobody consumes** (`section/header`, `state/empty`, `input/mode-row`,
      `input/stepper-slider`) become public on the day this repo is public. See §3 for the decision; it is
      cheaper to make before the first release than after somebody compiles against them.
- [ ] **The one thing this decision actually changes: publishing makes the library referenceable by
      strangers.** With lockstep releases and one consumer, a breaking change is genuinely fine - our own
      mismatch path already degrades to a readable `Require` error rather than a crash. What is not
      reversible is that a Workshop page with a stable packageId turns `FerriteLib.UiKit` into something
      any modder can compile against, and from their first build our breaking edits become their breakage.
      That is the real one-way door, and it is independent of our own discipline.
      Decide before the upload, in one line on the page: is third-party use **invited** (then §3 and the
      per-surface theme restructure in §4 should land first, because they are breaking by their own
      admission and will stop being cheap afterwards), or **unsupported** (then we may keep breaking it
      for as long as US is the only consumer, and say so plainly). Both are fine; discovering the choice
      after someone builds on us is the only bad outcome.
- [ ] A no-content library mod will draw "what is this doing in my mod list". Its `description` has to
      answer that in one sentence, name the mods that need it, and say plainly that it has no content of
      its own and must not be uninstalled while a consumer is present.
- [ ] `About.xml` currently carries a placeholder-grade description and no preview. Both are player-facing
      on a Workshop page and are not yet written.
- [ ] **No `README.md` and no `CONTRIBUTING.md` exist yet, and the first push makes the absence
      conspicuous.** The release body written by `release.yml` now carries the player-facing explanation
      (what a no-content prerequisite is, how to unzip it, why not to copy the DLL), so a README is not a
      release blocker - but a public repository with no README reads as abandoned, and "what is this doing
      in my mod list" needs answering somewhere a browser can find without opening a release. Scope it to
      what is true: what the two layers are, that it ships no content, that the shipped payload is one
      DLL, how to verify locally (`pwsh scripts/verify-local.ps1`, 7 gates), and which mods need it. Keep
      counts out of it, same rule as `AGENTS.md`.
- [ ] **Decide whether third-party use is invited, and let that decide `CONTRIBUTING.md`'s existence.**
      If invited, `§3` and the per-surface theme restructure in `§4` land first and a contributing guide is
      part of the offer. If unsupported, say so in the README and do not write a contributing guide that
      implies otherwise. This is the same one-way door as the Workshop page above, and GitHub publishing
      opens it a little: a stable packageId plus a browsable repository is what a modder compiles against
      whether or not anyone invited them.

## 6. Documentation hygiene: what this audit found, and the rule that keeps it from coming back

Every item below was produced by reading code and both repos' scripts, not by reading a document. Four
claims that were circulating in prose turned out not to hold; all four are corrected in `MEMORY.md`, and
these are the follow-throughs.

- [ ] `About/About.xml`'s header comment carries two stale claims: **"License stays undecided by
      maintainer"** (MPL-2.0 was adopted in `5a2fb72` and gate 6 enforces the text) and consumers binding
      **"through the local NuGet feed"** (the scheme is a sibling `HintPath` + `<Private>False`; §4 keeps
      the feed deliberately off). The comment ships inside the mod package, so it is a durable text a
      reader will meet. Fix it together with the `description` rewrite in §5.
- [x] Gate 6's licence claim corrected in `MEMORY.md`: it is the sixth gate, and it asserts local text
      structure only. The cross-repo parity half belongs to the consumer, so `MEMORY.md` must keep naming
      it that way.
- [x] Consumer banned-substring list corrected to five names; the phantom `UiPanel` still sits in the
      consumer's own `MEMORY.md:81` prose and will travel back into this repo from there unless fixed at
      source. Worth a one-line correction over the fence next time that repo is open (its docs, not its
      code).
- [ ] **Standing rule, once this repo has a Workshop page:** a gate's *capability* is whatever its script
      does, and the only way to keep prose honest is to name the file and line range when claiming one.
      "Gate 6 rejects divergence from a consumer's copy" survived one full revision cycle because it read
      like a confident summary of a check nobody re-opened. The same class of error is what `MEMORY.md`'s
      neutrality lane records twice, in the other direction.
- [ ] Open question worth one line of policy: is a one-directional series guard acceptable at all, given
      that the neutrality scan was moved *into* this repo precisely because a consumer-side check passed
      vacuously after the split? The actionable version is §3's two-sided-guard item.

**Method note for whoever picks this up.** Derive every number with `git ls-files` + `wc -l` / `grep -c`,
and every gate claim from a read of the named range. A figure or capability repeated from prose inherits
whatever error the last author measured into it - which is how all four defects above survived a day.

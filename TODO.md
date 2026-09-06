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

## 2. Second consumer: the withheld sibling mod

- [ ] Before touching it: it builds against a machine-local game path, so decide whether it moves to
      the same RimRef + relative-reference scheme US and this repo use.
- [ ] First slice should be a **dialog-shaped** surface (the operator-console family), not the
      tactical table. The table is a full-screen world-overlay window with hand-flowed rects and
      per-window private interaction state; it is the worst available first test of a page model.
- [ ] Settled by the maintainer, and it narrows the earlier separation proposal: the colonist-bar
      navigation affordance **stays in `its framework`**, because the framework must at least
      let a player find a pawn inside a megastructure. Only the **window** moves to the consumer.
      Recorded consequence, so the rule is not written down as something it no longer is: "the
      framework owns capability and zero UI" is now false, and "zero translation keys" is
      unreachable - `its records file` owns `the affordance's label key` and
      `the affordance's description key`, the affordance's label and description. The boundary that survives is:
      **the framework may own the affordance that reaches an in-world thing, including its own two
      strings; it may not own a window, a layout, or a session.** Dependency direction is unaffected -
      `its framework` still takes no reference to this library, because an affordance label
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
      topmost-first. **Half done by `4dd97bf`:** `UiPopup.RectFor` is now the only popup rect rule and
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
      seven visual-core files. `UiPopup` (added on `4dd97bf`) is in neither list, so
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

- [x] **Pre-push privacy scan on the full reachable history — executed clean before the 2026-09-07 push.**
      Final run at the pushed tip `bdbeae0` via `scripts/privacy-audit.ps1 -FullHistory`: working tree 0,
      commit messages 0, all 34 historical revisions 0, single noreply identity. The scan's own history
      lesson stands: the 09-05 measurement at `382e862` was real but did not survive the next day's own
      session - a TODO note quoting US's literal personal path put one dirty blob into history, caught
      only by re-running the blob scan (a working-tree fix commit does not remove it). Disposed by
      squashing the two tip commits; the squashed hashes are deliberately not cited - they no longer
      resolve. **Each vector must be re-scanned after any commit lands; a clean tree says nothing about
      history, and a scrubbed file says nothing about the blob its dirty version already became.** The
      sibling repository paid for this lesson twice.
- [x] **Repository name ruled by the maintainer 2026-09-07: `FerriteLib`, PascalCase** — matching the
      series convention (`SqueakyRatkin`, `UniversalSqueaker`, both measured live on GitHub). The earlier
      "lowercase or a Linux runner breaks" argument is void on evidence: GitHub resolves owner/repo
      case-insensitively (measured: `repos/Coahuilite/squeakyratkin` redirects to `SqueakyRatkin`), US's
      workflows reference the carrier via `repository:` (an API lookup) plus a literal checkout
      `path: ci-ferritelib` that no repo-name case affects, and both runners are windows-latest anyway.
      Machine identity stays lowercase where it is load-bearing: packageId `coahuilite.ferritelib`
      (save-data reference, immutable) and the local sibling directory `ferritelib` (what the csproj
      HintPath actually resolves). Creating `Coahuilite/FerriteLib` also squat-proofs the variants:
      GitHub forbids a second case-variant under one owner, so every misspelling redirects here.
      Availability measured 2026-09-07: both variants free under the owner, zero global name collisions
      (`Ferrite*` hits are unrelated projects), the `FerriteLib` user/org handle is free, and NuGet
      `FerriteLib` / `FerriteLib.UiKit` are both unclaimed (404) — relevant only if §4's feed ever turns on.
- [x] **Repository created and pushed 2026-09-07 (maintainer authorization this session).**
      `gh repo create Coahuilite/FerriteLib --public` → SSH remote → `git push -u origin main` at
      `bdbeae0`. The token's missing `workflow` scope never bit: the push went over SSH, and both
      workflow files landed and ran (measured: `contents/.github/workflows` lists ci.yml + release.yml).
      Rehearsal executed in the rc dialect: `v0.2.0-rc1` tagged on the tip, Actions Release run green,
      **the platform's server-computed digest `sha256:e8a54be6…` equals a local `pack-release.ps1` run
      of the same commit byte-for-byte** — the digest-determinism fix from this morning is what made
      that check meaningful; it would have failed on timing before it. Bare `v0.2.0` then tagged the
      same commit (final-state anchor held), its release verified by `scripts/verify-release.ps1`
      (published / flag stable / single asset / latest=v0.2.0 / no dangling tags, exit 0). CI on main
      green on the first run.
- [x] **`About/About.xml <url>` filled 2026-09-07** with `https://github.com/Coahuilite/FerriteLib` —
      the naming ruling made the url a pure function of a decided value. It is the only pointer a player
      or modder gets inside the game. No Workshop id exists yet, and no `<steamAppId>` goes in before any
      Workshop upload. If the repository is ever renamed, this line and the two pack scripts' source
      pointers move together.
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
      After US's first release, run `scripts/verify-release.ps1` from this repo against it
      (`-Repo Coahuilite/universalsqueaker -AssetPrefix UniversalSqueaker`): the checks are generic
      (prerelease-flag-vs-tag, draft, single asset, digest, latest-pointer, dangling tags) and US has
      the same rc exposure once its own trials start.
- [x] **Cross-repo hash anchor applied 2026-09-07.** US executed its targeted blob rewrite and reported
      the commit-map mapping `0fe60b0 -> 6c7053a`; `MEMORY.md:8` now cites the new hash with the old one
      recorded as its pre-rewrite name. Verified independently from this repo: `6c7053a` is reachable from
      US `main` and carries the identical message; `0fe60b0` resolves only as a dangling local object.
      US's one-time guide was deleted pre-push (their ruling; survivors folded into US `MEMORY.md`), so
      the method pointer is `modding_documents/privacy-debt-vector-triage-zh.md`, not the guide.
- [x] **US's CI dependency chain — settled by US's own session, superseding this sketch.** What was
      written here (`path: ../ferritelib`) is impossible: `actions/checkout` resolves `path` inside
      `GITHUB_WORKSPACE` and throws on anything outside (US verified against the action's own
      `input-helper.ts`). US landed the working variant — carrier checkout to the `ci-ferritelib/`
      subdir, build there, run-step copy to the sibling path the HintPath expects (path math verified
      in US `MEMORY.md`). Follow-through for the name ruling above: US's two workflows still say
      `repository: Coahuilite/ferritelib` — case-insensitive resolution means it works either way, but
      align it to `Coahuilite/FerriteLib` when US next touches those files. **Cross-repo write: report,
      do not edit.**
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
- [~] `About.xml` description rewritten 2026-09-07 (bilingual, answers "what is this doing in my mod
      list", says not to uninstall while a consumer is present). **Preview image still missing** - it is
      a Workshop-page asset and the Workshop step is undecided; nothing player-facing is published
      without it.
- [x] **README landed with the push 2026-09-07**: `README.md` + `README.zh-CN.md`, bilingual interlinked,
      scoped to what is true (two layers, no content, one DLL, verify commands, which mods need it), no
      counts. `CONTRIBUTING.md` deliberately **not** written - its existence is the item below's output,
      and the README states the currently-true stance (bug reports welcome, PRs not promised while the
      surface is provisional).
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

- [x] `About/About.xml`'s header comment stale claims fixed 2026-09-07: "License stays undecided" now
      names MPL-2.0, "local NuGet feed" now describes the actual sibling-`HintPath` + `<Private>False`
      scheme, and the comment records the repository-name ruling next to the display-name confirmation.
      The comment ships inside the mod package, so it is a durable text a reader will meet.
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

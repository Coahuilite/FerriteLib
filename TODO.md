# TODO

## 0. No open HANDOFF rounds; the round-1 release and the one remaining cross-repo report (branch `0.3.x`)

- [x] **US→FL round 3 (2026-09-08) — CLOSED, implemented 2026-09-09.** Filed as "round 2", renumbered
      to 3 under the global-counter rule. Verdicts (buffer, `996a6bc`-verified): N1+N2 accepted as ONE
      package, N3 closed as verdict (b). **Maintainer refiled 0.4.0 → 0.3.0 (2026-09-09): the release
      never shipped, so its contract axis has no goalpost to move.** Landed on `0.3.x` with three
      evidence-forced deviations (MinWidth/MaxWidth not Min/Max — value-range collision; Width joins
      the common widget attributes — the engine read it while no schema allowed it; narrow-state
      attributes refused without a governing Breakpoint) — all three in `MEMORY.md` round-3 entry,
      which is also the permanent record. US's own docs still say 0.4.0 and still date this round as
      pending scheduling: report, never edit, and hand it over at the next FL session rather than
      waiting for the sibling to notice (see the bullet below and `MEMORY.md`).
      The pointer retires with this check: nothing from round 3 stays open on the FL side.

Round 1 is SCHEDULED and, on this branch, implemented: P1–P6 (P3 landed here rather than slipping to
0.3.x — it is session-scoped, provenance-cited and lane-tested, and holding it back would cost US the
lockstep migration) plus FL-side items A–E, on the 0.3.0 contract axis. What is left is release work,
not design work.

- [ ] **Ship 0.3.0 — the last gate is the maintainer's trial decision, nothing else.** US's migration
      commit landed (`7777cbe`, gate re-verified by FL read-only: pin `[0.3.0,0.4.0)` at `Mod.cs:27-28`,
      thin `UiWindowHost`, zero raw hovers, gate 14 green), so the lockstep precondition round 1 set is
      **met**. The branch is now `0.3.x` (renamed 2026-09-09 when the round-3 package joined 0.3.0):
      0.3.0 = P1–P6 + FL-side A–E + N1/N2 responsiveness + the measured slider label. Tag dialect
      unchanged: `v0.3.0-rc1` at the then-tip, bare tag only on the commit that ends the trial
      (`MEMORY.md`, publication state). Remote merges are merge-commit only (squash/rebase buttons
      disabled server-side 2026-09-09); merging into `main` and the tag itself stay maintainer calls.
- [ ] **Cross-repo follow-through — report, do not edit** (`AGENTS.md` Boundaries). Remaining after
      `7777cbe`: (a) CLOSED by re-derivation 2026-09-08 — the phantom `UiPanel` is resolved at source:
      US's scan now lists six names including `UiPanel`
      (`UiSourceInvariantTests.cs:153`, commented as the round-2 fix), and its MEMORY's two prose hits
      describe that list accurately. The same re-derivation that closed it nearly missed it the other
      way: the scan lives in `tools/`, not in `scripts/*.ps1`, so a scripts-only grep reports "no scan
      at all". See `MEMORY.md` enduring corrections; (b) its release body gains the lib-release link
      only once this repo publishes a 0.3.0 asset — blocked by the bullet above, not by US; (c) **added
      2026-09-09 after a live re-derivation of both trees**: US's docs now lag FL's round-3 state in two
      ways — its `HANDOFF.md:5` still labels the round "REVIEWED, pending scheduling" although FL closed
      and implemented it (PR #1 merged, so `main` carries the round-3 surface), and `TODO.md:9,46,60`
      pair that stale status with the withdrawn "0.4.0 package" label — **DELIVERED and fixed 2026-09-10**:
      the maintainer authorised a cross-directory read followed by direct edits to the sibling, and the
      corrections landed as US `2951934` (its `TODO.md:8,9,46,60`, one durable capability line in its
      `MEMORY.md`, and its local buffer's FL-state summary plus a dated "FL 递交" section recording every
      line this session changed there so a US session can audit or revert them). It is closed *because it
      was applied*, not because it was reported: US had rewritten the very same files on 2026-09-09 with
      the refile already recorded here, and none of those lines moved — see `MEMORY.md`, "A report-only
      item with no delivery channel is not queued, it is lost". What stays open on this axis is only (b),
      the lib-release link in US's release body, which waits on this repo's own `v0.3.0` cut.

## 1. In-game verification — the round-3 package makes this the next thing to do (2026-09-09)

- [ ] **GO TO THE GAME SOON. This section is now the critical path, not the backlog.** The merge of
      PR #1 put the first geometry-changing surface in front of a game: Auto columns size from
      measured glyph advance, and `Breakpoint`/`Narrow`/`Cols`/`NarrowHidden` re-arrange live on
      width. The harness proves them against `StubTextWidth`'s linear character-count model, which is not
      the game's ruler (`MEMORY.md`, "Text fit: the harness measures a model, not a font"), so every lane
      here is a future-regression guard and not a mutation proof of in-game geometry. The branch sync of
      2026-09-09 ff'd `0.3.x` to the PR #1 merge; `0.3.x` has carried docs-only commits since, so the
      check is a predicate and not a SHA pair:
      `git diff --name-only main 0.3.x` must return `.md` files only, and that is what "the tested bytes
      and the shipped bytes are one tree" means. **The procedure and the seven live checks are a form, not
      more prose: `docs/in-game-walkthrough.md`.** Fill one row per check there and write the outcome into
      this section — a blank row is not a pass, and "no error" is not one either (§1's duplicate-carrier
      check has three valid outcomes and the point is to learn which one the game picks).

The blocking risk is cleared. What remains is two branches from the split plus three new ones the
round-1 surface introduced; every one of the five only changes code if it surprises us.

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
- [ ] **The window shell has never been opened in a game.** `UiWindowHost` is compile-verified against
      `Krafs.Rimworld.Ref 1.6.4871` and lane-verified against the `Verse.Window` stub slice, and the two
      signature facts that killed the first harness runs (`Window..ctor(IWindowDrawing)` and
      `Close(bool doCloseSound = true)`) were read from the reference assembly rather than guessed. That
      is still not a game run: the shell must be opened once through the real window stack, where
      `WindowOnGUI`'s actual group/matrix plumbing, `layer = Dialog` ordering and the close-sound path
      are all live. A signature the ref assembly and the stub agree on and the executable disagrees with
      shows up here and nowhere else.
- [ ] **Two text paths just came into the fit audit.** Container titles and the stepper-slider's label
      and `−`/`+` glyphs used to bypass `UiFitAudit.Check`; item A routed them through
      `UiThemeDraw.Label`, so they can now report overflow they never could report before, and they also
      changed vertical anchor from ambient to `MiddleLeft`. Walk the pages with detailed logging on and
      look for newly-reported overflows and for visible title misalignment. Absence of both is the
      result to record; either surprise reshapes A, not the audit.
- [ ] **Engine-owned recovery has never tripped in a game.** Item C moved per-widget recovery into the
      tree, so a throwing core widget now paints a warning band with its layout path and trips its
      session slot. The band's paint (theme `Warning` fill, `TextOnDanger` path text) has only ever been
      seen in a stub. Force one trip by hand — a temporary consumer widget that throws — and confirm the
      page keeps drawing around it and the log line appears once per slot.

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
- [x] **Responsiveness package (US→FL round 3, N1+N2) — landed 2026-09-09 on `0.3.x`, refiled from
      0.4.0 to 0.3.0 by maintainer order.** Shipped shape and the three evidence-forced deviations
      (`MinWidth`/`MaxWidth`; `Width` into the common widget vocabulary; narrow-state attributes
      refused without a governing `Breakpoint`) are recorded in `MEMORY.md`'s round-3 entry, with the
      harness lanes as the acceptance evidence (glyph-model positive control, one-engine narrow→wide
      re-arrange, `Cols`/`NarrowCols` grid, seven creation-time refusals). The N3 consequence landed
      too: `input/stepper-slider`'s label band is measured, not the 80f constant.

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
- [x] **Fit-audit blind spots closed by round-1 item A (2026-09-07, this branch).** Both bypasses went:
      the engine's private `DrawLabel` and `StepperSliderWidget`'s copy now call `UiThemeDraw.Label`, so
      `UiFitAudit.Check` sees container titles and stepper glyphs, and `Label` can honestly be called the
      single text outlet again. The engine also lost its hand-copied border rule (`DrawSurface` →
      `UiThemeDraw.Panel/Base`). Recorded consequence, because it is a behaviour change and not only a
      dedup: those two paths now paint with `Text.Anchor` set explicitly (`MiddleLeft`, the outlet's
      default) where the deleted copies inherited the ambient anchor — an alignment shift nobody observed
      in game, and still no evidence that a real string overflows on either path.
      `KernelTextAuditTests` still drives `UiThemeDraw.Label` directly, so it covers the outlet, not the
      two routes into it.
- [ ] **Decide what to do with the four widget kinds nobody consumes.** `section/header`, `state/empty`,
      `input/mode-row` and `input/stepper-slider` are registered unconditionally by
      `KernelCoreWidgetRegistrar`, with zero references in the wired consumer's source — re-derive against
      that project's published tree at a pinned revision (`Coahuilite/UniversalSqueaker@09366f8`, or
      `gh api` on it), because the count in the next sentence is five days old. Before any
      freeze they are given a real consumer, deleted, or declared unproven in the page text; the honest
      statement of this library's validated surface is "3 of 7 kinds", not 7. **The criterion now exists**
      (`AGENTS.md` "What earns a kind", plus the per-kind re-derivation item below), so this item is no
      longer a shrug: each of the four either passes the ownership test and stays, or fails it and becomes a
      container, an attribute, or a recipe.

- [ ] **Make the licence-parity guard two-sided, or stop calling it ours.** Gate 6 is credited, in prose
      that has since circulated between both repos, with comparing `LICENSE` against the consumer's copy.
      It does not - only US's gate 10 does
      (`Coahuilite/UniversalSqueaker@09366f8:scripts/verify-local.ps1:141-146`), and it skips silently when our file is
      absent. A truncated or edited licence here cannot redden anything in this repo. Two clean options:
      pin the expected SHA-256 of the series licence as a literal in this repo and assert it locally
      (self-contained, no sibling dependency, and mutation-testable by editing `LICENSE`), or record the
      asymmetry as accepted policy. A sibling path check is not a third option: it would make this
      library's gates depend on a consumer tree, which is the vacuous-guard shape `MEMORY.md` ("Neutrality
      lane") records as the reason the neutrality scan was moved inside this repo in the first place. The
      2026-09-10 narrowing of `AGENTS.md` Boundaries (this repo answers for this repo alone; a clone has no
      consumer tree) settles the choice in favour of the pinned literal — it is now a policy consequence,
      not just a preference.
- [ ] **Give the harness `Stubs/**` a local guard.** The consumer's `UniversalSqueakerKernelHostTests`
      builds our four stub projects by relative path and copies them out of `bin/stubs/<name>/`, so that
      tree is a published surface. Renaming a stub project or its output folder breaks the consumer while
      all seven gates here stay green. A five-line assertion that the four project paths and their expected
      assembly names exist would close the hole without introducing a cross-repo dependency.
- [ ] **Keep the boundary guard's symbol list alive, or make it transitive.**
      `VerifyVisualCoreIsPageModelFree` rejects lines naming hand-maintained page-model symbols in
      seven visual-core files. `UiWindowHost` was added to that array with P2 and is mutation-proved (a
      planted mention in `UiThemeDraw.cs` fails the lane). `UiPopup` — added on `4dd97bf` — is still in
      neither list, so `UiThemeDraw → UiPopup → UiSession` would pass while breaking the "usable without
      a Host" claim the lane exists to hold. Cheapest fix: add `UiPopup` and mutate-test it the same way
      `UiWindowHost` was. Real fix, later: derive the page-model set from the types instead of a list
      nobody remembers to update.
- [ ] Clean up `UiLayoutManifest.ParseFile`: it has no production caller. Either wire a real
      load-from-disk path (which is the only thing that would make XML authoring worth its cost) or
      delete it and the `Schema="2"` version slot with it. **The intent is written straight here because
      this item's own wording had drifted it:** the requirement on XML is that it declares page layout and
      composes the kinds the library already provides. Hot-adding a widget kind from data was never asked
      for — the founding spec declined even that harder variant — so the fork is not "how do we hot-reload
      components" but "who hands the library the manifest string". Today that is the consumer (US embeds it
      as an assembly resource: `MEMORY.md` Charter), which is coherent and needs nothing from us. A disk
      path would buy exactly one thing — editing layout without recompiling the consumer — and would then
      owe an answer on where the file lives, when it is re-read, and what a parse failure shows a player.
      Deleting the dead entry costs nothing and stops implying a capability nobody owns.
- [ ] **`Verse.Window`'s custom-drawing seam is unexamined.** The reference read recorded
      `Window(IWindowDrawing customWindowDrawing = null)` (`MEMORY.md`), i.e. the game's own window manager
      takes a drawing delegate. Whether Verse honours it is an IL-level question, and it is the only known
      fact that could move the "no non-IMGUI backend" non-goal; answer it before anyone builds a case on the
      current wording, which rests on a compositing-order claim this repo has never measured.
- [ ] **The 0.4.0 sweep, as one breaking window (tier list, 2026-09-10).** Four parts, and they belong in
      one minor because pre-1.0 each would otherwise be its own breaking release — `MEMORY.md` records the
      count that makes this arithmetic rather than taste: 20 of 40 public types are blocked from stable by
      exactly these debts. (a) Per-surface theme tokens (§4). (b) Element identity. (c) Announce. (d) The
      internalise sweep: `KernelCoreWidgetRegistrar`, `UiLayoutEngine` and the six kind classes the
      internalize-candidate tier names, which needs the stable container of `const string` kind identifiers
      first so a manifest author never has to write `SomeWidget.Kind`. Fold into (d) the decision below about
      kinds that do not earn registration: the cheapest way to shrink the frozen vocabulary is to stop
      registering things that are really a container plus attributes.
- [ ] **`UiHost.MeasureAndArrange` / `UiHost.Draw(snapshot)` have no cited use.** The harness exercises the
      two-phase split (caching and revalidation lanes), but neither the wired consumer nor `UiWindowHost`
      names it — the shell drives `DrawFrame`. So either a consumer supplies the need the split was built for
      (measure before you know the window's size) or the split becomes part of the internalise sweep above.
      Leaving public surface that only our own tests call is the same debt as an unconsumed widget kind.
- [ ] **A deprecation channel for manifest attributes, per the 2026-09-10 ruling.** Today
      `UiHost.ValidateAttributes` refuses an unlisted attribute outright, which is correct for unknown names
      and wrong for a name we are retiring: the ruling is accept-and-redirect for at least one minor, warning
      in a lane that proves the warning fires, then refuse at the minor boundary. Needs a declared
      deprecated-attribute table (per kind, with the redirect target) and a harness lane driving both ends.
- [ ] **Re-derive the kind vocabulary against `AGENTS.md`'s "What earns a kind" (rule added 2026-09-10).**
      Measured per kind in this repository rather than judged by taste. `section/header` **fails**: its
      `Measure` returns a declared or default height and its `Draw` is one `UiThemeDraw.Label` plus a 1px
      divider, while the engine's own container already carries a `Title`/`TitleKey` band — the kind
      duplicates a container attribute. `chrome/banner` and `state/empty` **pass on one shared capability
      only**: a wrapped string whose Measure reserves the wrapped height, which is the behaviour of a missing
      leaf atom named twice. `input/mode-row` (column count derived from width, selection read and written
      through one binding), `input/dropdown` (session-owned popup anchor plus covered-rect publication),
      `input/stepper-slider` (value, focus and commit interlock) and `chart/line` (hot-control capture with
      per-point drag state) own something a manifest cannot express, and they stay.
- [ ] **Design the leaf vocabulary from what the consumer hand-rolls, then rebuild the composites on top of
      it.** The measurement is in `MEMORY.md`: inside its own kinds the wired consumer calls
      `UiNative.Button` 14 times, `UiNative.NumberField` 4, `UiNative.Slider` 3 and `UiNative.TextField` 2,
      while this library's manifest vocabulary is eleven structural element names plus a `Widget` leaf host —
      **no clickable leaf, no plain text leaf, no rule, no bare slider**. That asymmetry explains the coverage
      numbers better than any preference does: consumers build leaves because the vocabulary cannot reach
      them. Sequence: propose the atom set (wrapped text, button, rule, slider, number field), prove each in
      the self-check spec under real glyphs, then re-express `chrome/banner` and `state/empty` as compositions
      over the text atom **while keeping their names** — the survey in `MEMORY.md` found no toolkit that
      dissolves named composites into atom-only code, and ours has no style layer to absorb the long tail
      instead, so retiring a reachable name would push authors into C#. `section/header` is the exception, and
      the reason written down for it is that it duplicates the container's `Title` band, not that it is
      composite.
- [ ] **Bind the existing role vocabulary into the manifest; do not build a stylesheet.** The census is in
      `MEMORY.md`: the manifest's only visual knobs today are `Height`, `ButtonWidth`, `FieldWidth`, `Tab`
      and `Hidden`, while the roles already exist centrally as `UiStatusTone` (six values) behind
      `UiThemeDraw`'s 14 named outlets, and `UiTheme` carries no geometry tokens at all (275 numeric
      literals across the seven widget files). Two cheap moves, each gated on a citation into a consumer
      tree: add `Tone` to the atom schemas once the atoms exist, and move the geometry constants into
      `UiTheme` so width knobs stop being per-kind vocabulary. Refuse selectors, cascade and specificity
      until somebody is forced to hand-roll them — RimWorld has no style layer to interoperate with
      (`Assembly-CSharp` in 1.6.4871 has zero `WidgetDef`, `GUISkin`, `StyleSheet` and `UIElements` hits, and
      references no UI module beyond IMGUI and text rendering), so a parsed style file would be a second
      resolver with no host counterpart plus a measurement-order problem: the fit audit needs the resolved
      font before `Measure`.
- [ ] **The carrier's own diagnostic surface — shipped as a page *spec* a consumer mounts, not as a settings
      page and not as a carrier-owned window (evaluated 2026-09-10; reasoning in `MEMORY.md` Charter).** The
      proposal was a mod-settings page listing the version contract and the atomic kinds; what killed that
      shape is measured, not argued: this assembly has **no `Verse.Mod` subclass and no `ModSettings` at all**,
      so a settings row means adding a game-facing door and rewriting the README's claim that enabling the
      carrier changes nothing, and a dev-menu door needs a Harmony patch, which the series refuses. The shape
      that needs neither: a factory returning an `UiElementSpec` tree plus an `IUiBindings` view over live
      registry, version and carrier-collision data, **with every caption passed in as a parameter** — so the
      mounting consumer owns the keys, the carrier still ships zero Defs and zero strings, and the census
      reaches a real window in a real session. Content: the `Require` verdict and the duplicate-carrier report,
      one cell per **atomic** kind with worst-case strings in both scripts, and no composite cells.
      Sequencing rule: it ships *with* a mount or not at all — an unmounted factory is the speculative surface
      this protocol refuses. The mount that already exists is the consumer's diagnostics panel (its migration
      onto `UiWindowHost` is in flight), which makes this round-4 material rather than a library gift.
- [ ] **Second section of that spec: the registered-kind census.** Every scope and kind the loaded assemblies
      registered, each with its allowed-attribute schema and declared label set — the view that finds work,
      because a foreign kind with an empty schema or an undeclared label set is a hand-rolled bypass
      announcing itself. Two hard rules: **metadata only, never instantiate a consumer's kind in a page the
      carrier built**, and cap/total it the way `UiFitAudit.MaxReports` does. Runtime printing of a consumer's
      scope string is not a neutrality breach; the scan governs this repository's source, not observed data.
- [ ] **Amend the carrier's self-description the moment any door is added.** `README.md` (both languages) and
      the `About.xml` description currently promise that enabling FerriteLib changes nothing in the game, and
      gate 5 enforces the assemblies-only payload by refusing a content directory by name. If a `Mod` /
      `ModSettings` door is ever built anyway, that sentence and the identity line move in the same commit —
      a promise the gates cannot check is exactly the class of drift §6 exists to catch.
- [ ] **Prove the self-check spec in a lane before anybody mounts it.** Build it in the stub harness: every
      registered kind gets a cell; a planted fake kind that throws at Draw must land as a `RecoveryBand` row
      instead of killing the page; the census must list a planted foreign scope with its schema and label set
      while never instantiating it. What no lane can do is show real glyph advance, so this green says nothing
      to the coverage count in `MEMORY.md` — `AGENTS.md` "Our own demo is not consumption" is the rule that
      keeps the two claims apart.

## 4. Deferred by decision, with the upgrade path written down

- [ ] **Package feed / registry — deliberately not now.** Measured reason in `MEMORY.md`: same-version
      republish is not re-fetched, and build metadata is stripped from the cache identity, so a
      hash-suffixed dev version provides false freshness. **Two of three revisit triggers are now met
      (2026-09-10):** a remote exists, and the public ruling means an outside party may consume the
      library; a second machine building consumers is still false. The ecosystem's own answer is a
      compile-time-only `.Ref` NuGet package alongside the runtime DLL (`MEMORY.md` Charter). Upgrade path
      if revisited: `0.1.0-dev.<sha>` prerelease label + floating consumer version + drop
      `--no-restore` from the cross-repo gates (otherwise they go green against a stale graph).
      GitHub Packages was checked and rejected for a different reason: it requires a token to *install*
      even public packages.
- [ ] **Keyboard focus traversal — deferred by maintainer decision 2026-09-10**, not by oversight: RimWorld
      players drive the mouse, so this is not worth a public-surface change inside the 0.3 window. The
      upgrade path is written so it is a resumption and not a rediscovery. `UiNative.cs:173-236` already
      keeps per-element focus state in `UiValueState`, so what is missing is a focus *owner* on the session
      plus a traversal rule over the visible elements in tree order; the comparable Minecraft library solves
      it with a `focusable` flag whose signal drives Tab-ring membership (`MEMORY.md` Charter). The
      expensive half is naming, and it must be decided with this item: `Tab` already means a workspace tab
      (`UiLayoutEngine.cs:1249`, resolved against `UiBindings.ActiveTabKey`), so a keyboard axis needs a
      different word and that word is schema, not a local rename.
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

**Packaging shape, settled 2026-09-07 after reading both siblings' scripts.** Three channels, one staging
engine (`scripts/stage-package.ps1`), thin identity packers — the ancestor repo's structure, adopted because
this repo's two independent staging copies had already drifted apart in a way that cost a debugging pass.
The three artifacts and what each one guarantees:

| channel | artifact | identity rule | archive |
|---|---|---|---|
| `pack-dev.ps1` | `dist/dev/FerriteLib/` — the folder an in-game pass installs | dirty tree allowed, `-dirty` in the label | none (`-Zip` on request) |
| `pack-release.ps1` | `dist/github/FerriteLib-<tag>.zip` — what CI attaches | tag grammar + build axis + clean tree (rehearsal hatch `-AllowDirtyTree`) | deterministic, top-level `FerriteLib/` |
| `pack-steam.ps1` | `dist/steam/FerriteLib/` — what the uploader points at | same as GitHub, plus **no** dirty-tree hatch: it is the last step | none, by design |

Design consequences worth stating because they are the parts a later editor is tempted to "tidy": the
`-dev` payload refusal lives in the stager so GitHub and Steam cannot diverge on it; only GitHub archives,
which confines the entry-timestamp problem to the one digest that is public; `version.txt` always carries
`build=` and `commit=` so any folder can be attributed without opening its DLL; and `About/PublishedFileId.txt`
is never staged anywhere because the stager copies `About.xml` as a file, not the directory — Workshop
identity is created locally at upload time.

- [ ] **Steam upload itself is not scripted and should not be, until the Workshop decision lands.** SR's
      precedent is manual upload from the staged directory (`上传人工，非 SteamCMD`), and nothing here changes
      that: `pack-steam.ps1` produces the folder and prints the payload hash, and the upload still waits on
      the maintainer's invited/unsupported call below and on the missing preview image. Revisit only if
      rc churn makes hand-uploading the actual bottleneck.
- [ ] **Optional consolidation, cross-repo, one round at the earliest:** collapse Dev/Release into one
      configuration with a flavor property, the way SR's `SqueakyBuildFlavor` does. That deletes the
      shared-OutputPath hazard `MEMORY.md` records and the `--no-incremental` workaround with it. It is not
      free: US's `scripts/build-dev.ps1` drives `-c Dev` on this project, so the change lands in a round
      with its migration, not in a packaging cleanup.

- [ ] **Pre-push privacy scan on the full reachable history — reopened 2026-09-09: every content
      vector still clean, the identity vector is not.** The 2026-09-07 run at `bdbeae0` (working tree
      0, messages 0, all 34 revisions 0, single noreply identity) stands for what it scanned; the
      merge of PR #1 put `Fe <19252128+Coahuilite@…>` on the reachable history — GitHub's web merge
      button stamps the PR clicker's display name as author (committer `GitHub <noreply@github.com>`
      is platform boilerplate, not a leak). The bare local name `Fe` is the only personal signal
      anywhere; the email stays the noreply form the gate itself admits. `privacy-audit.ps1
      -FullHistory` now FAILS on `identity uniqueness (3)`; it will keep failing until the history is
      rewritten, which the 2026-09-07 ruling forbids for hash citations but this is not — a dangling
      citation is archaeology, a leaked display name on a public repo is the thing the scan exists to
      catch, and this repo's tags are rc-only so nothing external anchors on these SHAs yet.
      **Decision + action belong to the maintainer:** amend the one commit's author to the series
      identity (or filter-repo the single commit), force-push both branches, re-run the scan green,
      and only then cut `v0.3.0-rc1`. The scan's own lesson applies to itself: a clean tree says
      nothing about history — re-run `-FullHistory` after any commit lands, which is exactly how this
      regression was caught instead of shipped.
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
      that check meaningful; it would have failed on timing before it. CI on main green on the first run.
      **Overstep corrected the same day:** the bare `v0.2.0` was also tagged and released in that flow,
      and the maintainer ruled it premature - the push authorization covered the repo and the rc trial,
      not ending the trial. Release and tag withdrawn (`gh release delete --cleanup-tag`); the page is
      back to rc-only, `/releases/latest` 404s as the scheme intends. Consequence encoded by the gate,
      not by trust: the tip has moved past rc1's commit since, so the eventual stable is `v0.2.0-rc2` at
      the then-tip plus the bare tag on that same commit - a bare `v0.2.0` on today's tip would be
      rejected by `release.yml`'s final-state anchor. Cutting a stable release is a maintainer decision,
      never a pipeline step.
- [x] **`About/About.xml <url>` filled 2026-09-07** with `https://github.com/Coahuilite/FerriteLib` —
      the naming ruling made the url a pure function of a decided value. It is the only pointer a player
      or modder gets inside the game. No Workshop id exists yet, and no `<steamAppId>` goes in before any
      Workshop upload. If the repository is ever renamed, this line and the two pack scripts' source
      pointers move together.
- [ ] **US side: the link itself** - **cross-repo write, needs maintainer authorization.** The mechanism is
      one derived line in US's release body plus one pin, and it must be derived from a single source so the
      two cannot drift:
      `PrerequisiteApiMin` / `PrerequisiteApiMax` in
      `Coahuilite/UniversalSqueaker@09366f8:Source/UniversalSqueaker/Mod.cs:27-28` (both re-derived
      2026-09-09: `0.3.0` / `0.4.0`) is the truth for which lib range US was compiled against, so the link must
      be **resolved from that range, not string-built from the floor**: the floor is only the minimum, so if
      lib has since shipped a later patch inside the window, linking to the floor's tag sends players to an
      older carrier than the one US was tested against. US's `release.yml` should list lib's releases and
      take the highest whose base version
      satisfies `>= min` and `< max`, and then fail the release if (a) no release satisfies the range, (b) the
      chosen release is a prerelease while US is being released as stable, or (c) a lib payload exists in US's
      own stage directory. (c) already exists as `stage-package.ps1:41-45` and US gate 9; both stay valid
      under this decision unchanged. Pin the resolved tag into the release body, so the pairing is a published
      fact rather than something a reader has to recompute. Status 2026-09-07 (US feedback round): US's CI
      carrier checkout is deliberately **unpinned** - it tracks this repo's default branch so lib breakage
      surfaces early in US's CI (measured: its gate 13 death on DLL-only staging caught a real staging bug);
      that makes the release-body link the only pinned half, and US's `release.yml` does not carry it yet
      (grep: no tag link, no digest line). The item stays open until US lands it.
      After US's first release, run `scripts/verify-release.ps1` from this repo against it
      (`-Repo Coahuilite/universalsqueaker -AssetPrefix UniversalSqueaker`): the checks are generic
      (prerelease-flag-vs-tag, draft, single asset, digest, latest-pointer, dangling tags) and US has
      the same rc exposure once its own trials start.
- [x] **Cross-repo hash anchor applied 2026-09-07.** US executed its targeted blob rewrite and reported
      the commit-map mapping `0fe60b0 -> 6c7053a`; `MEMORY.md:8` now cites the new hash with the old one
      recorded as its pre-rewrite name. Verified independently from this repo: `6c7053a` is reachable from
      US `main` and carries the identical message; `0fe60b0` resolves only as a dangling local object.
      US's one-time guide was deleted pre-push (their ruling; survivors folded into US `MEMORY.md`), so
      the method pointer is `modding_documents/privacy-debt-vector-triage-zh.md`, which is maintainer-local
      and present in no clone.
- [x] **US's CI dependency chain — settled by US's own session, superseding this sketch.** What was
      written here (`path: ../ferritelib`) is impossible: `actions/checkout` resolves `path` inside
      `GITHUB_WORKSPACE` and throws on anything outside (US verified against the action's own
      `input-helper.ts`). US landed the working variant — carrier checkout to the `ci-ferritelib/`
      subdir, build there, run-step copy to the sibling path the HintPath expects (path math verified
      in US `MEMORY.md`). Follow-through closed by re-derivation 2026-09-09: US's two workflows now read
      `repository: Coahuilite/FerriteLib` (`ci.yml:40`, `release.yml:36`), so the case alignment this bullet
      asked for is done — the case-insensitive-resolution note survives only as archaeology. No FL action;
      **cross-repo write, report, do not edit.**
- [ ] **The four widget kinds nobody consumes** (`section/header`, `state/empty`, `input/mode-row`,
      `input/stepper-slider`) become public on the day this repo is public. See §3 for the decision; it is
      cheaper to make before the first release than after somebody compiles against them.
- [ ] **The one thing this decision actually changes: publishing makes the library referenceable by
      strangers.** With lockstep releases and one consumer, a breaking change is genuinely fine - our own
      mismatch path already degrades to a readable `Require` error rather than a crash. What is not
      reversible is that a Workshop page with a stable packageId turns `FerriteLib.UiKit` into something
      any modder can compile against, and from their first build our breaking edits become their breakage.
      That is the real one-way door, and it is independent of our own discipline.
      Decide before the upload, in one line on the page: is third-party use **invited** (then `§3` and the
      per-surface theme restructure in `§4` should land first, because they are breaking by their own
      admission and will stop being cheap afterwards), or **unsupported** (then we may keep breaking it
      for as long as US is the only consumer, and say so plainly). Both are fine; discovering the choice
      after someone builds on us is the only bad outcome.
      Round 1 enlarged the door without changing its nature: `UiWindowHost` is the largest new freeze
      surface the library has ever shipped, and it landed **before** the second wired consumer exists —
      which is exactly the pressure the freeze rule waits for, so the shell is provisional in a way that
      a theme token is not. If the call is "invited", the shell is the item most likely to need reshaping
      once a second consumer pressures it; say so on the page rather than implying the chrome API is done.
- [~] `About.xml` description rewritten 2026-09-07 (bilingual, answers "what is this doing in my mod
      list", says not to uninstall while a consumer is present). **Preview image still missing** - it is
      a Workshop-page asset and the Workshop step is undecided; nothing player-facing is published
      without it.
- [x] **README landed with the push 2026-09-07**: `README.md` + `README.zh-CN.md`, bilingual interlinked,
      scoped to what is true (two layers, no content, one DLL, verify commands, which mods need it), no
      counts. `CONTRIBUTING.md` deliberately **not** written - its existence is the item below's output,
      and the README states the currently-true stance (bug reports welcome, PRs not promised while the
      surface is provisional).
- [x] **Third-party use: ruled INVITED on 2026-09-10** (maintainer), which settles the item that had been
      open since the Workshop decision — "a stable packageId plus a browsable repository is what a modder
      compiles against whether or not anyone invited them" was the correct read, and the ruling accepts it
      instead of tolerating it. Consequences, all now derived rather than optional: §3's identity layer,
      announce, hit stack and focus traversal plus §4's per-surface theme restructure are **pre-stable
      debt**, because every one of them is breaking by its own admission and the cheap moment to pay is
      before anything compiles against the current shape; a `CONTRIBUTING.md` is owed as the second half of
      the offer (`MEMORY.md` Charter: the nearest precedent, Lightweave, advertises itself as a shared
      dependency in About.xml while its README says primitives may break freely — invitation without
      contract is the failure mode this repo now has to refuse); and `README.md`'s "PRs are not promised
      while the surface is provisional" line stays true only while it is paired with a written list of what
      the provisional surface will not do.
- [x] **Published-contract proposal — approved by the maintainer on 2026-09-10 ("草案我们先用；真实需求总比
      虚空打靶强")**, with the deprecation clause strengthened: an attribute under retirement keeps working,
      **redirected** to its replacement, for at least one minor, and full removal happens only at a minor
      boundary. Status of each part:
  1. **DONE** — `docs/api-tiers.md` (40 exported types: 12 stable, 20 public-unstable, 8
     internalize-candidate) with `FerriteLibApiTierTests` enforcing classification, staleness, the pinned
     stable list and a planted-name control. Mutation-proven by renaming a stable entry: four red lanes with
     exact names, green on restore.
  2. **Half-approved, both halves open.** The `.props` release asset (compile reference without shipping a
     runnable DLL) is the step to take; the metadata-only `.Ref` package waits until a stranger needs it.
     **Briefing owed to the maintainer, on his request and not yet delivered:** explain concretely what a
     `.props` file is versus a `.Ref` package, why one is a build-script include and the other is a
     non-executable metadata assembly, and which promise each one buys. Do not let the next session assume
     he already knows; he asked to be told after the decision, not before it.
  3. **Decided, wording owed.** The one-sentence promise goes into both READMEs now, and into the release
     body when `v0.3.0-rc1` is cut — the last one is the cutter's step, not a script change to land blind,
     because `release.yml`'s body is what a player reads.
  4. **Ruled, not implemented** — tracked in §3 as the deprecation channel item; the current creation
     contract refuses unknown attributes outright, so there is today no mechanism to accept-and-redirect.
      What this deliberately still excludes is a NuGet feed, any back-compat shim layer, and multi-version
      support.

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
      code). RESOLVED at source 2026-09-08: US added `UiPanel` to the scan
      (`UiSourceInvariantTests.cs:153`) rather than deleting it from the rule; the list is now six and
      the prose is accurate. Re-derived, not re-trusted — the earlier "re-verified" claim was itself
      wrong in the other direction, for the same reason this bullet exists.
- [ ] **Standing rule, once this repo has a Workshop page:** a gate's *capability* is whatever its script
      does, and the only way to keep prose honest is to name the file and line range when claiming one.
      "Gate 6 rejects divergence from a consumer's copy" survived one full revision cycle because it read
      like a confident summary of a check nobody re-opened. The same class of error is what `MEMORY.md`'s
      neutrality lane records twice, in the other direction.
- [ ] Open question worth one line of policy: is a one-directional series guard acceptable at all, given
      that the neutrality scan was moved *into* this repo precisely because a consumer-side check passed
      vacuously after the split? The actionable version is §3's two-sided-guard item.
- [ ] **Queued documentation work (2026-09-10), not to be written in this pass:** `docs/design-charter.md`
      carrying the tier-by-tier comparison of a retained layer's irreducible core against this library's
      actual standing, one external source per row. The substance already exists as prose in `MEMORY.md`
      "Charter"; the file exists so a stranger can be shown *why* the number is five rather than being told.
      Write it together with the API-tier list if §5's contract proposal is approved, so one document answers
      purpose and promise and the two are never maintained apart.

**Method note for whoever picks this up.** Derive every number with `git ls-files` + `wc -l` / `grep -c`,
and every gate claim from a read of the named range. A figure or capability repeated from prose inherits
whatever error the last author measured into it - which is how all four defects above survived a day.

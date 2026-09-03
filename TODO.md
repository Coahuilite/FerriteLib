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
      topmost-first. Deletes `YieldsToCoveringPopup`, the previous-frame `OpenPopupRect` reasoning, and
      the popup-specific coordinate conversions.
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

## 5. Publication as a real prerequisite — decided 2026-09-04

- [ ] FerriteLib will get its own Workshop page and US will be released **in lockstep** with it, per the
      maintainer's decision. The dual-version-line cost was raised and answered: synchronised releases
      carry it, and `FerriteLibVersion.Require`'s range assertion is what catches a player who updated US
      but not the carrier — that path already degrades to a readable error rather than a crash.
- [ ] **Sequencing consequence, which is the one thing this decision actually changes**: publishing freezes
      the public surface in practice, because subscribers keep whatever shipped and a later breaking
      change hits them. §3 and the per-surface theme restructure in §4 are all breaking by their own
      admission. So either land them **before the first upload**, or accept that they stop being
      breaking edits and become compatibility shims from that moment. This is a one-time ordering choice,
      not a permanent constraint — decide it once, before the upload, rather than discovering it after.
- [ ] A no-content library mod will draw "what is this doing in my mod list". Its `description` has to
      answer that in one sentence, name the mods that need it, and say plainly that it has no content of
      its own and must not be uninstalled while a consumer is present.
- [ ] `About.xml` currently carries a placeholder-grade description and no preview. Both are player-facing
      on a Workshop page and are not yet written.

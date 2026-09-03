# TODO

## 1. In-game verification — the only thing that can invalidate the split (blocking)

Everything so far is compile-time, stub-harness and reference-assembly evidence. Two assumptions have
never executed inside the game and both are load-bearing:

- [ ] **Cross-mod assembly binding.** `UniversalSqueaker.dll` carries an assembly reference to
      `FerriteLib.UiKit`, satisfied at runtime by the carrier mod. Verify by installing both mods and
      simply opening the US settings page. If RimWorld's resolver does not hand it the carrier's copy,
      the sibling-`HintPath` design is wrong and the NuGet/`Private=true` question reopens.
- [ ] **The carrier guard's own report path.** With both mods installed, confirm `Require` passes and
      logs nothing; then copy `FerriteLib.UiKit.dll` into US's `1.6/Assemblies` deliberately and confirm
      the `DUPLICATE CARRIER` line appears with both paths. This is the branch no harness can reach by
      natural means.
- [ ] **Language switch mid-window.** With the settings page open, change the game language and confirm
      text bands re-measure (the cache key now includes `TranslationRevision`). Reproduce the old shape
      first if possible: before the fix, English→Chinese or back should have left stale bands.
      `usdiag evt=ui.text.overflow` with detailed logging on must stay silent afterwards.
- [ ] FerriteLib appears in the mod list as "FerriteLib" with no content side effects, and toggling it
      off makes the game refuse to load US with a readable unmet-dependency message.

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
- [ ] License for this mod is still undecided by the maintainer. No `LICENSE` file has been invented.

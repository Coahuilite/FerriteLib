# TODO

> The action surface only. Settled rulings, done work and the release/round history live in `MEMORY.md`
> and `OBLIVIONIS.md`; a closed item is a pointer here, not a paragraph.

## Current objective and execution order

Branch `0.7.x`, API `0.7.0`, consumer range `[0.7.0,0.8.0)`. Source HEAD `d5af4d8209823bb19f7c112805fcb1b8caf56bbc`,
clean; the compatibility carrier and the Release payload are both `648f5769…`, the Dev payload `5b331379…`.
Historical event: privacy alignment `4d67b70` was pushed to `origin/0.7.x` and CI run `37714688341` succeeded.
That SHA is not a live working-tree identity; documentation edits dirty the tree and a later docs commit moves
HEAD without rebuilding held bytes. Prioritize long-term library contracts and shared-mod use, accepting
consumer release delay. Use the existing consumer settings redesign as the integration surface. The execution
handbook is `docs/development/0.7/next-stage-guide-zh.md`; build and instrument commands are in
`docs/build-and-debug.md`. Completed foundations and evidence pointers live in `MEMORY.md`.

**Current paired rehearsal: `dist/checkpoints/interaction-20261009-r1/FerriteLib`** (5 files, Dev bytes, source
`d5af4d820982`, payload `5b331379…`), paired with the US sibling of the same name at US source `562fc47`; the
14-file identity source is the PM's `paired-candidate-manifest.json`. Retained older packages
(`dist/dev/FerriteLib` at `0181268`, `dist/candidates/{checkpoint-20261007,sa1}`) keep their bytes and are not
the current rehearsal. **One bounded human pass is the only open gate** (`human-pass-20261009.md`).

- [x] **Expose unexpected reload diagnostic-publication failures.** The internal-buffer path now propagates
      host/document context plus the original exception; 10 existing local checks pass. Runtime failure
      reproduction remains absent, not a claimed pass. (Its source `0181268` is no longer the held package's
      source — the current rehearsal is `d5af4d8` and includes this change.)

- [x] **P1 composite popup arbitration:** maintainer passed Packs and Tuning A/B; session-bearing primitive
      guards remain in place. **The owner-id isolation this line named is gone:** FL-IC1 retired it, because a
      menu drawn over its own trigger left that trigger's option rows unselectable (reported in game as F09,
      witnessed as D1). Popup coverage is geometric now, and the trigger keeps its toggle-to-close on the strip
      the menu does not cover. That is a stub/lane result, not a game acceptance.
- [ ] **P1 follow-up: game-accept live popup anchoring.** The anchor/clip/orphan-owner fix passed the full
      verify/PackDev chain and three faithful reverts, and FL-IC1 later made popup coverage geometric and
      bounded the menu to whole rows — so this check now rides the single human pass below rather than being
      requested separately. Open a dropdown first, then scroll its containing page: it follows a visible owner
      and closes when that owner leaves the effective clip or disappears; check Packs and Tuning plus input
      recovery.
- [ ] **FL-IC1 short human pass (stub passes do not close it).** In one short session: an option menu covering
      its own trigger selects, a 40-item menu's first and last rows are reachable and the wheel moves the menu
      rather than the page under it, a chart under an open menu writes nothing while a normal drag still
      captures, moves and releases, and the typed menu answers the same way as the string one.
- [ ] **FL-IC2 short human pass (stub passes do not close it).** In one short session, in a real consumer window:
      a `Live="false"` number field keeps what was typed when focus leaves by a click on a control drawn earlier,
      Enter commits the open edit without closing the window, Escape drops the draft without writing it and
      without closing the window, and a second Escape undoes the next layer (menu, then the tree, then close).
      The window-stack dispatch order and eligibility are the harness double's reading, and Verse only calls the
      hooks for a window its `closeOnCancel`/`closeOnAccept` makes eligible - both are in-game facts this lane
      set cannot establish.
- [ ] **Consumer-side consequence of FL-IC2.** `IUiBindings` gained `TryInvokeCommand`, a breaking addition for a
      hand-written implementation, so the wired consumer's harness needs the one-line forward before it compiles
      against this carrier. Its two diagnostic windows keep their own two-press Escape policy through the new
      `UiWindowHost.TryHandleUnansweredCancel()` (asked only after the page's ladder declined; the shell consumes
      the key it answered), and because they set `closeOnCancel = false` they must also set the public Verse field
      `forceCatchAcceptAndCancelEventEvenIfUnfocused` to be eligible for the hook at all. Note for whoever wires
      it: declaring no `CancelBind` removes only the tree layer - an open menu, a held capture or a focused edit
      is still answered before the key can mean close.
      FL does not change US product source in this slice; the rewire is the consumer's own task.
- [ ] **Continue one settings-page slice at a time.** Select the next slice from the existing redesign,
      identify the contracts it touches, and record ownership, failure-sensitive tests and applicable game
      acceptance. Review shared state/lifetimes when the slice touches coexistence; the global
      `UiFitAudit.Enabled` switch is an unproven cross-mod risk, not a confirmed defect.
- [ ] **Stabilize the demonstrated contract surface.** Document inputs, ownership, layout/input behavior,
      fallback, migration and a consumer example, then decide API tiers individually. No blanket promotion
      follows from one page or one automated gate chain passing.

## Capability candidates — select through the current slice

These retain their previous conditions and deferrals. They are not prerequisites to finish the entire
library before validating the real page, and their listing order does not override the queue above.
They stay independent of the interaction checkpoint: FL-IC1 and FL-IC2 are now **source/stub-accepted**
(2026-10-08) and their only open item is the human pass above, while the unrelated broader library backlog
(task-11, FL-17, FL-18/B6, FL-13/B10) is not a blocker on it and must not be folded in as one.

- [ ] **task-11 — no stroke / per-edge stroke with configurable edge width.** `Chrome` is currently the
      default or `none`, and a border is all four edges or none. The consumer's flat scope and selected
      rail motivate the candidate; the current 1px hairline cannot express its 3px rail. Select this work
      when the active page slice requires it, with a general contract and its own acceptance. It is no
      longer designated as the unconditional next carrier batch.
- [ ] **FL-17 — the optional per-row template on `container/tree` (route A), the largest remaining item and
      scheduled last.** The one capability `Repeat` + `<Templates>` does not already cover: the cross of
      hierarchy and composition. `container/tree` renders one label band per row and takes no template, and
      `Repeat` has no level/indent. The maintainer **leans toward** an optional per-row template but holds it
      until the consumer has used the existing components, so it is **held for pricing, not scheduled** — be
      ready to price it when the friction report arrives. It is also the prerequisite for the two
      `HelpBind` sites below. Every new specialized kind still needs its four promotion-gate clauses.
- [ ] **FL-18 + B6 — one item**, the chrome action slot: an action a consumer can hang on the window
      chrome. (B6 and FL-18 are the same request under two names.)
- [ ] **FL-13 + B10 — one item, DEFERRED to the P3 UI/UX reset.** Choosing the alignment axis now would
      freeze vocabulary the reset may contradict. B10 is text alignment as a **layout** attribute, not an
      appearance axis.
- [ ] **The state-document palette selector — DEFERRED for lack of a citation.** The resolver applies
      token overrides onto the host's injected theme, and a request is not evidence. Re-open with a
      citation into a consumer tree.
- [ ] **CONDITIONAL, tied to the FL-17 decision — do not build alone.** A binding-resolved help identity
      (`HelpBind`) for a DATA-DEPENDENT key. The consumer has exactly two such sites, both in
      `us/scope-tree`, and that widget will not be migrated declaratively unless route A lands — so if it
      stays composite, those two sites stay imperative forever. Two citations, not a general need:
      `Coahuilite/UniversalSqueaker@ad1a7447b298b104e6afafa5e7fa5567ec0f3556:Source/UniversalSqueaker/UI/Kernel/UsScopeTreeWidget.cs:388,616`.
- [ ] **Batch 2's additive half (after the live feedback)**: CP-3 (skin-source axis) → CP-6② (role→surface
      mapping as data; blocked by CP-3) → CP-5 (regional scope), plus CP-7 (sibling-relative placement) if
      the feedback asks for it. Additive today and cited by nothing; the live pass is what supplies the
      citation.
- [ ] **Bucket classification is mandatory for every US→FL item** (maintainer policy directive 2026-09-20):
      (A) US misuse / US's own job — fix on the consumer tree, file no request; (B) a genuine FL gap, which
      must be a **general** capability, symmetric with an existing one or a funnel primitive, and useful to
      a consumer that is not US. B2②/B5/B6/B7/B10/B11 are (B) and non-blocking; B9 is (A); the consumer's
      `diag-nav-col` report is (A) and produces no FL work item and no version consequence.

## Library-side items that are real and unblocked

- [ ] **Give the harness double the two-level window/content coordinate transform** (from the 2026-10-09 focus
      audit, PM-accepted as a verification-capability gap, not a product defect). `GUI.BeginGroup(windowRect.AtZero())`
      can express neither a non-zero screen origin nor the real `Margin` content group, so an occlusion or
      key-routing verdict measured at the origin says nothing about the game. Shape: a screen-space group plus a
      `GUI.Window(windowRect)`-local content group matching `InnerWindowOnGUI` inside `rect.ContractedBy(Margin)`,
      then re-observe with a non-zero origin and a multi-window scenario. **No product behaviour may be changed
      to match the double**, and the native facts the audit lists are in `MEMORY.md` "Current state".
- [ ] **B8's schema half is still the maintainer's call.** `Description1..8` remain legal attributes on
      `input/mode-row` that the widget reads but does not paint — they are the per-option hover-help identity the
      publication carries, so the question is "keep the help identity or drop the capability", not "delete an
      orphan name". Pure library hygiene, no consumer evidence either way, and not a reason to raise the minor.
- [ ] **Run the R12 faithful-revert batch.** `tools/mutation/batches/r12-2026-09-28.ps1` lands with the
      correction round (**tracked, not run** — the shell sandbox fails OS-side `SetNamedSecurityInfoW Win32 5`, so
      `dotnet` cannot run and the PM/Codex owns every build, test and mutation command). Validate the anchors
      first (`-ValidateOnly`), then run it whole or one case at a time with `-Only <name>`. Its seven cases cover
      exactly the behaviour this batch changed: the selected font reaching measure, the font NOT moving density,
      the scoped `ColourRevision` cache refresh, the claim that keeps an explicit transparent, the look's body
      entering natural size, the OFF control edge, and the restored ON accent. Each case names its
      **mutation-target** assertion and the **guards** that hold on both sides — a guard is regression
      protection, never evidence. **Every old anchor in that file is deliberately single-line**: a multi-line
      anchor is line-ending sensitive and would refuse on a checkout with the other convention.
- [x] **The R12-FL surfaces have been compiled and run.** Every lane named in that item — the
      `KernelResolvedStyleTests` corridor lanes, `KernelControlKindTests.VerifyBooleanAppearanceSeam` /
      `VerifySwitchThumbTokens`, `KernelToneVocabularyTests.VerifyDanglingRule` and the parser lanes a readable
      legacy `<Font>` must not disturb — executes inside the committed harness: at `d5af4d820982` the chain ran
      1 setup + 10 gates green with Release 2940 ok and Dev 3208 ok, 0 FAIL. The **faithful-revert batch is a
      separate open item above** (tracked, not run); this item claimed only compilation and execution, and the
      earlier "nothing has been compiled since" sentence had gone stale.
- [ ] **Run the R3-A instrument batch.** `tools/mutation/batches/r3a-2026-09-29.ps1` lands with the R3-A slice
      and its correction (**tracked, not run**): eight cases, each naming its mutation-target assertion and its
      guards, and **it must run in the DEV configuration** (`run-fl-harness-dev.cmd`) because the whole
      instrument is inside `#if FER_DEV`; the batch's own default does that. Validate the anchors first
      (`-ValidateOnly`) — the PM measured **one pre-existing anchor mismatch** in that file during the FL-IC1
      round, so expect to re-cut that single anchor before the run rather than treating the refusal as a defect
      in the current slice.
- [x] **The R3-A surfaces run in both configurations.** The five Dev-only `KernelDevGeometryTests` lanes
      (`VerifyStructuredSnapshotMatchesTheDump`, `VerifyEffectiveAppearance`, `VerifyBoundariesAndUnknowns`,
      `VerifyBoundedCaptureAndDroppedCount`, `VerifySnapshotIsACopyAndHostsAreSeparate`) and the Release-only
      `TryGetGeometrySnapshot` answers all execute in the committed chain: Gate 10 runs the Dev harness and
      requires the named dev-only assertions as passing `ok:` lines, so at `d5af4d820982` the instrument half is
      covered (Dev 3208 ok, 0 FAIL) and a Release-only pass is no longer the only evidence. The three R3 fixture
      errors the PM measured (two wrong `Page` paths, one refused bounded fixture) are corrected and have run
      green since. **Residual, still open and unchanged:** `UiDevGeometryProbe.Outline`'s exact ink and hairline
      geometry is a faithful-intent reconstruction inside `UiThemeDraw.Solid` — the file was rewritten wholesale
      in that slice, and no lane or document pins the overlay's pixels.
- [x] **R4-A's typed value/options path has been compiled and run.** `KernelCoreWidgetTests.VerifyTypedChoices`
      executes in every harness run (Release 2940 / Dev 3208 ok at `d5af4d820982`), the legacy
      A4/FL-16/A5/popup lanes stayed green beside it, and Gate 10 is unchanged — `UiChoice<T>`,
      `IUiTypedChoices` and `UiPopup.DrawChoiceList` are covered by the same chain. What that run cannot do is
      a real-game check, which belongs to the single human pass.
- [ ] **SA1 and DT1: consumption and human acceptance only.** Both are source-accepted with their contracts in
      `docs/development/0.7/05-api-contract.md` (§SA1, §DT1), tiers in `docs/api-tiers.md` and the consumer
      wiring in `docs/consumers/consume-from-0.7.0.md`; DT1 closed through r2 with faithfuls red-then-green
      through the game-shaped entrant (`DoWindowContents(windowRect.AtZero())`) and frozen Dev/Release hashes
      identical across the correction. **Open:** the consumer's palette-document and composite adoption, the
      Demo showcase, and real-font in-game appearance — none of which FL can measure from here.
- [x] **D4-Viewport `VisibleRows` (source, 2026-10-07): BUILT, Dev harness ALL PASS, 10 gates green**
      (PM held the shared-stubs slot; carrier unchanged). Scroll-only positive `VisibleRows` budgets the
      viewport from the SAME round's measured rows (floor(k) bottoms + the fraction of the next row's own
      height from its real position; Repeat expands to stamped row roots - the stamp survives the
      OffsetBox copy; hidden siblings skipped; fewer/none answer natural; any numeric Height, zero
      included, outranks it; the budget recomputes after the scrollbar's reserved-width re-measure),
      creation refusals in `UiHost`, mirrored vocabularies in lockstep, `KernelRepeatTests` lane with a
      per-text wrap ledger. Two PM-verified faithful backouts red-then-green: delete row-root stamp and
      skip the narrowed-width budget recomputation. Direct-child counting, padding overflow and the
      visible empty fixture are guards; existing visibility fault injection is not feature backout proof.
      Contract: `docs/development/0.7/05-api-contract.md` §D4-Viewport +
      `docs/consumers/consume-from-0.7.0.md`. US source integration is delivered:
      `UsKernelSettingsHost.cs:83` and `Layout.Schema2.xml:459`/`:471` declare `VisibleRows=4.5`;
      no `DomainListBudget` remains under US `Source`. Remaining: genuinely unmeasured human
      in-game appearance only. No new FL product task. FL source for this item is already committed.
- [x] **Canonical consumer type-loading support (2026-10-07):** minimal definition/world/mod/Harmony
      type tokens and the dev-only Window field; no database/audio/patch simulation. 10 local checks
      pass. Checkpoint source identities and consumer verification belong to the PM handoff.
- [ ] **Game-check R4-B images and composed rows in the paired rehearsal.** All 10 local gates pass after PM integration; API tier details are in "docs/api-tiers.md". Real RimWorld rendering/input remain unverified.
- [ ] **Derive the visual-core boundary guard's symbol set from the types.** The cheap half landed
      2026-09-24 (`UiPopup` added to the reject list, mutation-tested); the hand-maintained list is still the
      real defect, since a type that joins the tree next is in neither array until somebody notices.
- [ ] **`LineChartWidget` → internalize-candidate, at the next breaking window.** The tier reason is false
      at the consumer's current tip (the composite that named it was removed in `f378715`; US and the demo
      reference neither, and US reaches the chart by manifest kind `chart/line`). Internalising a public type
      is a breaking action, so it belongs to the next 0.4.0-style sweep, and the entry itself records that
      **removal** waits on the stable kind-name container (`docs/api-tiers.md:427-429`). Only the *stable*
      list is pinned, so the move itself is one deliberate edit — the cost is measured in
      `docs/development/0.7/60-capability-dispositions.md` §C.1, whose disposition is *owed, recommended, not
      executed*.
- [ ] **`UiChartPointChange` stays `public-unstable`; the citation was the defect and it is corrected.**
      `f378715` deleted the *editor widget*, not the *consumption*: the consumer binds the action by type
      (`BindAction<UiChartPointChange>` and the generic type arguments in its harness), and a generic type
      argument is what keeps the type public. The corrected reason landed in `docs/api-tiers.md` with fl-contract's
      T8; no tier label moved and no further work is owed here beyond keeping the citation live.
- [ ] **No gate can notice a stale tier reason** (the durable fact is in `MEMORY.md` §Version axes): the tier
      lane reads type **names** and the stable list, never an entry's prose. Re-open only if someone builds a
      prose-reading guard; until then a stale reason is a human review item, not a lane item.
- [ ] **Move the battery's consumer half into the consumer repository.** `tools/mutation/mutation-check.ps1`
      writes one source file in the consumer checkout and then builds it - a cross-repo write performed by a
      script that lives here. The engine, the criteria and the Fl half belong here; the consumer half belongs
      there, with this file keeping only the shared engine. Not urgent, and not to be done without the
      consumer owner: it is a structural risk, not a defect.
- [ ] **Wire `UiLayoutManifest.ParseFile`** — the style-document ruling (2026-09-10) settles the fork: a
      standalone style document with its own loader makes "XML authoring without recompiling" a library
      promise, so deleting the disk entry would strand its structural half. The file's location is the
      consumer's call (nothing scans a directory), it is re-read at host creation only, and a parse failure is
      appearance-class: render with defaults, log through the fit audit, prove it in a lane.
- [ ] **`Verse.Window`'s custom-drawing seam is unexamined.** `Window(IWindowDrawing customWindowDrawing = null)`
      means the game's own window manager takes a drawing delegate. Whether Verse honours it is an IL-level
      question and **the only known fact that could move the "no non-IMGUI backend" non-goal**; answer it
      before anyone builds a case on wording that rests on an unmeasured compositing-order claim.
- [ ] **`UiHost.MeasureAndArrange` / `UiHost.Draw(snapshot)` have no cited use.** The harness exercises the
      two-phase split; neither the consumer nor `UiWindowHost` names it (the shell drives `DrawFrame`). Either
      a consumer supplies the need, or the split joins the internalise sweep. Public surface only our own
      tests call is the same debt as an unconsumed kind.
- [ ] **A deprecation channel for manifest attributes** (2026-09-10 ruling). `ValidateAttributes` refuses an
      unlisted attribute outright — correct for unknown names, wrong for a name being retired. Needs a
      declared deprecated-attribute table (per kind, with redirect target) and a lane driving both ends.
- [ ] **Decide what to do with the kinds nobody consumes.** `section/header`, `state/empty`, `input/mode-row`
      and `input/stepper-slider` have zero references in the consumer's source — re-derive against its
      published tree at a pinned revision, because any count is stale within days. The criterion now exists
      (`AGENTS.md` "What earns a kind"): `section/header` **fails** it (it duplicates the container's
      `Title`/`TitleKey` band), `chrome/banner` and `state/empty` pass on one shared capability (a wrapped
      string whose Measure reserves the wrapped height — a missing leaf atom named twice). Each either passes
      and stays, or becomes a container, an attribute, or a recipe. The honest validated-surface statement
      remains "3 of 7 kinds", not 7.
- [ ] **The 0.4.0 sweep, as one breaking window** (tier list, 2026-09-10). Four parts, and they belong in one
      minor because pre-1.0 each would otherwise be its own breaking release — 20 of 40 public types are
      blocked from stable by exactly these debts. (a) per-surface theme tokens; (b) element identity;
      (c) announce; (d) the internalise sweep (`KernelCoreWidgetRegistrar`, `UiLayoutEngine` and the six
      internalize-candidate kind classes), which needs the stable container of `const string` kind
      identifiers first so a manifest author never writes `SomeWidget.Kind`.

## Steam upload and packaging rulings owed

- [ ] **Steam upload is not scripted and should not be, until the Workshop decision lands.** The precedent
      is manual upload from the staged directory; `pack-steam.ps1` produces the folder and prints the payload
      hash, and the upload waits on the maintainer's invited/unsupported call and the missing preview image.

## 1. In-game verification — still the critical path

The harness measures a **model**, not the game's font (`MEMORY.md`, "Text fit"), so every geometry lane is a
future-regression guard and not a mutation proof of in-game geometry. The procedure and the live checks are a
form: **`docs/in-game-walkthrough.md`** — fill one row per check there and record the outcome here. A blank
row is not a pass, and "no error" is not one either.

- [ ] **Verify the current paired build in the game: `interaction-20261009-r1` (FL `d5af4d8` Dev bytes + US
      `562fc47`).** Fill one row per scenario in `docs/in-game-walkthrough.md` for the PM's seven-scenario
      sheet; earlier player observations do not constitute acceptance of the IC1/IC2 geometry and input changes.
- [x] **PASSED BY THE USER (2026-09-28): the popup click-priority pair — (A) Packs and (B) Tuning.** Both entry
      points were explicitly passed in game, so the owner-arbitration defect (a covered control stealing the
      option click inside one composite element) is CLOSED by human acceptance. Its fix is the owner-id-keyed
      yield (`UiHitLayer.PopupOwnerId`; the `UiSession.IsPointerOverHigherLayer` owner overload;
      `UiPopup.DrawOptionList` publishing the owner id; `UiNative` threading its own id through both checks)
      plus the covered-popup refusal in the value primitives. Lanes: `KernelPopupTests` (same-node siblings,
      covered primitives, real event pump, scrolled/offset container).
- [ ] **NEW, NOT yet human-verified: an open dropdown must not detach from its trigger when the parent
      scrolls.** The anchor is now a per-frame fact — `UiSession.NotePopupOwnerDrawn` is the only writer after
      the open, refreshed from the trigger's own window-space rect before the covered check, so rendering and
      hit geometry read one number. Lifecycle: the popup is released when the owner's rect leaves the Host
      viewport, and at the next `BeginHitPass` when the owner did not report at all. Lane:
      `KernelPopupTests.VerifyOpenPopupFollowsTheTrigger` (popup opened BEFORE the scroll, non-zero origin,
      owner gone ⇒ no orphan clickable menu, recovery after it returns).
- [ ] **RESIDUAL, recorded not implemented: an owner clipped by an INNER `Scroll` but still inside the Host
      viewport still reports**, so its popup follows a clamped anchor instead of being dismissed. Closing it
      needs the engine to publish the element's effective clip rect into the context (one field) plus a lane.
- [ ] **Verify in-game: open a dropdown, then scroll its parent — on BOTH the Packs and the Tuning page.**
      This is the human gate for the anchor fix; a green harness does not close it.
- [ ] **The carrier guard's duplicate branch.** Copy `FerriteLib.UiKit.dll` into the *installed*
      `Mods/UniversalSqueaker/1.6/Assemblies` (never the repo — three gates refuse it there) and restart.
      Record **which** of three outcomes occurs; all three are valid results: (1) `DUPLICATE CARRIER` naming
      both paths — the guard works as designed; (2) no error and US runs — RimWorld deduped by assembly
      identity, so the guard is dead weight in exactly the case it was written for and must compare file
      location/identity instead of counting names; (3) the game errors before US's constructor runs — the
      engine rejects duplicate assembly names itself. `ModAssemblyHandler` installs a global `AssemblyResolve`,
      so (2) is the likely one. **Do not read "no error" as a pass.**
- [ ] **Failure shape with the carrier absent.** Disable or remove FerriteLib and report what the player sees:
      a readable unmet-dependency message, a silent absence, or an exception wall.
- [ ] **Bilingual overflow across two launches.** Launch once in Chinese and once in English, walk all five
      workspaces plus the overlay with detailed logging on, and confirm `usdiag evt=ui.text.overflow` stays
      silent. (The withdrawn "language switch mid-window" item is unreachable: switching language needs a
      restart, which destroys the settings window, and the host caches per window. `TranslationRevision` in
      the cache key is insurance against a future in-place switch, **not** a fix for an observable defect —
      do not describe it louder than that.)
- [ ] **The window shell has never been opened in a game.** `UiWindowHost` is compile-verified against
      `Krafs.Rimworld.Ref 1.6.4871` and lane-verified against the `Verse.Window` stub slice; that is not a
      game run. The shell must be opened once through the real window stack, where `WindowOnGUI`'s actual
      group/matrix plumbing, `layer = Dialog` ordering and the close-sound path are live. A signature the
      reference assembly and the stub agree on and the executable disagrees with shows up here and nowhere else.
- [ ] **Two text paths just came into the fit audit.** Container titles and the stepper-slider's label and
      `−`/`+` glyphs used to bypass `UiFitAudit.Check`; they now route through `UiThemeDraw.Label`, so they
      can report overflow they never could, and their vertical anchor changed from ambient to `MiddleLeft`.
      Look for newly-reported overflows and visible title misalignment; absence of both is the result to record.
- [ ] **Engine-owned recovery has never tripped in a game.** Force one trip by hand — a temporary consumer
      widget that throws — and confirm the page keeps drawing around it and the log line appears once per slot.

## 2. Second consumer: the withheld sibling mod

An integration/validation follow-up, not an API-stabilization prerequisite (maintainer, 2026-09-17). It does
not authorize changes to another repository.

- [ ] It builds against a machine-local game path: decide whether it moves to the same RimRef +
      relative-reference scheme this repo and the wired consumer use.
- [ ] First slice should be a **dialog-shaped** surface, not the tactical table — the table is a full-screen
      world-overlay window with hand-flowed rects and per-window private interaction state, the worst available
      first test of a page model.
- [ ] Settled by the maintainer: the colonist-bar navigation affordance **stays in its framework**, because
      the framework must let a player find a pawn inside a megastructure; only the **window** moves to the
      consumer. Recorded consequence: "the framework owns capability and zero UI" is now false and "zero
      translation keys" is unreachable — the framework owns that affordance and its two strings. The boundary
      that survives: **the framework may own the affordance that reaches an in-world thing, including its own
      two strings; it may not own a window, a layout, or a session.** Dependency direction is unaffected — verify
      it holds when the work is done rather than assuming it.
- [ ] Record what the table needs from the library. Theme + drawing helpers + text metrics only would mean the
      visual-core seam paid for itself; needing the session and the engine would mean the boundary claim in
      `AGENTS.md` is too narrow and should be revised from evidence.

## 3. Retained-mode roadmap (this library's own, after the split is proven)

Each item is expected to delete a workaround, not add a layer. **The 0.7 line closed most of this list** —
elements own stable identity and state, hover/active observation members exist, the fit-audit blind spots and
the responsiveness package landed, and `input/text-field` closed the last missing leaf anyone was hand-rolling.

- [ ] **A third operation on bindings: announce.** `IUiBindings` has get and set but no notification, so
      invalidation is a single global `ContentRevision` counter whose call sites are policed by source-text
      assertions. Give every key a revision, then delete `SessionRevisionBumper`, the help panel's hand-written
      before/after diff, and those assertions.
- [ ] **Owned hit stack — the remaining half.** The popup layer and the element-level half are done
      (`UiPopup` is the only popup rect rule and publisher, and the atoms dispatch through
      `UiSession.IsPointerOverHigherLayer`). What is left is the **content-layer-vs-content-layer** boundary: a
      `(rect, element)` list exists, arbitration between two content layers does not. Closing condition: once the
      stack lands, every element's yield behaviour comes from it and no element keeps a private yield branch.
- [ ] **The element identity layer's last step**: a real node object owning identity, state and dirty flags in
      one type, plus the string-keyed remnants (`UiNative.GetControlId`, the popup owner key, `SetScrollTarget`).
      The hit stack depends on it. Removes the path-aliasing hazard where two unnamed same-kind siblings share a
      path.
- [ ] **Bind the existing role vocabulary into the manifest; do not build a stylesheet.** The manifest's only
      visual knobs are geometry attributes while the roles already exist centrally (`UiStatusTone` behind
      `UiThemeDraw`'s named outlets) and `UiTheme` carries no geometry tokens. Two cheap moves, each gated on a
      citation: add `Tone` to the atom schemas, and move the geometry constants into `UiTheme` so width knobs
      stop being per-kind vocabulary. Refuse selectors, cascade and specificity until somebody is forced to
      hand-roll them.
- [ ] **Settle the duplicate tone tokens** (`UiTheme.Warning` / `UiTheme.Danger` are the same RGB under two
      names, and no library widget selects either). Either a cited consumer shapes them apart or one name goes —
      the same debt class as an unproven kind.
- [ ] **Make the tone treatment table queryable, then route the three call sites through it.** The dropdown and
      the mode row re-implement the `Active`/`Neutral` rows of `UiThemeDraw.StatusTreatment` verbatim and
      re-derive a text colour that lives inside `StatusBadge`'s private switch. Shape: one internal table
      returning fill, border **and** text for `(tone, prominence)`, consumed by both outlets so the three sites
      cannot drift. Keep it `internal` — a public addition bumps the minor.
- [ ] **The carrier's own diagnostic surface — a page *spec* a consumer mounts**, not a settings page and not
      a carrier-owned window. This assembly has no `Verse.Mod` subclass and no `ModSettings`, so a settings row
      means adding a game-facing door and rewriting the README's claim; the shape that needs neither is a
      factory returning a `UiElementSpec` tree plus an `IUiBindings` view over live registry, version and
      collision data, **with every caption passed in as a parameter** — the mounting consumer owns the keys.
      **It ships with a mount or not at all**: an unmounted factory is the speculative surface this protocol
      refuses. The mount is the consumer's diagnostics panel, which makes this round-4 material. Two sections
      (the version contract + collision report; the registered-kind census) and their hard rules — **metadata
      only, never instantiate a consumer's kind**, cap and total as `UiFitAudit.MaxReports` does, and prove it
      in a lane (a planted fake kind that throws at Draw must land as a `RecoveryBand` row) — are in
      `OBLIVIONIS.md` under the carrier-diagnostic entry. **Amend the carrier's self-description in the same
      commit as any door that is ever added**: `README.md` (both languages) and `About.xml` currently promise
      that enabling FerriteLib changes nothing in the game.
- [ ] **A style capability, staged by contract cost** (decomposed 2026-09-10; scope confirmed the same day).
      The governing rule is the use/extend boundary: XML is the surface for **using** components, layout and
      appearance; C# is the surface for **extending** the vocabulary. Layout complies and appearance does not,
      so all three appearance classes are in scope. Stage 0 is the internal dedup above (no bump, any time);
      stages 1–3 land with the leaf atoms; stage 4 is the region level; stage 5 ships with whichever change
      first needs a `Disabled` producer. Precedence is nearest-wins — element > region > window > library
      default — written down, no selectors and no specificity arithmetic. **Inheritance is asymmetric and that
      asymmetry is the design:** scheme and density inherit, roles do not, because a region marked danger makes
      everything inside it read as dangerous. Appearance values go **fail-soft** (an unknown `Tone` falls back
      to the default treatment) while structure stays fail-closed — and fail-soft must not mean silent: the
      fallback is logged, reported through the fit audit's channel and exercised in a lane. The page-level
      rule source is a **standalone style document with its own loader** (2026-09-10 ruling, superseding "no
      separate style file"); one parser, one `<Style>` vocabulary, two text origins.

## 4. Deferred by decision, with the upgrade path written down

- [ ] **BACKLOG (maintainer ruling 2026-09-23): outline BOTH rects in the dev instrument's overlay.** The dump
      already carries the arranged, drawn and window rects plus the origin between the spaces, which is what the
      layout questions needed; drawing both outlines needs the engine's conversion inputs, so it is not worth
      the scope today. Re-open only if a reader cannot answer a question from the numbers.
- [ ] **Package feed / registry — deliberately not now.** Same-version republish is not re-fetched and build
      metadata is stripped from the cache identity, so a hash-suffixed dev version gives false freshness.
      Two of three revisit triggers are met (a remote exists; an outside party may consume the library); a
      second machine building consumers is still false. Upgrade path if revisited: `0.1.0-dev.<sha>`
      prerelease label + floating consumer version + drop `--no-restore` from the cross-repo gates. GitHub
      Packages was checked and rejected: it requires a token to *install* even public packages.
- [ ] **Keyboard focus traversal — deferred by maintainer decision**, not oversight: RimWorld players drive
      the mouse, so it is not worth a public-surface change. The upgrade path is a resumption rather than a
      rediscovery: focus state already exists per element; what is missing is a focus *owner* on the session
      plus a traversal rule over visible elements in tree order. The expensive half is **naming** — `Tab`
      already means a workspace tab here, so a keyboard axis needs a different word, and that word is schema.
- [ ] **Default-look discussion / remaining theme acceptance.** `UiSurfaceStyle`, per-surface border
      overrides, and the named `UiTheme.Vanilla` / `DarkGold` palettes already exist; the old "not
      representable" item was stale. The demo explicitly chooses Vanilla, which is a reference/derived
      palette, not a verified exact match to a named game build. Discuss a recommended out-of-box look and
      a neutral square-toggle promotion from the real consumer; no theme implementation is authorized by
      that discussion alone. Preserve explicit host injection and decide API freezing separately.
- [ ] Optional two-minute experiment: whether RimWorld tolerates an unknown tag in `About.xml`. The parsed
      tag set is closed and no tolerance could be proven from stripped metadata, so nothing depends on it.
- **CLOSED (2026-09-22 audit): the version-axis lane owes no re-cut** — no lane reads additions and no
      gate enforces the minor bump (named lane by lane in `MEMORY.md` § Version axes). Kept only as a pointer so
      the question is not re-litigated; if a lane ever starts reading additions, align it in the batch that next
      touches the carrier.

## 5. Publication — form decided 2026-09-05: two repos, two release pages, linked not copied

**The decision.** FerriteLib publishes its own GitHub Release and stays the single carrier of
`FerriteLib.UiKit.dll`. The consumer publishes its own release containing only its own package, and **every
consumer release body links to the specific lib release it was compiled against**. Players install two mods
from two pages; no DLL is ever duplicated, rebuilt by a foreign pipeline, or re-attached. Workshop is the
later step: FerriteLib gets its own page and the consumer is released in lockstep with it.

**The packaging table is `MEMORY.md`'s packaging discipline section** (three channels, one staging engine,
the identity rule per channel, and what each artifact guarantees). What remains an action here:

- [ ] **The invited-vs-unsupported call — the one irreversible decision, still owed by the maintainer.** After
      a Workshop page carries the stable packageId, "breaking changes are expected" stops being free. It is
      never a session's call.
- [ ] **The lib-release link in the consumer's release body — unblocked, still undelivered.** The original
      blocker is gone (rc assets exist), so this is a plain report and no longer waits on this repo. **Deliver
      it actively at the next handoff** — the round-3 lesson is that a report-only item with no delivery
      channel is lost. Report, never edit the sibling tree.

## Line documents — the per-round homes

Each open line's scope, packages, ownership, contract and live status has exactly one home; nothing is
restated here.

- **0.7.x** (current, axis `0.7.0`): `docs/development/0.7/` — the change plan, the API contract with its
  amendments, the verification record and the capability-disposition table.
- **0.6.x** (published 2026-09-17 at `2545346`): `docs/development/0.6/`. The line added the MVVM-adjacent
  surface (page lifecycle hooks, the notification adapter, reload scheduling with a testable time seam, the
  read-only widget catalog) plus an independent demo mod, which is **not** a second real consumer. What is
  still not closed by it: the `0.5.x` in-game acceptance and a real consumer compiling against
  `[0.5.0,0.6.0)` — both other actors' steps.
- **0.5.x** (frozen at `354d90a`): `docs/development/0.5/`. Library-side surface landed and the external
  review's seven findings are fixed; what remains is external — in-game acceptance and a real consumer
  compiling against it — and neither may be recorded as done from this repository.

## 0. Rounds — nothing open

The US→FL round counter is **global across directions, next-unused at filing, later filer yields**. Rounds 1,
2 and 3 are CLOSED; the counter stands at round 4 unused. Round 1 shipped the 0.3.0 contract axis (P1–P6 plus
FL-side A–E), round 2 was packaging fixes on both sides, and round 3 shipped the responsive vocabulary
(`Width="Auto"`, `MinWidth`/`MaxWidth`, container `Breakpoint`, the measured slider label) — all three with
their permanent records in `MEMORY.md`/`OBLIVIONIS.md`. The `0.3.0` release and the cross-repo report items
those rounds left behind are superseded by the 0.7 line and by `MEMORY.md`'s publication record; nothing from
any round stays open on the FL side except the report in §5 above.

**Round 4 opens only on a shell-level defect the consumer's diagnostics-panel migration exposes.** That
migration files no round and asks FL for no new surface, so the number stays unused.

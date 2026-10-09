# MEMORY

## Current state (2026-10-09)

- **Build and delivery identity.** The line's last **source** commit is
  `d5af4d8209823bb19f7c112805fcb1b8caf56bbc` on `0.7.x` (committed as `worker <worker@localhost>`), and every
  accepted payload, carrier and rehearsal in this checkpoint attributes to it. The compatibility carrier at
  `1.6/Assemblies/FerriteLib.UiKit.dll` and the Release payload are both
  `648f5769fe9a31c13a645086e3e17a2482316ea20e1f0f27a4c8739f994a264a` (the carrier was exported during the
  2026-10-08 freeze, so it no longer lags the line). The Dev payload is
  `5b33137905fff0392532696fc7c880a37307516d534f22629528bd72bd7097c4` with
  `ProductVersion 0.7.0-dev+d5af4d8209823bb19f7c112805fcb1b8caf56bbc`. Measured at that commit: 1 setup +
  10 local gates green, harness Release 2940 ok / Dev 3208 ok, 0 FAIL.
  **A later docs or memory commit moves HEAD and changes none of those bytes** — measured 2026-10-09: after the
  compaction commit, the 25-entry frozen inventory still matched on hash and size with the tree clean, while
  `pm-verify-frozen-handoff.ps1` reported `head_matches=false` because it pins the source SHA. That refusal is
  the check working, not a broken freeze; read `source_head` in its observation before escalating.
- **The paired rehearsal is `dist/checkpoints/interaction-20261009-r1/FerriteLib`** — 5 files, staged from the
  frozen Dev bytes with **no build**, `version.txt` recording `build=dev` / `commit=d5af4d820982` /
  `payload-sha256=5B331379…`. Its US sibling is
  `UniversalSqueaker/dist/checkpoints/interaction-20261009-r1/UniversalSqueaker` (US source `562fc47`, payload
  `9951bdaacf58…`), and the US build recorded a `CompilerFL.SHA256` equal to this FL Dev DLL — that is the
  cross-repo proof the pair was compiled against one payload. The 14-file single source of identity is the PM's
  `paired-candidate-manifest.json`. Older held packages (`dist/dev/FerriteLib` built at `0181268`,
  `dist/candidates/checkpoint-20261007`, `dist/candidates/sa1`) keep their bytes as retained history; they are
  not the current rehearsal. **A later source, docs or memory commit moves HEAD without changing any of those
  held bytes** — package identity is what the stager measured, not what HEAD points at.
- **Nothing is installed, pushed, tagged or released.** The one open gate on this checkpoint is a single bounded
  human pass (PM's `human-pass-20261009.md`, seven scenarios); PM acceptance of a slice is never a real-game
  pass, and installation stays the developer's own step.
- **FL-IC1 and FL-IC2 are PM-accepted at the source/stub boundary.** IC1 accepted 2026-10-08 08:28 UTC at
  `8dad3d5` + `11f1603`; IC2 accepted 2026-10-08 14:13 UTC over the chain `11f1603 → 84d1504 → 1d86ff8 →
  `d5af4d8`. Contracts: `docs/development/0.7/05-api-contract.md` §FL-IC1 and §FL-IC2; tiers in
  `docs/api-tiers.md`; lanes `KernelInputEligibilityTests` (13), `KernelEditTransactionTests` (13) and
  `KernelCancelLadderTests` (12); faithful reverts `tools/mutation/batches/fl-ic1-20261008.ps1` (5 cases) and
  `fl-ic2-20261008.ps1` (10 cases, all intended-red); evidence under
  `evidence/interaction-development-20261008/fl/`. IC1 shape: one receive-eligibility rule
  (`UiNative.CanReceivePointerPress`), capture ownership named by the drawing element and ended at the pass
  boundary, geometric popup coverage with the trigger's own-id exemption **retired**, and a menu bounded to whole
  rows inside the viewport with the wheel routed to the open menu at the engine's pass boundary. IC2 shape: one
  edit transaction with a single commit (the frame that ENDS the edit carries the parsed draft; an unparseable
  draft is dropped and never coerced; a popup drawn over a field PAUSES the edit instead of answering it), the
  window's two keys entering through `UiWindowHost` before `DoWindowContents`, `CancelBind` in the engine-wide
  vocabulary, and one press undoing one layer — menu, held capture, open edit, then the nearest EXECUTABLE layer
  climbing `Parent` from the subject (the element a consumer named, else the last element that actually took an
  interaction).
- **Four rules that came out of the IC1/IC2 reviews, each bought by a real defect:**
  - A sign or coordinate convention copied from an unfamiliar backend must be read in that backend's own source
    first. The wheel read `-delta.y` while native IMGUI adds a positive `delta.y`, and this slice's own lanes
    pumped the same negation, so **nothing in the harness could see the inversion** — only the PM's probe on a
    copy of the changed sources did.
  - Sealing an override on a base class locks a consumer's existing subclass out. "Library first, then the
    consumer's own policy" is an overridable seam (`protected virtual bool TryHandleUnansweredCancel()`), asked
    only after the page's ladder declined, with the shell consuming the key on the handler's behalf; no Accept
    sibling exists because no consumer owns an Accept policy.
  - A contract sentence written from the implementer's model instead of the code is a defect with the same weight
    as a bug. Two delivered sentences were wrong (Accept sharing Cancel's layer list; "a page that declares
    nothing keeps the old key behaviour", which holds only while no menu, capture or edit is open).
  - A member added to an interface (`IUiBindings.TryInvokeCommand`) is a breaking addition for a hand-written
    implementation whatever the version axis says, and the consumer's harness needs the one-line forward.
- **Multi-window focus and key attribution are understood, not verified** (`fl-focus-audit.md`, PM-accepted for
  source attribution and evidence grading 2026-10-09; no product change). Known native source, read-only and
  cited by method path in the audit — `WindowStack.WindowStackOnGUI` calls each window's `ExtraOnGUI` top-down
  and then `WindowOnGUI` bottom-up; `Notify_PressedCancel` / `Notify_PressedAccept` scan from
  `windows.Count-1` downward and **break at the first eligible window**; `Notify_ClickedInsideWindow` re-inserts
  in the same layer and sets `focusedWindow` after `GetsInput`; `GetsInput` is the complete top-scan rule (true
  at self, false if a higher absorb-first window is met first) and reads neither `focusedWindow` nor pointer
  coordinates; `Verse.Mouse.IsOver` requires rect containment **and** `!GUI.InInputBlockedNow`, where
  `MouseObscuredNow = GetWindowAt(UI.MousePosUIInvertedUseEventIfCan) == currentlyDrawnWindow`. What the harness
  double does instead is modelled, not native: its scan direction differs, and
  `GUI.BeginGroup(windowRect.AtZero())` can express neither a non-zero screen origin nor the real `Margin`
  content group. Consequences held as rulings, not gaps: the Catalog active-target guard is a **candidate
  policy** (instance-scoped, never equal to Verse's global window focus) and must not default to replacing Verse
  authority without a confirmed contract and a ruling; ordinary cross-window outside-click blur that commits a
  valid draft follows the already-accepted IC2 rule; a temporary popup occlusion is not an outside click.
- **Open FL verification-capability item from that audit:** give the double the two-level window/content
  coordinate transform (a `GUI.Window(windowRect)`-local content group under a screen-space group, matching
  `InnerWindowOnGUI` inside `rect.ContractedBy(Margin)`), then re-observe occlusion and key routing with a
  non-zero origin and a multi-window scenario. Until then a stub green on those axes is not evidence about the
  game, and no product behaviour may be changed to match the double.
- **Build-slot protocol, measured 2026-10-08/09.** FL froze its accepted outputs into a 25-entry inventory
  (payloads, carrier, canonical stub tree, harness executables — SHA-256, bytes and full-precision UTC mtime),
  handed the exclusive canonical-build slot to US, and ran no build while US held it; after US finished, FL
  restored the four ignored outputs by recorded copy and the inventory re-verified 25/25 with the tree clean.
  `hash/size/mtime` equality proves the identity **now**; it does not by itself prove "could never have been
  rebuilt", and the copy operations are what witness the restoration.
- **Push privacy automation.** `scripts/privacy-audit.ps1` follows the series' Mwah five-vector gate,
  `scripts/install-hooks.ps1` enables the full-history pre-push check per clone, and
  `.github/workflows/privacy-audit.yml` covers every pushed branch and PR; scope and limits in
  `docs/push-privacy-gate.md`. Historical event: the privacy alignment `4d67b70` was pushed to `0.7.x` and CI
  run `37714688341` succeeded **for that SHA** — it is not a live-HEAD identity, and a docs or memory commit
  moves HEAD without rebuilding held bytes. Scanner success is a privacy result, never gameplay, build or
  release acceptance.
- **Working direction (the PM's `WORKING-DIRECTION-20261004.md` is the authority).** Task specs lead with
  **purpose, ownership and the main path plus a few key invariants**; process and history stay in handoff
  material rather than being repeated into every task. **Checks belong at the responsible boundary**, and work
  inside a boundary proceeds on already-established contracts — "add more validation" is not a goal, and
  **a hash answers payload identity, never business correctness**. Accept that a first design and
  implementation can be wrong: an unexpected error keeps its context and original exception and surfaces **at
  the responsible boundary**, and a failure that is isolated or absorbed by recovery UI must not be recorded as
  success. Add tolerance only for **known business states or observed faults** — no speculative null-guards or
  fallback sweeps across the library on the chance that something is wrong.
- **Earlier rounds on this line, all PM-accepted at the source/stub boundary; contracts are the authority.**
  Diagnostic-publication observability (`PublishToHost` propagates an unexpected non-OOM failure with
  host/document/path/version/result context and the original exception as inner cause, and never rolls back a
  determined document result); SA1 appearance seam (`input/dropdown` `Appearance="selector"`,
  `UiTheme.SwitchThumbOff`, `GeometryOverlay` independent of `GeometryEnabled`, `UiWindowHost.ShellChrome` the
  present-tense read); DT1 outline coverage (scoped viewport bands and the shell's own chrome outline and
  record from the same switch); D4-Viewport `VisibleRows` (the Scroll budgets its viewport from the same
  arrange round's measured rows); and the canonical stubs' consumer production-path type loading (minimal Def,
  SoundDef, XenotypeDef, Mod, Pawn, CellRect and Harmony tokens plus `Find.WindowStack` and
  `Window.onlyDrawInDevMode` — type loading and the window shell only, never definition loading, audio, world
  simulation or Harmony execution). Each is specified in `docs/development/0.7/05-api-contract.md` under its own
  section, tiered in `docs/api-tiers.md`, and migrated in `docs/consumers/consume-from-0.7.0.md`; the round
  narratives are archived in `OBLIVIONIS.md` (2026-10-09 compaction). None of them is a real-game pass.
- **Open common-capability work stays independent of the US checkpoint except FL-IC1/2.** task-11, FL-17,
  FL-18/B6 and FL-13/B10 remain unrelated broader library backlog. Provenance for a new specialized kind still
  needs a citation into a consumer tree; a confirmed public capability does not wait for a second consumer to
  hand-roll it.

## Archived round records

The 2026-09-30 PM integration checkpoint, the 2026-09-28/29 R1-R2 palette and R3-A instrument narratives, and
the 2026-09-17..2026-10-07 landing-narrative family (including the 2026-09-17..09-22 maintainer rulings, whose
live consequences `AGENTS.md` carries) moved **verbatim** to `OBLIVIONIS.md`, "2026-10-09 — second compaction".
Read them there only for a historical conflict; they cannot override the current scripts, contracts or ledger.

## Start here — current direction (2026-09-24)

Read `AGENTS.md` for invariants, this file for durable state, and `TODO.md` for the active queue.

| Document | Role |
| --- | --- |
| `docs/development/0.7/next-stage-guide-zh.md` | Next-stage development handbook: objective, ownership, slice order, and acceptance criteria. |
| `docs/build-and-debug.md` | Operational commands, build-input selection, diagnostic interpretation, and automated-evidence limits. |
| `docs/development/0.7/05-api-contract.md` | Existing 0.7.x contracts and migrations. |
| `docs/development/0.7/40-verification.md` | Earlier line verification record; evidence applies to its named scope and revision. |
| `docs/api-tiers.md` | Compatibility commitment by type. |
| `docs/consumers/consume-from-0.7.0.md` | Consumer integration and migration guidance. |
| `docs/development/0.7/60-capability-dispositions.md` | Existing capability decisions and their conditions; not the current execution order. |

### Maintainer direction and current boundary

- **Priority:** long-term FL stability and extensibility for shared mod use, accepting a delay to US.
  Use the existing US ModSettings redesign as the real integration surface. Retain the page model over
  Verse IMGUI and advance through complete consumer slices with explicit library/consumer ownership.
- **Popup anchoring (2026-09-28, still not game-accepted):** the maintainer passed both Packs and Tuning
  click-arbitration scenarios (A/B). The follow-up scroll-anchor fix is built and is inside the current
  rehearsal: a drawn trigger refreshes its window-space anchor, and leaving the effective nested clip or not
  drawing that owner closes the popup and clears stale hit layers in the same pass
  (`KernelPopupTests.VerifyOpenPopupFollowsTheTrigger`, reddened under separate faithful anchor, orphan-owner
  and scroll-clip reverts). FL-IC1 later made popup coverage geometric and bounded the menu to whole rows, so
  the open-then-scroll check must be re-walked in the same human pass as the IC1/IC2 items, not separately.
- **Implemented foundation:** configuration-isolated builds, explicit compatibility export, per-host
  consumed-input diagnostics, and consumer compiler-reference/package hash checks. Commands and
  failure-sensitive evidence are in `docs/build-and-debug.md`; this does not establish overall architecture
  stability or close the game defect.
- **Evidence boundary:** automated contracts, artifact identity, and real-game E2E are separate results.
  Stub-based success cannot close an in-game input/layout defect. Multiple-mod coexistence has not been
  demonstrated merely by making the library referenceable; the shared `UiFitAudit.Enabled` switch remains
  a risk to review, not a reproduced cross-mod failure.
- Branch `0.7.x`, API `0.7.0`, consumer range `[0.7.0,0.8.0)`. API tiers and the temporary 0.7.x version
  exemption retain their existing conditions; tags, releases and Workshop publication remain separately
  authorized (the one historical push event is recorded in "Current state").
- Packages identify their own bytes through `version.txt`, embedded source identity and measured hashes.
  A docs-only commit does not rebuild them. Any strict embedded-commit-versus-HEAD check needs a fresh
  build before its next acceptance run; previous evidence remains scoped to the artifact actually tested.

### The library-owned development instrument

`UiDiagnosticSubscription.GeometryEnabled` / `.GeometryOverlay` / `.DumpGeometry()` expose the bounded,
per-host instrument in `UiDevGeometryProbe.cs`. It observes engine geometry and native input decisions;
its samples never govern layout or dispatch. Host entry preserves the event phase before consumption,
queries report event-before/event-after, and an already-Used entry explicitly marks its origin unknown.
Release excludes the capture and rejects enabling it. Gate 10 executes the Dev assertions, including the
consumed-input case; gate 1 alone is not Dev coverage. Usage and proof details belong to the operational guide.

## Current durable state

The dated records below explain earlier changes and their scoped evidence. Current priorities are above;
current build behavior is in "Carrier identity and build isolation". Historical gate counts, capture sites,
and delivery procedures do not override the current scripts or the next-stage handbook.

### Evidence lessons (measured cases, not standing acceptance gates)

- Earlier rounds produced misleading red and green results when the fixture, ruler, screen or observation
  channel differed from the intended input. A named product backout proved specific fixes; a guard that
  stays green on both sides is regression protection, not the proof. A channel change changed which checks
  applied. These are reasons to select relevant verification, not a requirement to enlarge every suite or
  to treat every unexecuted mutation batch as a standing gate.
- Counter-based checks once reused another lane's accumulated findings. Resetting the measurement and
  checking the increment distinguished the current operation. A spatial budget under a stub ruler smaller
  than the game is not in-game geometry evidence.
- A new gate is useful when planting the defect makes the process exit non-zero. Printing FAIL without
  counting it is not a gate. Strengthening a gate belongs with the change it depends on; it is not a
  permanent ban on later recuts.
- Coherent verified changes were committed separately. Invoking a check without observing its completion
  did not establish a green result, and "fixed" does not cover an unbuilt or unverified half.

- **The mutation battery is tracked and self-testing (2026-09-24, task-15).** `tools/mutation/` holds the
  engine (`Invoke-Mutation.ps1`), the two halves (`mutation-check.ps1`), the batch that describes a round's
  mutations (`batches/t2-2026-09-24.ps1`) and the one-token commands they run. Logs stay in `dist/dev-work/`,
  which is gitignored - tracking the GENERATOR is what keeps a log's command findable (M8), and the log's own
  header carries it. The engine implements M1-M10/K3 as its README states: an anchor must be unique (a bare
  `.Replace` is a silent no-op); the run must exit non-zero **and** print the named assertion (a red for
  another reason is not evidence - measured once in this very battery); the restore is by bytes and is
  re-read and asserted; after the restore the mutated configuration is REBUILT and its artifact must come
  back to a **baseline established by the same command from clean source** (an earlier version compared
  against whatever was on disk and false-reddened on a baseline built at an older commit); the log binds
  itself; the artifact fingerprint is derived from the mutated file, so an unrelated artifact is refused
  rather than fingerprinted with no signal (K3); and a watched carrier may not move its hash or mtime (M10).
  Nine fixtures including a positive control prove the generator cannot lie; three of them are the acceptance
  checks (missing anchor, un-restorable target, unrelated artifact), each runnable alone with `-Fixture`.
  **M11 applies M6 to the instrument itself**: every log pins the generator's and the batch's own sha256, and
  the dirty set is a path plus a per-file sha256, because "the engine that ran is the committed engine" is
  otherwise an inference from a name list. A non-intended outcome label must also carry `# why this label:`,
  since two logs can share their red text and differ only in cause.
  **Input records cover only the inputs a log cannot derive** (2026-09-24; the first version of this rule was
  wrong and the second was loose): `-DriverCarrier` records the payload the command LINKS, separately from
  the carriers it may not move. **"Derivable" means exactly this: re-running the recorded build command at
  the recorded HEAD reproduces it - which is a BUILD, not a reading.** So the Fl half's driver carrier is
  derivable (its build is in this repository, at the recorded HEAD) while the consumer half's is not (a
  cross-repo moving target: it moved at least once inside one session) and must be recorded. The difference
  between the halves is not whether the data exists but whether the build the derivation needs is in this
  repository and at the recorded HEAD - and that is precisely why `-DriverCarrier` should exist: it turns
  "rebuildable" into "recorded". The first batch's 11 logs (engine `e7f2215`) lack the line, and only the
  three library-source cases (b1/b2/b11) get the number by another name, because there the K3 artifact IS the
  library Dev DLL and the baseline line already prints it; the other eight - a test assembly, the stub tree,
  or `none` - do not, so a read-only reviewer cannot obtain their driver identity at all. The purpose of an
  input record is to save the reader from having to ask the author, not to make the log longer.
  **Two carriers, two names:** `-WatchCarrier` is the frozen root payload; the *driver* carrier is what the
  command links, and the consumer half selects the Dev payload explicitly. The consumer half performs a
  cross-repo write, so it is the consumer owner's step and belongs in the consumer repository long term.
  The three one-shot migration scripts that used to sit under `dist/` are **not** tracked: their batch landed
  at `07f3c40`, they carry machine-specific absolute paths a tracked file may not contain, and their
  replace-as-you-go shape is what M9 forbids. `Invoke-AnchoredEdits.ps1` replaces them - collect operations,
  simulate every one, write only if all validated, `-ValidateOnly` to stop after the simulation.
- **The harness text convention: width is modelled, line height is CALIBRATED (2026-09-24, T27/T29).**
  Width: a CJK ideograph or full-width punctuation is one em, anything else about half an em -
  `advance = units * em * 0.5`, `em` = 12/16/18 for Tiny/Small-Large/Medium. Height: one line is
  **measured, not derived** - Tiny 18.0, Small 21.33333, Medium 30.0, calibrated from in-game
  `ui.text.overflow` need values - and a string needs `ceil(advance / width) * lineHeight`, so the answer
  depends on the text, the font AND the width. **Large is not calibrated** and borrows Small's until someone
  measures it; a formula filling that gap is the same defect in a new place. Both fit-audit and layout take
  ONE wrap-aware `ITextMetrics` instance (injected, never hard-coded). The Verse stub's `CalcHeight`
  returned a constant 16f until T29, which made every vertical budget and vertical fit verdict vacuous while
  the width axis stayed real. That change flips **no** lane - every `UiHost` lane must inject a ruler (the
  constructor throws on null) and the two `UiWindowHost` subclasses on the production default draw chrome
  whose assertions are rects and horizontal fit - so it is **future-proofing, not the repair of a current
  false green**; writing it as a repair would be a false claim.
  **A fixture's size comes from its claim, never from the instrument** (ledger rule). The wrap lane's
  paragraph case drew into a 40px band and `TinyBand = 16f` that *was* the dead ruler's return value, so it
  was green because the ruler could not count lines. It now derives its band in the lane and is paired with a
  **negative control** - the same paragraph in half the height it needs must be reported - because a silent
  assertion alone can never see a dead ruler; a diagnostic assertion compares the lane's arithmetic with the
  ruler so the two cannot drift in silence. In that lane `VerifyHeightFinding` is **PLUMBING** (one line in a
  wide band: axis routing, not wrapping). Absolute pixel truth stays the real font engine's.
- **A run's log carries its own identity (2026-09-25, T31).** `tools/evidence/Write-EvidenceFooter.ps1`
  appends the M6/M11 footer - generator and batch hashes, HEAD, the dirty set, the configuration-derived
  artifact line, the two carriers as hash+mtime pairs, the gate lines and the pinned files - and
  `verify-local.ps1 -EvidenceFooter` prints it as the last output of a GREEN chain, so a redirected log answers
  "which bytes produced this?" itself instead of being bound to a commit by the reader's inference from
  timestamps. (A red chain exits from inside `Invoke-Check`, so a red log is bound by calling the writer
  directly and stating the non-zero exit.) The SHAPE is the consumer repository's writer, deliberately: two
  footers would be the drift this project has already paid for once. A footer added after the fact is marked
  `recomputed, not recorded at run time` and says that its `git HEAD` is the HEAD NOW, not at run time.
  Its `# generator:` is the WRITER's own sha256 and its `# batch:` is
  `SHA256(subject|command|configuration|exit|HEAD|dirty)` - verify both by RECOMPUTING them, never by looking
  for a same-named file.

<!-- archived verbatim: OBLIVIONIS.md, 2026-10-09 compaction, block MEMORY.md:305-387 -->

- **Three durable rules lifted out of the R12/R3-A narratives so they stay live.** (1) **A scheme a fixture never
  selects proves nothing**: three R12 fixtures declared a NAMED scheme while `ApplyTo` selects only a ROOT one,
  so the lane asserted a path that never ran — a lane anchor can be wrong while the product is right.
  (2) **One capture, three renderings**: the instrument's text dump, overlay and machine-readable snapshot all
  read the SAME `UiDevGeometryCapture` filled during the real pass, so a second collection or a replay is a
  defect, not a convenience; the overlay paints only entries the bounded capture retained.
  (3) **A public type may not live inside a configuration `#if`.** `FerriteLibApiTierTests` requires every
  exported type to be classified (impossible for a Dev-only type in a Dev build) and every classified type to
  exist (impossible in a Release build), so a configuration-dependent public type fails in one configuration and
  is a compile-against-Dev/ship-against-Release trap; the Dev DTOs stay outside `#if FER_DEV` and the reader
  answers `false` in Release.

- **The half-width convention exists TWICE, and both copies are now compared (2026-09-25, T31).** The
  tests-side `StubTextWidth` and the inline model inside the Verse stub's `Text.CalcSize` implement the same
  convention; the second is the one `VerseFerriteTextMetrics` - and therefore a production host - walks.
  Nothing compared them, so an edit to either was a silent divergence. Measured by mutating the STUB copy alone:
  **6 assertions reddened and all six were the new cross-check** (`VerifyStubWidthModelMatchesTheSharedOne`),
  nothing else in the harness noticed. Mutating the tests-side copy reddens 23 assertions in all, so that copy
  is load-bearing across the lanes; the stub copy had no lane at all.
- **Measured correction:** all **5** `UiPageWindow` construction sites inject a ruler (2 in the recipe and
  lifecycle lanes, 3 in the window-catalog lane). An earlier note in a commit message said "both", which came
  from a truncated search - the same error class as the truncated call-site list this ledger already records
  once. Two `UiWindowHost` subclasses (`ProbeShell`, `LaneShellBase`) do take the production default, for chrome
  whose assertions are rects and horizontal fit.
- **A popup layer covers exactly its option rows, so a press inside it always consumes a row.** `UiPopup.RectFor`
  returns a rect whose height is a whole number of `OptionHeight` rows (since FL-IC1 it is bounded to the
  viewport, which keeps that property rather than breaking it) and both option lists tile one row per visible
  option across it, so every point inside the layer is inside a row. The row fires and calls `Session.ClosePopup()`,
  which drops the layer from BOTH `hitLayers` and `dispatchLayers`. Consequence for a lane: "assert the popup is
  still open after a covered press" is not a control that can hold - the covered press closes it through the row.
  What a lane CAN assert is the value write (the option row, not the trigger, consumed the click) plus the
  counterfactual (with the layer gone, the same press hits and dispatches).
  **Corrected 2026-10-08 (FL-IC1): the last sentence of the original note is superseded.** It said a rect-blind
  override reddened the premise "because the trigger draws first, fires, and closes the popup it owns" - that was
  the owner-id exemption's behaviour, and the exemption was the F09/D1 defect. Coverage is geometric now: a press
  inside the menu belongs to the row even when the menu lies over the trigger that owns it, so the trigger draws
  first, *yields*, and the row fires. The `covered` verdict and its mutation proof are unchanged; what changed is
  that a dropdown's own popup no longer escapes that verdict.

<!-- archived verbatim: OBLIVIONIS.md, 2026-10-09 compaction, block MEMORY.md:414-1036 -->

- **Two durable facts lifted out of those narratives so they stay live.** (1) On net472,
  `string.Split(char)` binds to the `Split(char, StringSplitOptions)` overload this framework does not have:
  a `MissingMethodException` at run time, not at build time — the array form is the one that exists.
  (2) The `KernelRepeatTests` vocabulary-drift guard's failure message lists only the host-minus-engine
  difference, so when the HOST list is the smaller one it prints "missing: " empty; the lane still fires, only
  the message is one-directional.

## Version axes and the compatibility promise

- **Three axes, and the harness pins all three agreeing on major.minor.** Contract
  `FerriteLibVersion.Api`, release `About/About.xml <modVersion>`, build csproj `<VersionPrefix>` (the build
  axis is what `pack-dev` derives the artifact name and `version.txt` from). Re-derive, never quote:
  `grep -n 'Api = new Version' Source/FerriteLib.UiKit/Kernel/FerriteLibVersion.cs`, `<modVersion>`,
  `<VersionPrefix>`. `AssemblyInformationalVersion` embeds the commit and is never a compatibility value.
  On the 0.7.x line the axes are `0.7.0` and the consumer range is `[0.7.0,0.8.0)`.

- **TEMPORARY EXEMPTION (maintainer ruling 2026-09-22): a public addition does NOT bump the minor on the
  0.7.x line.** Words at filing: "minor 可以不升级，仍然算到 0.7.x 内"; the qualifying reason came the same day:
  **"这个算临时放行，因为现在就在跨库合作开发 fl0.7.x"**. **What it is:** a release valve for the duration of the
  `0.7.x` cross-repository lockstep development — NOT a change to the rule itself. **Criterion:** the question
  is whether the change is an *addition to the public surface*; if it is, it lands inside `0.7.0` with the
  axes unmoved (scope: this stage / the `0.7.x` line — contract axis `0.7.0`, consumer range
  `[0.7.0,0.8.0)`). **Reason:** library and consumer evolve in lockstep, so a minor bump buys no
  compatibility signal there and only churns both trees. **Expiry (suggested wording; a later maintainer
  ruling overrides it):** the exemption lapses at the FIRST of — the `0.7.x` line's first release/tag, or
  the end of the cross-repo lockstep development; from then on the generic pre-1.0 rule resumes and a
  public addition moves the minor again. **Not inheritable:** it belongs to the `0.7.x` line and this stage
  alone, and may not be carried into the next line or into any published state by analogy.
  **Relation to the entries now archived:** it does not delete the generic pre-1.0 promise (`0.5.0 -> 0.6.0`
  remains the precedent for a DELIVERED line) and it is not a case-by-case exception either — it is the same
  line ruling the 2026-09-20 entry already carried ("additions are allowed inside `0.7.0` while the local
  consumer is mid-development", archived in `OBLIVIONIS.md` 2026-10-09), now stated by the maintainer directly
  rather than derived from "an rc that never shipped has no goalpost to move". **Consequence for the next
  session: do not raise `Api.Minor` for a public addition while the line is `0.7.x` — and do not treat this
  exemption as the rule once the line has shipped.** **Status 2026-10-09: still in force** — the line has no
  release and no tag, and the `interaction-20261009-r1` rehearsal is a staged directory, not a publication, so
  neither expiry criterion has been met; FL-IC1's and FL-IC2's public additions landed inside `0.7.0` under it.
  **The gate half: there is nothing to re-cut — audited 2026-09-22, lane by lane.** Every lane that reads
  the public surface or a version axis, named: (1) **`FerriteLibVersionTests`** (Program.cs:76) — pins
  `Api` == `<modVersion>` == `<VersionPrefix>` on major.minor (`:260`, `:316`) and drives `Require`/`Evaluate`
  with synthetic ranges; it never enumerates the surface and never sees an addition, so it needs no change;
  (2) **`FerriteLibApiTierTests`** (Program.cs:79) — reacts to an addition by requiring the new type in
  `docs/api-tiers.md`, and to a stable-tier move by requiring the pinned array here to move with it
  (`:114-117`); that is a **classification** act, NOT a version bump, and the lead's `api-tiers` claim
  resolves exactly here; (3) **`KernelContractTests`** (`:675-677`) calls `Require` with the loaded `Api`, a
  behaviour probe, not a surface reader; (4) **the script gates** — `verify-local` gate 7 only checks that
  `<modVersion>` is present and parses, and the packers compare `<VersionPrefix>` against the tag/label,
  never the surface. **=> No lane reads "additions", and no lane or gate anywhere enforces the minor bump.**
  Therefore no re-cut is owed for this exemption; if a future lane starts reading additions (or its message
  wording starts asserting the minor rule) align it in the batch that lands the next capability — running
  the harness and library must exercise that change together. Builds now use isolated outputs; see "Carrier identity and build isolation".
  **A wrong statement of the lead's is corrected here for the record:** the lead told the maintainer that
  "the gates would block your approved change". That is false as stated — what reacts is `api-tiers`'
  classification lane, satisfied by classifying the new type, while the "bump the minor" half was never
  executed by any gate. The defect was the wording in `AGENTS.md` (generic rule stated without the line's
  exemption), and that wording is now fixed. Scope of this entry: a version-axis and document ruling only —
  no source, lane, tier, payload or release changed, and no build was run (the frozen carrier
  `0F95DF35…DB10D6` at HEAD `b997f3a9` was left untouched).

- **The pre-1.0 rule: any change to the public surface — additions included — bumps `Api.Minor`,** and
  `modVersion` moves with it. The 0.1.0 -> 0.2.0 bump exists because an additive type (`UiPopup`) shipped
  without one and a consumer desynced into a `TypeLoadException` inside `UiHost.Draw`. A bump is the
  breaking signal and the "does this carrier contain what I compiled against" counter at the same time,
  which is what makes `Require`'s readable-report promise hold in both directions.
- **An rc that never shipped has no goalpost to move.** Additions are allowed inside `0.7.0` while the local
  consumer is mid-development (opening entry, 2026-09-20); the `0.4.0 -> 0.3.0` refile is the precedent, and
  the `0.5.0 -> 0.6.0` move is not — there the dev package and its handoff had been delivered. When the
  phase ends the normal rule returns: an addition moves the minor.
- **The game cannot express a prerequisite version** (`Verse.ModRequirement` parses only `packageId`,
  `alternativePackageIds`, `displayName`; `ModDependency` adds only download URLs), so every consumer
  asserts the API range in its own constructor. `ModAssemblyHandler` holds a global resolver flag, which is
  why duplicate carriers resolve by load order silently — the collision report is the only detector.
- **Manifest vocabulary retires the way code does**: a deprecated attribute keeps working, redirected to its
  replacement rather than ignored, for at least one minor; removal only at a minor boundary.
- **The promise is an artifact: `docs/api-tiers.md` plus its guard lane.** Every exported type is classified
  stable / public-unstable / internalize-candidate exactly once, the stable list is pinned a second time
  inside `FerriteLibApiTierTests`, and an unclassified addition reddens the lane. A promotion or demotion
  needs two deliberate edits in one commit. **What the lane reads, precisely — four assertions, and no
  reason text:** `Every public payload type is classified in exactly one tier` (`:77-87`) reads exported
  type **names** against the entries parsed from the three headings; `No tier entry names a type that no
  longer exists` (`:89-100`) reads an entry whose type has gone; `The stable tier matches the pinned
  promise` (`:102-120`) reads the **stable** section only, against `PinnedStableTier` (`:36-50`); and
  `Tier comparison fires on a planted unclassified type` (`:122-152`) is the positive control. **No
  assertion reads an entry's reason prose**, so a reason that stopped being true cannot redden anything —
  "the entry exists" is the whole membership test. That is how `LineChartWidget` / `UiChartPointChange` kept
  a reason the consumer tree had already deleted; the review, the citations and the owed dispositions are in
  `docs/development/0.7/60-capability-dispositions.md` §C (its C.0 states the same blind spot from the
  document side), and `TODO.md` carries the decision. **API stabilization is not gated on a consumer count**
  (2026-09-17 ruling): define the contract, verify it, state the commitment; integration validates it.
- **One assembly, two layers.** The declarative page engine (manifest, constrained layout, typed bindings,
  per-window session, widget registry, creation-time validation) and the visual core (theme tokens, drawing
  helpers, text measurement, fit audit, version contract) ship in one DLL; the split is enforced by a
  name-level lane. Re-open only if a consumer needs the visual core without the page model.
- **What earns a kind** is ownership, not atomicity: per-element interaction state, a measure contract over
  its own content, or a hit/geometry rule. The promotion gate (provenance cited; neutral, no consumer
  numbers as library defaults; no new process-wide mutable statics; a harness-drivable lane) governs *new
  specialized kinds*, not confirmed general capabilities. Our own demo is never consumption.
- **Growth has one legal source**: a real consumer was forced to hand-roll something. A request is not
  evidence; a transcription into this file (`owner/repo@sha:path:line` + excerpt + what it proved) is.
- **Nothing persists into a save**, and the Squeaky Ratkin repo is never a write target.

## Carrier identity and build isolation

- **A hash is an identity only with its inputs pinned.** `1.6/Assemblies/FerriteLib.UiKit.dll` is the whole
  payload; a hash quoted without the build command and the clean/dirty state is a machine-local fact.
  Measured 2026-09-20: the build is **deterministic** (a forced rebuild at the old stamp reproduced the
  bytes exactly), so a differing hash means different inputs. Byte length is not a signal.
- **The stamp records the committed revision and is silent about uncommitted source.** Committing source
  moves HEAD, which leaves a stale stamp and makes every attribution check refuse it; the residual window
  is a dirty build whose source is never committed. Hence the delivery order — **commit first, build last,
  report the identity measured from that build** — and hence a doc-only commit reddens "embedded commit ==
  HEAD" until the carrier is rebuilt. That is expected, not a defect: broadcast the moved HEAD.
- **Current output contract:** builds and harnesses write `dist/build/<Configuration>`. Packagers read the
  matching output and stage into `dist/<channel>`; only `export-carrier.ps1` updates the root compatibility
  DLL, and it accepts Release only. Verification checks that the held compatibility DLL's hash and mtime
  are unchanged. The former shared-output Release rebuild/PDB-removal ritual is retired.
- **Loaded files can still be locked.** Read assembly metadata in a child process or a copy. Serialize
  harness builds because their canonical `bin/stubs` output remains shared; isolate build configurations
  without assuming that arbitrary concurrent writers of the same artifact are safe.
- **Identity comes from the selected artifact.** Configuration is measured from
  `AssemblyConfigurationAttribute`, not inferred from the version suffix. Staged `version.txt` records the
  payload hash. An exporter copies the last built Release artifact and prints its identity; it does not
  prove current HEAD or a clean source tree. Publication and consumer release checks retain stricter rules.

- **Carrier identity moved three times on 2026-09-22, and `0.7.x` was first-pushed the same day.** History:
  `876750a`/`58B57EAD…` -> `e668344`/`1BBF5F4D…` -> `553fc53`/`3FA8CABE…` (all 252416 B, Release, no PDB) ->
  `06ff1c2`/`1891A5CE…D649`/252416 B (the frozen Release the maintainer's live in-game pass ran against) -> a
  2026-09-23 rebuild at HEAD `9938121` after gate 10 landed (253440 B, Release, no PDB), **whose hash is
  deliberately not quoted here**. Those deliveries used the old FREEZE NOTICE workflow (hash plus mtime);
  current staged identities are read from the selected package, not inferred from this history.
  **2026-09-25 delivery (T32).** The frozen carrier's identity is recorded in the CROSS-REPOSITORY coordination
  ledger (§14.43 / §16 - outside both repositories, which is the only place it can live). What belongs in a
  tracked file is the RULE, because of a self-reference that is not a discipline problem: **committed files
  cannot contain the hash of a carrier built from themselves.** The payload embeds the SHA of the commit it was
  built from, so writing that SHA down requires a commit, that commit moves HEAD, and the next build is a
  different payload - the number can never catch itself. The shape recorded here is therefore: the export commit
  MUST be the current HEAD **at export time**; Release; 253440 B; **no PDB**; byte-identical to the
  `dist/build/Release` artifact the same day's chain verified; and the paired dev package NOT re-cut (`Source/`
  unchanged since `07f3c40`, measured, which is what keeps the consumer's embedded
  `FerriteLib.SHA256=4729E275…` valid).
  **Order is the rule: commit first, export last.** The consumer's chain has two teeth and this order is what
  keeps clear of both: a commit after the export leaves the payload's embedded commit behind HEAD, and comparing
  the two SHAs *for equality* counts a documentation commit as payload staleness, so that chain **throws**
  rather than going soft-red; and the same chain requires the carrier checkout to be **clean**, so an
  uncommitted tree fails it too. The invariant that actually matters is that the payload's product SOURCE equals
  the current product source - which is what the consumer's check is being re-cut to (task-36): the embedded
  commit is an ancestor of HEAD AND `git diff <embedded>..HEAD -- Source` is empty.
  **This line previously recorded `4d66cf7` / `BF633AF6…`; superseded and wrong**, because that export ran
  before its ledger commit instead of after. Kept as a correction rather than deleted.
  **Delivery checklist, in order - each step is the one that failed if the next throws:** (a) commit everything
  first; (b) `dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release`; (c)
  `scripts/export-carrier.ps1`; (d) assert no PDB beside the carrier; (e)
  `scripts/verify-local.ps1 -NoRestore -EvidenceFooter`; (f) re-check the carrier pair AND that `dist/dev` did
  not move; (g) issue the FREEZE NOTICE (hash + mtime as a pair) and put the identity in the coordination
  ledger; (h) commit nothing that moves HEAD until the next delivery.
  **Status at handover:** the FL half of this delivery is complete and independently reviewed (six items, plus
  three recomputations of the footer's fields), with the carrier, the `dist/build/Release` artifact and the
  exported root payload all the same bytes. The push
  went out after `privacy-audit -FullHistory` was
  CLEAN over 342 revisions; **no tag was created, so nothing was published** (`release.yml` fires on
  tags/releases only). **US's carrier stayed byte-identical across its full chain run** - the measurement behind
  "a sibling consumer reads the payload read-only".
- **Staging is candidate-then-commit (2026-09-22, adopted from the demo's packer, which has always worked this
  way).** `stage-package.ps1` copies and asserts against a SCRATCH tree (`dist/<channel>/.staging-<Mod>`) and
  replaces the delivered folder only once every assertion has passed; it then re-points `$stageDir` at the
  delivered path, so the archive step is unchanged. **Why:** the old order destroyed the delivered folder
  first and asserted afterwards, so a refused pack left a half-written package behind. **Proof:** with a
  post-copy failure planted, the pack exited non-zero and the delivered folder's file-hash fingerprint was
  IDENTICAL before and after; the restored script exits 0 and leaves no `.staging-*` behind.
## Packaging discipline
- **Prior push lessons (moved from AGENTS on 2026-10-08).** The 0.6 first-push round paid for a
  full-history scan before an unpublished line left the machine: a working-tree fix still failed while
  history kept the blob, and a backup tag created after the fix re-exposed that history. Keep recovery
  copies as local bundles rather than extra Git refs. Unpublished-range rewrites, when separately
  authorized, preserved dates/messages and required a byte-identical tip plus rebuilt payloads because
  `AssemblyInformationalVersion` embeds the SHA. A stray commit identity forced rewriting everything
  after it. Stale feature branches and worktrees also kept history reachable. Those incidents explain
  checking actual refs and inputs; they do not establish a permanent ban on authorized history repair or
  require deleting the current repository's version branches automatically.

- **Three channels, one staging engine.** `stage-package.ps1` owns what a package *is* (closed five-file
  set, content probe on named paths, licence copy, `version.txt`, optional deterministic archive) and
  measures the payload's configuration, so a channel label that does not match the bytes is refused
  rather than trusted. The packers own identity only: dev tolerates a dirty tree and says so, github
  requires the tag shape and the build axis, steam additionally a clean tree, and only github archives.
  `About/PublishedFileId.txt` is gitignored and the stager copies `About.xml` as a file, so no rehearsal
  or GitHub artifact can carry a Workshop identity.
- **A release asset is built after the gates run, not during them.** Every gate builds into
  `dist/build/<Configuration>` and leaves both configurations there; there is no shared build slot. The
  stager *measures* the payload's configuration instead of trusting the channel label
  (`stage-package.ps1`), so a Dev payload cannot reach the github/steam channels, and the release workflow
  rebuilds with `-p:VersionSuffix=` between the gates and the pack (`--no-incremental`, so "freshly built"
  is literally true). The packers refuse a stale payload by comparing the embedded commit to HEAD. **None
  of this touches the root compatibility carrier** — `scripts/export-carrier.ps1` is its only writer.
- **The GitHub archive is a function of the commit.** Entry timestamps are written into the archive
  (sorted enumeration, each entry at the tagged commit's author date, top-level `FerriteLib/` inside);
  `Compress-Archive`'s mtime stamping gave two digests for one commit. No digest value is quoted in these
  files — a stale one reads like a live contract. The dev channel's optional `-Zip` is not deterministic.
- **Nothing under `scripts/` writes outside the repository**, and no step copies, links or junctions
  anything into a `Mods/` directory; installing a staged folder is the developer's own step.
- **A consumer integrates through the published GitHub Release asset.** A same-level sibling folder with
  `Private=false` is one developer's lockstep arrangement, not a contract. Either way `1.6/Assemblies/`
  and `tools/.../Stubs/` with its `bin/stubs/` shape are de-facto published surfaces: relocating them
  breaks a consumer's harness while every gate here stays green.

## Public surface, structure and the memory protocol

- **The payload is exactly `1.6/Assemblies/FerriteLib.UiKit.dll`** — zero Defs, Patches, Languages,
  textures. Gate 5 probes those names rather than filtering an enumeration, so an empty tree cannot pass
  vacuously. The DLL and PDB are gitignored; only `.gitkeep` is tracked, so a fresh clone has no payload
  until it builds.
- **`Source/FerriteLib.UiKit/Kernel/`** is the whole payload surface (`net472`, `TreatWarningsAsErrors`,
  `Nullable` on, output pinned to `1.6/Assemblies/`). Page model: `UiHost`, `UiWindowHost`, `UiSession`,
  `UiLayoutEngine`, `UiLayoutManifest`/`UiLayoutSnapshot`, `UiBindings`/`IUiBindings`, `UiWidgetRegistry`,
  `KernelCoreWidgetRegistrar`, `UiWidgetContext`, `UiElementSpec`, `UiSessionGuard`, `UiValueState`,
  `UiNative`, `UiPopup`, `Widgets/` (the registered kinds). Visual core: `UiTheme`, `UiThemeDraw`,
  `UiFitAudit`, `UiKitFonts`/`UiFont`, `ITextMetrics`, `VerseFerriteTextMetrics`. `FerriteLibVersion` is
  BCL-only on purpose so a consumer can call `Require` from its earliest constructor; since item E it
  reads `UiHostLedger` (also BCL-only), so the pure decision stays in `Evaluate`.
- **`tools/FerriteLib.UiKit.Tests/`** is a plain `Main()` runner (no test framework): lane files plus
  `Program` and `StubTextWidth`, with four standalone stub projects under `Stubs/`. The stub tree is
  reference-driven and gated — a member whose semantics cannot be verified stays missing (a hole that
  throws beats a double that lies), and every member a lane calls is taken over by the scan.
- **Backend containment is a gate, and the funnel is five files**: `UiNative.cs`, `UiThemeDraw.cs`,
  `UiLayoutEngine.cs` (four structural scopes only), `UiSessionGuard.cs`, `VerseFerriteTextMetrics.cs`.
  An allowlisted file that no longer reaches one of its granted names is a failure, and `UiWindowHost.cs`
  is deliberately absent. Raw IMGUI outside the tree is unsupported and unmeasurable, not forbidden.
- **`UiPopup` is the single popup geometry and input primitive**, and **`UiWindowHost` owns window
  chrome** — a consumer's window is a subclass, not a re-implementation. Its failure contract is
  deliberate: a pass that throws reports once on the *next* pass from a clean IMGUI state and is terminal
  for the instance. Do not "improve" it into a synchronous retry.
- **`UiHost` is per-window and disposable**; the consumer disposes it, so nothing here may hold
  process-wide interaction state. **Recovery belongs to the tree**: the engine wraps every element's
  Measure and Draw through the session guard and paints a recovery band in that element's rect.
- **The one wired consumer is `Coahuilite/UniversalSqueaker`** (its own `0.5.x` line), integrating through the
  kernel settings redesign plus its own diagnostics and developer-tool windows; the independent demo tree is a
  reference consumer, never a second real one, and renders no evidence value here. Any coverage count must be
  re-derived against its published tree at a pinned revision — the measured census is the archived 2026-09-04
  record in `OBLIVIONIS.md`, and quoting it as current is the stale-anchor error this ledger already names.
  Until a citation raises the number, the honest validated-surface statement stays **"3 of 7 kinds"**.
- **Memory protocol**: `AGENTS.md` stable · `MEMORY.md` this ledger · `TODO.md` action surface ·
  `OBLIVIONIS.md` cold archive. Two compactions have run: the 2026-09-20 one moved this file's
  pre-2026-09-17 body (consumer coverage and the kind census, the Charter and its surveys, the style-layer
  argument, the structure narrative, the consumer contract, the cross-repo round couplings), and the
  2026-10-09 one moved the 2026-09-17..10-07 dated round narratives. Both moved **verbatim**, each with its own
  index entry and frozen-block markers. Read them there only for a historical conflict; they cannot override
  current sources.
- **Inbound `HANDOFF.md` is transient and is not a memory tier.** Durable content is promoted here at the
  moment of a round; an open round keeps a pointer line in `TODO.md`, a closed round leaves nothing behind.

## Environment facts (cheap to get wrong, expensive to re-learn)

- **RimWorld's minimum window is 1024x768, and UI scale cannot push the logical width below it.**
  `UIScaleSafeWithResolution` requires `w/scale >= 1024` and `h/scale >= 768`, so `UI.screenWidth` is
  always at least 1024. A narrow-layout trigger therefore fires only just above that floor
  (the maintainer's working window for it is a logical width in `[1024, 1131]`; re-derive the upper end from
  the window you actually test at). A lane that assumes it can shrink the logical viewport freely to test
  `Breakpoint` is measuring a state the game cannot produce.
- **Adding an audit hook is two halves, and the switch alone measures nothing.** `Enabled` only opens the
  toggle; the host must additionally hold **its own subscription**. A host with no subscription measures
  through a null ruler — every reading is empty, and a green result means nothing was measured.
- **The game has no style layer to inherit** and `Verse.ModRequirement` cannot pin a version (see "Version
  axes"); the payload owns all appearance, which is why a style document would be this library's own
  contract rather than an interop with the host.

- **The SSH transport to the remote is intercepted on this machine; HTTPS through the local proxy is not.**
  `github.com` resolves into the fake-IP range (`198.18.x.x`) and port 22 is closed mid-handshake
  (`Connection closed by UNKNOWN port 65535`), while `git -c http.proxy=http://127.0.0.1:7897 push <https-url>`
  succeeds - that is how the 0.7.x first push went out. Probe the transport before blaming permissions.
- **Build commands need the project path.** The repository root has no project file; use
  `dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release`. It now writes the isolated Release
  output. Updating a compatibility delivery remains a separate explicit export.
## Diagnostic coverage — the rulings that shape the channels

- **`Available` is the inset label band, not the element rect** (FL-21). An overflow verdict compares the
  band against the content it could not hold (`Needed`).
- **A fit-audit count is distinct findings, never a census** (FL-22): dedup key, `MaxReports = 48`,
  `Saturated`, `Reset`. **The counters are cumulative and process-wide, so a count-assertion must `Reset()`
  and measure a delta** — a lane that skipped that passed against the unfixed tree.
- **A missing translation key is drawn as the key, on purpose**; the check belongs to the host, which is
  the only party that sees both its keys and the loaded language data.
- **A measurement/notification channel change invalidates every lane that asserts the old channel.** A
  stale lane is worse than no lane. When the hover rule changed, the round-3 lane was corrected with it.
- **An overflow record carries exactly two non-content discriminators** (`RectWidth`, `TextLength`),
  because a record must not carry UI text.
- **A lane must be red under a FAITHFUL revert**, and a lane that cannot tell the two states apart is not
  evidence. One mutation per item, attributable by assertion name.
- **Check the instrument's inputs before changing the product or the assertion.** A lane can go green for
  the wrong reason (a fallback accepting the case under test, an accumulator another lane satisfied, a stub
  ruler smaller than the real one, a stale DLL the scan read) and — measured this phase — red for the wrong
  reason too (a lane with no language table measuring the key instead of the translation). **Red is no more
  trustworthy than green.**
- **A spatial-budget assertion only proves anything at the real size**: under a small stub ruler the content
  is shorter than it is in the game, so a budget that would collapse for real passes anyway.
- **A lane that prints a failure without counting it is not a gate.** The criterion for a new gate is not
  "the console shows FAIL" but "plant the defect and the process exits non-zero".
- **Counter-type assertions must `Reset()` and measure an increment** — a process-level accumulator can be
  satisfied by a finding another lane produced.

- **Scope an assertion to the region that can legitimately carry the defect.** Gate 6's first version
  searched the whole `LICENSE` for the Exhibit B sentence and went red on a *correct* licence, because the
  reproduced MPL body always contains that sample notice.
- **Assertion order is part of an assertion.** A `Copy-Item LICENSE` placed before the staging directory's
  `Remove-Item` would have shipped a package with no licence while every gate stayed green; any claim
  about package contents must run after the tree is final.
- Mutation-test a new gate, not only the new feature. Half of `VerifyThemeColorsDoNotAffectLayout` is a
  future-regression guard rather than present evidence, and it is labelled that way for exactly this
  reason.
- **`Mouse.IsOver` does not mean the same thing in the harness as in the game (measured 2026-09-24 by the
  defect scout, read-only).** The harness stub checks the GUI clip region
  (`tools/FerriteLib.UiKit.Tests/Stubs/VerseStub/VerseStubs.cs:558-567`); the game's own `Mouse.IsOver`
  does not (`Mouse.cs:27-34` in the game's source). Consequence, recorded before it can bite: a future lane
  built on "the pointer is really over this rect" would report a **false red** at a scroll viewport's edge,
  because the harness applies a clip the game does not. Same family as "the lane measured the wrong
  layer": it changes no conclusion today and it is exactly the kind of difference that makes the next lane
  lie.

## Gates and what each actually proves

Ten gates in `scripts/verify-local.ps1`: 1 harness, 2 Dev build (`FER_DEV`, warnings-as-errors), 3
Release build (warnings-as-errors), 4 payload present, 5 content-free (named-path probe for `Defs`,
`Patches`, `Languages`, `Sounds`, `Textures`, `ThingSets` under `1.6/` - probed as names, not filtered
from an enumeration, so an empty tree cannot pass vacuously), 6 LICENSE, 7 About.xml identity, 8 two
halves (the net472 source-shape scan and the reference-driven stub-coverage scan), 9
`dependency-reality.ps1 -SelfTest` over a TEMP fixture tree, so the boundary tool's pattern set is
proven able to go red on every full run rather than only when someone remembers `-SelfTest`, 10
`verify-dev-instrument.ps1` — the development-only geometry instrument's **dev half**. Gate 1 runs the
harness in Release, where `#if FER_DEV` is undefined, so gate 10 builds and runs it in **Dev** and refuses
to accept "the project compiled": every named dev-only assertion must be observed as a **passing `ok:`
line** (the count is measured from the output and printed beside the list length — the earlier wording
quoted the list constant as if it were the measurement, and its `ok:`-line floor of 12 could not move
against a real run's thousands), the release-only half's name must be absent, and the printed numbers must
be there (dump header, a floor on dumped-node lines, a press line). Its checker is control-tested on every
run (an empty output, a release-half-only output, a green-exit-but-missing-name output, an output where a
listed name is present but not as a passing assertion, and a fully populated sample that must NOT be
rejected). **No gate writes the compatibility carrier.** Gates 1, 2, 3 and 10 build into
`dist/build/<Configuration>` (the library csproj pins `OutputPath` there and gate 4 asserts the evaluated
`TargetPath` equals it), and the chain reads the root `1.6/Assemblies/` payload read-only: it snapshots
hash+mtime before the first gate and refuses to finish if either moved. `scripts/export-carrier.ps1` is the
**only** writer of that path — Release-only, an atomic `[IO.File]::Replace`, and it removes the PDB — and no
gate, packer or workflow calls it, so a delivery is a deliberate step that ends the current freeze and
needs a new FREEZE NOTICE. (The earlier text here called gate 10 "a third writer of the carrier" and had
gate 2 writing Dev bytes into it; both were true before builds became configuration-isolated in
`07f3c40`, and the sentence is corrected rather than deleted so the contradiction with "Carrier identity
and build isolation" is not left standing.) The three
source-text gates and the version axes all run *inside* gate 1, so **the gate count is not the check
count**. Gate 6 proves what is visible from inside this repo, and since 2026-09-24 that is the whole file:
it runs `scripts/verify-license.ps1`, which asserts the MPL-2.0 title, Exhibit B, section 10.4, the absence
of the applied incompatibility notice — and **pins the series licence's SHA-256 as a literal**
(`71B96808…`, 15 780 bytes, measured byte-identical to the wired consumer's copy), so a truncation that
still carries every searched phrase is caught too. The checker is a script of its own so the mutation proof
("edit `LICENSE` and the gate goes red") runs against this gate alone, and it is read-only, which is why
gate 6's retry hint is a re-run of it rather than a build.
It does **not** compare the text against a consumer's copy, and no script in this repo refers to a sibling
repo at all (checked: no `..\` path in `scripts/`). That half — this file byte-identical to the consumer's
copy — stays consumer-side: US's own gate 10 hashes both copies.

Dated correction, 2026-09-04: earlier text here credited gate 6 (as "the seventh") with a SHA-256
comparison to the consumer's copy. It never ran one then, and **a `LICENSE` edited or truncated in this repo
could not turn any gate here red** — only a consumer-side gate noticed, and only while its sibling tree was
present. Closed 2026-09-24: the local half now exists as the pinned literal above, and the dated correction
is kept because anything that repeats "byte-identical to the consumer's copy" as a property of this repo's
gates is still wrong in the same way.

Lane and assertion counts are re-derived, never quoted — they move within a day of work:
`ls tools/FerriteLib.UiKit.Tests/*Tests.cs | wc -l`, `grep -c 'Run("' tools/FerriteLib.UiKit.Tests/*Tests.cs`,
and `grep -c '^  ok:'` on a release run. Conflating files with assertions is a rot this ledger has
already paid for twice. Three assertion groups are worth naming because they are the reason this repo can
be trusted across a boundary:

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
- Visual-core boundary lane: the ten visual-core files may not name any page-model type. The reject list
  carries 16 symbols since `UiPopup` was added on 2026-09-24. Mutation-checked by planting a `UiSession`
  reference, and by planting a real `typeof(UiPopup)` reference into `UiThemeDraw.cs`, which reddens it at
  `UiThemeDraw.cs:17`.
- The guard is a **name-list check, not a transitive one**: it reads the visual-core files and rejects any
  line naming one of the listed page-model symbols. `UiPopup` was in neither array until 2026-09-24 — it
  joined the tree on `4dd97bf`, after the guard was written — so `UiThemeDraw` → `UiPopup` → `UiSession`
  passed while breaking the "usable without a Host" claim; the symbol is in the list now. **The added symbol
  is an armed proof plus a future-regression guard.** Armed, because strengthening a guard is a change a
  mutation can hold to account: planting a real `typeof(UiPopup)` reference reddens it naming
  `UiThemeDraw.cs:17` and the symbol, which is the defect the guard exists to catch. A guard for the future,
  because nothing in the visual core names `UiPopup` today — so what is proven is that the guard is armed,
  not that a live breach exists. The real fix, still open in `TODO.md`, is to derive the set from the types.

Honest limit on the theme lane: `VerifyThemeColorsDoNotAffectLayout` has two halves. The shared-instance
half is mutation-proven. The rect-equality half has **no available failing mutation** today, because no
colour token currently feeds layout — it is a future-regression guard, not present evidence.

## Enduring corrections

- **The `Warning`/`Danger` collapse rested on a census that read the wire as empty (correction, 2026-09-11).**
  The 2026-09-10 census said the two names had no users, and the 0.4 window deleted `UiTheme.Warning` on that
  basis. It measured **this** repository's source and inferred the consumer from it, and the inference was
  wrong: `Coahuilite/UniversalSqueaker` `Source/UniversalSqueaker/UI/Kernel/UsKernelDraw.cs:27` takes
  `theme.Warning` as the fill while the same expression takes `theme.Danger` as the border -- so removing the
  name broke a consumer's build at compile time rather than a pixel at runtime (found by the independent
  verifier while pairing a 0.4 carrier with the consumer tree, then confirmed here by compiling that tree
  against the 0.4 payload: with the name absent it fails, with the name restored it builds clean). The name
  is back as a redirect onto `Danger` -- the two always held one RGB -- and retires at the next minor
  boundary, once the consumer has moved. **Rule reinforced:** a census of this repository is evidence about
  *this* repository; "no users anywhere" is a cross-repo claim and is only as good as its citation.

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
  MSBuild's generated `AssemblyInfo`/`AssemblyAttributes` files. Count with `git ls-files` + `wc -l` /
  `grep -c` instead: generated output is untracked, so it cannot leak in. **Every file and line figure this
  ledger once carried has been replaced by its command**, because two of them rotted while their own bullet
  was warning about rot; if you find a bare number here again, treat it as a stale claim until the command
  beside it is run.
- **The consumer's banned-substring list is six names, enforced by a C# invariant test — not by any
  `scripts/*.ps1` gate.** `UiSourceInvariantTests` forbids `UiInteract`, `Palette`, `SurfaceFrame`,
  `UiText`, `UiValueStore`, `UiPanel`, at
  `Coahuilite/UniversalSqueaker@09366f8:tools/UniversalSqueakerUiLogicTests/UiSourceInvariantTests.cs:153`.
  Two traps live here. First, a cross-repo re-check that greps the consumer's `scripts/`
  finds "no scan at all" and concludes the rule is a phantom — the gate is a test project, so the check
  must target `tools/`. Second, `UiPanel` used to be prose-only (five names scanned, six claimed); after
  FL→US round 2 reported it, US added it to the list rather than deleting it from the rule, so the scan
  and the memory now agree at six. The phantom is resolved at source; do not re-report it.
- **Documentation describes a moment, not a state.** A commit anchor in a prose doc rots the next time
  code lands; during this tidy the tree moved `958ac7d → 4dd97bf → 632a9a3 → a05fddf → 12dacb4` in about
  thirty minutes, retiring a "measured at <sha>" claim twice. Prefer a re-derivable command and a date
  over an anchor nobody re-checks.
- **A report-only item with no delivery channel is not queued, it is lost.** The reclassification of the
  round-3 package from 0.4.0 to 0.3.0 was recorded here and in the buffer annex with the instruction that
  US's docs "should re-point when that repo is next open". US then rewrote its own `TODO.md` and
  `HANDOFF.md` on 2026-09-09, and its three "0.4.0 package" lines plus its summary of FL's round-3 state
  ("REVIEWED, pending scheduling") survived that rewrite unchanged. A sibling session does not read our
  buffer, and a buffer rewrite is precisely the moment when stale cross-repo status is *not* consulted.
  **Resolved 2026-09-10, and the resolution is the rule**: the maintainer authorised this session to
  inspect and then edit the sibling's docs directly, and the four stale claims landed as US `2951934`
  (its `TODO.md:8,9,46,60`, one durable line in its `MEMORY.md`, its local buffer's FL-state summary and
  pointer index). So an outstanding ledger item goes to the user *with an offer to apply it*: `AGENTS.md`
  Boundaries make the sibling read-only by default, only an authorization turns a report into a fix, and
  the report alone was never the delivery.
- **A ref pair rots like a SHA anchor.** "`0.3.x` and `main` both at `19cfdcc`" was true for hours. Say
  the predicate instead and let it be re-checked: `git diff --name-only main 0.3.x` returning only `.md`
  paths is what "the tested bytes and the shipped bytes are one tree" means.
- **A green pre-push privacy scan says nothing about the identity a remote merge button will stamp.**
  The 2026-09-07 scan passed at the pushed tip; the 2026-09-09 PR #1 merge added one commit whose
  author is the clicker's public account display name (with a GitHub noreply address), GitHub itself as committer, and the
  `-FullHistory` identity vector went red through no edit of ours. The scan must be re-run after
  remote-side history events, not only before pushes — the same rule it already states for commits,
  extended to merge buttons, tags' absence and any other hand that writes to the graph. **The fix planned
  here — amend the merge commit's author and force-push both branches — was overtaken on 2026-09-10:
  `gh api user` confirms the name is this account's own GitHub-published display name, so the vector was a gate bug
  rather than a leak, and the gate now measures accounts instead of name strings (see the privacy-gate
  record in this file). The lesson about re-running after remote-side history events stands; the
  history-rewrite advice does not.**
- **File-driven invocation is the requirement; kind hot-reload is not (maintainer ruling 2026-09-14, carried across from the consumer's scope narrowing).** The consumer's 0.4.x window was cut back to a single feature port and takes no source changes, so wiring `ParseFile` is a **0.5.x** item. The requirement it must satisfy is now stated precisely: a layout or style file edit must be visible **after reopening the window**, with no game restart and no recompile, while **adding or changing widget kinds is explicitly excluded** (kinds are compiled). That is this library's own use/extend boundary with the use half moving from a copy embedded in the consumer's assembly to a file on disk; the embedded copy stays as the fallback. `UiHost` already takes both entry points at construction (`UiLayoutManifest` plus the optional `UiStyleDocument`), so no runtime tree mutation is needed - parse once at host creation, keep the previous page on failure, warn loudly. `TODO.md` §3 carries the wire-up.
  **Superseded in part, 2026-09-15:** the bar recorded here — a file edit "visible after reopening the window" — is no
  longer the 0.5 target. A save in development mode must update the windows that are already open, with manual reload
  and a last-known-good fallback behind it; C# kind changes remain outside hot reload exactly as this entry says. The
  stronger contract and its eight clauses are in `docs/development/0.5/00-baseline.md` §2.2.

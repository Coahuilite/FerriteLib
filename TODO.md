# TODO

## Current stabilization ruling — 2026-09-17

The maintainer removed the second-wired-consumer prerequisite for API stabilization to break the
"no stable API -> no integration -> no stable API" cycle. Establish explicit supported contracts and
verification so consumers can adopt them; do not block stabilization on the consumer count.
`AGENTS.md` and `docs/api-tiers.md` carry the operative rule; `MEMORY.md` records the decision.
No API tier was promoted and no implementation/version change was made by this documentation update.
Real-consumer and in-game evidence remain separately recorded; specialized-kind provenance and
compatibility/version requirements are unchanged. Older scheduling text below does not restore the
removed consumer-count prerequisite.

## Memory protocol repaired — first compaction pending (2026-09-18)

`AGENTS.md` now carries the four-file memory protocol (`AGENTS.md` / `MEMORY.md` / `TODO.md` /
`OBLIVIONIS.md`) and demotes maintainer-local `HANDOFF.md` to a transient artifact with no standing
authority. Two statements in `MEMORY.md` that granted that file a protocol role are superseded by the
2026-09-18 ruling recorded there.

- [ ] **First compaction into `OBLIVIONIS.md` — open action, needs separate authorization.** Superseded
      and settled blocks move out of `MEMORY.md`/`TODO.md` **verbatim**; nothing has been archived yet,
      and choosing what leaves the only volatile ledger is the maintainer's call. Not started by this
      update: this update is rules and documents only.

## 0.7.x line — placement/alignment change plan (2026-09-18)

Second amendment **inside 0.7.x**: the contract axis stays `0.7.0` and the consumer range stays
`[0.7.0,0.8.0)`. The original 2026-09-18 reason — "never shipped, nothing consumer-compiled against it" —
was **spent on 2026-09-19**, when the local consumer pinned `[0.7.0,0.8.0)` and compiled against the sibling
carrier; the maintainer re-ruled that the work stays inside `0.7.0` as coordinated in-flight development,
and that ruling **lapses** the moment a consumer is remote or the line is published (`MEMORY.md`, top entry;
the plan §9 log). Live plan, one home: `docs/development/0.7/10-change-plan.md`. It closes
`docs/architecture.md` §3.2/§6.3's **alignment** gap and half of the **relation** gap, with CP-0 as a
prerequisite.

**Phase order — maintainer ruling 2026-09-20, authoritative, and it replaces the earlier "wait for one go":
P1 seam and library fixes → P2 migration and retiring/removing the legacy project → P3 the full UI/UX reset
(last) → P4 new work.** FL is in **P1**, the foundation of that chain: **S3–S7 must not move until P1 closes.**
The backlog is still **friction the consumer's real use exposes**, not a wish list, and `container/tree`'s
optional per-row template (the one capability `Repeat` + `<Templates>` does not already cover) stays **held for
pricing** until the friction report arrives (`MEMORY.md`, phase-purpose entry). One freeze per round: every fix
moves HEAD and invalidates the consumer's gate green.

- [x] **P1-A — the three documentation口径 items, 2026-09-20**: **FL-8** the old→new migration section now exists
      as §4c of `docs/consumers/consume-from-0.7.0.md` (four parts; time attribution verified at the branch tips,
      "0.4 → 0.5 introduced, continued since, not a 0.6 regression"); **FL-11** the authoritative-payload rule is
      stated in all three consumer documents (the GitHub Release asset; dev folders, sibling copies and
      `1.6/Assemblies/` are rehearsals); **FL-12** `api-tiers.md`'s count corrected to **seventeen** with its
      predicate — the LIBRARY's own `Kernel/Widgets/` classes that declare `IUiWidget` (16 until
      `input/text-field`). Docs only: no source, no carrier build.
- [x] **P1-B closed 2026-09-20 (task-18) — evidence and dispositions, not new code.** Four of the five were
      already fixed on this line, so the red-first evidence is a mutated build rather than a new lane:
      **X-21** FIXED `c8fe06e` (A4) — lane `KernelCoreWidgetTests` and the ledger's exact case is its first
      check (`:143`), reddening as "the required case: a later exact value beats an earlier text collision";
      **FL-3** FIXED `21a81cd` (A1) — `KernelLayoutTests`' Row-Auto-fallback lane, five assertions red on the
      pre-A1 1-unit stub; **FL-1** FIXED `e30af94` (A2) — the ledger's V1 spelling `Height="Fill"` is now run
      for real (widget + container) and refused **at creation** with a located `UiContractException`, and the
      lane excludes an arrange-time `FormatException` by construction; **FL-2** FIXED `e30af94` (A3) — four
      assertions red with the refusal disabled. **FL-4 REJECTED** with its argument and a citation: an
      unmeasurable Auto is A1's *documented* answer (the value applied; it just has no natural width) while an
      unresolvable `VisibleKey` is a page defect, so a note would fire on legitimate pages — citation
      `Coahuilite/UniversalSqueaker@0a1b7c05c5ce:…/Diagnostics/UsDiagnosticsSpec.cs:64` (the "Auto column so it
      costs the wide layout one pixel" nav column, since reclassified (A) and replaced with `VisibleKey`).
      Evidence: `dist/p1b-verify/harness-red-baseline.txt` (20 FAIL lines, one run) and `harness-green.txt`
      (ALL PASS, 2573 ok).

- [x] **G1 + G4 landed 2026-09-20** — route (a) approved by the maintainer. G1: the engine-wide
      `HelpKey` on widget elements, claimed on the existing session hover surface (no new member, no new
      channel), with the round-3 hover rule corrected to drop the disabled rule; G4: `TitleKey1..8` on
      `input/mode-row`, plus the indexed-key fix in the `Width="Auto"` seam. The option level was generalized
      in the same round: `HoverHelpKey` now drives `input/dropdown`'s popup rows too, through one shared
      `OptionHelp` implementation. Three new lanes, five mutated controls (A/C/D/E plus the attribution bug the
      lane caught), gates 9/9. Contract: "Element-level help, and the option level generalized".
- [x] **`Tab` on containers — LANDED 2026-09-20** (maintainer-approved coherence fix, "fix what needs
      fixing"): two lines, `Tab` added to both container lists, because the engine's read and its
      `ActiveTabKey` registration were already container-inclusive while the contract forbade the declaration.
      Lane `KernelContainerTabTests` (+ the existing drift guard), red on either half of the mutation. The
      consumer switches its checklist gate from `VisibleKey` to `Tab="Packs"` and deletes its one-bool shim at
      the next freeze. Boundary unchanged and out of scope: per-row `Tab` inside a template stays inexpressible
      by design (page-level answer) and belongs with route A.
- [x] **Gate-1 contention diagnostic — LANDED 2026-09-20** (accepted by the Lead): `verify-local.ps1` now
      probes the payload for an exclusive open before any gate and fails fast naming the cause (a sibling
      checkout's harness or probe loads the carrier and holds the file) instead of letting it surface as
      `MSB3027`/`MSB3021` inside gate 1's build output. Demonstrated against a deliberately held file.
      Related durable finding in `MEMORY.md`: the carrier build is deterministic, the suspect bytes were a
      dirty build reproduced byte-for-byte, and the stamp cannot record dirtiness.
- [x] **P1-E disposition table written 2026-09-20 (task-22, docs only)** —
      `docs/development/0.7/60-capability-dispositions.md`: G2/G3/G5, FL-13/16/17/18/19, FL-21/22,
      B2(2)/B5/B6/B10, the style-document palette selector, the button selected state, and the two unproven
      `state/empty` items. ACCEPTs: G2, G3, G5, FL-16, FL-17, FL-18+B6 (one item), B2(2), B5, `SelectedKey`;
      ACCEPT-DOC: FL-21, FL-22; DEFER: FL-13+B10 (with the P3 reset), the palette selector (no citation);
      CLOSED: FL-19; **not gaps**: the two `state/empty` items, each with the evidence it would need.
- [ ] **P1-E-2 (task-23, blocked by task-19 + this table)** — fold every ACCEPT into **one** carrier rebuild and
      one FREEZE NOTICE, so US/demo re-verify once. New surface stays inside `0.7.x` (no minor move). FL-17's
      per-row template is the largest item and is scheduled last inside the batch.
- [ ] **CONDITIONAL, tied to the hierarchy × composition decision — do not build alone.** A binding-resolved
      help identity (`HelpBind`) for a DATA-DEPENDENT key. The consumer has exactly two such sites, both in
      `us/scope-tree`, and that widget will not be migrated declaratively unless route A (an optional per-row
      template on `container/tree`) lands — so if it stays composite, those two sites stay imperative forever
      and no `HelpBind` is needed at all. Price it **with** that decision, never before it
      (`docs/development/0.7/05-api-contract.md`, "Element-level help…"; `MEMORY.md`).
- [x] **Batch 1 landed 2026-09-19** — CP-0, CP-1, CP-2 and CP-6①③④ implemented, two new lanes,
      42 expectations re-pinned (all classified as the accepted break, none relaxed), gates 9/9.
      Recorded in `MEMORY.md` and in the contract under "Batch 1".
- [ ] **Delivery to the local consumer** — the carrier is `1.6/Assemblies/FerriteLib.UiKit.dll` and the
      sibling reads that path directly, so delivery is a **Release rebuild from the committed HEAD**
      (`dotnet build … -c Release --no-incremental`), **not** `-PackDev` — the dev channel writes Dev bytes to
      the same OutputPath and is the B1 carrier-configuration trap fixed on 2026-09-19 (`MEMORY.md`, "A
      sibling-HintPath consumer reads …"). Hand the consumer the migration list in
      `docs/consumers/consume-from-0.7.0.md` §4b and let it
      compile against `[0.7.0,0.8.0)`. The four in-game checks in `docs/development/0.7/README.md` remain the
      maintainer's/operator's; they are the only thing that can lift the harness-only evidence boundary.
- [x] **`consume-from-0.7.0.md` §1's rehearsal identity — CLOSED 2026-09-20 by FL-11.** The block stays as
      history, now labelled as a rehearsal of one working tree with the authoritative-payload rule stated
      directly above it (the GitHub Release asset), so the stale value is no longer read as a target.
- [ ] **(superseded, kept for the record) Refresh `consume-from-0.7.0.md` §1's dev-artifact identity, or drop the identity block.** It quotes
      `version.txt commit=88095fb3cbed` and a DLL SHA-256 that match neither the tree nor the staged folder
      (measured 2026-09-20: `dist/dev/FerriteLib/version.txt` reads `commit=d82a3ca8db83`). §1 is the
      consumer's first table, so a stale identity there is the one place it is read as current; the delivery
      path this line actually uses is the **Release carrier**, not the dev folder. Refresh it the next time a
      dev package is genuinely staged, or replace the row with the carrier + gates commands.
- [x] **Delivery guardrail — CLOSED 2026-09-20: the maintainer declined the structural split** ("the side
      effect does not exist, because every pack is freshly built"), so no `OutputPath` move and no channel
      redesign. The claim was then measured channel by channel: **dev** already builds
      `-c Dev --no-incremental` (`pack-dev.ps1:39`; verified by running it — exit 0, a full 21.95 s compile,
      `[stage-package] flavor=dev`), so nothing changed there; **github/steam** do not build but **refuse** a
      stale payload (`pack-release.ps1:73-78`, `pack-steam.ps1:58-61`) and the stager measures the configuration
      stamp (`stage-package.ps1:81-88`); **the one gap** was the CI payload build `release.yml:119`, which was
      incremental and runs after `verify-local`'s Dev/Release gates over the shared OutputPath — it now carries
      `--no-incremental`, which makes "every pack is freshly built" literally true with no structural change.
      The stale "7 gates" labels in `release.yml` and `ci.yml` were corrected to 9 in the same pass. The
      proposal below is kept as the record of what was weighed and declined.
      Original proposal (declined): separate the dev and release artifacts, and make every pack a fresh build.
      Measured root cause:
      `Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj:20` sets `<OutputPath>..\..\1.6\Assemblies\</OutputPath>`
      with **no configuration condition**, so Dev and Release write one folder, and `verify-local.ps1`'s
      `-PackDev` block runs `pack-dev.ps1` as the **last** step, after every check. Proposed shape, none of it
      implemented: (a) pin **Release** to `1.6/Assemblies/` (a de-facto published path a stranger's project
      references — do not relocate it); (b) give **Dev** its own output folder so it cannot overwrite the
      carrier; (c) force a clean rebuild + PDB removal at the end of every pack channel. Open questions the
      proposal's two questions, answered by measurement in the round-2 report: **(i) nothing legitimate reads
      Dev bytes from the carrier path.** The readers are gate 4 (`verify-local.ps1:121`, which asks MSBuild for
      `TargetPath -p:Configuration=Release`, so it is Release-only), gate 5's content probe, gate 9
      (`stub-coverage-scan.ps1:309` reads the DLL at that path — it should read the shipped bytes, and after
      the change it does), the two publishing packers (`pack-release.ps1:28`, `pack-steam.ps1:19` — Release
      channels), and the sibling consumer's HintPath, which improves. The one that must change is
      `stage-package.ps1:31`, which hardcodes the carrier path for **all three** channels: after Dev moves it
      would measure Release bytes against the `dev` channel and **refuse** at `:86-88` — fail-closed, not
      silent, but the dev channel would stop working, so `$payloadDll` must become channel-aware in the same
      change. **(ii) the measurement still holds and gets stronger**: it reads the bytes it is about to copy
      (`Get-AssemblyConfiguration` at `:81`, ProductVersion at `:93`), so a channel-specific source makes it
      measure the configuration the channel names, with no shared path left to confuse.
      **Two findings that change the shape of (c).** The commit-freshness guardrail the maintainer asked for
      **already exists on both publishing channels** as a *refusal* — `pack-release.ps1:73-78` and
      `pack-steam.ps1:58-61` compare the payload's embedded commit to HEAD and throw; today's dev channel is
      the only packer that builds, and it builds Dev into the shared path. So "force a clean rebuild at the end
      of every pack channel" is a **design change to the three-channel split** (packers own identity, the
      release workflow builds between the gates and the pack), not a gap to patch; and relocating Dev's output
      makes the manual `Remove-Item …pdb` step unnecessary rather than mandatory, because the Dev PDB moves
      with the Dev output while Release leaves none (`DebugType=none`). `artifacts/` is already gitignored, so
      a Dev output folder under it keeps the tree clean.
- [ ] **Batch 2 (after the live feedback)** — CP-3 (skin-source axis) → CP-6② (role→surface mapping as
      data; blocked by CP-3) → CP-5 (regional scope), plus CP-7 (sibling-relative placement) if the feedback
      asks for it. Additive today and cited by nothing; the live pass is what would supply the citation.
- [ ] **Batch 2 also owns the consumer's own list** (`modding_documents/team-mode/us-to-fl-2026-09-19-zh.md`,
      evaluated in `fl-to-us-2026-09-19-zh.md`): B2② `WideHidden`; B5 `WidthKey` (blocked on the consumer
      committing its citation); B6 the chrome action slot; **B7 `input/text-field` — LANDED 2026-09-20** (the
      kind + the identity-bearing `UiNative.TextField` overload; contract Amendment 3, tier entry, lane
      `KernelTextFieldTests`; no minor moved under the 2026-09-20 ruling);
      **mode-row per-option hover help — LANDED 2026-09-20** (maintainer-approved, generality argument: vanilla
      mode selectors already show per-option help; the identity is the existing `DescriptionN`, published
      through the new `HoverHelpKey` binding key so the consumer renders the help itself — no tooltip — plus
      `UiNative.IsMouseOver(Rect, ctx)`; contract section "Mode-row hover help", lane `KernelModeRowHelpTests`);
      B8's **vocabulary** half (`Description1..8` out of the schema, or a drawing path — maintainer ruling
      owed; the **label-set** half, the pixel-moving defect, landed 2026-09-20 as a fix, `MEMORY.md`; and the
      descriptions are **no longer orphan names** — hover help now publishes them as the options' help identity,
      so removing them would remove that capability: report only, the maintainer decides); B9
      refused for this line; B10 text alignment as a **layout** attribute, not an appearance axis; B11 the L1
      orphan-name check, for which B8 is the first positive control.
      All of it stays inside `0.7.0` under the 2026-09-19 coordinated-development ruling, each item with its
      own contract amendment and lane.
      **Bucket classification is mandatory for every US→FL item** (maintainer policy directive 2026-09-20,
      `MEMORY.md`): (A) US misuse / US's own job — fix on the consumer tree, file no request; (B) a genuine FL
      gap, which must be a **general** capability, symmetric with an existing one or a funnel primitive, and
      useful to a consumer that is not US. Buckets: B2②/B5/B6/B7/B10/B11 = (B) general, all non-blocking
      (B7 strongest); B9 = (A) US's own kind; B8's label-set half = defect (landed), its vocabulary half = a
      **shrinkage** of a general kind's vocabulary, maintainer's call. The consumer's diagnostics-lane
      `diag-nav-col` report is **(A)**: `Width="Auto"` was documented as the label set's natural text width
      and the 1px collapse was never a contract, so US fixed it with the existing general `VisibleKey` and it
      produces **no FL work item and no version consequence**.
- [x] **The Batch 2 pick was B7 `input/text-field`, ruled by the maintainer on 2026-09-20 and landed the same
      day.** Remaining (B)-general candidates, in the order this ledger rates them: B5 `WidthKey` (waits on the
      consumer committing its citation), B6 the chrome action slot, B2② `WideHidden`, B10 alignment as a layout
      attribute, B11 the L1 orphan-name check (B8 is its first positive control). Each still owes its own
      contract amendment **before** code, a failure-sensitive lane, and the consumer-guide line; all stay inside
      `0.7.0` while the coordination phase runs.
- [x] D1–D7 — all seven Step-1/Batch-1 decisions settled; see the plan §0b for what each was settled as.

## 0.7.x round — implemented, external acceptance open (2026-09-17)

Branch `0.7.x` (forked from the published `0.6.x` at `12d0dbb` + the maintainer's ruling docs), contract
axis **0.7.0**. Scope: A reliability ×5, B ordinary-author recipe, C two peer palettes (`Vanilla` /
`DarkGold`; constructor is a bag), D contract + gates. Execution status, the supported contract and the
four external checks each have one home: `docs/development/0.7/`. This section is a pointer only.
Consumer/game acceptance is owned by the maintainer/operator and is **not** claimed here; push/tag/release
need separate authorization.

## 0.6.x round — open (2026-09-16)

Branch `0.6.x` (forked from the frozen `0.5.x` tip `354d90a`), contract axis **0.6.0**. Scope, packages,
ownership, the frozen public API and status: `docs/development/0.6/`. This section is a pointer only; the
round's live status has one home and it is that directory.

Product: lightweight MVVM (page lifecycle + an optional explicit `INotifyPropertyChanged` → binding-key
adapter), automatic hot-reload scheduling with a testable time seam, a read-only widget description snapshot
that keeps scope — and an **independent demo mod** `ferritelib_uikit_demo` that consumes only public API
(vanilla ModSettings entry, three tabs, core samples, consumer directory by scope, two windows sharing one
model) and is explicitly **not** a second real consumer.

- [x] T0 — fork, version ruling (0.5.0 → 0.6.0), frozen API contract, nine new public types + tier entries,
      compiling skeletons with package-named markers, ten gates green at `02a6aea`.
- [ ] T1 — page lifecycle (`HostAttached`/`HostDetached`) and the MVVM notification adapter.
- [ ] T2 — reload scheduling: quiet period, bounded retry, bounded deferral, pause independence, the vanilla
      commit-point evidence.
- [ ] T3 — `UiWidgetCatalog`/`UiWidgetDescriptor`: scope-preserving identity, isolation, zero factory calls.
- [ ] T4 — the demo mod, its package, and proof it carries no FL DLL.
- [ ] T5 — independent adversarial verification, the external compile probe, package checks, the in-game
      checklist, and the delivery record.

Carried over unchanged and **not** closed by this round: `0.5.x`'s in-game acceptance (A1–A11) and a real
consumer compiling against `[0.5.0,0.6.0)`. Both are other actors' steps.

- [x] Follow-up — **closed on `0.7.x`** (package A, item A5, commit `88f1c77`): a dropdown bound with
      `BindReadOnly<IReadOnlyList<T>>` now gets a creation error that names the wrong registration kind and
      points at `BindOptions<T>`, with the key and the element path; the lane was re-run red on the 0.6
      baseline first. (Original finding: the demo mod, 2026-09-16, `docs/development/0.6/20-api-and-xml.md` §5.)

## 0.5.x round — open (2026-09-15)

Branch `0.5.x`, contract axis 0.5.0. Scope, packages, ownership and status: `docs/development/0.5/`.
This section is a pointer only; the round's live status has one home and it is that directory.

- [x] Window instances — generic shell + keyed identity + focus/active-target + pause policy (P1+P1h).
- [x] Bindings — per-key notification, batch commit, invalidation classes, command executability,
      conditional visibility (P2+P2b+P2c).
- [x] Collections and common controls — keyed repeater with a template table, checkbox, progress, tree (P3).
- [x] Documents — file sources, dependency tracking, candidate validation, atomic batch commit,
      last-known-good, manual reload (P4+P4b+P4c).
- [x] Diagnostics — per-host/session subscriptions, attributed bounded events, and the shell/page routing
      seams that make them reachable (P5 + task-13 + task-14).
- [~] Delivery — external review of `0117c01` returned Request changes with 7 reproduced defects; all fixed and the reviewer's
      own probe re-run green (tip `354d90a`, package rebuilt at `d6b3f6e`). What remains is
      external: in-game acceptance (A1-A11) and a real consumer compiling against `[0.5.0,0.6.0)`. Both are
      other teams' steps and neither may be recorded as done from here.
- Supersessions this round registers (detail in `docs/development/0.5/00-baseline.md` §2): confirmed
  public capabilities no longer wait for a consumer to hand-roll them first; hot reload now targets the
  open window, not the reopened one; and the 0.4 node/hit-stack state is restated accurately.

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

- [ ] **Cross-repo report owed to US: the lib-release link in its release body — unblocked 2026-09-10,
      still undelivered.** The original blocker ("this repo has published no 0.3.0 asset") is gone
      (`v0.3.0-rc1` was cut that day; `v0.4.0-rc1` followed), so this is now a plain report and no longer
      waits on this repo. The link target is whatever lib release the consumer's own pin was compiled
      against, so the US side picks it — report, never edit its tree. Deliver it actively at the next
      handoff: the round-3 lesson is that a report-only item with no delivery channel is lost.

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

This is an integration/validation follow-up, not an API-stabilization prerequisite (maintainer,
2026-09-17). It does not authorize changes to another repository.

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
      **Progress 2026-09-11 -- first step landed** (`dfba792`, archived on `0.4.x`): stable element identity
      (`UiNodeId`, declared index rather than visible index so Tab/Hidden toggles do not renumber), per-element
      state keys, an ambient element scope restored in `finally`, and the two observation members
      (`ActiveElement`, `HoverClaimElement`) with their retirement condition recorded in `api-tiers.md`. The
      real node object -- identity plus state plus dirty flags in one type -- is the next step, together with
      the string-keyed remnants this one deliberately left (`UiNative.GetControlId`, the popup owner key,
      `SetScrollTarget`); the owned hit stack depends on the node step.
- [ ] **A third operation on bindings: announce.** `IUiBindings` has get and set but no notification, so
      invalidation is a single global `ContentRevision` counter and its call sites are policed by
      reading source files and asserting on substrings. Give every key a revision, then delete
      `SessionRevisionBumper`, the help-panel's hand-written before/after text diff, and those
      source-text assertions.
- [ ] **Owned hit stack.** Produce a z-ordered `(rect, element)` list during arrange and dispatch input
      topmost-first. **Half done by `4dd97bf`:** `UiPopup.RectFor` is now the only popup rect rule and
      `UiPopup.DrawOptionList` the only publisher of the covered rect, so the popup-specific coordinate
      conversions are gone. **Updated 2026-09-15 (0.5 independent audit): the element-level half is done and the
      older wording here was stale.** `YieldsToCoveringPopup` has zero hits in `Source` at `99acc5f`;
      `UiNative.Button(rect, ctx)` and the dropdown trigger dispatch through `UiSession.IsPointerOverHigherLayer`,
      and the atoms call the two-argument form. What is left is the content-layer-vs-content-layer boundary (a
      `(rect, element)` list exists; arbitration between two content layers does not), out of scope for 0.5 and
      still carrying the recovery condition recorded in `docs/api-tiers.md`.
      **Trigger evidence added 2026-09-11 (lib-atoms, task-3 report, quoted):** popup yield is not in the
      atoms -- `input/button`, like the existing `input/mode-row` and `input/stepper-slider`, calls
      `UiNative.Button` directly, so a consumer that puts a button under an open popup can rediscover the
      2026-09-04 click-theft class. The atom lane deliberately neither copied a second yield rule nor
      widened `UiNative`'s public surface. Closing condition for this item: once the hit stack lands, every
      element's yield behaviour must come from the stack, and no element may keep a private yield branch.
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
- [ ] Wire `UiLayoutManifest.ParseFile` — the fork is settled by the style-document ruling (2026-09-10):
      a standalone style document with its own loader makes "XML authoring without recompiling" a
      library promise, so deleting the disk entry would strand the promise's structural half. The three
      questions the old wording owed are answered by the same ruling's shape: where the file lives is
      the consumer's call (nothing scans a directory — the consumer hands the path, as with any file it
      owns); when it is re-read is at host creation only (no runtime mutation); and a parse failure is
      appearance-class — the page renders with defaults, the fallback logged through the fit audit's
      channel and exercised in a harness lane. Scope note: this item is the manifest loader the ruling
      settles; the style document's own loader is the stage-3 sweep's, with the same policy.
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
      **Progress 2026-09-11 -- the atom half landed.** The five kinds exist (`bab3c07`..`2ce61ce`) and the
      kind strings are now a contract: `text/wrapped`, `input/button`, `chrome/rule`, `input/slider`,
      `input/number-field` (justifications and the one debt verdict: `MEMORY.md`). What remains of this
      item is the second half -- re-express `chrome/banner` and `state/empty` over `text/wrapped` while
      keeping their names -- and it is scheduled as its own slice, not folded into the atoms' commit.
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
      Priority note (raised by the maintainer's 2026-09-10 question about our buttons): this binding is worth
      more here than the raw citation count suggests, because we own every state appearance — the kernel
      draws no game texture, `UiNative.Button` is `VerseWidgets.ButtonInvisible`, and vanilla's free
      hover/pressed/disabled chrome is ours to draw by hand. Still no resolver: a selector engine in front of
      `UiThemeDraw` deletes no drawing code and only adds matching.
- [ ] **Settle the duplicate tone tokens before the atoms make them visible.** `UiTheme.Warning` and
      `UiTheme.Danger` are the same RGB under two names, and no library widget selects `Warning`, `Danger`,
      `Success`, `TextSecondary` or `TextDisabled` at all (`MEMORY.md`, tone census). Either a cited consumer
      shapes them apart or one name goes in the 0.4.0 sweep — an unproven token in the bag is the same debt
      class as an unproven kind, and the self-check page will put the duplication on screen where a player
      can be pointed at it.
- [ ] **Make the tone treatment table queryable, then route the three call sites through it.** The
      duplication is measured in `MEMORY.md`: `DropdownWidget.DrawField:127` and
      `InputModeRowWidget.DrawOption:114` re-implement, verbatim, the `Active`/`Neutral` rows of
      `UiThemeDraw.StatusTreatment`, and both re-derive a text colour that currently lives inside
      `StatusBadge`'s private switch. Shape of the fix: one internal table returning fill, border **and**
      text for a tone plus a prominence flag, with `StatusTreatment` and `StatusBadge` consuming it so the
      three sites cannot drift. Equivalence is checkable before writing it — `Active` ⇒ Selected +
      AccentGold + TextOnGold, `Neutral` ⇒ Raised + Border + TextPrimary, which is what both widgets paint
      today. Keep it `internal` until the 0.4.0 sweep: a public addition bumps the contract minor, and the
      wired consumer pins `[0.3.0, 0.4.0)`. The prominence flag exists because the badge's neutral text is
      `TextSecondary` while a field's is `TextPrimary` — do not resolve that by adding tone values, which
      would widen a public enum for a library-internal distinction.
      Input shape: the table's key is `(tone, prominence)` today and gains writability the moment a
      read-side query exists on `IUiBindings` — `BindReadOnly` has six consumer citations and no read path,
      which is why `UiStatusTone.Disabled` has zero producers (`MEMORY.md`). Do not add the writability read
      in the same change as the table: the table is deduplication, the read is a public API addition and
      therefore 0.4.0 material.
- [ ] **Style capability, staged by contract cost (decomposed 2026-09-10; scope CONFIRMED by the maintainer
      the same day — reasoning and non-goals in `MEMORY.md`).** The governing rule is the use/extend
      boundary: XML is the surface for **using** components, layout and appearance, and C# is the surface for
      **extending** the vocabulary by registering a kind. Measured against it, layout complies and appearance
      does not, so all three appearance classes are in scope: (a) a colour scheme selectable per window and
      per region, (b) density (row height, padding, font size), (c) role tags per element. Stage 0 is the
      internal dedup above — no bump, doable any time. Stages 1-3 land in the 0.4.0 sweep **together with the
      leaf atoms**: per-surface and geometry tokens (already pre-stable debt under the §5 INVITED ruling),
      then the scheme, density and `Tone`/`Emphasis` attributes onto the atom schemas, then the
      resolve-before-`Measure` pass with its harness lane and the role-name deprecation channel. Precedence
      is nearest-wins — element > region > window > library default — written down, with no selectors and no
      specificity arithmetic. **Inheritance is asymmetric and that asymmetry is the design:** scheme and
      density inherit, roles do not, because a region marked danger makes everything inside it read as
      dangerous. Stage 4 shrinks accordingly — the region level is in scope now, the page level waits for a
      cited need; stage 5 (the `IUiBindings` writability read) ships with whichever change first needs a
      `Disabled` producer. Cross-page sharing was explicitly **not** built: reuse rides the registry, not
      a shared style document. Nor are the other non-goals: no selectors, no specificity, no `@media`,
      no runtime mutation. **Amended 2026-09-10, maintainer ruling: the page-level rule source is a
      standalone style document with its own loader** (`MEMORY.md`, "The style surface is a standalone
      document"), superseding "no separate style file"; the embedded `<Styles>` section remains legal
      as the second text origin feeding the same document type, so there is still one parser and one
      validation entry point. Document shape owes: the document type in `docs/api-tiers.md`, the
      page-level precedence slot (which is what stage 4 was waiting for), and resolve-before-`Measure`.
      Validation policy for stage 3, per the failure ladder in `MEMORY.md` and the maintainer's ruling:
      structure stays fail-closed (page-fatal, bucket 2), appearance values go fail-soft — an unknown `Tone`
      falls back to the default treatment — and fail-soft must not mean silent: the fallback is logged,
      reported through the fit audit's channel and exercised in a harness lane, because the web's weakness
      here is that a dropped declaration fails invisibly and gets found by a user instead of by its author.
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

- [ ] **The 0.5.0 vocabulary expansion is now scheduled against a real consumer migration (maintainer directive 2026-09-14; the cross-repo board lives in the consumer workspace at `modding_documents/team-mode/task-decomposition-us-fl-board-zh.md`).** US's 0.5.x main goal is dissolving its **18 consumer-owned `us/*` kinds** into manifest subtrees. Three capabilities stand in the way and each is a vocabulary question, not a convenience: a **checkbox** (the consumer needs a two-level parent/child row pair), a **repeater / list template** (checklist, preset list, race layer, xenotype layer and the help catalog all render data-driven row sets, and the engine has no per-item template today), and a **tree kind** (`us/scope-tree` walks action -> mood -> factor). Per `AGENTS.md` "What earns a kind" the provenance already exists - the consumer was forced to hand-roll all three - but the shape must be decided before anything is registered, the kind must not be one only our own harness drives, and the former wait for the second wired consumer (TODO §2) is **superseded by the 2026-09-17 maintainer ruling**; specialized-kind provenance requirements remain. **Scope note (maintainer, same day): the `ParseFile` wiring is a 0.5.x item, not 0.4.x** - the consumer's 0.4.x window was cut back to a single feature port and takes no source changes. The requirement it must satisfy is now stated precisely: a layout or style file edit must be visible **after reopening the window**, with no game restart and no recompile, while **adding or changing widget kinds is explicitly excluded from hot reload** (kinds are compiled). That is this library's own use/extend boundary with the use half moving from an assembly-embedded copy to a file on disk; the embedded copy remains the fallback, and `UiHost` already takes both entry points (`UiLayoutManifest` plus the optional `UiStyleDocument`) at construction, so no runtime tree mutation is required.

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
- [ ] **Maintainer ruling owed: should "Release rebuild + PDB removal" become a script step?** The delivery
      order is manual today and lives in prose (`MEMORY.md`, the sibling-HintPath bullet): commit → gates →
      harness → `dotnet build -c Release --no-incremental` → `Remove-Item 1.6/Assemblies/FerriteLib.UiKit.pdb`.
      A step in `verify-local`/`stage-package` would make the last artifact deterministic for a
      sibling-HintPath consumer instead of relying on the operator, but it also changes what the dev channel
      means (gate 2 is the Dev build, so a script step placed wrong would ship Dev bytes). Escalated
      2026-09-19; not a session's call.

- [x] **Pre-push privacy scan: the identity vector was a gate bug, not a leak (ruled and fixed
      2026-09-10).** The 2026-09-09 reopening said `Fe <19252128+Coahuilite@…>` on the PR #1 merge commit had
      to be rewritten out of history. Checked against the source the maintainer named: `gh api user` reports
      login `Coahuilite`, id `19252128`, name **`Fe`**, email `null` — so `Fe` is that account's own
      GitHub-published display name, and GitHub's web merge button stamps exactly that as the author of a
      squash-or-merge commit; the committer `GitHub <noreply@github.com>` is platform boilerplate. One human,
      one account, one noreply address. **The leak vector is the email address, never the display name**, so
      the gate now measures accounts: any non-noreply author or committer address fails, more than one
      distinct noreply account fails, and display-name variance inside one account is reported instead of
      fatal. Positive control run before trusting it: a real mailbox → foreign 1; two accounts → 2; the same
      name on two accounts → 2; this repository's history → 1 account, 2 names, `PRIVACY AUDIT CLEAN` across
      76 revisions. Consequences: no history rewrite, no force-push, `v0.3.0-rc1` is unblocked on this axis,
      and the amend-and-force-push instruction this item used to carry is superseded. Standing rule
      unchanged: re-run `-FullHistory` after any commit lands, because a clean tree says nothing about
      history — that is how this was caught at all.
- [~] **Release sequencing for the style sweep — first half done 2026-09-10.** `v0.3.0-rc1` is cut from
      `main` at `6a92331` and published by CI with the prerelease flag correct, one asset, and
      `/releases/latest` still 404 as the rc window requires; a local pack at the tag commit matched the
      platform's digest byte-for-byte, which is the first real exercise of §5's cross-machine check rather
      than a rehearsal. Second half is open: the `0.4.x` line now carries all three axes at 0.4.0 and holds
      the leaf atoms plus the three style classes (per-surface scheme, density, role tags) and the `UiTheme`
      restructure, and every new public type needs its `docs/api-tiers.md` classification in the same
      commit. US's re-pin to `[0.4.0, 0.5.0)` is a cross-repo write — report it in a round, never edit it
      here — and until that lands the sibling checkout has to stay on a 0.3-axis branch, because `Require`
      will correctly refuse the pair.
- [x] **`v0.4.0-rc1` cut and published 2026-09-14 - the 0.4 axis now has a real rc in the field.** CI
      published it as a prerelease with one asset, `FerriteLib-v0.4.0-rc1.zip` (68,777 B), `/releases/latest`
      still 404 as the rc window requires, and `main` = tag = `b60f7fd`. The cut moved no version axis: the
      0.4.0 sweep (leaf atoms, the three style classes, the `UiTheme` restructure) was already on `0.4.x`
      before the merge, so `Api` / `<modVersion>` / `<VersionPrefix>` are 0.4.0 on `main` and the consumer's
      `[0.4.0, 0.5.0)` pin is satisfied - no re-pin is pending. Every ref in this repo is pushed: `0.4.x` now carries the rc1 record commit (`5971cfd`) and is
      one docs commit ahead of `main`, which itself stays at the tag. The remaining sweep
      work in the item above is unchanged and still owned here.

- [x] **Repository name ruled by the maintainer 2026-09-07: `FerriteLib`, PascalCase** — matching the
      series convention (`SqueakyRatkin`, `UniversalSqueaker`, both measured live on GitHub). The earlier
      "lowercase or a Linux runner breaks" argument is void on evidence: GitHub resolves owner/repo
      case-insensitively (measured: `repos/Coahuilite/squeakyratkin` redirects to `SqueakyRatkin`), US's
      workflows reference the carrier via `repository:` (an API lookup) plus a literal checkout
      `path: ci-ferritelib` that no repo-name case affects, and both runners are windows-latest anyway.
      Machine identity stays lowercase where it is load-bearing: packageId `coahuilite.ferritelib`
      (save-data reference, immutable) and the local sibling directory `ferritelib` (what the csproj
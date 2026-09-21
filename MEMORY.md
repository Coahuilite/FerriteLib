# MEMORY

## Current durable state

- **FL-16 landed on its second attempt, and the difference between the attempts is what to copy (2026-09-20).**
  The first attempt wrote the implementation, saw the suite green, and withdrew because the lane stayed green
  under a faithful revert. The second attempt ran the order the maintainer asked for and the evidence came out
  right: lane written and seen RED before the public type existed (with a stand-in pair shape, since `UiOption`
  could not be promoted first), then `UiOption` + its `public-unstable` tier entry + the pair path in one commit,
  then the revert proof on the FINAL lane - pair path removed -> `a valid pair-bound page recorded 1
  diagnostic(s)` (the FL-23 report firing on the mismatch) -> restored -> ALL PASS, 2596 ok. **The probe is
  non-reporting** (`ValidateOptions<T>` throws and records nothing; `GetOptions<T>` is then called exactly once
  for the matching type), which is both the FL-23 requirement and what makes `StyleFallbackCount == 0`
  assertable for a valid page.

- **FL-23 landed (2026-09-20): a wrong-type binding read is now REPORTED, not only thrown.** `GetOptions<T>` and
  `Invoke<T>` record one deduplicated diagnostic on the fail-soft channel (path = binding key, kind = `binding`,
  attribute = `OptionsBind`/`ActionBind`, authored/resolved = the two type names) before throwing as they always
  did. Evidence: the lane is red on the pre-fix tree ("the mismatch threw but nothing was reported:
  StyleFallbackCount=0"), green after, and red again with the reporting call disabled; green run ALL PASS 2595 ok.
  **Two durable lessons came out of it.** (1) `UiFitAudit`'s counters are CUMULATIVE and process-wide, so a
  "count == 1" assertion must `Reset()` and measure a delta - the first version of this lane passed against the
  unfixed tree for exactly that reason, which is the same green-for-the-wrong-reason shape the phase keeps
  paying for. (2) The reporting makes FL-16's dual-shape probe a design problem: a probe that legitimately tries
  one shape and falls back to the other must read through a NON-reporting path, or a valid page records a
  mismatch for the shape it is not. FL-16's redo starts there.

- **A lane that stays green when the feature is removed is not evidence; this session paid for that lesson once
  (2026-09-20).** FL-16 (typed `UiOption` pairs) was implemented, its public type classified and the whole suite
  green - then the faithful pre-fix revert (a single string read in `BuildOptions`) left the new lane GREEN,
  because the string fallback accepted a pair-bound key instead of reporting the element-type mismatch. The type,
  its tier entry and the lane were **removed**, not shipped; the diagnosis seeds the next attempt. The same
  standard did hold for the two items that landed in that batch: **G2 `PayloadKey`** (a command receives its
  bound payload; scoped per item like the other binding roles, and the creation contract follows the shape -
  `BindAction<string>` with a payload, `BindCommand` without) and **G3 `Chrome`+`Height=Auto`** (a bare hit area
  whose height is measured from content), each red on its own assertion and green after.

- **P1-E's first half landed 2026-09-20: four vocabulary items + two diagnostic corrections, each with a lane
  proven red against the reverted implementation in ONE mutated build** (HARNESS_EXIT=1, 6 FAIL lines — G5, the
  selected state, WideHidden, WidthKey — then green: ALL PASS, 2592 ok). **B2(2) `WideHidden`** is the exact
  mirror of `NarrowHidden` (arranged only while not narrow; refused under a parent with no `Breakpoint`, like
  its twin). **B5 `WidthKey`** is the numeric sibling of `VisibleKey` (a written `Width` wins; the declared key
  is registered so an announcement re-arranges; an unanswerable key leaves the unsized answer and records one
  note). **`SelectedKey`** is a bool binding that resolves the role to the **active** treatment — deliberately
  not a `ToneKey`, which would re-authorise the retired `Active` name; state beats the author, as writability
  does. **G5** put `chrome/banner` on the atoms' role pair with a declared default emphasis of `Muted`, which is
  what keeps an untone banner's ink identical. **FL-21/FL-22 are documentation only**: an overflow verdict is
  about the **inset label band** (`Available`) against the content it could not hold (`Needed`), and a fit-audit
  count is **distinct findings** (dedup key, `MaxReports = 48`, `Saturated`, `Reset`), never a census — both
  statements now live in the source docs and the consumer guide. **G2, G3 and FL-16 are DEFERRED to the next
  batch with their ACCEPT verdicts unchanged**: this batch's evidence bar is one failure-sensitive lane per item
  with its own mutation, and folding a new public type plus two more attributes into the same freeze window would
  ship surface no lane had been shown red against.

- **P1-E's capability dispositions are written and awaiting a ruling (2026-09-20, task-22; table in
  `docs/development/0.7/60-capability-dispositions.md`).** The table gives every raised item one row — anchors,
  consumer citation, the four promotion gates, what implementing it touches — with the outcome ACCEPT /
  ACCEPT-DOC / DEFER / REJECT / CLOSED. Three outcomes change the surface rather than the prose, so they are
  durable here: **FL-19 is CLOSED** (the mode-row's `Description1..8` left the label set in the B8 fix and are
  now the payload `HoverHelpKey` publishes, so they are no longer orphan vocabulary, and the vocabulary half is
  now harder to justify); **FL-13 and B10 are one item with two names and are DEFERRED to the P3 UI/UX reset**
  (choosing the alignment axis now would freeze vocabulary the reset may contradict); and **the state-document
  palette selector is DEFERRED for lack of a citation** — the resolver applies token overrides onto the host's
  injected theme, and a request is not evidence. The ACCEPTs are manifest-only except FL-16 (one new public type,
  `UiOption`) and FL-17 (the per-row template, scheduled last so one rebuild covers the rest). FL-21/FL-22 are
  documentation only: their behaviour is implemented and correct, the wording is what misled a reader. The two
  `state/empty` items are **not gaps** and are recorded with the evidence each would need.

- **P1-B closed 2026-09-20 (task-18) as evidence, not as new work: four of the ledger's five items were already
  fixed on the 0.7 line, and the fifth is REJECTED with its argument.** The acceptance form was "revert the fix,
  watch the named lane redden, restore" — the only red-first evidence available for a defect that is already
  closed — and it was done for all four in **one** mutated build so each lane's red is attributable by assertion
  name (`dist/p1b-verify/harness-red-baseline.txt`: HARNESS_EXIT=1, 20 `FAIL` lines; after reverting,
  `harness-green.txt`: HARNESS_EXIT=0, 0 FAIL, ALL PASS, 2573 ok).
  - **X-21 (dropdown display order) — FIXED at `c8fe06e` (A4)**; lane `KernelCoreWidgetTests` "Dropdown exact
    value wins over display-text fallback (A4)", and the ledger's exact case is already its FIRST check
    (`KernelCoreWidgetTests.cs:143`): `Option1="b" Value1="a" Option2="Second" Value2="b"` with current `b` must
    display "Second". Reverting `FindDisplayText` to the single-pass "value OR text" test reddens it with
    "the required case: a later exact value beats an earlier text collision" (+ the lane-level line). Two passes
    (all values, then all texts) is the shipped shape.
    **Not strengthened, deliberately:** a symmetric case where an earlier VALUE collides and a later TEXT matches
    is not discriminating — the value pass is a superset of the old test, so both implementations answer
    identically; adding it would be a second copy of the same proof, which is what "one lane" is for.
  - **FL-3 (Row Auto silently collapsing) — FIXED at `21a81cd` (A1)**; lane `KernelLayoutTests`
    "Row Auto child that cannot be measured falls back to unsized (A1)" (`VerifyRowAutoFallbackToFlex`).
    Reverting `ResolveColumnWidths` to the pre-A1 `Math.Max(1f, natural)` stub reddens five assertions,
    beginning with "unmeasurable Auto shares the remaining space with the omitted-Width sibling, no 1-unit stub".
    The ledger's condition ("no positive MinWidth and no measurable label") is exactly the lane's first two cases.
  - **FL-1 (Height had no creation-time validation) — FIXED at `e30af94` (A2)**; lane `KernelLayoutTests`
    "Height is validated at creation, not at arrange (A2)" (`VerifyHeightValidation`). **The ledger's V1
    question is now measured, not reasoned:** I added the reported spelling `Height="Fill"` as two cases (widget
    and container) and ran them — both are refused **at creation** with a located `UiContractException`
    ("widget Height=\"Fill\" (FL-1's reported value) — rejected at creation"), and the lane's helper fails if
    anything but a `UiContractException` escapes, so the old arrange-time `FormatException` shape is excluded by
    construction. Disabling the Height refusal reddens eight assertions including both new ones.
  - **FL-2 (Cols/NarrowCols inert outside Wrap) — FIXED at `e30af94` (A3)**; lane `KernelLayoutTests`
    "Cols/NarrowCols are Wrap-only vocabulary (A3)" (`VerifyColsWrapOnly`). Disabling the refusal reddens four
    assertions, including the located-message check ("the refusal names the attribute, the rule and the element
    path").
  - **FL-4 (an unmeasurable `Width="Auto"` returns 0 silently while an unresolvable `VisibleKey` records a note)
    — REJECTED, with the argument and a citation.** They are not the same class of fact. `VisibleKey` names a key
    that *should* resolve, so its absence is a page defect and the note is the author's only signal. An
    unmeasurable Auto is a **documented, supported answer**: A1's contract sentence says such a child "is allocated
    like an otherwise identical child with no `Width` attribute", i.e. the authored value *did apply* — its answer
    is "no natural width, take the unsized share". The fit audit's appearance channel exists for authored values
    that did **not** apply (unknown tone, dropped style token, unresolvable `VisibleKey`), so a note here would
    fire on legitimate pages and the channel would lose its meaning as a defect signal.
    **Citation** (the growth rule wants provenance, not a request): a real page used the unmeasurable-Auto path on
    purpose — `Coahuilite/UniversalSqueaker@0a1b7c05c5ce:Source/UniversalSqueaker/UI/Diagnostics/UsDiagnosticsSpec.cs:64`
    declares `<Column Id="diag-nav-col" Width="Auto" Gap="4">` under a comment that says "An Auto column so it
    costs the wide layout one pixel (a 1px rect is skipped by every widget's Draw); below the Breakpoint it takes
    the full width". That specific idiom was later classified **(A) consumer misuse** in this phase (it depended
    on the undocumented 1px collapse, and the consumer replaced it with `VisibleKey`), which is *why* this is a
    rejection rather than a feature: the shape stays legal and documented under A1, and the correct place to
    record it remains the A1 contract sentence plus the deferred general intrinsic-size seam, not a per-frame
    warning. If a future consumer asks for "Auto could not measure" as an explicit, opt-in diagnostic, that is a
    new request with its own citation.

- **Phase order is fixed and P1 gates everything else (maintainer ruling 2026-09-20): P1 seam and library fixes →
  P2 migration and the legacy project's retirement → P3 the full UI/UX reset (last) → P4 new work.** This replaces
  the earlier "wait for one go" framing in `TODO.md`, and it is authoritative: **S3–S7 do not move until P1
  closes**, and FL's backlog remains friction the consumer's real use exposes rather than a wish list.
- **P1-A closed 2026-09-20 (documentation口径 only, no source and no carrier build): FL-8, FL-11, FL-12.** One of
  the three was fixed twice, and the second reason is the durable part.
  (1) **FL-8** — the old→new migration section is §4c of `docs/consumers/consume-from-0.7.0.md` with the four
  required parts (change point, impact, detection, supported paths) and with the time attribution **verified at
  the tips rather than asserted**: `PruneNodesExcept` is present in `0.5.x` (2 hits in
  `git show 0.5.x:…/Kernel/UiSession.cs`) and absent in `0.4.x` (0 hits), and the introducing commit
  `0ab9015` is an ancestor of `0.5.x`. It reads "introduced in the 0.4 → 0.5 move, continued since, **not** a
  0.6 regression", and the supported paths are exactly the ledger's four: hide with `Visible`/`VisibleKey`/
  `Tab`; accept the release for a real removal; keep state that must survive in the model keyed by business
  identity.
  (2) **FL-11** — the authoritative-payload rule is now stated in all three consumer documents
  (`consume-from-0.7.0.md` §1, `consume-from-0.6.0.md` §1, `docs/consumers/README.md`): the **GitHub Release
  asset** is the only verifiable payload identity; a repo dev folder, a sibling checkout's copy and whatever a
  build left in `1.6/Assemblies/` are **rehearsals**, and two hashes for one Api are two builds (a Dev channel
  builds different bytes than a Release one, and the commits differed) rather than two identities for one
  artifact. Consumers are told to verify what they actually bound (`AssemblyConfiguration`, embedded commit,
  `Require` verdict) instead of matching a number written in a document.
  (3) **FL-12 — and the correction of my own first fix, which measured the wrong object.** The sentence's count is
  the **library's own** kinds: the classes in `Source/FerriteLib.UiKit/Kernel/Widgets/` that declare
  `IUiWidget`, internal ones included = **seventeen** (it was sixteen until `input/text-field` arrived, which is
  the whole staleness). My first pass replaced "sixteen" with the **consumer's** widget count (18) — the wrong
  tree, and the wrong question. The ledger's FL-12 entry makes the same substitution (it rebuts the library's 16
  with the consumer's 18), so the correction is recorded here: **a count without its predicate is not a
  measurement, and "how many kinds implement `IUiWidget`" must name whose tree** — the library's count answers
  "how much surface must a change consider", the consumer's answers "how much of it one project uses".

- **A carrier hash identifies a build's INPUTS, and the stamp cannot record dirtiness — measured 2026-09-20, with
  the reproduction that settles it.** During round 5 the carrier at `1.6/Assemblies/` was measured by the Lead
  as 246 784 B / SHA-256 `5E6F9BF4…` stamped `0.7.0-dev+64af720e…` while HEAD had already moved to
  `ee387f1` (the Tab fix). Rather than guess between "non-deterministic build" and "dirty build", the answer
  was produced: a **full rebuild of the same source with the stamp forced back to the old commit**
  (`dotnet build … -c Release -t:Rebuild -p:SourceRevisionId=64af720e25efbb1d60708c2e1f497760893aba31`)
  reproduced `5E6F9BF4…` **byte-for-byte**, and two full rebuilds at the current stamp both produced
  `6BC192E4…`. So the build is **deterministic** — the difference was inputs, not entropy — and the suspect
  bytes were a **dirty build**: the pre-commit harness run that compiled the uncommitted Tab fix while
  `AssemblyInformationalVersion` still named the previous commit. Byte **length is not a signal**: all three
  builds were 246 784 B.
  What follows, so it is not rediscovered: (i) the stamp records the **committed** revision, so it is honest about
  the commit and silent about uncommitted source; (ii) in practice that is caught because committing the source
  MOVES HEAD, which leaves the carrier's stamp stale and makes every attribution check refuse it — the residual
  window is a dirty build whose source is never committed or is reverted, which no stamp can see; (iii) so the
  delivery order stays load-bearing (commit first, build last, report the identity measured from that build), and
  a hash quoted anywhere is an identity **only with its inputs pinned** — a hash ledger entry without the build
  command and the clean/dirty state is a machine-local fact, not an identity.
- **The shared payload path is shared with READERS, not only with builders (measured 2026-09-20), and the
  diagnostic now says so.** `1.6/Assemblies/FerriteLib.UiKit.dll` was held by another process while a delivery
  ran, and the symptom was `MSB3026` retries ending in `MSB3027`/`MSB3021` deep inside gate 1's captured
  build output — which reads like a broken build, not contention, and it cost a blocked round while the cause
  was hunted in the wrong repository. A **loaded assembly holds its file open**, so a sibling checkout's harness
  or probe blocks this build exactly as another builder would. `scripts/verify-local.ps1` now probes the
  payload for an exclusive open before any gate runs and fails fast naming the cause and the remedy; the probe
  was demonstrated against a deliberately held file (it threw the new message and ran no `dotnet`) and passes in
  a quiet window (the 9 gates below). **Correction to a claim this session made in a channel message:** a
  consumer's gate 6 does **not** rebuild this carrier — its action block only checks existence, reads the stamp,
  requires this tree clean and compares the embedded commit to HEAD ("US never builds the carrier from here").
  It can LOCK the file transiently by loading it; it cannot overwrite it. Any unexplained byte change at that
  path is a build, and only this repository builds it.

- **`Tab` on containers is legal as of 2026-09-20 — a maintainer-approved coherence fix, and the ruling's own
  words were "fix what needs fixing".** The defect was an internal contradiction: the engine reads `Tab` when
  deciding visibility for **roots and children of any type**, while the container attribute contract omitted the
  name, so the read was unreachable and a container could not be declared to appear on one tab. The consumer hit
  it as a hard `UiContractException` while migrating a real card; the demo found the same thing by reading the
  source. The change is **two lines** — `Tab` added to `UiHost.ContainerAttributes` and to the engine's mirrored
  `TemplateContainerAttributes`.
  **The four pieces of evidence, in the form that decides it (this is a coherence fix, not new surface):**
  (1) the read is already generic — `UiLayoutEngine.IsHidden` (:2570-2602) reads `Tab` unconditionally alongside
  `NarrowHidden`, `Visible`/`VisibleKey` and `Hidden`, and every call site (:211 roots, :973 the shared child
  filter `VisibleChildren`, :1262, :1408, :1543) passes container-typed children through it;
  (2) the invalidation plumbing is already container-inclusive — `RecordDeclaredKeys` (:436-455) registers
  `UiBindings.ActiveTabKey` for **any** spec that declares `Tab` (called from :978, :1264, :1410, :1545, the same
  walks that include containers), with nothing gating widget-ness. **That is the decisive one: the plumbing
  admits containers while the contract did not, which is a contradiction rather than a policy**;
  (3) the asymmetry sits inside one function: of the three sibling visibility attributes `IsHidden` reads,
  `NarrowHidden` and `Hidden` were in the container list and `Tab` was the only one missing;
  (4) it was never dead code — the read is live for widgets, atoms, `Repeat` (widget by validation, container by
  arrangement) and widget rows in templates; only its container-element branch was unreachable. So the two lines
  do not add a capability, they stop the contract from forbidding one the engine already implements.
  **The one semantic consequence, stated so it is not discovered later:** a `Tab`-gated container hides its whole
  **subtree**, and the `HelpKey` claims of that subtree go with it — consistent with every other hidden element,
  and with the engine's rule that a hidden element keeps its node and state (:1885-1887; pinned by
  `KernelIdentityTests`' Tab-switch lane).
  **Boundary, deliberately out of scope:** per-row `Tab` inside a template stays inexpressible **by design** — a
  `Tab` inside a template is not item-scoped because "a tab is a page-level answer and not an item's"
  (`UiLayoutEngine` :1863-1867). Anything per-row belongs with the route-A hierarchy × composition decision.
  **Lane** `KernelContainerTabTests` (16 printed `ok:` lines): the declaration creates (with a contrast that a
  genuinely unknown container attribute is still refused), a gated container appears and disappears with
  `active-tab` **subtree included** and switches back with no residue, announcing `active-tab` re-arranges in
  place, a hidden container keeps its node and its state, and both container lists are read by reflection and
  asserted to carry `Tab` and to be the same vocabulary. **Red-first evidence is the mutated list**: removing
  `Tab` from the host list reddens six assertions (including `KernelRepeatTests`' drift guard), and removing it
  from the engine list alone reddens three — so neither half can be reverted silently. Both reverted; the final
  run is `HARNESS_EXIT=0`, zero `FAIL` lines, ALL PASS. Contract: "`Tab` on containers (2026-09-20) — a
  coherence fix, not new surface"; evidence boundary: harness only.
  **Observation, not fixed here:** that drift guard's failure message lists the host-minus-engine difference, so
  when the HOST is the smaller list it prints "missing: " with an empty list. The lane still fires; only the
  message is one-directional. Fixing another lane's diagnostics was out of this round's scope.

- **Maintainer phase purpose (2026-09-20) — FL's queue is driven by the consumer's real use, not by a wish
  list.** The point of scheduling FL and US together is to **use US's practice to validate FL's components and
  features**; that is why the pins (FL 0.7.x, US 0.5.x) are allowed. What binds every FL session in this phase:
  the backlog is **friction that US's usage exposes**, classified (A) US misuse / (B) genuine general gap, with
  only the (B) side fixed; **no other Batch 2 item is started**; and the one capability that looked missing —
  composing a sub-tree per collection item — is **narrower than assumed**, because `Repeat` + `<Templates>`
  already provides composition (sub-tree per item key, item-local scope, `input/checkbox` inside a row,
  per-item measure, hits routed to inner widgets). What is genuinely absent is only the **cross of hierarchy and
  composition** (`container/tree` renders one label band per row and takes no template; `Repeat` has no
  level/indent); the maintainer **leans toward an optional per-row template on `container/tree`** (route A) but
  will wait until US has used the existing components, so it is **held, not scheduled** — be ready to price it
  when the friction report arrives. Every (B) fix moves HEAD and invalidates the consumer's gate green, so work
  is batched: **one freeze per round, not one per item**.

- **Maintainer ruling — additions are allowed inside `0.7.0` for the coordination phase (2026-09-20).** The
  axis keeps the **number** `0.7.0`; no minor is opened while the local consumer is mid-development, and the
  phase now runs until that consumer completes its UI work. This **amends** the 2026-09-19 ruling rather than
  replacing its reasoning: the goalpost argument is still spent, and the maintainer's answer is that a range
  pin tolerates growth under it while the consumer is local and in the loop. What an addition still owes, every
  time: a contract entry recorded **before** the code, a failure-sensitive lane, its `docs/api-tiers.md`
  classification in the same commit, and its consumer-guide line. What the ruling does **not** do: declare any
  surface stable, waive a gate, or move a tier. **When the phase ends, the normal pre-1.0 rule returns** — an
  addition moves the minor. Recorded in the line's contract as Amendment 3. First addition under it: `input/text-field` (B7).

- **Maintainer policy directive — every US→FL item is classified before it can become work (2026-09-20).**
  Two buckets, exactly one per item, and the classification must be written down wherever the item is
  recorded. **(A) US misuse / US's own job:** the consumer relied on incidental behaviour FL never contracted,
  or the need is satisfiable on the tree with its own widget kinds — fix it consumer-side and **file no
  request**. **(B) A genuine FL gap:** and then it must be a **general** capability — neutral, symmetric with
  an existing general property or a primitive already in the funnel, and useful to a consumer that is not US;
  a bespoke feature only one consumer would ever use stays a consumer kind, and when a (B) item needs new
  public surface the escalation **must carry the generality argument explicitly**. The triage of the current
  list, derived from FL's own dispositions: **B2② `WideHidden`, B5 `WidthKey`, B6 chrome action slot,
  B7 `input/text-field`, B10 alignment, B11 orphan-name check are (B) general and non-blocking** (B7 the
  strongest — the string sibling of `NumberFieldWidget` over the existing `UiNative.TextField` funnel
  primitive); **B9 (tree inline child controls) is (A)** — composing a consumer kind is the consumer ladder's
  first rung; **B8 is a defect, not a request** (label-set half fixed, schema half a maintainer call); and the
  consumer's diagnostics-lane `diag-nav-col` report is **(A) US misuse** — `Width="Auto"` is documented as
  the natural text width of a kind's registered label set and the 1px collapse was never a contract, so US
  fixed it on its own side with the existing general `VisibleKey` plus two mutually exclusive presentations,
  and it therefore produces **no FL work item and no reason to add surface or raise the minor**. FL's real
  share of that report was its A1 migration advice, wrong for that shape and already corrected in
  `docs/consumers/consume-from-0.7.0.md`. Scope: a classification rule and a triage; no surface, tier,
  version axis or code changed by it.

- **Maintainer ruling — the 0.7.x line continues through the consumer's first live pass (2026-09-19).** The
  local consumer has pinned `[0.7.0,0.8.0)` and compiled against the sibling carrier, so the "an rc that
  never shipped has no goalpost" argument that kept Batch 1 inside `0.7.0` is **spent**. The maintainer ruled
  anyway that further public surface and fixes stay inside `0.7.0`, treating this stage as **coordinated
  in-flight development**: the pin is a *range*, so growth under `0.7.0` does not invalidate it, and the
  consumer is local, in the loop, and can absorb one recompile per carrier. **What this ruling does not do:**
  it does not declare the surface stable, does not waive the contract amendment or the lane that each item
  owes, and does not move any tier. **It lapses** the moment a consumer is remote or the line is published —
  then the normal pre-1.0 rule applies again (any addition bumps the minor), which is the reading the
  `0.5.0 → 0.6.0` precedent established for a delivered number. Scope: a scheduling/axis ruling; no code, no
  vocabulary and no tier changed by it.

- **Batch 1 of the 0.7.x line landed (2026-09-19) — placement vocabulary, density reach, tone/accent
  tightening.** `已实现 + 已自动化验证` only: no in-game session and no consumer compile. All of it inside
  the one minor, under the 2026-09-18 fast-development ruling, with every break recorded in
  `docs/development/0.7/05-api-contract.md` before it shipped.
  (1) Four placement attribute names — `AlignX`/`OffsetX`/`AlignY`/`OffsetY` — implemented as one
  parent-relative rule in the new `UiPlacement.cs`, valid in a placement container (`Overlay`) and, for a
  flow container's child, on the cross axis with a pixel nudge only; ten creation-time refusals plus two
  stated boundaries (validation uses the **declared** kind while the engine reads the **effective** one, so a
  `Narrow` direction swap can leave a placement inert; template roots refuse all four).
  (2) Container `Padding`/`Gap` fall back to `UiTheme.Geometry` instead of 0, and **both** call sites — the
  measure path and the draw-side title rect — resolve identically, which is measured rather than asserted.
  (3) The authored tone vocabulary shrinks to `{Neutral, Success, Warning, Danger}`; `Active` and
  `Disabled` redirect to the states they always meant for one minor and write one appearance note per
  declaration per page.
  (4) `UiTheme.HoverPoint` is removed and replaced by the read-only derived `UiTheme.AccentHover`
  (each RGB channel 30% of the remaining distance to white, alpha carried) — one stored accent.
  **No new public type**; `UiStatusTone` stays stable with all six members, so the tightening is manifest
  vocabulary only. The test surface gained two lanes (`KernelPlacementTests`, 13 checks, and
  `KernelToneVocabularyTests`) and **42 existing expectations moved, every one classified**: all were
  fixture pins of the accepted break (`Padding="0"`/`Gap="0"` with the number kept exactly); none was
  relaxed, deleted or re-toleranced, and the reviewers' red-first artifacts are under `dist/`. Gates 9/9.
  **Independent verification (2026-09-19).** A verifier who wrote none of it produced its own lane, own
  fixtures and own arithmetic — 18 probes, `已自动化验证`, not a replay of the authors' assertions. All
  confirmed, including the ones that matter most: the deprecation note is per runtime element and not per
  frame, the `Disabled` redirect really lands on the data-derived state, `Padding="0"`/`Gap="0"` reproduces
  the pre-batch result to the float, a density-scoped container resolves the **same** pad in the measure half
  and the draw half, all fourteen refusals are located, and content exceeding the arranged rect really does
  arrive as a `UiOverflowReport` on the named `Height` axis of the fit audit.
  It found one documentation defect (**F1**: `docs/architecture.md` still asserted "no alignment property of
  any kind" and "density cannot reach the layout layer"; both corrected in this same work, which the plan's
  change surface already named), one pre-existing lane-print convention that is **not** a Batch 1 defect
  (**F2**: a lane prints `ok:` for a test whose inner checks failed), and one wrong sentence of the Lead's —
  the deprecation note is **per runtime element**, not per manifest declaration, so a collection
  materialising one declaration N times records N notes; the contract and the consumer guide were amended.
  **Declared unverified rather than glossed:** a cross-build A/B against the pre-batch binary; pass
  instrumentation for "no solver and no second pass"; an independent re-probe of the tone attribute-*name*
  gate and of role non-inheritance; "refused at the next minor" (future by construction); a re-enumeration of
  `UiStatusTone`'s six members; and in-game/consumer acceptance.
  **Deferred to batch 2** because they are additive and cite nothing yet: CP-3 (skin-source axis), CP-6②
  (role→surface mapping as data, blocked by CP-3), CP-5 (regional scope) and CP-7 (sibling-relative
  placement). Evidence boundary: harness only.

- **Element-level help landed, and the option level was generalized off its first kind (2026-09-20, G1 + G4).**
  Additions inside `0.7.0`; the maintainer approved route (a) and supplied the argument: the consumer declares
  help on EVERY element (its own corrected count is **35 claim sites = 33 widget claims + 2 in its shared draw
  helper, plus 12 manifest declarations**, not the 43/46 an earlier count gave), so a page migrated to
  declarative elements silently loses help — which is why `Repeat` has no consumer evidence, an inability
  rather than an unwillingness.
  **G1 — `HelpKey`, engine-wide on widget elements.** Any kind accepts it without listing it (the
  `Visible`/`VisibleKey`/`Width` gate); the value is an **opaque literal identity** the consumer's catalog is
  keyed by, never translated and never interpreted — a deliberate third flavour of the `*Key` family.
  Publication is the **existing session claim machine**, so there is no new member and no second channel: the
  engine's draw walk claims an element's `HelpKey` while the pointer is over it, and the consumer reads
  `HoverClaim`/`HoverClaimElement` exactly as it already does. The claim is made **inside the element's node
  scope** — that is what attributes it to the declaring element, and the first version of this wiring got it
  wrong (claimed outside `EnterNode`, so `HoverClaimElement` named whatever node was active before); the lane
  caught it with two red assertions before the fix. Refused on containers and template roots (not hit surfaces,
  so it could never claim — the A3 shape rather than an inert declaration). `UiSession.ClaimHover`'s parameter
  was renamed from `elementId` to `claim` and both it and `HoverClaim` are redocumented as an **opaque claim
  token**: every caller already passed a help identity, which is exactly what the next reader would have
  tripped over.
  **The hover rule changed with it and that is a correction to the round before**: `UiNative.IsMouseOver(Rect,
  UiWidgetContext)` no longer applies the disabled rule. Disabled-ness refuses input; hover also drives
  inspection, and a control that is unavailable is exactly when help matters — the consumer picks an
  `...UnavailableHelpKey` by row state and claims it, and vanilla shows tooltips on disabled controls. Only the
  higher-layer rule stays.
  **The option level, generalized.** `input/dropdown`'s popup rows are options of one element, so the element
  hook cannot reach them: `HoverHelpKey` now drives both kinds through one implementation (`OptionHelp`), and
  the dropdown publishes the hovered row's **value** (a dropdown's options come from the consumer's data, so the
  value is the machine token a catalog is keyed by). `UiPopup.DrawOptionList` returns the hovered row's value so
  the caller publishes without re-deriving the popup geometry; the return is additive. `HoverHelpKey` is
  therefore **complementary** to `HelpKey`, not redundant: the claim carries one live topic for the page, the
  binding carries a per-element fact the consumer's own model can read and invalidate.
  **G4 — localized option labels on `input/mode-row`**: `TitleKey1..8` through the translation seam, key-wins
  over the literal `TitleN`, value when neither; both families in the label set so `Width="Auto"` measures the
  translated title; an orphan `TitleKeyN` refused at creation.
  **A second engine defect fell out of G4 and is fixed**: the label-set seam tested "is this a translation key?"
  with a plain `EndsWith("Key")`, which cannot see an INDEX suffix — `TitleKey1` ends in `1`. It now strips
  trailing digits before the test. The threshold is measured, not asserted: the lane compares the Auto width
  against the translated string's own measure and prints all three numbers (`72` raw key vs `88` translated).
  **Contingent, deliberately not built**: a binding-resolved sibling (`HelpBind`) for a data-dependent identity.
  The consumer has exactly two such sites (`UsScopeTreeWidget.cs:388,616`) and both live in a widget that will
  not be migrated declaratively unless the hierarchy × composition decision lands, so the need is contingent on
  that decision and recorded in the contract as such rather than pulling speculative surface into this round.
  `DescriptionKeyN` was **cancelled** by the consumer (no evidence) and was not added.
  **Lanes**: `KernelElementHelpTests`, `KernelModeRowLocalizationTests`, `KernelOptionHelpTests`; round 3's
  lane corrected where the hover rule changed. **Red-first evidence is mutated controls**: mutation A (the
  engine's claim disabled) reddens nine assertions; C (the mode row's key resolution removed) three; D (the Auto
  seam back to the suffix test) one, printing the measured numbers; E (the popup's hover capture removed) three.
  The attribution bug is its own red record (`harness-4.txt`: two assertions). All reverted; the final run is
  `HARNESS_EXIT=0`, zero `FAIL` lines, ALL PASS. Evidence boundary: harness only.

- **`input/mode-row` per-option hover help landed (2026-09-20) — the kind's half, and deliberately not a
  tooltip.** The maintainer approved it and supplied the generality argument: **vanilla RimWorld's mode selectors
  already show per-option help**, so this is expected behaviour of the control rather than one consumer's
  convenience. The requirement, stated in consumer terms: (a) each option carries its own help identity,
  declared and validated at creation; (b) the consumer can learn **which option is hovered** without
  re-implementing this kind's cell geometry, because the wired consumer renders help itself in its own panel.
  What shipped: the identity is the **existing `DescriptionN`** — no new per-option vocabulary, and the names B8
  reported as orphans are now the payload — published through the new manifest attribute **`HoverHelpKey`** (an
  element-declared, creation-validated **writable string** binding key), plus the new public-unstable funnel
  member **`UiNative.IsMouseOver(Rect, UiWidgetContext)`**: the context-carrying counterpart of the raw
  `IsMouseOver(Rect)`, applying the same disabled and higher-layer rules as `Button(Rect, ctx)` and consuming
  nothing. The publication contract, stated once: while an option is hovered the row writes that option's
  `DescriptionN`, or its `ValueN` when it declares none (so the key always **names** the hovered option), and
  the empty string when nothing is hovered — **only when the answer changes**, so one write per transition
  rather than one per frame. Two creation-time refusals: a `TitleN`/`DescriptionN` whose index has no `ValueN`
  (inert before, refused now), and a `HoverHelpKey` that is not a writable string binding (the write would
  otherwise be refused mid-frame and trip the recovery band). **No new type** — `UiNative` is public-unstable, so
  the tier *type* list is unchanged and only its member clause moved (27 → 28).
  **Lane** `KernelModeRowHelpTests` (9 lanes, 37 printed `ok:` lines = 28 assertions plus the nine lane lines):
  hover moving between options changes the identity once per transition, re-hovering the same option writes
  nothing, leaving clears it once (not per frame), the value fallback names an option that declares no
  description, the click still selects in the same frame the claim is published, both creation-time refusals
  fire, and the funnel rules are driven against real arranged nodes (a command-disabled element is not hovered;
  an element under another element's popup layer is not hovered, while its own popup leaves it hovered). Its
  `Run` keeps the F2-safe shape. **Red-first evidence is a mutated control** (a new capability has no pre-fix
  revision): mutation 1 (the clear skipped when the identity is empty) reddens the leaving lane with three
  assertions; mutation 2 (both creation-time refusals removed) reddens the two validation lanes with three. Both
  reverted; the final run is `HARNESS_EXIT=0`, zero `FAIL` lines, ALL PASS.
  **B8's open half, answered but not decided** (the maintainer still owns it): per-option help makes
  `Description1..8` **not redundant** — they are the help identity the publication carries, so removing them
  would remove the capability. That is a report, not a ruling. Evidence boundary: harness only.

- **`input/text-field` landed (2026-09-20, B7) — an addition inside `0.7.0` under the 2026-09-20 ruling.**
  The maintainer called the library lacking a single-line text field **an oversight** and approved the shape this
  ledger had already ruled on: the string sibling of `input/number-field` over the existing funnel primitive,
  so the four-question promotion gate was passed before the round. What shipped: the kind (schema `Bind`,
  `Live`, `Placeholder`/`PlaceholderKey`, `Label`/`LabelKey`, `Height`, plus the engine-wide names and the
  `Tone`/`Emphasis` roles; label set = `Label`/`LabelKey` only) and one new **public-unstable** funnel member,
  `UiNative.TextField(Rect, string, UiSession, string, out bool)`, which closes the gap `docs/api-tiers.md` used
  to name ("the one interactive primitive that cannot consult the disabled state"). It owns per-element focus,
  the draft in the session's value bag, the commit rule (`committed` = the buffer changed, **or** focus left with
  a buffer differing from the model; `Live` writes on the keystroke, `Live=false` defers while focused), the
  placeholder's conditional paint, and the same disabled refusal as its numeric sibling.
  **Provenance, two independent hand-rolls in the one wired consumer** (transcribed because its tree is not
  cloneable here): `Coahuilite/UniversalSqueaker@ad1a7447b298b104e6afafa5e7fa5567ec0f3556:Source/UniversalSqueaker/UI/Kernel/UsVoicePackChecklistWidget.cs:272-323`
  (`DrawSearchField`: `UiNative.TextField` at :286, session-held focus/draft at :279-283 and :313-321, a
  placeholder painted only while empty and unfocused at :294-311) and the independent second hand-roll at
  `Coahuilite/UniversalSqueaker@ad1a7447b298b104e6afafa5e7fa5567ec0f3556:Source/UniversalSqueaker/UI/Diagnostics/UsDiagnosticsWidgets.cs:521-556`
  (funnel call at :534, session focus/draft at :527-531 and :545-553) —
  two files, written independently, converging on one contract, with the model side bound as `search-text`
  (`UsFilterBarWidget.cs:60,136`). That is evidence the **shape** is generally needed; it is **not** consumption
  evidence, and no consumer compiles against the kind yet.
  **Lane** `KernelTextFieldTests` (9 lanes, 63 printed `ok:` lines = 54 assertions plus the nine lane lines)
  drives the interaction rather than the paint: a pointer
  that focuses and one that does not, a live edit on the keystroke, a deferred draft that must not reach the
  model until the commit frame, the blur and Enter commit paths (exactly one write), the read-only refusal, the
  command-disabled funnel refusal against a real arranged element, the placeholder's four conditions, and the
  measure. It also breaks the repo's F2 convention on purpose: its `Run` prints the lane's `ok:` **only** when the
  action returned and left no failed check, so a green lane name cannot hide a red assertion in this file.
  **Red-first evidence is a mutated control**, because a new kind has no pre-fix revision: with the deferred-write
  condition narrowed to `if (live)` the deferred-commit lane reddens, and with the `writable == false` guard
  removed the read-only lane reddens (it throws through the binding); both were reverted and the lane is green
  (`HARNESS_EXIT=0`, zero `FAIL` lines, ALL PASS). **No gate enforces "an addition bumps the minor"** — measured:
  `FerriteLibVersionTests` only pins the three axes agreeing on major.minor, and `FerriteLibApiTierTests` requires
  a tier entry for every public type; neither reads the ruling. So nothing had to be weakened to land this, and
  the classification lane reddening while `TextFieldWidget` was unclassified is the gate that did fire.
  `Api` stays `0.7.0`. Recorded in the contract (Amendment 3 + "### B7"), the 0.7 README and the consumer guide.

- **B8's label-set half is fixed on the 0.7.x line (2026-09-20) — a defect fix, so it stays inside `0.7.0`.**
  **Provenance corrected the same day, because the first wording of this entry overstated it.** It said "the
  consumer's first live pass reported it" and cited `UsModeRowWidget.cs` — but that file is the consumer's
  **own** kind (`Coahuilite/UniversalSqueaker@ad1a7447b298b104e6afafa5e7fa5567ec0f3556:Source/UniversalSqueaker/UI/Kernel/UsModeRowWidget.cs:11`
  declares `us/mode-row`), and it cannot be provenance for this library's `input/mode-row`. Re-measured in the
  consumer tree: the literal `input/mode-row` appears **0** times under its `Source/**`, `Description1..8`
  appears **0** times there, and the only mode row it declares is its own `us/mode-row` (its help catalog keys
  off `us/mode-row`; the help claim stays on its side). B8 therefore arrived as a **report about this
  library's own code** — which FL then verified in its own source — and it is a **library-internal latent
  defect with zero consumer evidence**: a kind's declared label set must equal the text it paints, and
  `input/mode-row` broke that on its own. The consumer's pixels were never affected, and the fix is right for
  that reason rather than because anyone was hit by it. The **label set** is now
  `Title1..8` only, so a `Width="Auto"` mode-row stops measuring text that can never appear — the seam is
  `UiLayoutEngine.MeasureLabelWidth`, which measures the kind's *declared label set*, while `DrawOption`
  paints `option.Title` alone. Pinned by a failure-sensitive lane (`KernelLayoutTests`, "A mode-row's Auto
  width does not include a description (B8)"): **shown red on the pre-fix code** — the lane's three inner
  checks `FAIL`ed (a 12-character description measured 96px against the title's 16px) while the lane line
  itself still printed `ok:`, which is F2 — and green after (`HARNESS_EXIT=0`, zero `FAIL` lines, ALL PASS).
  The **schema** half is deliberately untouched: the four names stay legal attributes and the widget still
  reads them, so this is not a vocabulary retirement — removing them (or giving them a drawing path) is a
  maintainer ruling, and it is **pure library hygiene** (an orphan name in a general kind's vocabulary), not a
  consumer need and **not a reason to raise the minor**. **Updated later the same day, and it changes the shape
  of the open question:** they are no longer orphan names — the mode-row's per-option hover help publishes
  `DescriptionN` as each option's help identity (see the hover-help entry above), so the decision is now "keep
  the help identity or drop the capability", not "delete a name nothing reads". Still the maintainer's call.
  Recorded in the line's contract (`docs/development/0.7/05-api-contract.md`,
  "### B8") and in the consumer guide's fix list; no public type, signature, tier or manifest vocabulary
  moved. Evidence boundary: harness only, no in-game run, no consumer-side evidence either way.

- **Maintainer ruling — splitting and breaking changes are allowed in this window (2026-09-18).** The
  0.7.x line is in a fast-development phase: nothing has been pushed, nothing is consumer-compiled, and the
  local consumers (US and the other local mods) can follow a break on request. Consequences a session may
  rely on: an existing type may be split, renamed or re-shaped when cohesion or coupling demands it; a
  manifest attribute may move between the page file and the style document; and the "one minor per
  break" convention is satisfied **inside** the line rather than by opening a new one, exactly as the
  version-axis ruling above says. What this does **not** relax: every break is still recorded in the
  line's contract before it ships, with its migration; the purity, containment, neutrality, tier and
  version-axis gates still run; and once the line is delivered or published, this ruling lapses and the
  normal pre-1.0 minor rule applies again. Scope: a development-phase permission, not a change to any
  compatibility promise already made to a consumer.

- **Maintainer ruling — the placement/alignment work stays on the 0.7.x line (2026-09-18).** New
  manifest vocabulary (the placement vocabulary planned in `docs/development/0.7/10-change-plan.md`) is a
  public addition, which normally moves the pre-1.0 minor. The maintainer kept the contract axis at
  **0.7.0** and the consumer range at `[0.7.0,0.8.0)`, on the rule this ledger already carries: **an rc
  that never shipped has no goalpost to move** (the 2026-09-09 0.4.0 → 0.3.0 refile). What distinguishes it
  from the `0.5.0 → 0.6.0` move is the delivery fact that ruling named: there the dev package and its
  handoff had been delivered, while 0.7.0 has been neither published nor compiled against by anyone.
  Consequences: the vocabulary is a **second amendment** to `docs/development/0.7/05-api-contract.md`
  recorded before it ships; `docs/development/0.7/README.md`'s "no public type, kind or XML vocabulary was
  added on this line" sentence is amended with it (recorded as an obligation of CP-1); and the plan moved
  from `docs/development/0.8/` to `docs/development/0.7/`. Scope of this update: a version-axis and
  document ruling only — no source, manifest vocabulary, API tier, payload or release was changed, and
  nothing in the plan is implemented.

- **Maintainer ruling — the memory protocol is repaired and handoff is demoted (2026-09-18).** The
  protocol is four files: `AGENTS.md` (stable), `MEMORY.md` (the only volatile ledger), `TODO.md`
  (action surface) and `OBLIVIONIS.md` (cold archive; created by this ruling and **empty by design** —
  nothing has been archived out of this ledger yet). `AGENTS.md` "Memory protocol" now carries the
  archive's read rule, the English rule, compact-by-default, "documentation edits are not memory events",
  and the demotion itself. **Superseded here:** the two statements that made maintainer-local
  `HANDOFF.md` the home of a protocol — "the round/buffer protocol lives only in maintainer-local
  `HANDOFF.md`" (repository-shape ruling) and "Round lifecycle and section kinds are pinned in each repo's
  HANDOFF header" (harvest-loop fact) — both restate a grant this ruling withdraws. The file stays
  gitignored and transient. **Open action, separately authorized:** the first compaction of this ledger
  into `OBLIVIONIS.md`; choosing what leaves the only volatile ledger is a maintainer decision, not a
  session's. Scope of this update: rules and memory documents only — no source, API tier, version axis,
  build, runtime test or release was touched or claimed.

- **Maintainer ruling — consumer-count prerequisite removed (2026-09-17).** A second wired real
  consumer is no longer required before API stabilization or a supported-contract freeze. The
  previous rule created a circular dependency: the consumer could not integrate without a reasonably
  stable API, while the library would not stabilize before integration. Establish the explicit
  contract, verification, and compatibility commitment first; use subsequent integration to validate
  and improve it under that commitment. Consumer/game evidence remains honestly scoped, but its
  absence is not a consumer-count veto on stabilization. This supersedes the older freeze/scheduling
  statements elsewhere in this ledger and dated review records. `AGENTS.md`, `docs/api-tiers.md`,
  `TODO.md`, both READMEs, and the current 0.6 consumer-entry documents were aligned. Specialized-kind provenance, neutrality, API-tier
  classification checks, version/migration rules, and publication permissions are unchanged.
  **Scope of this update:** rules/documentation only; no source, API tier membership, version axis,
  build, runtime test, consumer integration, or release was changed or claimed. The 0.7 execution
  plan remains subject to a separate implementation approval.

## Version axes and the compatibility promise

- **Three axes, and the harness pins all three agreeing on major.minor.** Contract
  `FerriteLibVersion.Api`, release `About/About.xml <modVersion>`, build csproj `<VersionPrefix>` (the build
  axis is what `pack-dev` derives the artifact name and `version.txt` from). Re-derive, never quote:
  `grep -n 'Api = new Version' Source/FerriteLib.UiKit/Kernel/FerriteLibVersion.cs`, `<modVersion>`,
  `<VersionPrefix>`. `AssemblyInformationalVersion` embeds the commit and is never a compatibility value.
  On the 0.7.x line the axes are `0.7.0` and the consumer range is `[0.7.0,0.8.0)`.
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
  needs two deliberate edits in one commit. **API stabilization is not gated on a consumer count**
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

## Carrier identity and the shared payload path

- **A hash is an identity only with its inputs pinned.** `1.6/Assemblies/FerriteLib.UiKit.dll` is the whole
  payload; a hash quoted without the build command and the clean/dirty state is a machine-local fact.
  Measured 2026-09-20: the build is **deterministic** (a forced rebuild at the old stamp reproduced the
  bytes exactly), so a differing hash means different inputs. Byte length is not a signal.
- **The stamp records the committed revision and is silent about uncommitted source.** Committing source
  moves HEAD, which leaves a stale stamp and makes every attribution check refuse it; the residual window
  is a dirty build whose source is never committed. Hence the delivery order — **commit first, build last,
  report the identity measured from that build** — and hence a doc-only commit reddens "embedded commit ==
  HEAD" until the carrier is rebuilt. That is expected, not a defect: broadcast the moved HEAD.
- **A loaded assembly holds its file open, and readers count.** A sibling checkout's harness or probe
  blocks a build exactly as another builder does, and the symptom is `MSB3026`/`MSB3027`/`MSB3021` deep
  inside captured build output — which reads like a broken build, not contention.
  `scripts/verify-local.ps1` probes the payload for an exclusive open before any gate and fails fast
  naming the cause.
- **Never `Assembly.LoadFile` the payload in the session that is measuring it** — the handle survives to
  process exit. Read identity from a child process or from a copy (the Store build of PowerShell has a
  trimmed `System.Reflection.Metadata`, so `PEReader.GetMetadataReader` is unavailable there anyway).
- **`-PackDev` leaves Dev-configuration bytes at the shared output path**, and a sibling-`HintPath`
  consumer compiling against `../ferritelib/1.6/Assemblies/` picks them up. After any `-PackDev` or
  `verify-local` run the delivery step ends with a forced `dotnet build -c Release --no-incremental` **and**
  removal of the stale `.pdb` (Release sets `DebugType=none`, so it neither rewrites nor deletes one
  already there). `AssemblyConfigurationAttribute` is the detector, never the version suffix: a correct
  Release carrier still reads `0.7.0-dev+<sha>`.
- **Two concurrent harness runs collide** on the shared output path and die with `CS2012`, so a cross-repo
  build-and-harness stretch serialises on one engineer.

## Packaging discipline

- **Three channels, one staging engine.** `stage-package.ps1` owns what a package *is* (closed five-file
  set, content probe on named paths, licence copy, `version.txt`, optional deterministic archive) and
  measures the payload's configuration, so a channel label that does not match the bytes is refused
  rather than trusted. The packers own identity only: dev tolerates a dirty tree and says so, github
  requires the tag shape and the build axis, steam additionally a clean tree, and only github archives.
  `About/PublishedFileId.txt` is gitignored and the stager copies `About.xml` as a file, so no rehearsal
  or GitHub artifact can carry a Workshop identity.
- **A release asset is built after the gates run, not during them.** The Dev and Release gates leave a
  `-dev`-suffixed DLL at the shared path; the github/steam channels refuse it, and the release workflow
  rebuilds with `-p:VersionSuffix=` between the gates and the pack (now `--no-incremental`, so "freshly
  built" is literally true). The packers refuse a stale payload by comparing the embedded commit to HEAD.
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
- **Memory protocol**: `AGENTS.md` stable · `MEMORY.md` this ledger · `TODO.md` action surface ·
  `OBLIVIONIS.md` cold archive. The pre-2026-09-17 body of this file — consumer coverage and the kind
  census, the Charter and its surveys, the style-layer argument, the structure narrative, the consumer
  contract, the cross-repo round couplings — is archived there **verbatim** with its own index. Read it
  only for a historical conflict; it cannot override current sources.
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

## Gates and what each actually proves

Nine gates in `scripts/verify-local.ps1`: 1 harness, 2 Dev build (`FER_DEV`, warnings-as-errors), 3
Release build (warnings-as-errors), 4 payload present, 5 content-free (named-path probe for `Defs`,
`Patches`, `Languages`, `Sounds`, `Textures`, `ThingSets` under `1.6/` - probed as names, not filtered
from an enumeration, so an empty tree cannot pass vacuously), 6 LICENSE, 7 About.xml identity, 8 two
halves (the net472 source-shape scan and the reference-driven stub-coverage scan), 9
`dependency-reality.ps1 -SelfTest` over a TEMP fixture tree, so the boundary tool's pattern set is
proven able to go red on every full run rather than only when someone remembers `-SelfTest`. The three
source-text gates and the version axes all run *inside* gate 1, so **the gate count is not the check
count**. Gate 6 proves only what is visible from inside this repo: the file exists, carries the MPL-2.0
title, still contains Exhibit B and section 10.4, and does not apply the incompatibility notice in its
header block.
It does **not** compare the text against a consumer's copy, and no script in this repo refers to a
sibling repo at all (checked: no `Get-FileHash`, no `..\` path in `scripts/`). The byte-parity assertion
is consumer-side - US's own gate 10 hashes both copies.

Dated correction, 2026-09-04: earlier text here credited gate 6 (as "the seventh") with a SHA-256
comparison to the consumer's copy. It never ran one, and **a `LICENSE` edited or truncated in this repo
cannot turn any gate here red** — only a consumer-side gate notices, and only when the sibling tree is
present. The byte-parity assertion is consumer-side. Anything that repeats "byte-identical to the
consumer's copy" as a property of this repo's gates is wrong in the same way.

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
  author is the clicker's display name (`Fe <…@users.noreply…>`), GitHub itself as committer, and the
  `-FullHistory` identity vector went red through no edit of ours. The scan must be re-run after
  remote-side history events, not only before pushes — the same rule it already states for commits,
  extended to merge buttons, tags' absence and any other hand that writes to the graph. **The fix planned
  here — amend the merge commit's author and force-push both branches — was overtaken on 2026-09-10:
  `gh api user` shows `Fe` is this account's own GitHub-published display name, so the vector was a gate bug
  rather than a leak, and the gate now measures accounts instead of name strings (see the privacy-gate
  record in this file). The lesson about re-running after remote-side history events stands; the
  history-rewrite advice does not.**
- **File-driven invocation is the requirement; kind hot-reload is not (maintainer ruling 2026-09-14, carried across from the consumer's scope narrowing).** The consumer's 0.4.x window was cut back to a single feature port and takes no source changes, so wiring `ParseFile` is a **0.5.x** item. The requirement it must satisfy is now stated precisely: a layout or style file edit must be visible **after reopening the window**, with no game restart and no recompile, while **adding or changing widget kinds is explicitly excluded** (kinds are compiled). That is this library's own use/extend boundary with the use half moving from a copy embedded in the consumer's assembly to a file on disk; the embedded copy stays as the fallback. `UiHost` already takes both entry points at construction (`UiLayoutManifest` plus the optional `UiStyleDocument`), so no runtime tree mutation is needed - parse once at host creation, keep the previous page on failure, warn loudly. `TODO.md` §3 carries the wire-up.
  **Superseded in part, 2026-09-15:** the bar recorded here — a file edit "visible after reopening the window" — is no
  longer the 0.5 target. A save in development mode must update the windows that are already open, with manual reload
  and a last-known-good fallback behind it; C# kind changes remain outside hot reload exactly as this entry says. The
  stronger contract and its eight clauses are in `docs/development/0.5/00-baseline.md` §2.2.

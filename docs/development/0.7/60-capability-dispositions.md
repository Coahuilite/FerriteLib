# Capability dispositions — the P1-E batch (2026-09-20)

**What this file is.** One written disposition per item the review batch raised, so the maintainer/PM can rule
before anything is built. It is a **checklist, not prose**: each row carries its disposition, the source anchors
that decide it, the consumer citation if one exists, the four promotion gates, and what implementing it would
touch. Nothing here is implemented, and this round deliberately does not build or touch the carrier (a sibling
checkout is verifying against it). **Added 2026-09-24:** §C is a later, separate review batch — an audit of two
`docs/api-tiers.md` *reasons* rather than a capability request — and uses the same disposition vocabulary. It
changes no tier by itself.

**The four promotion gates** (AGENTS.md, "Ecosystem protocol"), stated once and abbreviated per row: **P**
provenance cited (a real consumer was forced to hand-roll it — a request is not evidence); **N** neutral (no
consumer's product vocabulary, no consumer's numbers as library defaults); **S** no new process-wide mutable
static; **L** a harness-drivable lane exists. An ACCEPT needs all four; a partial answer is written as such.

**Disposition vocabulary.** **ACCEPT** — implement in the one batched rebuild (still inside `0.7.x`, no minor
move). **ACCEPT-DOC** — the behaviour exists and is correct; what is missing is a written contract line. **DEFER**
— justified, not now, with the evidence that would promote it. **REJECT** — not a defect, with the argument.
**CLOSED** — already delivered; named so it stops appearing as open work.

## A. The list, one row per item

| Item | Disposition | Anchors that decide it | Consumer citation | Gates | What it would touch |
|---|---|---|---|---|---|
| **G2** a repeated row cannot report its own item key | **ACCEPT** | `ButtonWidget.cs:83` invokes the command with no payload (`ctx.Bindings.Invoke(ReadActionKey())`); `input/checkbox` writes only its own item-local bool (`CheckboxWidget.cs:34`, item-keyed state) | the hierarchy rows that stay composite (`UsPresetListWidget.cs:225-228` toggles by hand) | P ✓ N ✓ S ✓ L ✓ | manifest-only: a payload attribute on `input/button` (e.g. `PayloadKey="item-key"`) read from the row's item scope and passed to `Invoke`; new lane asserting the payload reaches the command per row |
| **G3** no chrome-free hit area sized to measured content | **ACCEPT** | every clickable core kind paints a surface (`ButtonWidget.Draw`/`Paint` :71-110); button height is `Height` or `Geometry.RowHeight` (:61) — no "invisible, measure my content" shape | US's 20x20 row check squares and tag-sized chips (`UsPresetListWidget.cs:225-228`) | P ✓ N ✓ S ✓ L ✓ | a `Chrome="none"` (or `Bare="true"`) attribute on `input/button` + `Height="Auto"` measured from its content; touches `ButtonWidget.Measure/Paint` and the atom schema; lane for "no surface painted, hit test intact, height from content" |
| **G5** `chrome/banner` declares no `Tone` | **ACCEPT** | `ChromeBannerWidget.cs:43` schema is a literal list without the role names, unlike the atoms that go through `AtomVocabulary.Schema` | US keeps its own status-band composite precisely for this | P ✓ N ✓ S ✓ L ✓ | one-word vocabulary move: banner adopts the role pair like every atom (its own colour literals become the default role); touches the banner schema + its paint path; lane pins "an authored Tone moves the band" and "no Tone keeps today's look" |
| **FL-13** no alignment axis in the page model | **DEFER** (shape is a layout attribute, not appearance) | 20 `TextAnchor` literals in `Source/` — alignment is each widget's C# (`ButtonWidget.cs:113`-style), while placement alignment already exists as `AlignX`/`AlignY` (`UiPlacement`, Batch 1) | US's right-aligned readouts (`UsCameraReadoutWidget.cs:44-50`) | P ✓ N ✓ S ✓ L ✓ | a **layout** attribute (e.g. `TextAlign`) read by the label outlets through `ctx`, or a per-kind attribute — a design decision, not a rename; defer until the P3 UI/UX reset says which axis it wants, because choosing it now would freeze vocabulary the reset may contradict |
| **FL-16** dynamic options have no typed (display, value) pairs | **ACCEPT** | static `OptionN`/`ValueN` pairs work (`DropdownWidget.cs:197-198`); the dynamic path is string-only — `DropdownWidget.cs:173` calls `GetOptions<string>` though `IUiBindings.GetOptions<T>` is generic (`IUiBindings.cs:75`), so a non-string element type cannot validate | US's hand-rolled dropdown, ~95 lines (`UsKernelDraw.cs:606-701`) | P ✓ N ✓ S ✓ L ✓ | a library-owned pair type (e.g. a public `UiOption` = display + value) plus a dropdown path that accepts it; **new public type**, so the tier list moves with it in the same commit; lane for "typed pairs render display, commit value" and the option-order/duplicate rules |
| **FL-17** `container/tree` rows cannot hold an inline control | **ACCEPT (route A, priced here)** | `TreeWidget` binds `IReadOnlyList<UiTreeRow>` (`:10`, `:72`, `:194`) and each row renders one label band + marker; no template per row | US's two-level check tree, 319 lines (`UsPresetListWidget.cs`) | P ✓ N ✓ S ✓ L ✓ | the per-row template the maintainer leaned toward: `<Templates>` on `container/tree`, reusing `BuildItemSpec`/`PruneNodesExcept` (`Repeat`'s machinery, `UiLayoutEngine.cs:1838-1861`, `:1889-1908`) so a tree row is a `Repeat` row with a level; the largest item in this batch and the one to schedule last |
| **FL-18** chrome band is not extensible | **ACCEPT (re-shaped)** | `UiWindowHost.DoWindowContents` is `public sealed override` (16 `protected virtual` members beside it) — but the correct ask is a **declarative non-scrolling region**, not unsealing the draw | US's help toggle scrolls away inside the page title band (`UsPageTitleWidget.cs:112-149`) | P ✓ N ✓ S ✓ L ✓ | a declarative slot the shell renders outside the scroll scope (an attribute on the page root or a reserved band on `UiWindowHost`); merges with **B6** — implement once, not twice |
| **FL-19** mode-row `Description1..8` are orphan vocabulary | **CLOSED** | the label set is `Title1..8` + `TitleKey1..8` only (`InputModeRowWidget.cs` label set), so they no longer widen an Auto column; they are now the **payload** `HoverHelpKey` publishes per option (the shared `OptionHelp` contract), and `HelpKey` carries the element-level topic | the B8 report itself | — | nothing; recorded so it stops being counted as open. The vocabulary half (removing the names) remains the maintainer's call and is now *harder* to justify, since they carry the help identity |
| **FL-21** `UiOverflowReport.Available` is the inset label rect, read as the element height | **ACCEPT-DOC** (+ one optional field) | `UiFitAudit.cs:35,50` — `Available` is the caller's `available` (the label rect after padding: `WrappedTextWidget.cs:108-110`), while `Needed` is the measured text height; the reader-facing phrase "has N px" is the consumer's log line | a consumer operator was misled once (in-game feedback 2026-09-20) | P ✓ N ✓ S ✓ L ✓ | fix the XML doc on `Available`/`Needed` to say which rect each is, and say in the consumer guide that an overflow verdict is about the **label band**, not the element; a second field for the element's own extent is possible but only if a consumer asks — do not add surface for a wording problem |
| **FL-22** audit saturation: log lines ≠ element count | **ACCEPT-DOC** | `MaxReports = 48` (`UiFitAudit.cs:136`), dedup key = path+text+font+axis+needed+available (`:313`), `Saturated` is public (`:160`), `Enabled` defaults false (`:157`), `Reset()` clears (`:217`) — the semantics exist and are implemented, not missing | the consumer's `Begin()`/`Reset()` per session (`UsTextFitAudit.cs:21-28`) made "xN" counts look like a census | P ✓ N ✓ S ✓ L ✓ | a contract paragraph in the consumer guide: findings are deduplicated by key, capped at 48, `Saturated` says the cap was hit, and a count is a count of **distinct findings**, never of elements. No code change |
| **B2(2)** `WideHidden` | **ACCEPT** | `NarrowHidden` is in all three attribute lists (`UiHost.cs:884`, `:909`, `UiLayoutEngine.cs:108`, `:115`) and read by `IsHidden` under `narrow` (`UiLayoutEngine.cs:2574`); `WideHidden` is the same rule under `!narrow` | US's diagnostics lane fix (round 2) worked around its absence with two mutually exclusive presentations | P ✓ N ✓ S ✓ L ✓ | one attribute name in the three lists + one branch in `IsHidden`; lane for the mirror (wide-hidden disappears past the breakpoint, `NarrowHidden` disappears below it, and the two on one element are refused) |
| **B5** `WidthKey` | **ACCEPT** | `VisibleKey` is the precedent (a binding answers a layout question, `UiLayoutEngine.cs:2628-2634`); `Width` is numeric and engine-wide (`UiHost.cs:884`) | US's resizable columns (accepted a two-tier degradation) | P ✓ N ✓ S ✓ L ✓ | one attribute + a numeric read through the same declared-key registration (`RecordDeclaredKeys`, so an announcement re-arranges); lane for "the key moves the column, the announcement re-arranges, a malformed value falls back loudly" |
| **B6** chrome action slot | **ACCEPT, merged into FL-18** | as FL-18 | as FL-18 | P ✓ N ✓ S ✓ L ✓ | the same slot serves both: a declared region plus an action bound to it. One lane covers both, which is why they must land together |
| **B10** text alignment as a layout attribute | **DEFER, with FL-13** | as FL-13 — it is the same decision seen from the other end (FL-13 asks where alignment lives; B10 asks which axis) | as FL-13 | P ✓ N ✓ S ✓ L ✓ | nothing now: implementing either freezes vocabulary the P3 reset may contradict. They are one item with two names |
| **State-document palette choice** (a document cannot select a built-in appearance) | **DEFER** — no citation yet | the resolver applies **token overrides** onto the host's injected theme (`UiStyleResolver`, `UiStyleDocument`); there is no base-palette selector in the vocabulary | none supplied | P **✗** (a request, not a citation) N ✓ S ✓ L ✓ | if earned: a document-level base selector (e.g. `Theme="Vanilla"`) resolved at host creation, which touches theme resolution and the two named palettes. **Evidence that would earn it:** a consumer page that must ship both looks *without* the host choosing, i.e. a real document whose absence of this costs it a second page |
| **Button has no bindable selected/tone state** | **ACCEPT** | `Tone` is manifest-authored only (the atoms' role resolution reads the authored string; a probe for a binding-driven role name finds none), while `VisibleKey` shows the binding-driven pattern | US hand-rolls selection: `UsKernelDraw.SelectionButton(rect, ctx, label, theme, **selected**, danger, font)` with six call sites (`UsFilterBarWidget.cs:115,118,144,150,156`, `UsAttenuationEditorWidget.cs:149`) | P ✓ N ✓ S ✓ L ✓ | **not** a `ToneKey` that could re-author the retired `Active` name: a **`SelectedKey`** (bool, symmetric with `VisibleKey`) that resolves the element's role to the `Active` state when true. Manifest-only, engine-wide, read where roles resolve; lane for "true selects, false does not, an unresolvable key is fail-soft and recorded once" |
| **Unproven: `state/empty` has no `Bind`** | **NOT A GAP — evidence first** | `EmptyStateWidget.cs:35` schema = `{Id, Kind, Text, TextKey, Height, Tab, Hidden}` — no `Bind`; the text is a literal or a translation key, which is how every other text-bearing atom reads its caption | none | — | **What is needed to make it a finding:** a consumer page whose empty-state text must come from the *model* (a runtime string with no translation key) and that therefore cannot use the kind. Then the shape is "a `Bind`/value path on the text atoms", evaluated together with `WrappedTextWidget` (which already has `Bind`) rather than as a one-kind patch |
| **Unproven: `state/empty` has no `MinHeight`/reserved band** | **NOT A GAP — evidence first** | the atom measures `Height` or the theme's row height; there is no minimum-size concept anywhere in the vocabulary | none | — | **What is needed:** a page where an empty state must reserve a minimum height inside a `Fill` region, with the measured failure (the band it gets and why that is wrong). A general `MinHeight` for one kind would be a per-kind patch; if it is real, the shape is an engine-wide measure floor — a design item, not a field |

## B. What the ACCEPTs add, in one place (for the batched ruling)

**Manifest vocabulary only (no new public type):** G2's payload attribute, G3's chrome-free/auto-height pair,
G5 (banner adopts the existing role pair), B2(2) `WideHidden`, B5 `WidthKey`, the `SelectedKey` of the
button-state item, FL-18/B6's declarative non-scrolling region.

**One new public type:** FL-16's `UiOption` pair (display + value), with its `docs/api-tiers.md` entry in the
same commit.

**Documentation only:** FL-21 and FL-22.

**Deferred:** FL-13, B10 (one item, two names — wait for the P3 reset), the state-document palette selector
(no citation).

**Largest and last:** FL-17's per-row template on `container/tree`, which reuses `Repeat`'s materialisation and
prune machinery and should be scheduled after the smaller items so one rebuild covers the rest.

**Batch 2 outcome (2026-09-20, task-25).** **G2** and **G3** landed, each with a lane whose red is attributable
by assertion name in one mutated build (2 FAIL lines: the command received an empty payload, and Chrome none
still painted five surfaces), green after (ALL PASS, 2594 ok). **FL-16 was WITHDRAWN, not deferred for effort**:
the implementation and the public type were written and the suite went green, but with the pair path reverted to
the faithful pre-fix shape the lane stayed GREEN - a lane that does not discriminate is not evidence, so the
type, its tier entry and the lane were removed rather than shipped. The recorded diagnosis is the starting point
for the next attempt: the string fallback accepted a pair-bound key instead of reporting the element-type
mismatch, which is exactly what made the probe blind.

**Batch 1 outcome (2026-09-20, task-23).** Landed with lanes and mutation evidence: **B2(2) `WideHidden`**,
**B5 `WidthKey`**, **`SelectedKey`**, **G5** (banner role pair), and the **FL-21/FL-22** wording.
**Moved to the next batch, verdicts unchanged — G2, G3, FL-16**: each is ACCEPTed on the four gates, but the
batch's evidence bar is one failure-sensitive lane per item with its own mutation, and a new public type plus two
more attributes would have shipped in the same freeze window without a lane shown red against them. They are
listed here rather than half-landed. FL-17 and FL-18+B6 remain with the maintainer (task-24).

**Then:** task-23 folds every ACCEPT into **one** carrier rebuild and one FREEZE NOTICE. New surface stays inside
`0.7.x` under the phase ruling — no minor move. Nothing in this file is implemented yet.

## C. Tier-reason review (2026-09-24) — one reason found false, one citation corrected, one disposition owed

**What this batch is.** Not a capability request but an audit of two **reasons** written into
`docs/api-tiers.md`'s public-unstable section, each citing a consumer artefact that has since moved. The audit
started from "the consumer no longer references either name" and **that premise was half wrong**: it is true for
`LineChartWidget` and false for `UiChartPointChange` (§C.2). The distinction matters, because only one of the two
entries needs its verdict revisited. The batch is
recorded here because the disposition is owed by the session/maintainer exactly as §A's are, and because the
guard that owns that file cannot see this class of drift. Nothing here is implemented, no tier is changed by
this section, and the reviewer ran **no build** — the review is documentation-only.

### C.0 What the tier guard does and does not check (why a human has to look)

`tools/FerriteLib.UiKit.Tests/FerriteLibApiTierTests.cs` enforces four things, and only these four:

| Assertion (lane name) | What it reads |
|---|---|
| `Every public payload type is classified in exactly one tier` (:77-87) | the set of exported type **names** against the entries parsed from the three headings |
| `No tier entry names a type that no longer exists` (:89-100) | an entry whose type has gone |
| `The stable tier matches the pinned promise` (:102-120) | the **stable** section only, against `PinnedStableTier` (:36-50) |
| `Tier comparison fires on a planted unclassified type` (:122-152) | a positive control that the reader is not vacuous |

**No assertion reads an entry's reason text.** A reason that has stopped being true therefore cannot redden
anything: "the entry exists" is the whole membership test. That is the general shape to remember — *a
hand-kept document with a membership guard has no guard on its prose* — and the two entries below are what that
costs when a consumer retires a composition. The lane's own summary says the same thing in its own words
(`:10-21`, quoting the `UiWidgetRegistry.Clear` precedent).

### C.1 `LineChartWidget` — the stated reason is false at the consumer's current tip

**The reason as written** (`docs/api-tiers.md:180-181`): "named by the consumer's own composition, so it cannot
go internal yet; that use is also the specimen behind the tree-membership metric, and the debt list would rather
it be a kind string."

**What the trees say now** (read-only; revisions pinned so the reading can be re-run):

- **The composition is gone.** `coahuilite/UniversalSqueaker@f378715` deletes
  `Source/UniversalSqueaker/UI/Kernel/UsAttenuationEditorWidget.cs` (S4-3b, 2026-09-22); a deletion-filtered
  log over that path names `f378715` and nothing later.
- **The consumer no longer names the type anywhere.** A type-name search over that repo's `Source/` and
  `tools/` at `coahuilite/UniversalSqueaker@a13f8af` returns **zero** hits; the name survives only in that
  repo's own `docs/` and `OBLIVIONIS.md` prose, which is not a compile-time reference.
- **It is reached by kind string instead:**
  `coahuilite/UniversalSqueaker@a13f8af:Source/UniversalSqueaker/UI/Layout.Schema2.xml:236` declares
  `<Widget Id="attenuation-chart" Kind="chart/line" Bind="attenuation-points" ActionBind="attenuation-point"
  Editable="true" EditablePoints="1,2" Height="64" …/>`, and the consumer's own lane pins that declaration
  (`…@a13f8af:tools/UniversalSqueakerKernelHostTests/DeclarativeAttenuationLaneTests.cs:94-135`).
- **The demo names only the string as well:**
  `ferritelib_uikit_demo@29b4f61:Source/FerriteLibUiKitDemo/Xml/Settings.xml:123` and
  `…@29b4f61:Source/FerriteLibUiKitDemo/DemoCatalog.cs:173`; a type-name search there is **zero**.
- **The type's own shape is already the kind-string shape:**
  `Source/FerriteLib.UiKit/Kernel/Widgets/LineChartWidget.cs:20` (`public sealed class LineChartWidget :
  IUiWidget`), `:22` (`public const string Kind = "chart/line"`) and `:35-42` (its `Register()`), called
  from **inside** the assembly at `Source/FerriteLib.UiKit/Kernel/KernelCoreWidgetRegistrar.cs:13`.

⇒ The entry's own wish — "the debt list would rather it be a kind string" — is satisfied by both consumers
today, and that is exactly the shape of every entry already in the internalize-candidate section
(`WrappedTextWidget`, `ButtonWidget`, `SliderWidget`, `NumberFieldWidget`, …).

**The three options, with what each needs.**

| Option | What it needs | Assessment |
|---|---|---|
| Re-argue (keep public-unstable, find a new justification) | a consumer that names the type in code, or a general reason independent of consumers | **no such consumer exists at either pinned tip**; a request is not evidence (`AGENTS.md`, "Ecosystem protocol") |
| **Move to internalize-candidate** | one edit in `docs/api-tiers.md` | **recommended.** Only the *stable* list is pinned in the lane (:102-120), so a move between the two non-stable tiers is one deliberate edit, not two. Actual **removal** still waits on the kind-name container that section already names (`docs/api-tiers.md:427-429`) |
| Keep public-unstable, rewrite the reason | a sentence true at the current tips | acceptable fallback; it leaves a public type no consumer names, which is the debt class `TODO.md` calls "an unconsumed kind" |

**Disposition (session ruling 2026-09-24): 待裁 — recommended, deliberately not executed.** The tier label is
**not** changed now. Internalising a public type is a **breaking action**, so it belongs to the next breaking
window (the 0.4.0-style sweep), and the entry itself records that removal waits on the stable kind-name container
(`docs/api-tiers.md:427-429`). The recommendation above therefore stands as a *candidate* recorded here until that
window opens, and the one edit a decision would need is measured rather than assumed — as C.0 records, only the
**stable** list is pinned, so a public-unstable → internalize-candidate move is a single deliberate edit.

**Not measured (UNRUN).** Whether an `internal LineChartWidget` compiles and leaves the harness and the ten
gates green. That question belongs to the batch that would make the change.

### C.2 `UiChartPointChange` — the verdict holds, the citation does not, and the name is NOT unreferenced

**The reason as written** (`docs/api-tiers.md:182-183`): "the typed drag result of `chart/line`, consumed by
the attenuation editor today; it travels with that widget's shape, which the metric wants expressed as a kind
plus bindings instead."

**Correction to this review's own opening claim, found by checking the instrument's input.** The audit started
from "the consumer has zero references to both names". That is true for `LineChartWidget` and **false** for
`UiChartPointChange`: what `f378715` deleted was the *editor widget*, not the *consumption*. At
`coahuilite/UniversalSqueaker@a13f8af` the type is named in shipped source and in the consumer's harness:

- `Source/UniversalSqueaker/UI/UsKernelSettingsHost.cs:335` —
  `bindings.BindAction<UiChartPointChange>("attenuation-point", change => { ApplyAttenuationPoint(…); bump(); });`
- `…:944` — `private static void ApplyAttenuationPoint(…, UiChartPointChange change)`
- `tools/UniversalSqueakerKernelHostTests/Program.cs:938` (`Invoke`), `:1351`
  (`ValidateAction<UiChartPointChange>`), `:1715` (`Invoke`).

The manifest routes to that binding: `…@a13f8af:Source/UniversalSqueaker/UI/Layout.Schema2.xml:236`
(`Kind="chart/line" ActionBind="attenuation-point"`).

⇒ A **generic type argument in a consumer's own source is a compile-time dependency on the type.** The tier
stays public; what is wrong is the sentence's *location* ("the attenuation editor"), not its verdict.

| Option | Assessment |
|---|---|
| **Keep public-unstable, rewrite the reason** | **recommended** — the citation becomes the host's typed action binding above, and the "kind plus bindings" sentence stays true |
| Internalize | refuted by the citations above while the consumer binds the action by type; it would break that tree's build at compile time |
| Re-argue | unnecessary; the verdict was never wrong |

**Disposition (session ruling 2026-09-24): the citation is corrected in this same batch.** `docs/api-tiers.md`
now names the current consumption sites — the host's typed action binding and that repo's harness generic type
arguments — instead of the deleted composition file, and its **tier is unchanged** (accuracy, not a layering
decision). Read C.2 as **closed for the citation half**. The entry's "kind plus bindings" sentence stays open as
prose, which is the same class of item as C.1's, and no gate reads it.

### C.3 What a ruling would touch, and one process note

**Touched by any of these decisions:** `docs/api-tiers.md` only. No source, no lane, no manifest vocabulary,
no carrier, no version axis — the classification lane is satisfied by an entry's presence and by the stable pin,
so nothing here re-cuts a gate. **Part of it is already done:** C.2's citation was corrected in the same batch
(2026-09-24) with no tier move; C.1's tier label is untouched and waits for a ruling.

**Process note (a suggestion, not a decision).** A tier reason that cites a consumer tree carries no
re-derivation, so it rots silently. This repository's own rule for that class is already written down — prefer a
re-derivable **predicate** over a commit anchor (`MEMORY.md`, "Documentation describes a moment, not a state").
Applied here: a reason of this class should state what would have to be true ("no consumer names this type in
code; both reach it by kind string") so the next reader can re-check it with one search, rather than naming a
file that may since have been deleted.

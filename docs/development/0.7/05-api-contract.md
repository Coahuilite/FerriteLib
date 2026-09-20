# 0.7 — the supported contract for the subtraction-and-stabilization line

Written before any behavior change on this branch, per the 0.7 plan. This file records what 0.7.x commits
to; `docs/api-tiers.md` keeps the per-type tier membership, which this line does not change. The consumer
assertion range for the line is `[0.7.0, 0.8.0)`.

## Public surface

No new public types, widget kinds, or XML vocabulary land in 0.7. No signature changes on existing
members. The API-tier *type* list stays the 0.6 pin.

**Amendment 3 (2026-09-20) — additions are allowed inside `0.7.0` for this coordination phase.** The
maintainer ruled that the axis keeps the **number** `0.7.0` and that no minor is opened while the local
consumer is mid-development: additions land inside the line, each one still owing its contract entry (recorded
here before the code), its failure-sensitive lane, its `docs/api-tiers.md` classification in the same commit,
and its consumer-guide line. This supersedes the "no new public types, widget kinds or XML vocabulary"
sentence above **for the duration of that phase only**; it declares no surface stable, waives no gate, and the
normal pre-1.0 rule (an addition moves the minor) returns when the phase ends. The API-tier *type* list is no
longer the 0.6 pin: it gains one internalize-candidate entry (`TextFieldWidget`) and one member on the
public-unstable `UiNative`.

**Amendment (2026-09-18):** `UiTheme` gains one public static factory, `Vanilla`, so the two built-in
palettes are named peers. The addition is recorded here before the member ships.

**Amendment 2 (2026-09-18), and it is a break, not an addition.** The same package makes `new UiTheme()`
an unpainted token bag, so a theme built that way no longer inherits a recommended skin for the tokens it
does not set. A consumer that relied on the constructor default is repainted. The pre-1.0 rule is
unconditional for *additions*; a behavioural break is a fortiori a break, and recording it as an addition
because no signature moved would be exactly the silent growth the 0.6 window rejected in writing. It is
affordable here for one measured reason and no other: **the 0.7 line has never been delivered** — not
published, not compiled against by any consumer, used locally only — so there is no pinned number and no
migration for a stranger. The migration for a local consumer is one line, stated under C below. This
paragraph is the amendment the 0.7 README's "no public type, kind or XML vocabulary" sentence takes
with it.

## Behavior changes, in contract terms

### A1 — Row `Width="Auto"` falls back to unsized, not to a 1-unit stub

A Row child declaring `Width="Auto"` whose label measurement yields no positive width (unregistered or
label-less kind, empty measured text) after the declared-width clamp is allocated like an otherwise
identical child with no `Width` attribute: same flex/remaining-space distribution, same constraints.
Positive measured Auto widths and positive `MinWidth` Auto behavior are unchanged. Stack and Wrap keep
their own fallbacks. A 1-unit width may still appear when the row genuinely has no usable space left.
Migration: an author who depended on the 1-unit stub for a collapsed filler should expect that child now to
share remaining space; give it an explicit fixed `Width` to keep it collapsed.

### A2 — `Height` is validated at creation

Widget and container `Height` is validated at host creation and candidate-document validation with a
located `UiContractException` (element id/path, attribute, value). Accepted: omitted, empty/whitespace,
case-insensitive `Auto`, and finite invariant-culture numbers (including zero and negative, which keep the
existing clamp behavior). Rejected before arrange: malformed strings, `NaN`, infinities. The layout
engine's defensive checks stay. Migration: a document carrying a malformed `Height` drew nothing before
( arrange-time `FormatException`); it now fails at creation with an attributable message. Fix the value.

### A3 — `Cols` / `NarrowCols` are Wrap-only vocabulary

Declaring either attribute on a container whose kind is not `wrap` throws at creation with a located
contract error naming the attribute and path, because the grid-column path never reads them elsewhere.
Valid Wrap behavior, positive-integer validation, and the `NarrowCols` requirements (needs `Cols` and a
governing breakpoint) are unchanged. Migration: remove the inert attribute, or switch the container to
`wrap` if grid flow was the intent. This is validation tightening, not a neutral change.

### A4 — Dropdown exact values beat display-text fallback

`input/dropdown` resolves the bound value in two passes over the option list: an exact ordinal **value**
match anywhere in the list wins; only if no value matches does the existing ordinal **text** fallback run.
Option order is preserved within each pass; the no-match result (display the raw value) is unchanged.
Migration: a page whose bound value previously displayed an earlier item's text because that text matched
now displays the item whose value matches. That is the defect being fixed.

### A5 — Wrong-kind options registration says so

When a dropdown requires an options binding for key `x` and no options registration exists, but `x` **is**
registered in another category (e.g. `BindReadOnly<IReadOnlyList<T>>`), the creation-time error names the
wrong registration kind and points at `BindOptions<T>`, with the key and element path. Exception type is
unchanged (`InvalidOperationException` from the validation seam). A truly missing key keeps its existing
message; a key that legitimately carries both a value and an options registration is not rejected.
Diagnosis only: no binding category is ever coerced into another.

### C — two peer built-in palettes; the constructor is a bag, not a product

`UiTheme.Vanilla` and `UiTheme.DarkGold` are the two built-in palettes. They are peers: neither is
the default, neither is history of the other, and neither is selected by constructing the bag.
Each factory returns a fresh independent instance. Token names, geometry, fonts, spacing, and the
renderer do not move in this package.

- **Vanilla** — neutral-surface, yellow-accent palette aligned to vanilla window/section substrates
  and edges (provenance and the role table live in the C lane; derived values are marked derived).
- **DarkGold** — the warm-gold palette (near-black planes, shared-border-only edges). Same token
  names; a different look, not a compatibility alias for `new()` and not a frozen 0.6 escape hatch.

`new UiTheme()` constructs an empty token bag (style table, density, font) with no product palette.
A host still requires an explicit non-null theme; pass `Vanilla` or `DarkGold`, or start from one of
them and apply overrides. A custom `new UiTheme() { … }` no longer inherits a recommended skin for
unspecified tokens.

Migration: pick a named factory at the existing required theme parameter. There is no constructor
default to keep, and no ranking between the two palettes.

### Batch 1 (2026-09-18) — placement vocabulary, density reach, tone/accent tightening

The 0.7 line is in its fast-development window (maintainer ruling 2026-09-18): nothing is pushed, nothing is
consumer-compiled, and the local consumer can follow a break on request. Every break below therefore lands
inside **this one minor**, `0.7.0`, and is recorded here before it ships. The axis value and the consumer
range `[0.7.0,0.8.0)` do not move.

**Additions**

- Four manifest attribute names — `AlignX`, `OffsetX`, `AlignY`, `OffsetY` — for a child of a placement
  container (`Overlay`); a flow container's child accepts its **cross axis** (`AlignY` in a `Row`,
  `AlignX` in a `Column`/`Stack`) with a **pixel nudge only** — a percentage offset there, or an alignment
  on the flow's own main axis, is refused at creation. **No new public type**: the values are attribute
  strings, so the API-tier type list is untouched.
- **Two boundaries stated so they are not discovered later.** (i) Placement validates against the
  **declared** kind while the engine reads the **effective** kind, so a `Row` carrying `Breakpoint` plus
  `Narrow="Column"` may declare a placement its narrow state does not own: it applies wide and is inert
  narrow, exactly as `Narrow` already swaps a container's kind. Refusing it would be a new restriction with
  no citation, so it is documented rather than refused. (ii) A **template root** refuses all four names,
  because its parent is the collection element and not a container; an element *inside* a template is a real
  child and keeps the subset.
- `Tone` and `Emphasis` keep their attribute names and their closed-vocabulary behaviour: an unknown
  attribute *name* is still refused at creation, an unknown *value* still falls back and is recorded.

**Breaking changes**

1. **Container spacing falls back to the theme's geometry.** `Padding` and `Gap` on a container defaulted
   to **0**; they now fall back to `UiTheme.Geometry.Padding` / `Gap` when absent. Neither built-in palette
   overrides the geometry, so both carry `6`, and a container declaring neither moves from 0 to 6.
   *Migration:* write `Padding="0"` / `Gap="0"` to keep the previous result exactly.
2. **The authored tone vocabulary shrinks to four meanings.** `Tone` accepts `Neutral`, `Success`,
   `Warning` and `Danger`. `Active` and `Disabled` stop being authored names — `Active` is a **selected
   state** and `Disabled` is a **data-derived state** (a read-only binding), which is what the resolved table
   already derived. For one minor both names keep working and redirect to the corresponding state, each
   recording one deduplicated deprecation note on the appearance channel; both are refused at the next minor
   boundary. *Migration:* express the state instead of naming it.
   **No `UiStatusTone` member is removed** — that type is stable, so `Active` and `Disabled` remain members
   the library uses internally as states. The public-vocabulary tightening is what changed, not the type.
   **The deprecation note's shape**, because it rides the existing appearance channel rather than adding a
   second one: a manifest writing `Tone="Active"` or `Tone="Disabled"` renders **the same treatment it always
   did** and writes one `UiStyleFallbackReport` with `Attribute="Tone"`, `Authored` as written, and
   `Resolved` = `"the Active state (deprecated alias; the selected treatment)"` or `"the Disabled state
   (deprecated alias; derived from the bindings)"`. Deduplication is the channel's existing key
   (element path | kind | attribute | authored), so it is **one note per runtime element, not one per frame**
   — a manifest declaration that a collection materialises N times is N elements and therefore N notes
   (measured: two `Tone="Active"` declarations on distinct element paths record two). The appearance half is
   live whether or not `UiFitAudit.Enabled` is set. The attribute *name* stays fail-closed: only the
   accepted *value* set shrank.
3. **`UiTheme.HoverPoint` is removed** (the type is public-unstable) and replaced by
   **`UiTheme.AccentHover`**. The accent is one stored colour with a derived hover step, and the derivation
   is a **value**, not a promise: `AccentHover` is read-only and recomputed on every read, and each RGB
   channel of `AccentGold` travels **30% of its remaining distance to white** while alpha is carried
   unchanged (so a zero-alpha accent gets a zero-alpha hover step, never an opaque one). Worked examples:
   `Vanilla` (0.93, 0.77, 0.22) → (0.951, 0.839, 0.454); `DarkGold` (0.82, 0.60, 0.22) → (0.874, 0.72, 0.454).
   *Migration:* read `AccentHover` instead of the removed member. A style document that declared
   `HoverPoint` yields **exactly one recorded issue naming the unknown token**, the rest of the scheme still
   applies, and the declaration never reaches the theme — an appearance-class fallback, never a silent no-op
   and never a page failure.

**A rule stated once (CP-6③), because it decides what may dangle**

- A **role** (`Tone`) is code-owned: every tone must resolve in every skin, and a skin's freedom is what a
  tone is *painted with*, not whether it exists. A role reference therefore **cannot** dangle, which is why
  the role stays on the element in the page file rather than moving into the style document.
- A **`Scheme`/`Density` name** is document-owned: a name a skin does not declare leaves the reference
  unresolved, which is an appearance-class fallback recorded on the fit audit and never a page failure.

**Not changed by this batch:** the renderer, the fourteen drawing outlets, `UiGeometry`'s five metrics, the
token names other than `HoverPoint`, and the identity of the two built-in palettes.

### B8 (2026-09-20) — `input/mode-row`'s declared label set drops the description names

A **defect fix, not a vocabulary change**, and it stays inside `0.7.0` for that reason. The kind declared
`Title1..8` **plus** `Description1..8` as its **label set** — the names `Width="Auto"` measures through
`UiLayoutEngine.MeasureLabelWidth` — while no drawing path paints a description (`DrawOption` writes
`option.Title` and nothing else). So an Auto column measured text that can never appear: pixels moved for
invisible content.

- **Fixed:** the label set is `Title1..8` only, so an Auto mode-row hugs its titles.
- **Not changed:** the attribute **schema** still declares `Description1..8`, so a manifest that writes them
  still creates, and `InputModeRowWidget` still reads them into its option. Removing the names from the
  vocabulary — or giving them a drawing path — is a retirement-clock decision the maintainer owns and is
  deliberately **not** part of this fix.
- *Migration:* a page that relied on a long `DescriptionN` widening a `Width="Auto"` mode-row now gets the
  title's width. If the wide column was the intent, declare the width explicitly (`Width="320"`) or a
  `MinWidth`.

The change is pinned by a failure-sensitive lane — `KernelLayoutTests`, "A mode-row's Auto width does not
include a description (B8)" — shown red on the pre-fix code (the Auto column measured the 12-character
description at 96px against the title's 16px) and green after. No other lane's expectations moved: this is a
defect fix on one kind's declared label set, not the kind of break Batch 1 recorded.

### B7 (2026-09-20) — `input/text-field`, and the text-field funnel that carries element identity

An **addition**, landing inside `0.7.0` under Amendment 3. The maintainer called the library lacking a
single-line text field an oversight; the four-question promotion gate was already passed on the shape before
this round — the string sibling of `input/number-field`, over the funnel primitive that the raw
`UiNative.TextField(Rect, string)` already was.

- **New kind `input/text-field`** (`TextFieldWidget`, internalize-candidate: a kind string in the manifest is
  the only supported use). Schema: `Bind` (required), `Live`, `Placeholder`, `PlaceholderKey`, `Label`,
  `LabelKey`, `Height`, plus the engine-wide names and the `Tone`/`Emphasis` roles. `Validate` refuses a
  missing `Id`/`Bind` and a key bound to anything but a string, at creation.
- **What it owns, and therefore what earns it a name**: per-element focus, the in-progress draft in the
  session's value bag, the commit rule, the placeholder's conditional paint, and the disabled refusal — none of
  which a manifest attribute can carry. A hand-rolled text box outside the tree loses the disabled refusal, the
  recovery slot and the fit audit with it, which is the asymmetry this closes.
- **New funnel member** `UiNative.TextField(Rect, string, UiSession, string, out bool)` — the identity-bearing
  sibling of `NumberField`. Same disabled rule (a disabled element never reaches the native control and never
  focuses), same draft-in-session rule, and one commit report. `UiNative` is public-unstable, which is why this
  is a member addition rather than a new design.
- **The commit rule, stated once**: `committed` is true when the buffer changed this frame, **or** when focus
  just left with a buffer that differs from the model. `Live` (default true) writes on the keystroke; with
  `Live=false` the write is deferred while the field holds focus and lands on the frame focus leaves. An edit
  that arrives while the field is unfocused commits immediately under either setting — there is no draft to
  defer.
- **The label set is `Label`/`LabelKey` and nothing else.** `Placeholder`/`PlaceholderKey` are painted but
  deliberately **not** in the label set: a hint shown inside an empty field is not the element's declared width,
  and B8's rule is that the label set equals what the kind paints *as its label*. Nothing unpainted is declared
  and nothing painted-as-label is missing.
- **Not changed:** the raw `TextField(Rect, string)` keeps working for a caller with no element to name; no
  existing member's signature moves; `Api` stays `0.7.0`.
- *Migration:* none — an addition. A page that already hand-rolls a field over the raw overload plus a session
  value state can move to the kind and gain the disabled refusal, the recovery slot and the fit audit; nothing
  forces it to.

**Provenance, and its boundary.** Two independent hand-rolls in the one wired consumer were forced into this
shape and are transcribed in `MEMORY.md`
(`Coahuilite/UniversalSqueaker@ad1a7447b298b104e6afafa5e7fa5567ec0f3556`); they are evidence that the shape is
generally needed, not consumption evidence for this implementation, and no consumer compiles against this kind
yet.

### Mode-row hover help (2026-09-20) — per-option identity published, never painted

An **addition** inside `0.7.0` under Amendment 3, and the maintainer's own generality argument: vanilla
RimWorld's mode selectors already show per-option help, so this is expected behaviour of the control rather
than one consumer's convenience.

**The requirement, in consumer terms.** (a) Each option carries its own help identity, declared and validated
at creation; (b) a consumer can learn **which option is currently hovered** without re-deriving this kind's
cell geometry. The consumer renders the help itself — the library paints no tooltip, and that is the contract,
not an omission.

- **The help identity is the existing `DescriptionN`.** No new per-option vocabulary: the names were already
  declared in the schema and read by the kind, and this is the surface that finally consumes them. A
  `TitleN`/`DescriptionN` whose index has no `ValueN` is now **refused at creation** — it could never be
  drawn or published, so it is the inert declaration this contract refuses rather than one it ignores.
- **New manifest attribute `HoverHelpKey`** (optional): the binding key that receives the hovered option's
  help identity. Validated at creation as a **writable string value binding** — a read-only or unbound key
  would refuse the write mid-frame and trip the recovery band, so it is refused where it can be reported
  instead of discovered.
- **What is published, exactly.** While an option is hovered, the row writes that option's declared
  `DescriptionN`, or its `ValueN` when the option declares no description — so the key always **names** the
  hovered option, with or without help text. When no option is hovered the row writes the empty string. The
  write happens **only when the answer changes**, so a consumer reading the key sees one write per hover
  transition rather than one per frame.
- **"Hovered" means what it means everywhere else.** The row asks the funnel, not the raw rect test, so a
  disabled row is not hovered and a row under another element's popup layer is not hovered either. The claim
  **consumes nothing**: the click still selects the option in the same frame.
- **New funnel member `UiNative.IsMouseOver(Rect, UiWidgetContext)`** — the context-carrying counterpart of
  the raw `IsMouseOver(Rect)`, applying the disabled and higher-layer rules that
  `Button(Rect, UiWidgetContext)` applies. Public because a kind that re-derived those rules from the raw
  form would report a hover for an option underneath an open popup. `UiNative` is public-unstable, so this is
  a member addition; **no new type**, so the API-tier type list is unchanged.
- *Migration:* none — an addition. One behavioural tightening to know about: a manifest declaring `TitleN` or
  `DescriptionN` for an index with no `ValueN` created silently before and is refused now (delete the orphan
  declaration, or give the option a value).

**Still open, and the maintainer's call:** whether the descriptions *stay* in the schema at all (B8's
vocabulary half). This section is the argument that they should: removing them would remove the help identity
the publication carries, so the "orphan name" they were reported as is now a name with a live consumer.

### Element-level help, and the option level generalized (2026-09-20) — G1 + G4

Additions inside `0.7.0` under Amendment 3, driven by the consumer's real usage rather than by a wish list:
the maintainer approved route (a), whose whole argument is that a declarative page currently cannot carry help
at all.

**G1 — the element-level help hook, engine-wide.** `HelpKey` is a new **engine-wide attribute on widget
elements**: any kind accepts it without listing it in its schema (the same gate as `Visible`/`VisibleKey`/
`Width`), which is what makes it usable by a page and by a consumer's own kinds. It is an **opaque literal
identity** — the key the CONSUMER's help catalog is keyed by — and the library never translates or interprets
it. Deliberately a third flavour of the `*Key` family: translating it would destroy the lookup, and resolving
it through a binding would make a static claim dynamic.

- **Publication is the session's existing claim machine** — no new channel, no new public member: during its
  draw walk the engine claims an element's `HelpKey` while the pointer is over it (`UiSession.ClaimHover`),
  and the consumer reads `HoverClaim` / `HoverClaimElement` exactly as it already does. The claim is made
  **inside** the element's node scope, which is what attributes it to the element that declared it.
- **Refused on containers and template roots**, which are not hit surfaces: a declaration there could never
  claim, so the unknown-attribute gate refuses it at creation (the A3 shape) instead of leaving it inert.
- **The hover rule changed with it, and that is a correction to the previous round.** `UiNative.IsMouseOver(Rect,
  UiWidgetContext)` no longer applies the DISABLED rule. Disabled-ness refuses input, and that refusal stays
  where input is refused (`Button`, the session-plus-key primitives); hover also drives inspection, and a
  control that is unavailable is exactly when a player needs the help explaining it. The consumer's own
  unavailable-state help entries are the evidence, and vanilla shows tooltips on disabled controls for the same
  reason. The higher-layer rule stays: an element under another element's open popup is not hovered.

**The option level, generalized off the kind it started on.** `input/dropdown`'s popup rows are options of one
element, so the element-level hook cannot reach them; the `HoverHelpKey` binding form now drives both kinds
through one implementation (`OptionHelp`), and the dropdown's published identity is the option's **value** —
a dropdown's options come from the consumer's data, so the value is the machine token a catalog is keyed by,
which is the same choice the mode row already falls back to. `UiPopup.DrawOptionList` returns the hovered row's
value so the caller publishes without re-deriving the popup's geometry; the return value is additive, so a
caller that ignored the old `void` still compiles.

**G4 — localized option labels on `input/mode-row`.** `TitleKey1..8` is a translation key resolved through
`IUiTranslation`, key-wins over the literal `TitleN`, the option's value when neither is declared. Both name
families are in the declared label set, so `Width="Auto"` measures the **translated** title. An orphan
`TitleKeyN` (no `ValueN`) is refused at creation like the other per-option declarations.
**One engine defect came out of this and is fixed here**: the label-set seam decided "is this a translation
key?" with a plain `EndsWith("Key")` test, which cannot see an INDEX suffix — `TitleKey1` ends in `1`.
`TitleKeyN` is the library's first indexed keyed label, so the seam had never had to handle one; it now strips
trailing digits before the test, and the lane pins the difference (measured: the raw key's width against the
translated string's).

**Recorded as contingent, deliberately NOT built.** A binding-resolved sibling (a `HelpBind` read from the
consumer's bindings) would serve a data-dependent identity — a key that depends on row state — and the consumer
has exactly two such sites. Both live in a widget that will not be migrated declaratively unless the
hierarchy × composition decision ("Route A") lands, so the need is **contingent on that decision** and is
recorded here as such; building it now would be speculative surface, which is the thing this phase exists to
avoid. The other data-dependent shape — per-option help inside one element — is covered by `HoverHelpKey`.

*Migration:* none for the additions. One tightening: a container that declares `HelpKey` is now refused at
creation.

### `Tab` on containers (2026-09-20) — a coherence fix, not new surface

**Maintainer ruling: approved. "Fix what needs fixing."** The context is an internal contradiction the consumer
hit as a hard creation refusal while migrating a real card, and that the demo hit independently by reading the
source: the engine reads `Tab` when deciding visibility for **roots and children of any type**, while the
container attribute contract omitted the name — so a container could not be declared to appear on one tab.

**What changed.** `Tab` joins the container vocabulary in both mirrored lists: `UiHost.ContainerAttributes`
and the engine's `TemplateContainerAttributes`. Two lines; no other production change.

**Why this is a coherence fix and not a new capability** — the ruling's reasoning, in the strongest form:
- the read is already generic: `UiLayoutEngine.IsHidden` (:2570-2602) reads `Tab` unconditionally alongside
  `NarrowHidden`, `Visible`/`VisibleKey` and `Hidden`, and every call site (:211 roots, :973 the shared
  child filter `VisibleChildren`, :1262, :1408, :1543) passes container-typed children through it;
- the invalidation plumbing is already container-inclusive: `RecordDeclaredKeys` (:436-455) registers
  `UiBindings.ActiveTabKey` for **any** spec that declares `Tab`, and it is called from the same child walks
  that include containers (:978, :1264, :1410, :1545), with nothing gating widget-ness;
- the asymmetry is visible inside one function: of the three sibling visibility attributes `IsHidden` reads,
  `NarrowHidden` and `Hidden` were in the container list and `Tab` was the only one missing.

So the two lines **do not add a capability — they stop the contract from forbidding one the engine already
implements**. The drift guard is already in place: `KernelRepeatTests` reflects both container lists and asserts
they are the same vocabulary, so the change cannot half-land.

**The one semantic consequence, stated rather than left to be discovered.** A `Tab`-gated container hides its
whole **subtree**, and the `HelpKey` claims of that subtree disappear with it — consistent with every other
hidden element, and with the engine's own handling of a hidden element (it keeps its node and its state, so
nothing renumbers and nothing loses a state slot; `UiLayoutEngine` :1885-1887, pinned by `KernelIdentityTests`'
Tab-switch lane).

**Boundary, explicitly out of scope:** per-row `Tab` inside a template stays **inexpressible by design** — a
`Tab` inside a template is deliberately not item-scoped, because a tab is a page-level answer and not an item's
(`UiLayoutEngine` :1863-1867). Anything per-row belongs with the hierarchy × composition decision, not here.

**Lane.** `KernelContainerTabTests`: a `Tab`-gated container creates, appears and disappears with
`active-tab`, and an announcement on `active-tab` re-arranges it — with the container-list mutation as the
red-first evidence, so the omission cannot silently return.

*Migration:* none. A container declaring `Tab` was refused before, so no page can be relying on either
behaviour.

### P1-E vocabulary batch (2026-09-20) — four small additions and two diagnostic corrections

Inside `0.7.0` under Amendment 3, from the disposition table (`60-capability-dispositions.md`). Each item below
carries its own lane, proven red against the reverted implementation in one mutated build and green after.

- **B2(2) `WideHidden`** — the exact mirror of `NarrowHidden`: a child declaring it is arranged only while the
  state is NOT narrow, so "this element belongs to the wide presentation" is expressible without the two
  mutually exclusive subtrees the earlier workaround needed. Allowed on widgets and containers, subject to the
  same parent rule as `NarrowHidden`: a declaration under no `Breakpoint` is refused at creation, because
  nothing could ever switch the state it depends on (`UiHost`, `IsHidden`).
- **B5 `WidthKey`** — the numeric sibling of `VisibleKey`: a float value binding answers the declared width
  when no numeric `Width` is written (the static declaration wins, the same precedence the visibility pair
  uses), the declared key is registered so an announcement on it re-arranges the declaring node, and a key that
  cannot answer (missing, or bound to another type) leaves the unsized answer in place and records one
  deduplicated appearance note.
- **`SelectedKey`** — a bool binding that resolves the element's role to the **active** treatment when it
  answers true, so a consumer that knows the element is selected does not have to re-author its `Tone` per
  state. State beats the author, the same way writability does. Deliberately **not** a `ToneKey`: a binding
  handing back `"Active"` would re-authorise a name this line retired, and the treatment is a state the library
  already owns. An unresolvable key is fail-soft and recorded once, like `VisibleKey`. Engine-wide on widget
  elements; it takes effect in the kinds that resolve a role (the atoms and `chrome/banner`).
- **G5 `chrome/banner` adopts the role pair** — its literal schema list becomes
  `AtomVocabulary.Schema(ToneAndEmphasis, …)`, and it paints the role's TEXT colour with a declared default
  emphasis of `Muted`. That default is what keeps an untone banner's ink exactly what it was (the secondary
  ink), so this is a vocabulary gain with no visual change for existing pages.

**Diagnostic wording (FL-21, FL-22) — no code change.** `UiOverflowReport.Available` is the **inset label rect**
the widget measured into, while `Needed` is the measured content the band could not hold; an overflow verdict is
therefore about the **label band**, not the element's height, and the in-game misreading that prompted this is
evidence that the field needed saying out loud (its XML doc now does). Separately, the fit audit **deduplicates
by finding key** (path, text, font, axis, needed, available), is capped at `MaxReports = 48`, publishes
`Saturated` when the cap is reached and is cleared by `Reset`: a count of log lines is a count of **distinct
findings**, never a census of elements. Both statements are in the source and in the consumer guide.

**Deferred to the next batch, with the reason: G2 (`input/button` command payload), G3 (a chrome-free hit area
sized to measured content) and FL-16 (typed dynamic `UiOption` pairs).** Their dispositions in
`60-capability-dispositions.md` are unchanged — each is ACCEPTed on the four gates — but this batch's evidence
bar is one failure-sensitive lane per item with its own mutation, and folding three more items (one of them a new
public type) into the same window would put surface into a shared freeze that no lane had been shown red
against. They stay listed for the next rebuild rather than half-landing here.

### G2 and G3 landed, FL-16 withdrawn (2026-09-20)

- **G2 `PayloadKey` on `input/button`** - the command is handed the payload its binding declares, so a repeated
  row can say which item it is. It is scoped per item like the other binding roles (`Bind`/`ActionBind`/
  `OptionsBind`/`VisibleKey`), and the creation contract follows the shape: with a payload the consumer binds
  `BindAction<string>`, without one `BindCommand` as before. A key bound to something other than a string is
  fail-soft, loud, and fires the payload-free command.
- **G3 `Chrome="none"` + `Height="Auto"`** - a bare hit area: no surface is painted while the hit test still
  fires, and the height comes from the measured content band instead of the theme row height (an empty caption
  has nothing to measure and keeps the row height). Any other `Chrome` value is refused at creation, the A2/A3
  shape, rather than ignored.

**FL-16 (typed `UiOption` pairs) was withdrawn mid-batch, and the reason is the evidence, not the design.** The
implementation, the public type and its tier entry were written and the suite went green - but with the pair
path reverted to the faithful pre-fix shape (a single string read) **the lane stayed GREEN**. A lane that does
not discriminate is not evidence, and the protocol is explicit that a new public type needs one. So the type,
the tier entry and the lane were removed, and the diagnosis is recorded instead: the string fallback accepted a
pair-bound key rather than reporting the element-type mismatch, which is what made the probe blind. The next
attempt starts from that failure rather than from a fresh guess. Nothing here promotes a type.

## The selected ordinary-authoring path (B)

The recommended author route is existing surface only: one layout manifest over public atoms/containers
(`input/slider`, `input/number-field`, `input/dropdown`, text/section/wrap/scroll containers), typed
bindings through `UiBindings` (`BindValue`, `BindReadOnly`, `BindOptions`), explicit `NotifyChanged` from an
owned mutation path on an authoritative plain C# model, and `UiWindowHost`/`UiPageWindow` for the window.
`UiNotifyAdapter` + `INotifyPropertyChanged` stays optional. No VM base class, no builder DSL, no second
state store becomes part of the contract. The runnable reference lives in the harness fixture area and the
prose guide in `docs/consumers/ordinary-settings.md`.

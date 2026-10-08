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
normal pre-1.0 rule (an addition moves the minor) returns when the phase ends.

**The phase's end is defined rather than open-ended, and the line stays `0.7.x` (maintainer ruling 2026-09-24).**
The exemption lapses at the **first** of: the `0.7.x` line's first release/tag, or the end of the cross-repository
lockstep development. Until then the line is in **development**: no minor is raised and no next line is opened.
The exemption is **temporary and not inheritable** — it belongs to this line and this stage only, and it must not
be carried into a later line or into any published state by analogy. The API-tier *type* list is no
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

> **SUPERSEDED on the 0.7.x line by §R2 (below).** The library ships **one** look (`Vanilla`), the warm-gold
> factory is removed with no alias, and `new UiTheme()` is a **partial bag whose unset tokens answer
> `Vanilla`** — explicitly transparent included, which must stay transparent. The text below is the 2026-09-18
> decision and its reasoning at the time; it is kept because the accent-identity ruling inside it still
> stands, and because a removed promise is more useful with its history attached than without it.

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

**Accent identity — decided, and no longer an open item (maintainer ruling 2026-09-24).** The library's accent
identity is the **series gold already carried by `UiTheme.DarkGold` / `AccentGold`**, and it is unchanged: no token
moves and neither factory is re-tinted. An alternative accent hex (`#EBAD4D`) came up during the appearance work
as part of an **external product's** look; that product is an appearance reference, not a consumer of this library,
and its palette is **not** adopted here — a product's colours are not a library default (`AGENTS.md`,
"Neutrality"). Nothing in this line's palettes is pending a decision after this note.

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
  elements; it takes effect in the kinds that resolve a role (the atoms and `chrome/banner`). **Inside a
  `<Repeat>` template it is item-scoped** (B1, below). Two verifiable consequences of the `Active` cell:
  the treatment takes `TextOnGold` and **ignores `Emphasis`** (`Active/Normal` and `Active/Muted` are the
  same value, pinned by `KernelResolvedStyleTests`), so declare `SelectedKey` on the elements that should
  turn gold — a row title — and **not** on a row container or on a detail line meant to stay secondary.
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
  row can say which item it is. It is scoped per item like the other binding roles (the item-scope table
  `QualifyItemBinding` holds: `Bind`, `ActionBind`, `OptionsBind`, `VisibleKey`, `PayloadKey`, and
  `SelectedKey` since B1 below), and the creation contract follows the shape: with a payload the consumer binds
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

### B1 (2026-09-22) — `SelectedKey` joins the item-scope table

**Maintainer ruling: approved.** Technical debt must not be left to spread while it is visible and
repairable. This is a **consistency fix, not new surface**: `UiLayoutEngine.QualifyItemBinding` moved a
declared key into the item's scope for every binding role except `SelectedKey`, so a repeated row set could
not state which of its rows was the selected one.

**The criterion is the table's own, written beside it.** `Tab` is deliberately not scoped because a tab is a
page-level answer and not an item's; the question the table asks of every attribute is *"is this an answer
about one row?"*. `SelectedKey` answers *which row is selected*, so its absence was an inconsistency rather
than a boundary.

**Consumer evidence (reported in the S4-2 handoff).** The consumer's row-set rework (`7f54ea7`: `Repeat` +
`<Templates>` over the race/xenotype cards) could not show a selected row. The minimal reproduction, which is
what the lane pins: a template element declaring `SelectedKey="selected"` resolved the **page-level** key on
every row instead of `<Items>.<itemKey>.selected`, so all rows answered together and the card's selected
state was invisible.

**What changed.** One clause in `QualifyItemBinding` plus the comment above it. The scoped set is now
`Bind`, `ActionBind`, `OptionsBind`, `VisibleKey`, `PayloadKey` and `SelectedKey`; the comment states the
criterion rather than a count of names, so the next reader checks a rule instead of a number.

**Lane and mutation.** `KernelRepeatTests`, "SelectedKey answers per row, not for the page the row set sits
on": three rows whose template declares `SelectedKey="selected"`, against a page-level binding of that same
name that is a **decoy answering true**. The lane asserts that exactly one row carries the active treatment,
that it is the row whose own item key answers true, and that moving which item key answers true moves the one
active row with it. It was written and observed **RED first** — 3 active rows of 3, the consumer's defect
exactly — then green after the clause landed, and red again with the entry removed from the table (**faithful
revert**: exit 1 with the three assertions named in the output). One assertion is deliberately weaker and is
labelled as such in the lane: `StyleFallbackCount == 0` is a future-regression guard, not the
mutation-proving half, because the decoy is bound and so it holds in both states.

**The `Active` cell ignores `Emphasis` for text — the semantics worth copying.** A `SelectedKey` that
answers true resolves the element to `UiStatusTone.Active`, and that cell hands text `TextOnGold` under either
emphasis (`Active/Normal` and `Active/Muted` resolve to one value, pinned by `KernelResolvedStyleTests`;
emphasis is the muted/secondary axis and a saturated tone does not use it). Observed consequence on a
two-line row: declaring `SelectedKey` on the row **title** turns that title gold while a detail line meant to
stay secondary must keep its `Emphasis="Muted"` and therefore must **not** carry the declaration — a row
container carrying it takes its whole subtree with it.

*Migration:* none for a page that compiled before. The one shape whose meaning changes is a template that
declared `SelectedKey` and expected it to read a **page-level** binding of the same name; it now resolves in
the item's scope, which is the only shape that can answer "which row". Nothing in this repository, in the
harness, or in the consumer's reported page depended on the old resolution — the entry's absence is what the
consumer hit.

### FL-23: a binding element-type mismatch is reported, not only thrown (2026-09-20)

Reading a typed binding as the wrong element type has always been refused - `GetOptions<T>` and `Invoke<T>`
throw and name both types. What was missing is that the refusal was **invisible on the library's audit
surface**: a consumer that catches the exception keeps rendering a degraded control and no report ever says
why, which is the same 'fail-soft must not mean silent' hole as FL-21/FL-22. Both members now record one
deduplicated diagnostic on the existing fail-soft channel (path = the binding key, kind = `binding`, attribute
= `OptionsBind`/`ActionBind`, authored = the registered type, resolved = the requested type) **before** throwing
exactly as before. The refusal is unchanged; being reported is the addition.

**One pitfall this creates for the withdrawn FL-16 work:** a legitimate dual-shape probe (pair list, else string
list) must read through a **non-reporting** path, or a perfectly valid page would record a mismatch for the
shape it is not. That is a design point for FL-16's redo, not a defect of this change.

### FL-16 redone and landed: typed display/value options (2026-09-20)

A dynamic options binding may now carry **pairs**: `BindOptions<UiOption>` gives each option a display text and
the value the page commits, which is the separation the static `OptionN`/`ValueN` pairs always had. `UiOption`
is a `public-unstable` addition and the only new public type in this batch; `BindOptions<string>` is unchanged,
with display == value, so this is an addition and not a reshape.

**The order this was done in is part of the contract, because the first attempt failed it.** The lane was written
and run RED before the type existed (with a stand-in pair shape: `Options binding 'opts' at 'dd' is
'ProbeOption', not 'String'`), then the implementation and the tier entry landed, and then the **revert proof**
ran on the final lane: with the pair path removed the lane reddens - `a valid pair-bound page recorded 1
diagnostic(s)` - and with it restored the suite is green. That red is exactly the FL-23 interaction: a page that
declared the pair shape is read as strings, the read is a type mismatch, and the mismatch is now visible.

**The probe is non-reporting, and that is load-bearing.** The element type a page declares is discovered by
`ValidateOptions<T>` (which throws on a mismatch and records nothing) and the list is then read exactly once
through `GetOptions<T>` for the matching type. A page that declared either accepted shape therefore records no
diagnostic at all, which is what the lane asserts (`StyleFallbackCount == 0` after a reset, per the counter rule).
A third element type is refused at creation with the historical diagnosis preserved and the pair shape appended
as the second accepted answer.

### `Height="MatchContent"` — the height axis' content-relative mode (2026-09-22)

**Maintainer ruling: approved without asking again.** "Evaluate whether a change makes the library more general and
easier to use, or is a custom feature one consumer asked for. If the change proves general, so that everyone gets
better together, do it without asking me." The check that settled it: the WIDTH axis is complete
(`Width`/`WidthKey`/`MinWidth`/`MaxWidth` on the widget table, `MinWidth`/`MaxWidth` on the container table,
and `WidthKey` with its own registration, resolution and unsized report), while the height family had **zero hits**
in the kernel - one whole axis missing on a family the other axis already had. A gap on a general axis, not a
consumer's request; the promotion gate passes four ways (neutral, no new kind, no process-wide mutable static,
lane-drivable) with a forced consumer behind it.

**What changed.** `Height` accepted a number or `Auto`; it also accepts `MatchContent`, which resolves to the
height the parent's content resolved to, with the declaring element contributing **nothing** to that computation.

**The principle, not a list.** The reference can be answered only by a parent whose content height is a **maximum**
over its children, because then it exists before the declaring child is measured and does not include it:

- **accepted** — a child of a `Row` or an `Overlay` (both take the maximum over their children) that has at least
  one sibling which does not declare the mode;
- **refused at creation** — a vertical container (`Column`/`Stack`/`Section`/`Surface`/`Scroll`/`Clip`),
  whose content height is the SUM of its children, which includes the declaring element and would make the reference
  self-referential; a `Wrap`, whose line membership is discovered from the children in that line; a root, which has
  no parent at all; and a parent whose every child declares the mode, which leaves no content to match. Each refusal
  names the reason, not only the rule.

**Degradation, stated rather than discovered.** A `Row` declaring `Narrow="Column"` becomes a vertical flow at its
breakpoint, where the mode loses its reference and degrades to the element's own measured content - exactly `Auto` -
instead of throwing or collapsing to a zero-height band. The same holds for a programmatically built spec and inside
a template subtree (which the Host's creation-time walk does not cover: the known 0.5 gap).

**A declaring container's own children** keep the layout their own content produced, top-aligned in the taller box,
which is what every container does with space its content does not fill.

**Consumer evidence (transcribed in `MEMORY.md`).**
`coahuilite/UniversalSqueaker@d767d0f:Source/UniversalSqueaker/UI/Layout.Schema2.xml:350-359` — an `Overlay`
"because the hit area must COVER the text rather than sit beside it", whose hit `Column` holds two
`input/button Chrome="none" Height="Auto"` bands and whose text `Column` holds the label. The numbers come from
`…@d767d0f:tools/UniversalSqueakerKernelHostTests/DeclarativePacksLaneTests.cs:398-401`, which printed
`[packs-hit] flat row=… hit=… covered=hit/row*100` with `uncovered = row - hit`: **69.9% covered flat, 53.3%
covered once the text wrapped, 42px of the row uncovered.** The cheap workaround is already refuted and worth
writing down: `Height="Auto"` on a band measures the WRAPPED band while the caption draws single-line
(`ButtonWidget`'s measure against `UiThemeDraw.Label(..., singleLine: true)` → `Text.WordWrap = false`), and the
fit audit returns after the width axis whenever `singleLine` is set (`UiFitAudit` :280-289) — a shape that reserves
several lines, paints one and reports nothing.

**Lane and mutation.** `KernelContentHeightTests`: the citation's page. The hit column must equal the text column,
measured **104 vs 104**, while the same page *without* the declaration keeps **50 against 104** — the positive
control that the equality measures the mode and not the fixture. The lane also pins the refusal matrix (every
refusal naming the mode) and the narrow degradation (at the breakpoint the band falls back to the theme row height,
28, against the text's 76). Written and observed **RED first** (the value was refused outright:
`invalid Height 'MatchContent'`), green after the clause landed, and red again under the faithful revert of the
engine half — the reference removed while the value stayed accepted:
`the hit column takes the text column's measured height (50 vs 104)`, exit 1.

**API tiers.** No new exported type, so the tier list is unchanged and the classification gate stays green — checked
rather than assumed.

*Migration:* none. `MatchContent` is a value no earlier manifest could write, and every existing number/`Auto`
declaration reads exactly as before.

### An unknown scope name is attributed to the element that declared it (2026-09-22)

**A consistency fix with a live citation, not a new channel.** An element-level appearance drop already reached the
audit surface — the resolver records the drop and the Host publishes it in the frame that resolved it — but a
resolution-time drop was published under the **document's** path (`<source>#styles`), so the finding said "some
scheme is missing" and left its author to grep a palette they never declared in. The consumer's case:
`Scheme="us-flat-panel"` on one column, defined in a palette applied to a theme instance instead of in the document
the resolver reads. The scope looked applied and was not.

**What changed.** `UiStyleIssue` gains `ElementPath` (a member addition to a `public-unstable` type, so it stays
inside that tier's promise); the resolver's public `ThemeFor(chain)` keeps its signature and delegates to an internal
overload carrying the declaring element's path; the engine hands that path in from the arranged node; and the Host
publishes a drop under `issue.ElementPath` when there is one, falling back to the style origin for a document-level
drop. The message, the fallback behaviour and the number of findings do not change.

**The trap this closes, in the words a consumer needs.** A scheme (or density) NAME is resolved from the style
document the Host was built with — a standalone `<Styles>` file, or the manifest's own `<Styles>` section; the
Host's document parameter selects between them. A palette applied to a theme *instance* is **not** a definition site:
defining a scheme only there means the resolver never sees the name, the scope silently keeps the page-level values,
and the control merely looks styled. One sentence, one wasted step saved.

**Lane and mutation.** `KernelStyleScopeTests`, "An unknown scope name is attributed to the element that declared
it": a section declaring `Scheme="us-flat-panel"` against a document that defines no such scheme. Written and
observed **RED first** — one finding, but `attributed to the element that declared it, not to the document:
'style-scope#styles'` — then green (`root/region`, the scheme name in the diagnostic, and the page still arranging).
The faithful revert of the attribution half (the Host ignoring `ElementPath`) reddens exactly that assertion and
nothing else.

**Stated limitation.** The resolver caches one theme per effective (scheme, density) pair, so the record lands on the
first lookup that builds the pair; for a container's own declaration that is the declaring element, which is the case
the consumer hit. Two different elements writing the same unknown name produce one finding, which is consistent with
the audit surface's own rule that a count is a count of **distinct findings**, never a census of elements.

*Migration:* none behaviourally. A consumer reading `UiStyleIssue` gets a new property and the same message.

### `section/header`'s divider becomes declarable (2026-09-22)

**A general defect, approved under "if the change proves general, do it without asking".** The kind painted a 1px
surface along its own bottom edge unconditionally: `Chrome` was not in its schema, `Tone` is not engine-wide, and
the only lever a manifest had was a scope-level scheme — which also repaints every other `Divider` in that scope.
An atom painting something the manifest cannot control is the rule this library exists to keep, and any
borderless/whitespace-separated design (the S6 direction: no frames, no dividers, separated by whitespace) was
blocked by it — not one consumer's page only.

**What changed.** Three lines in `SectionHeaderWidget`: `Chrome` joins its schema and implements exactly one
value, `none`, which suppresses the divider while the title is still drawn; any other value is refused at creation
naming `none` (the A2/A3 fail-closed shape `input/button` already uses for its bare hit area). No new attribute
name was introduced, the divider is not made conditional on anything else, and its colour stays the theme's
`Divider` token — so a scope's scheme can still restyle the line, transparent included, instead of switching it
off.

**Lane and mutation.** `KernelSectionHeaderTests` — the kind had **no lane coverage at all** before this, which is
its own finding: the line had never been pinned. The lane holds that the default header paints five surface calls,
all inside its own rect and all in `theme.Divider`; that `Chrome="none"` paints **nothing** (0 calls) while its
title is still on the wire; that `Chrome="panel"` and `Chrome="true"` are refused at creation naming `none`;
and that a scope's scheme moves the divider colour off the baseline token, so the fix cannot remove the theming
half. Written and observed **RED first** (the attribute did not exist at all: `Unknown attribute 'Chrome' on
section/header`), green after the three lines landed, and red again under the faithful revert of the widget half —
the value accepted, the paint no longer suppressed: `Chrome=none paints nothing in the element's rect (5 surface
call(s))`.

*Migration:* none. An absent `Chrome` paints exactly what the header always painted.

### The development-only geometry instrument (2026-09-22) — a library capability, compiled out of a release

**Why it is here rather than in a consumer.** The five things a consumer cannot hand-roll safely include
**one audit surface**, and a numeric answer to "where is this element, and who did that press go to" is that
surface's own body rather than a feature grown for one page. The maintainer's ruling is that the tool which
makes precise layout possible belongs to the library.

**What it reports, and nothing else.**
- One line per arranged node, in paint order: display path, kind, container-ness, the **arranged** rect, the
  rect the widget's Draw was handed, that rect in **window space**, the **origin** between those two spaces,
  the declared **height mode** (`Fixed`/`Auto`/`MatchContent`) and **the height that mode resolved to**.
- For a `Scroll`/`Clip` container: the same numbers plus its **viewport** (the arranged rect, named as such)
  and its **content extent** — the real scrollable size, not the visible band.
- One line per sampled press: the element's path and kind, the **pointer** and the **queried rect** in window
  space, and the verdict — `disabled`, `covered`, `hit` or `miss`.

The two spaces side by side are the point of the record: a recorder reading content-group space and a snapshot
reading window space produce two positions for one control, and neither number says which space it is in.

**The surface, and why it adds no type.** `UiDiagnosticSubscription.GeometryEnabled` (off by default),
`.GeometryOverlay` and `.DumpGeometry()` (the diffable text). It hangs off the existing subscription, so its
lifetime, isolation and release path are the diagnostic surface's own and the instrument adds no
process-wide state of any kind — no new static, no new kind, no second channel.

**SA1.5: the outline is an independent switch.** It used to be refused until `GeometryEnabled` was on, which
made "look at my layout" pay for "record my layout" - the user ruling that the outline may be turned on
anytime to review the layout ends that coupling. The outline field now lives on the subscription, not inside
the capture: with no capture it outlines every entry that reaches the engine's per-entry DRAW step and stores
nothing (no buffer, the dump stays empty, `TryGetGeometrySnapshot` keeps answering false - the report channel
stays honest because it never gained a sample to report), and with a capture live it paints exactly the
RETAINED entries at that step as before. DT1 (the 2026-10-07 ruling that an uncovered outline is a library
defect, superseding the limit this section used to state) closed the scoped case: a container's OWN viewport
band is now outlined at its own entry - the engine's call sits after the nested walk returns, with the same
draw-local rect and the same `retained` answer its Note gave, so no second paint outlet at the clip push was
needed. Shell chrome joins through the same switch and ink, AND through the same buffer: the shell records
its window-space chrome into the existing capture the moment it paints (dump `chrome` line, snapshot
`ShellChrome` field), so a report's chrome numbers belong to the pass they describe instead of a
present-tense read spliced onto a finished dump; `UiWindowHost.ShellChrome` remains for acting on the
window as it is NOW, the two tenses documented apart. Both modes render one walk at draw-step call sites: there
is no second layout system, and `KernelDevGeometryTests` now pins the outlined set as the FULL retained list
(fixture asserts a scoped container exists, so the equality cannot be met by luck). Turning
`GeometryEnabled` off afterwards does not silence the picture; a release payload still refuses both switches
outright.

**The gate is the build configuration, and it fails closed.** The whole implementation is inside
`#if FER_DEV`, plus three guarded call sites (the engine's per-entry draw walk, the engine's overlay call, and
the funnel's click decision), so a release payload carries no capture, no buffer and no call: on a release
payload `GeometryEnabled = true` **throws** instead of accepting the opt-in and then answering every question
with emptiness. The harness project mirrors the constant in its own Dev configuration, which is what makes a
lane able to hold the dev half at all.

**It must not change the thing it measures.** No sample is read back by the engine, the session, the hit stack
or the fit audit, and the optional overlay is painted after the element it outlines has drawn and after the fit
audit closed for that entry, in that element's own draw-local space.

**Lane and mutation.** `KernelDevGeometryTests` (dev half: six arranged lines from a page with a
`Chrome="none" Height="MatchContent"` band, a scoped container and its child; press verdicts for
`hit`/`miss`/`disabled`). The mutation that has to redden it is a change to the **geometry producer**, not to
the instrument: with `ResolveHeight`'s `MatchContent` branch returning its reference `+ 2f`, the lane reports
`the band's recorded height is the sibling's measured height: 32 vs 30` and the run exits 1; reverting restores
ALL PASS. That is what makes the number a measurement rather than a restatement of the declaration. The
per-line `window == draw + origin` assertion is labelled a guard in the lane: it holds by construction today.

**Both halves are gate-guarded, and the arrangement is written down** (maintainer ruling 2026-09-23: a
dev-only mutation proof that only a human re-runs is a proof that rots). Gate 1 runs the harness in Release,
so it holds this lane's release half. The **new gate 10** (`scripts/verify-dev-instrument.ps1`) runs the
harness in **Dev** and refuses to accept "the project compiled": it requires the seven dev-only assertion
names, floors on the assertion (12) and dumped-node (5) counts, the absence of the release-only half's name,
and the numbers themselves, and its checker is control-tested on every run against four planted fixtures -
among them a fully populated sample that must **not** be rejected and an empty one that must be. Its own
mutation proofs, each run through the gate alone so no earlier gate could be what reddened it: the geometry
producer `+2f` exits 1 naming `the band's recorded height is the sibling's measured height: 32 vs 30`, and
removing the tests project's `FER_DEV` constant exits 1 naming release-branch assertions instead of dev ones.
The same change fixed a measured defect class: with `FER_DEV` undefined in the harness project, the
`#if FER_DEV` branch in `KernelDocumentReloadTests` had never been compiled, and a `-c Dev` run was red.
Which gate guards which half is also recorded in `docs/development/0.7/40-verification.md` and `AGENTS.md`.

**Amendment (2026-09-29, R3-A, no version bump): the two numbers in the paragraph above are the 2026-09-23
record and no longer describe the gate.** The named list is no longer seven entries - it was fourteen by the
2026-09-24 batch, and R3-A added sixteen more, so it stands at thirty - and the assertion floor is no longer
the constant `12`: it is derived from the list length, which is what stops a list that grows from passing
vacuously. The R3-A names exist so that deleting or compiling out one of the four new lanes reddens gate 10
instead of leaving it green, and the t2 batch case that pins this counter's red message (`showed N of the M
named`) was re-cut to the new numbers in the same slice, because a stale expectation there is a mutation that
cannot report its intended red.

*Migration:* none in either direction. A release payload cannot turn it on; a development build that never asks
for it pays nothing.

## The selected ordinary-authoring path (B)

The recommended author route is existing surface only: one layout manifest over public atoms/containers
(`input/slider`, `input/number-field`, `input/dropdown`, text/section/wrap/scroll containers), typed
bindings through `UiBindings` (`BindValue`, `BindReadOnly`, `BindOptions`), explicit `NotifyChanged` from an
owned mutation path on an authoritative plain C# model, and `UiWindowHost`/`UiPageWindow` for the window.
`UiNotifyAdapter` + `INotifyPropertyChanged` stays optional. No VM base class, no builder DSL, no second
state store becomes part of the contract. The runnable reference lives in the harness fixture area and the
prose guide in `docs/consumers/ordinary-settings.md`.

## Dependable-surface entries (stage 3) — the template, and three worked examples

`TODO.md` §"Stabilize the demonstrated contract surface" and `next-stage-guide-zh.md` §3 ask for the same
thing: before a demonstrated capability is depended on, write down its legal inputs, ownership, layout and input
behavior, errors and fallback, compatibility promise, and a consumer example. This section is the **form** for
those entries plus the first three worked examples, so the next session does not invent a fourth shape. It is
deliberately a section of an existing file rather than a new document.

**Writing an entry is not a promotion.** An entry records what is already true and cites the evidence for it;
the tier decision stays per item and belongs to the session/maintainer (`next-stage-guide-zh.md` §3: a single
page passing does not promote every public member to stable). A filled entry with an empty evidence field is not
an entry — write **UNRUN**.

### The template

| Field | What it must contain | Failure mode it prevents |
|---|---|---|
| **1. Status and ask** | the tier the capability has today, what is being asked (stabilize / keep unstable / internalize), and who rules | a stabilization that happens by accident because nobody wrote the ask |
| **2. Legal inputs** | the exact vocabulary a consumer may write, and every refusal — each one named, located and at creation time | an author discovering the boundary by compiling |
| **3. Ownership** | who owns the state, its lifetime, who disposes it, and that no process-wide mutable static is introduced | two hosts sharing state through a static |
| **4. Layout and input behavior** | the **measured** numbers and the rule that produces them, with stub-harness facts separated from in-game facts | an adjective ("flexible") standing in for a measurement |
| **5. Errors and fallback** | fail-soft vs fail-closed per input class, and the channel a fallback is reported on | "fail-soft must not mean silent" |
| **6. Compatibility promise** | the tier clause verbatim, what would count as breaking, and the migration for a page that compiled before | a break discovered by a stranger |
| **7. Consumer example** | `owner/repo@sha:path:line` (or `repo@sha:path:line` for the remote-less demo) plus what it proved | a capability nobody uses being called "proven" |
| **8. Evidence and gaps** | which half is mutation-proven and under which assertion name, which is only a future-regression guard, and what is **UNRUN** | red and green both being trusted, and a guard read as proof |

### E1 — a bare hit band: `Chrome="none"` plus a content-relative height

**1. Status and ask.** Manifest vocabulary on `input/button` (G3) and on the engine-wide `Height` axis
(`MatchContent`); no exported type is involved. Ask: keep as vocabulary and treat as dependable. The type tiers
are untouched because no type is involved.

**2. Legal inputs.** `Chrome="none"` paints no surface while the hit test still fires; **any other `Chrome`
value is refused at creation** (the A2/A3 shape — refused, not ignored). `Height` accepts a number or `Auto`,
and since 2026-09-22 also `MatchContent`, which resolves to the height the parent's content resolved to, with the
declaring element contributing **nothing** to that computation. Accepted only where the parent's content height is
a **maximum** over its children and at least one sibling does not declare the mode (a child of `Row` or
`Overlay`). **Refused at creation, each naming its reason**: a vertical container
(`Column`/`Stack`/`Section`/`Surface`/`Scroll`/`Clip`), a `Wrap`, a root, and a parent whose every
child declares the mode.

**3. Ownership.** No state of its own: the press goes through the funnel's `Button(rect, ctx)` on the element
the engine published, and the drag/hot-control state belongs to the session. `Chrome="none"` means the element
contributes no surface. No process-wide mutable static is added.

**4. Layout and input behavior (measured).** `KernelContentHeightTests`: the hit column equals the text column
at **104 vs 104**, while the same page *without* the declaration is **50 against 104** (the positive control that
the equality measures the mode and not the fixture), and at a `Narrow="Column"` breakpoint the mode degrades to
the element's own measured content — **28 against 76** — rather than throwing or collapsing. The disabled refusal
and the popup-layer arbitration are `UiNative.Button(rect, ctx)`'s documented funnel rules and are **inherited,
not re-measured for this attribute**; what the G3 lane pins for `Chrome="none"` is exactly
no-surface-painted / hit-test-intact / height-from-content.

**5. Errors and fallback.** Fail-closed for the vocabulary: an illegal `Chrome` value or an unanswerable
`MatchContent` parent is a creation-time `UiContractException` naming the reason. The one degradation is the
narrow-direction swap, which falls back to `Auto` by design and is pinned by the lane; the same degradation
covers a programmatically built spec and a template subtree, which the Host's creation-time walk does not cover
(the known 0.5 gap) — relevant because the consumer example below *is* inside a template.

**6. Compatibility promise.** No type moves, so `docs/api-tiers.md` is unchanged (checked, not assumed). The
promise is the manifest rule: `Chrome` accepts the default or `none`, and a third value is refused. Migration:
none — a page that compiled before declares neither name.

**7. Consumer example.**
`coahuilite/UniversalSqueaker@a13f8af:Source/UniversalSqueaker/UI/Layout.Schema2.xml:381-387` — a `Repeat`
template whose row is an `Overlay` holding one `input/button Chrome="none" Height="MatchContent"` declared
**first** (it paints nothing, so the text column draws over it) and a text `Column` beside it. The consumer's own
lane, `…@a13f8af:tools/UniversalSqueakerKernelHostTests/DeclarativePacksLaneTests.cs:441-477`, asserts coverage
`>= 99.5%` of the row and prints the retired two-band workaround as its control (69.9% flat / 53.3% wrapped /
42px dead). **What it proved:** the page model could not express "my hit area equals the sibling's
content-measured height", and the cheap workaround (`Height="Auto"` on a band) was refuted by measurement — it
measures the wrapped band while the caption draws single-line.

**8. Evidence and gaps.** Mutation-proven in this file's own entries: the `MatchContent` engine half under the
faithful revert — `the hit column takes the text column's measured height (50 vs 104)`, exit 1 — and, for the G3
half, `Chrome none still painted five surface(s)`. **UNRUN here:** no in-game run. Every number above is
stub-harness evidence; the real-font behavior of the same declarations is a game-acceptance item, not a claim of
this entry.

### E2 — `PayloadKey` on `input/button`: a repeated row names itself

**1. Status and ask.** Manifest vocabulary (G2); no exported type. Ask: keep dependable. `input/button` stays
internalize-candidate *as a type*, which is precisely what "kind string only" means.

**2. Legal inputs.** `PayloadKey` on `input/button`, naming a value binding. **With a payload the consumer
binds `BindAction<string>`; without one, `BindCommand` as before.** Inside a `Repeat`/template it is
qualified per item (`<Items>.<itemKey>.<declaredKey>`) like `Bind`, `ActionBind`, `OptionsBind`,
`VisibleKey` and `SelectedKey`.

**3. Ownership.** The payload is the consumer's value: the atom reads it from the binding and hands it to
`Invoke`; nothing is stored on the element, and no static is added.

**4. Layout and input behavior.** The declaration changes no geometry. The press path is the funnel's button —
disabled refusal and popup arbitration included — and the payload is read in the same frame the command runs.

**5. Errors and fallback.** A key bound to something other than a string is **fail-soft, loud, and fires the
payload-free command**: the page keeps working and the mismatch is reported rather than swallowed.

**6. Compatibility promise.** No type moves. Migration: none — the attribute is optional, and a button that
declares no `PayloadKey` keeps `BindCommand`.

**7. Consumer example.** Two shapes on one real page at `coahuilite/UniversalSqueaker@a13f8af`:
`…:Source/UniversalSqueaker/UI/Layout.Schema2.xml:382` (item-scoped — `ActionBind="select-domain"
PayloadKey="payload"` inside a template row, registered per item at
`…:Source/UniversalSqueaker/UI/UsKernelSettingsHost.cs:695` as
`BindAction<string>(prefix + "select-domain", …)`) and `…:Layout.Schema2.xml:245-247` (page-level — three
static preset buttons, each with its own read-only string binding, registered at `…:UsKernelSettingsHost.cs:315`).
**What it proved:** a repeated row could not report its own item key before this landed, so the consumer toggled
its hierarchy rows by hand.

**8. Evidence and gaps.** Mutation-proven by assertion name in the batch that landed it: *the command received an
empty payload* (`60-capability-dispositions.md`, "Batch 2 outcome"). **UNRUN here:** no in-game run; the
in-game press path is the open Packs row-click defect's subject, not this entry's evidence.

### E3 — `SelectedKey`: the model says "this one is on me"

**1. Status and ask.** A bool binding the engine resolves (B1); no exported type. Ask: keep dependable.

**2. Legal inputs.** `SelectedKey` names a bool value binding. It is **item-scoped** like the other binding
roles — that was the B1 consistency fix and it is the shape a `Repeat` row needs. Declaring it on a container
carries the declaration into its whole subtree.

**3. Ownership.** The binding is the consumer's; the engine only resolves the element's role from it and stores
nothing new. No static.

**4. Layout and input behavior (measured).** A `SelectedKey` answering true resolves the element to
`UiStatusTone.Active`, and that cell hands text `TextOnGold` **under either emphasis** (`Active/Normal` and
`Active/Muted` resolve to one value, pinned by `KernelResolvedStyleTests`). So on a two-line row the declaration
belongs on the **title**; a detail line that must stay secondary keeps `Emphasis="Muted"` and must **not** carry
it. The binding moves paint, not geometry.

**5. Errors and fallback.** The disposition's ACCEPT clause states that an unresolvable key is fail-soft and
recorded once (the `VisibleKey` shape); the landed entry does not restate that half, so treat it as **stated
there, not re-measured here**. One assertion in the landed lane is explicitly labelled a future-regression guard
rather than the mutation-proving half (`StyleFallbackCount == 0`), because the decoy key is bound and so it holds
in both states.

**6. Compatibility promise.** No type moves. Migration: none for a page that compiled before. The one shape whose
meaning changes is a template that declared `SelectedKey` and expected a page-level binding of the same name — it
now resolves in the item's scope, which is the only shape that can answer "which row".

**7. Consumer example.** `coahuilite/UniversalSqueaker@a13f8af:Source/UniversalSqueaker/UI/Layout.Schema2.xml:384`
and `:391` (item-scoped, on each row's title) and `:245-247` (page-level, one bool per preset button), with the
consumer's own reasoning for keeping it off the detail line at `…:Layout.Schema2.xml:266-271`. **What it proved:**
the S4-2 row set could not show which row was selected — every row answered together because the page-level key
was read.

**8. Evidence and gaps.** Lane `KernelRepeatTests`, "SelectedKey answers per row, not for the page the row set
sits on": three rows whose template declares `SelectedKey="selected"`, against a page-level decoy answering true;
written and observed **RED first** (3 active rows of 3 — the consumer's defect exactly), green after, and red
again under the faithful revert of the table entry (exit 1, the three assertions named). **UNRUN here:** no
in-game run; the `TextOnGold` consequence is pinned by a lane, not observed in a game.

## R1 — a resolved appearance seam, and the boolean kind that reads it

An appearance is a **name a kind declares**, not a private attribute read. `input/checkbox` had one
(`Appearance="switch" | "checkbox"`), resolved inside the widget by
`CheckboxWidget.ResolveAppearance(UiElementSpec)`, and that private reading had three costs: the registry
could not report which looks a kind has, so a creation-time contract could only be written inside the widget;
the natural-width contribution — which the registry invokes **without a widget instance**, to measure a
child's `Width="Auto"` — had to re-parse the attribute; and nothing but that one file could see the seam.

**The landing.** `UiAppearanceResolver` (kernel, **internal**) holds a kind's complete look set and one
normalization rule: `Resolve(spec)` answers the canonical look name plus whether the element *declared* one
this kind supports. `UiWidgetRegistry.Register` gains a **seventh**, **internal** parameter carrying that
resolver, kept as a separate overload so the five- and six-parameter overloads keep their exact CLR
signatures (the same rule that put the natural-body overload where it is).
`UiWidgetRegistry.GetAppearanceResolver` publishes it, so the set the widget resolves through and the set a
contract validates against are one object. `UiElementSpec` is untouched; `IUiWidget`'s three signatures are
untouched.

**Why the seventh parameter is internal, stated as a boundary.** The seam is this batch's shape and no
consumer has asked for an appearance axis on a kind of its own, so it stays inside the assembly while the
one kind that has looks uses it. Making it public would be an addition that owes a `docs/api-tiers.md` tier
and a vocabulary a stranger's kind would then be frozen to — the exact decision a citation is supposed to
trigger. Re-open it with a consumer that has hand-rolled the same axis. Consequently **no exported type is
added by this batch, so no tier entry is owed and no minor moves.**

**What the seam buys, stated as the two properties it makes structural.** (i) *Measure and draw cannot
disagree about the look*: `CheckboxWidget` resolves once per call through `Looks` and both halves read the
resolved value — the natural body from the registry's entry point and the painted body from `Draw`'s band —
and the drawn body is derived from the band the natural width was computed against, so the two are one
number rather than two derivations that happen to agree. (ii) *An unknown look is a creation-time refusal,
not a per-frame fallback*: `ResolvedAppearance.Declared` is false for absent, blank **and** unrecognized
declarations alike, and `Validate` refuses the third of those once, located on the element with the authored
text.

**Tight metrics, pinned by `KernelControlKindTests.VerifyBooleanAppearanceSeam`** (measured through a host on
the real registered kind, and the lane was extended rather than replaced):

| Case | Natural width |
|---|---|
| switch, nothing drawn beside it | 34 (the track alone) |
| switch, `Label=""` (declared, blank) | 34 (a gap for a label nobody draws is reserved space) |
| switch, label drawn | 34 + 6 (the switch's own gap) + the measured label |
| checkbox, nothing drawn beside it | the band's box side |
| checkbox, label drawn | side + `theme.Geometry.Padding` |

The switch's 34 is `SwitchTrackWidth`, a shape constant declared by the kind and deliberately **not** derived
from density — deriving a track from `RowHeight` would silently resize the control whenever density moved. An
explicit `Height` still wins over the look's natural band in both halves: `Measure` reads it through
`AtomVocabulary.ReadHeight`, and `Paint` sizes the body against that same resolved band, so the drawn box
grows with the band the page asked for instead of using the arranged rect.

**The OFF switch's outline is a control edge, not the raised surface's own.** The OFF track fills with
`RaisedSurface.Fill` and outlines from the theme's control-edge ladder (`BorderStrong`, else `Border`). Under
a scope that spells "no box" as fill == border — which sets `Raised` and `RaisedBorder` to the same colour —
reading the edge from `RaisedSurface.Border` flattens both halves of the track onto the card face and the
control disappears. A fill and an edge read from two independent tokens cannot both collapse that way. The ON
track is unaffected: a selected state is a treatment the resolved-value table owns, and it keeps its own pair.

**Compatibility.** No exported type is added, so no tier entry is owed and no minor moves. A consumer's
`Appearance="checkbox"` page keeps compiling and keeps drawing the box-and-mark look; a kind that registers
through the five- or six-parameter overload behaves exactly as before.

## SA1 — the same seam on `input/dropdown`, the shared selector outlets, and the independent outline

**The vocabulary.** `input/dropdown` now declares the appearance axis the checkbox opened:
`Appearance="field" | "selector"` through the SAME `UiAppearanceResolver` seam (registered via the internal
seventh parameter, so `input/dropdown` and `input/checkbox` are the two kinds that declare looks, and the
seam itself stays internal - no consumer has asked for an appearance axis on a kind of its own). Absent or
blank declares the default `field`; an unknown name is refused at creation naming the authored text and both
accepted looks. `field` is drawn by the code that drew the field before this entry: plane, hairline, one
padding-inset text outlet - so every existing page keeps its pixels. That pixel-compatibility is asserted,
not promised: `KernelCoreWidgetTests` measures the default outlet and its five solids on the registered kind.

**The selector shape, and where each half lives.** The contract this look answers separates SHAPE from
COLOUR, and the split is by type of number: the reserved slots are shape constants on `UiThemeDraw`
(`SelectorAccentWidth` 3 - the left accent line; `SelectorArrowZoneWidth` 18 - the independent right arrow
region), the fill/edge/ink are tokens (the same `Active`/`Neutral` status ladder the field always resolved;
the rail is `AccentRail`'s own `AccentGold` default; the arrow and the divider take the resolved surface's
text/border). The arrow is a procedural triangle stacked through `Solid` - no texture, no font glyph, no
second backend outlet. Neither look reserves a slot for a help button: the "?" is the reference mod's own
chrome and stays outside the library's vocabulary.

**The shared entry, for the consumer's composites and the demo page (r2: one composition, not three
recipes).** `UiThemeDraw.SelectorField(field, display, theme, style, font?)` is the COMPLETE selector paint -
plane, rail, divider, arrow and text outlet in this one order - and the core dropdown kind draws through it,
so a consumer kind that paints its own dropdown-shaped control calls the SAME method and cannot diverge even
in the composition sequence. The pieces stay public too for callers that need only part of it:
`SelectorArrowZone(field)`, `SelectorTextOutlet(field, theme)`, `SelectorArrow(zone, theme, color?)`. The hit
trigger stays `UiNative.DropdownButton` (one popup, one anchor source - SA1 adds no second business
popup/input). Colour adoption is the consumer's own theme work: a dark plane is the page's `Neutral`/`Active`
tokens, gold accents are `AccentGold` / the selected surface's pair. The switch was audited against the same
reference on the token half: FL's geometry is already the reference's `34x18` track, `14px` square knob,
`2px` inset and `6px` label gap - **no switch kind, attribute or shape constant changed**. What the audit
found as a real gap is the OFF-thumb colour: the thumb read the shared `TextPrimary`, so greying it could
only have been done by darkening every important text. SA1.1 lands `UiTheme.SwitchThumbOff`: unset it answers
`TextPrimary` (historical thumb, unchanged), assigned it greys the thumb and nothing else; the ON half keeps
answering `AccentGold`. Since r3 the role is ALSO a document colour token: `<Color Token="SwitchThumbOff"/>`
is in the grammar, the resolver applies it onto the scoped clone like every other colour, and the lane drives
the grey through the real document->resolver->clone->paint path (plus the same document with a scope that
declares nothing, which keeps the bright thumb). The row-wide hover plate behind a toggle stays the consumer's
band, not the control.

**The outline, separated from the recorder.** Covered by the SA1.5 paragraph above; the lane
`KernelDevGeometryTests.VerifyOverlayIsIndependentOfCapture` holds all four directions (accept-without-
capture, store-nothing-while-painting, capture-off-keeps-painting, per-subscription isolation), the both-on
case re-measures the retained-entry equality at the draw step. The outlined set was pinned in r3 as
retained-minus-scoped under the coverage limit the section stated; **DT1 supersedes both the limit and the
pin** - see the DT1 entry below for the closed coverage and the shell-chrome addition (the 2026-10-07
ruling records why "declared as a limit" was not "resolved").

**What the selector lane observes (PM r1 point 4).** `KernelCoreWidgetTests.VerifyDropdownSelectorAppearance`
reads GEOMETRY - the rail solid's rect on the left edge, the divider rect before the arrow zone, arrow ink
contained in and centred on the zone, and the exact text-outlet rect - never a call count, so a repaint that
keeps the picture equivalent (a different row count in the procedural arrow, a different surface spelling)
cannot redden it for the wrong reason; the default-field half of the lane pins that no rail appears where
the old field drew. `KernelControlKindTests.VerifySwitchThumbTokens` finds the knob by its 14x14 rect among
the recorded solids rather than by draw position, for the same reason.

**Compatibility.** No exported type is added; three classified types gain members (`UiThemeDraw` outlets and
`SelectorField`, `UiTheme.SwitchThumbOff`, `UiDiagnosticSubscription` semantics on an existing property). Old
manifests, old compiled consumers and the `field` default are pixel-identical; an unset `SwitchThumbOff`
paints the thumb it always painted; the `GeometryOverlay` setter is strictly more permissive in a dev build
and unchanged in a release one. The 0.7.x temporary public-addition exemption applies: no minor bump.

## DT1 — the outline covers everything the library itself draws

**The defect, in the ruling's words.** 描边不覆盖视为 FL 缺陷 (2026-10-07): areas this library painted but the
outline switch did not reach were carried as "stated coverage limits"; a stated limit is honest recording,
not a fix, and two areas stayed uncovered - the scoped container's own viewport band, and the shell's own
chrome (window frame, title band, close affordance). The ruling supersedes the earlier wording in the SA1.5
section; the SA1-round records stay as history of what was observed then.

**The fix stays one-source.** No new collector, no second ink, no new paint outlet:
- Viewport: the engine's existing per-entry outline call is also made for a scoped entry, positioned after
  `DrawScopedContainer` returns, so the band is outlined over its drawn content in the entry's own
  draw-local space, with the same `retained` answer `Note()` produced for it. Capture-live faithfulness is
  untouched: a dropped scoped node is still not outlined.
- Shell chrome: `DrawShell` saves the geometry it drew, then calls the two probe entries after
  `host.DrawFrame` completes, inside that host's current diagnostic scope (including a newly created
  first-pass host). `BeforeDraw` can still report the previous completed pass before any new chrome is
  published; `BeginPass` clears the old chrome with the other samples. `RecordShellChrome` writes
  the shell's `UiWindowChrome` - computed exactly once there - into the SAME bounded capture the node and
  input samples land in, stamped by that buffer's own pass, so the dump's `chrome` line and the snapshot's
  `ShellChrome` field describe the completed pass; `OutlineShellChrome` paints the same three bands with
  the same switch and ink every element outline uses. The rects come from `CloseButtonRect`/`TitleBandRect`
  extracted from the drawing code - draw, outline and record read ONE arithmetic.
- Report attribution lives in the capture, not in a present-tense read: a consumer's report composes the
  dump/snapshot (host, session, pass header; node, input and `chrome` lines from one buffer). Public
  `UiWindowHost.ShellChrome` remains for acting on the window as it is NOW - and for retiring the
  consumer's mirrored chrome constants (one helper site plus its lane, observed 2026-10-06) - with its
  doc stating the tense split: quoting a finished pass means reading `UiDevGeometrySnapshot.ShellChrome`,
  never gluing the accessor's current numbers onto an older dump.

**What DT1 deliberately does NOT add.** No panel framework: a small draggable developer overlay is today's
`UiWindowHost` + `UiWindowOptions` (`Draggable`/`Resizeable`/`NormalSize`/`AllowMultipleInstances` are
public already) with a page manifest - US composes it. No US page names, no diagnostic business data, no
game-wide inspector: the switch, the ink, the isolation and this accessor are all consumer-neutral, and
the per-window toggle remains which subscription's `GeometryOverlay` the consumer sets.

**Compatibility.** Additive: one public type (`UiWindowChrome`), one public property on a classified type,
and Dev-only paint extension - a release payload changes by nothing at all, and a dev payload without the
overlay switch changes by nothing at all. The 0.7.x public-addition exemption applies: no minor bump.
**Unverified until PM's build round:** lane `VerifyOverlayIsIndependentOfCapture` re-cut to the FULL
retained list; planned lanes - scoped-band delta (delete the engine's scoped Outline call ⇒ red by
exactly scoped×4 hairlines), chrome PAINT delta (delete `OutlineShellChrome` ⇒ red; flag off ⇒
zero-delta guard), chrome RECORD delta (delete `RecordShellChrome` ⇒ the dump's `chrome` line and the
snapshot field fall to the explicit none-state while outlines keep working - the faithful that proves
report attribution rides the capture, not an accessor), and one pass's equality of the two chrome
renderings (dump line vs snapshot field, same buffer). The accessor-vs-draw arithmetic equality is a
GUARD, not a faithful (PM review r2: a re-spelled equivalent arithmetic cannot be forced red; showing
a semantic red requires changing an actual geometry input). Real mouse drag/resize of a window carrying
the overlay remains in-game, not harness-provable.

## D4-Viewport — `VisibleRows`: the Scroll budgets itself from its measured rows

**The gap, cited.** The consumer's pack lists need "at least 4.5 readable entries" at whatever width and
language is current. Its budget helper states the missing capability in its own words
(`UniversalSqueaker@0584bb0:Source/UniversalSqueaker/UI/Layout/DomainListBudget.cs:21-24`: a per-arrange-width
viewport "would need a binding-driven container height the declarative API does not have") and works around it
by copying the shell's width chain and injecting one number at host creation - which filter, xeno, language,
content and resize never refresh (PM layout review, 2026-10-07 §17). The engine's own order is what closes it
honestly: `MeasureScroll` measures its children FIRST, resolves the viewport from those rects, and when the
scrollbar reserves its width it re-measures the children and recomputes the budget from the narrowed rects -
so no previous-frame write-back and no copied geometry exist anywhere in the chain.

**The vocabulary.** `VisibleRows` on `Scroll` only: a positive finite decimal, fractions included. Static
numeric `Height` - zero included, since a declared 0 already answers a 0-high viewport - keeps its exact prior priority (the same static-first order `WidthKey` keeps against `Width`);
with no Height, the budget is the first floor(k) countable entries' measured bottoms plus, when k carries
a fraction, that fraction of the NEXT entry's OWN measured height taken from its real position (the full
gap before it stays, so a half row shows half a row), plus the scroll's own bottom padding,
clamped to natural. Countable entries: a direct `Repeat` expands transparently to its materialized row roots
(stamped `IsMaterializedRow` at the single root entry per row - never the Repeat as one blob, never its
inner parts as several); any other visible direct child counts as its root entry; a `VisibleKey`-hidden
sibling (the empty state) contributes nothing; fewer countable than k - or none at all - answer natural.
Bad numbers and non-`Scroll` hosts of the attribute are creation refusals (`UiHost.ValidateViewportRows`),
the engine read stays fail-soft for programmatic specs, matching the closed-Height rule it joins.

**What it does NOT become.** No HeightKey (not needed by any cited gap - the fallback note in
`evidence/.../d4-capability-r2.md` stays the record), no general list editor, no consumer numbers as defaults
(4.5 is a manifest value), no vocabulary outside the two mirrored container lists (their drift lane pins them).

**Evidence.** Lanes in `KernelRepeatTests` (fractional equality from published final rects, the wrap-step
same-round case, the consumer's Repeat+empty structure both ways, empty-state natural, static priority, the
refusal matrix, the shrink-to-two re-budget) are SOURCE here; nothing in this section is a claimed run - the
named faithfuls and the 10 gates belong to the PM-scheduled build window.

## R2 — one shipped palette, a partial bag, a scoped re-tint, and the font redirect

**1. The bag is partial and answers `Vanilla`.** `new UiTheme()` is a complete, drawable look: every colour
token it never assigned answers the shipped palette, resolved per token on read. A consumer changes the four
tokens it cares about and keeps a coherent look everywhere else. The one asymmetry, and the reason the
fallback is a per-token claim flag rather than a comparison against the default: **an explicitly assigned
transparent is a value.** `theme.Panel = new Color(0,0,0,0)` must stay transparent, not be repainted by
Vanilla — a UI that wants a gap where a plane would be has to be able to say so. A nullable per-surface edge
keeps its documented `null` = "use the shared token" meaning, so transparency there is spelled as a
transparent colour.

**2. `UiTheme.DarkGold` is removed, with no alias and no shim.** The library ships one look. The warm-gold
palette moved into the harness (`tools/FerriteLib.UiKit.Tests/TestPalettes.cs`) because the only thing that
needs a *second* palette is a lane asserting what a second palette does, and a per-token claim lookup.

| Count | What happened |
|---|---|
| 225 | `UiTheme.DarkGold` references in the harness, all migrated |
| 203 | → `UiTheme.Vanilla`: lanes whose subject is layout, identity, input, invalidation or diagnostics, where the palette is a fixture and any complete token set does |
| 22 | → `TestPalettes.DarkGold`: the lanes that genuinely need a distinct second look |

**Preserved, not weakened:** `KernelResolvedStyleTests.VerifyDarkGoldPalette` keeps every one of its value
cells (`Base`, `Panel`, `Raised`, `Hover`, `Selected`, `Success`, `Danger`, `WorkspacePlane`, `SectionBand`,
all five text tokens, `AccentGold`, the derived `AccentHover`, `Border`, `BorderStrong`, `Divider`), the
"leaves every per-surface edge unclaimed" assertion, and the fresh-instance / independent-re-tint pair. Only
the *source* of the palette moved: the cells now read `TestPalettes.DarkGold`, which carries exactly the
values the library factory used to. The `Vanilla + DarkGold are different looks` comparison survives for the
same reason.

**3. A colour-only re-tint refreshes scoped themes and moves no layout.** `UiStyleResolver` caches one clone
per effective (scheme, density) pair, and a clone copies token **values**. A cached region therefore kept the
palette it was built with, so re-tinting the injected theme left every styled region painting the old colours
— the stale-colour defect this fixes. The fix is a second clock on the theme rather than a broader one:
`UiTheme.ColourRevision` moves on any colour assignment that changes a value and on nothing else, and
`ThemeFor` drops the cached clones when **either** `LayoutRevision` or `ColourRevision` has moved since the
last lookup.

*Why it cannot move layout.* The two clocks are disjoint by construction: the colour setters write only
`ColourRevision`, and `LayoutRevision`'s only writers remain `DefaultFont` and `Geometry`. No colour
assignment reaches the layout clock, so the engine's band cache — which compares `LayoutRevision` — is not
invalidated, the arranged snapshot is reused, and widget, session, focus, selection and popup state are
untouched (a repaint drops no node and no state). A scope that applies a colour equal to what the token
already holds is not a change either: both setters compare before they mark.

**4. (R12-T) Typography is a font selection; density is a separate axis.** A `<Scheme>` may carry the density
metric vocabulary (`<Metric Token="RowHeight" Value="…"/>`), and typography is one token of that same
vocabulary: **`<Metric Token="Font" Value="Tiny|Small|Medium"/>`**. Its value is a FONT CLASS, and it is the
only thing `UiStyleResolver.ApplyFont` writes — `UiTheme.DefaultFont`, which is what `ctx.Theme.DefaultFont`
answers, what `ITextMetrics.MeasureWidth`/`MeasureText` are called with, what the fit audit compares, and what
`UiThemeDraw.Label` paints with when the caller names no font. One selection, obeyed by measurement and paint
because both read the same member; nothing re-derives it.

The legacy `<Font Value="…"/>` element **keeps working by redirecting into that same font path**: the parser
sets the identical typography selection, records one issue naming both the legacy spelling and its successor
(`<Metric Token="Font" …/>`), and reports the redirect through the appearance channel. It is never ignored, and
it is **never translated into a distance** — an earlier revision of this batch mapped it to `RowHeight`, which
would have destroyed the declaration's meaning while appearing to honour it. A scheme declaring both is
resolved by the metric, and the shadowing is reported rather than resolved quietly.

Density stays independent in both directions: the font token is separated in the parser before the numeric
metric reader ever sees it, `ApplyMetric` has no font case, and `UiTheme.DefaultFont`/`UiTheme.Geometry` are
two distinct properties with two distinct setters. Selecting a scheme therefore cannot silently move density,
and changing a font is not implemented as changing a row height. Both are layout-bearing, so both move
`LayoutRevision` — a different typeface really does measure differently — while a colour moves only
`ColourRevision` (item 3 above).

`UiStyleDocument.ReadMetric` is still shared by `<Scheme>` and `<Density>`, so the numeric vocabulary cannot
drift between them; the `Font` key is routed to `ReadFontMetric` before that call, which is what keeps a
typeface out of a `Dictionary<string, float>` by construction rather than by convention.

**5. (R12-A) The appearance seam was corrected, not rebuilt.** One bool behaviour (`input/checkbox`) still
serves both looks; the shared seam (`UiAppearanceResolver`, per-kind sets via the internal 7-arg `Register`
overload, `UiWidgetRegistry.GetAppearanceResolver`) was left in place, and what changed is what the seam feeds.
The resolved look is read once per call and drives the creation-time contract, the natural band, the natural
width and the paint, so premeasure → natural-body → draw agree on normalization (case and surrounding
whitespace), on empty labels (`Label=""` reserves no gap) and on explicit dimensions (a declared `Height`
decides the band in measure *and* draw). The kind's public surface is unchanged: `IUiWidget` keeps all three
signatures and the **five-parameter `Register` overload keeps its exact CLR signature** (the six- and
seven-parameter overloads are additions beside it; the seven-parameter one is `internal`). The three
`Register` overloads and `GetNaturalBody` are member-level additions on an already-`stable` type and are
documented as such in `docs/api-tiers.md`; no exported type is added, so no type-level tier entry is owed.

**Evidence, labelled.** (a) CURRENT INSPECTED: every path and line in the batch report. (b) DATED HISTORICAL:
the 2026-09-18 palette decision quoted above. (c) INFERENCE: that the harness's migrated fixtures behave
identically under `Vanilla` — argued from the source of every affected assertion, not executed. (d)
UNVERIFIED RUNTIME: **nothing in this batch was compiled or executed** — the shell sandbox fails OS-side
(`SetNamedSecurityInfoW Win32 5`), so no `dotnet`, no gate, no harness run and no game run; every claim above
is static, and the build/verification slot is the PM's.

## R3-A — the per-host instrument grows a machine-readable rendering

An addition, inside `0.7.0` under Amendment 3, and **not** a correction round: R1/R2 were accepted after their
own recovery run, and nothing here reopens them.

**A.1 — one instrument, three views.** The development instrument already had two renderings: the diffable
`DumpGeometry()` text and the optional outline. `UiDiagnosticSubscription.TryGetGeometrySnapshot` adds a third,
as data. All three read the capture's own stored samples — the dump and the snapshot are built from the same
two lists in the same calls that fill them, and the overlay is now painted only for entries the bounded capture
actually RETAINED (`UiDevGeometryProbe.Note` returns that answer and `Outline` takes it). There is no second
collection, no replay and no synthesised input, which is what makes the relationship structural rather than a
promise two collectors keep.

**A.2 — what is captured.** `UiDevGeometrySample` and `UiDevInputSample` gained stable identity (the node's
canonical `UiNodeId.Key`, its display path, its declared ordinal and `Id`), the effective appearance and its
provenance (the resolved theme's `DefaultFont`, the scheme and density names read through the resolver's own
accessors over the same chain the theme was resolved from, whether the element drew with the injected theme or
a resolver-built region, and the theme's two revision counters), the authored role text, the published
disabled state, the **effective clip** (the session's own already-intersected value, in window space — so a
scoped container's sample carries the clip it was drawn inside while its viewport appears on its children),
the popup boundary (`OpenPopupId`, the anchor, and whether this element owns it), and the input's consumption
as the phase transition plus an explicit `OriginUnknown` for a pass that entered already `Used`.

**Honesty rule, and where it bites.** Three boundaries are recorded as `UiDevBoundary.Unknown` **with a
documented reason in the data**, never as a plausible default: hover (published during an element's own draw,
which runs after the sample), focus (this library publishes no focus owner — the session's active node is the
draw-time element stack, and no keyboard or focus subsystem was invented to have something to observe), and a
content rect on anything that is not a scoped container. The authored `Tone`/`Emphasis` are recorded as
authored TEXT rather than as a resolved treatment: the treatment is computed inside the widget, which may also
be applying hover/armed state, and calling the role parser from the instrument would record a deprecation note
the instrument itself caused.

**A.3 — invariants held.** Bounded storage and current-pass semantics are unchanged (the dropped counters are
still per-pass and still reported); subscriptions remain off by default, per-host, with the same isolation and
disposal behaviour; a handed-out snapshot holds no node, session or host object and cannot change after a later
draw, because it is built fresh from values per call and every setter is `internal`; nothing logs per frame and
nothing dispatches or consumes input; and a release payload still refuses enablement and captures nothing,
with the new reader answering `false` rather than throwing.

**A.4 — surface and tiers.** `Snapshot()` and `DumpGeometry()` keep their signatures. Four types are added —
`UiDevGeometrySnapshot`, `UiDevNodeSnapshot`, `UiDevInputSnapshot`, `UiDevBoundary` — all classified
**public-unstable** in `docs/api-tiers.md`. They are **public rather than internal** because of the packaging
constraint the demo packer establishes (it bans `InternalsVisibleTo`, so a structured export a consumer must
call has to be genuinely exported), and they are **deliberately outside `#if FER_DEV`** because the tier
document is one file read in both configurations: a Dev-only public type must be listed — which fails
`VerifyNoTierEntryIsStale` in a **release** build, since the entry would name a type that payload does not
export — or omitted, which fails `VerifyAllPublicTypesAreClassified` in a **Dev** build. A type that exists in
one configuration only therefore cannot satisfy the tier harness either way, and it would also be a
compile-against-Dev / ship-against-Release trap. The **four types are callable but inert in a release
payload**: they are exported and nameable, nothing there ever produces an instance, the reader answers `false`
and the enable path still throws. The capture behind them stays compiled out. No version bump — the 0.7.x
temporary public-addition rule applies.

**A.5 — lanes.** `KernelDevGeometryTests` drives a real `UiHost.DrawFrame` and adds: the dump-versus-snapshot
agreement compared fact by fact for EVERY retained node on two pages (one page-level, one with a NON-DEFAULT
scheme and density so provenance and the selected font are exercised), covering the identity, the rects, the
font, the appearance origin and every boundary INCLUDING the two that are always unknown - the dump renders
those from the same sample field the snapshot reads, so a snapshot that started reporting hover as known
while the text still said `unknown` reddens the agreement rather than passing on a subset of fields; the
nested-clip containment and the popup available-or-unknown pair, including the documented unknowns; a bounded
capture whose head, dropped count, text/data agreement and retained-only overlay are all asserted (the
overlay measured as the difference the switch makes); a copy/two-host/disposal lane whose decisive case is a
SECOND draw on the SAME host, after which an already-handed-out snapshot must still report the earlier pass;
and, in the release half, the new reader's refusal. The existing R12 and earlier assertions are untouched.

**Mutation proposal (proposed, NOT run).** `tools/mutation/batches/r3a-2026-09-29.ps1` names one-change cases
for the new failure modes: the text and data renderings disagreeing on the clip, the clip read from the
element's own draw rect instead of the session's effective clip, an unknown boundary filled with a plausible
rect, the node identity taken from the display string instead of the canonical key, the bounded capture's
dropped counter zeroed, the overlay painted for a dropped entry, the declared look reported as the kind's
default, and - as the only two-anchor case, because one edit cannot express it - the collector reusing one
snapshot instance so an already-handed-out snapshot follows a later draw. Each case names its mutation-target
assertion and its guards. It is a proposal in the tracked batch format so PM can validate anchors and run it;
this session did not execute it.

**Evidence, labelled.** (a) CURRENT INSPECTED: the files and lines in this batch's report. (b) DATED
HISTORICAL: nothing here re-reads a prior ruling. (c) INFERENCE: that the four new lanes will hold as written —
argued from the source of every assertion, not observed. (d) UNVERIFIED RUNTIME: **nothing in this slice was
compiled or executed**; no `dotnet`, no gate, no harness, no mutation.

## R3-A-CORR — the correction pass the PM-measured red baseline bought

`verify-local.ps1 -NoRestore` passed checks 1-9 and **failed gate 10 with three fixture errors in the R3-A
lanes**. Four defects were corrected; the frozen contract above is unchanged except where its own words were
not true.

**C1 — fixture identities and a real overflow.** Two lookups in the appearance/boundary lanes addressed
`root/band` on the `Page` fixture, whose button is nested at `root/row/band`; both now use the real path, and
the cross-host exclusion asserts both directions non-vacuously (A has its own nested band and not B's id; B
has its id and not A's band). The bounded lane declared **520 sibling widgets in one manifest**, which the
manifest's own node validation refuses at creation - so the lane never reached the capture. It is replaced by
a **runtime expansion**: one template (`Row` + a `chrome/rule`) and one `Repeat` over a bound item list, so
the declared page stays tiny while each item expands to a Row and a rule at arrange/draw time. The expansion
**rate is measured**, not assumed (two small runs), and the big run is checked against it; the dropped tail is
asserted at its **full** composed path (`rows/row#item319/r#item319`, the engine composing
`<declaredId>#<itemKey>`), non-vacuously: the same construction is first proved to find the last item's rule
on a small run where it IS retained, and the first item's rule is proved present in the overflowing pass.

**C2 — the effective appearance was missing.** Authored `Tone`/`Emphasis` is not resolved appearance. The
same capture now resolves the element's look through the registry's own seam -
`UiWidgetRegistry.GetAppearanceResolver(ctx.Source, kind)` → `UiAppearanceResolver.Resolve(spec)`, the very
instance the kind registered and its own widget resolves through - and reports the canonical look, whether the
element declared one, the kind's default and accepted set, and the provenance: `scope` when the resolver is
the element's own scope's registered set, `core` when it is the core scope's, `none` when the kind declares no
seam at all. Which scope answered is proved by asking for the core resolver as well and comparing instances,
so it is identity rather than a heuristic, and `AppearancePairRegistered` separately reports the mixed case (a
scope that declares the kind but no looks for it). **Fonts:** the field is renamed `ScopeFont` and a new
`PaintedFont` reports what a label finally paints with - null, with a documented reason, because that choice is
made inside the element's own draw and several kinds pass their own constant. Calling `ScopeFont` the painted
font would have been the misreport the split exists to prevent; there is no per-element `Font` attribute in
this library.

**C3 — the copy mutant is now faithful.** The c7 case cached the first snapshot around an object initializer,
which only makes later calls go stale: the snapshot never follows a later draw, so its named assertion stayed
green and the case proved nothing. `Snapshot` now creates a fresh instance and calls a `Fill` that assigns
every field on every call, and the mutant reuses that instance - so the collector **refreshes it in place** and
an already-handed-out snapshot follows the next draw. The assertion that reddens is the one that should: the
snapshot taken before the second draw on the same host no longer describes the earlier pass. Both properties
stay tested: new-pass advancement (which the live instance also satisfies, so it is a guard) and old-copy
preservation, with node `Pass` agreeing with its snapshot's.

**C4 — the documentation now matches the code.** "Immutable" was a stronger claim than the code makes: the
lists are the arrays the call allocated, so a caller who down-casts its own copy can change that copy. What is
guaranteed - a fresh snapshot object and fresh node/input objects per call, no view over the capture's buffers,
an old snapshot detached from later draws, no node/session/host object held - is stated, and the stronger claim
is explicitly not. The popup members now report their real sampling moment (as the engine reaches the element,
BEFORE its own draw, so a popup opened later in the same pass is not there yet and one about to be closed is
still there), and every boundary names its coordinate space (clip and popup anchor in Host window space, input
rect and point in window space, content in scroll-local space).

**Evidence, labelled (CORR).** (a) CURRENT INSPECTED: the PM-measured baseline and every file and line in the
correction report. (b) DATED HISTORICAL: the 2026-09-24 gate-10 measurement, kept as the record for the list it
was taken against. (c) INFERENCE: that the renamed gate names and the re-cut t2 expectation now match the
lanes - argued from the literal strings, not observed. (d) UNVERIFIED RUNTIME: **nothing was compiled or
executed**; no `dotnet`, no gate, no harness, no mutation.

## FL-IC1 (2026-10-08) — one receive-eligibility rule, one capture owner, a bounded scrollable menu

Specification: the interaction-convergence contract sections the PM relay names 3, 5 and 6, against the three
failure witnesses the independent all-controls audit confirmed in current source (own-popup overlap, chart press
under a menu, unbounded menu height). The slice fixes those three and the shared rule they were each missing;
FL-IC2 (edit transactions, the window Cancel/Accept entry and the tree cancel) is a separate slice and is NOT in
this section. Nothing here is a real-game acceptance.

**The ruling, in one paragraph each.**

- *Receive eligibility.* A control may take a NEW press only when the element (or an ancestor) is not disabled,
  the native backend lets the window receive input at that point (the raw hover answer, which is what honours the
  enclosing scroll views and the obscuring stack), the pointer is inside the element's own rect, no popup layer
  covers the point, and the point is inside the effective clip the engine published for that element. The first
  two answers are the ones `Button(rect, ctx)` already consulted; `UiNative.CanReceivePointerPress(rect, ctx)` is
  the same rule for a control that takes the pointer itself instead of handing the press to a native control, so a
  raw-pointer kind does not grow its own copy of the conditions.
- *Popup priority is geometric.* A popup layer covers the pointer or it does not. Owning the popup is not an
  exemption, and that exemption was the defect: a menu that `RectFor` pinned or clamped over its own trigger let
  the trigger consume the press and close the menu, so the option row under the pointer never saw a live event
  (the reported in-game F09, and the audit's D1 stub witness). A dropdown keeps its toggle-to-close because the
  menu's rect is not the trigger's rect: a click on the strip of bar the menu leaves free still closes it, while a
  click inside the menu belongs to the row.
- *Capture ownership.* A session capture records the element that took it, not only an id. Normal release, owner
  stop-drawing (hidden, switched away, removed) and host close each end exactly that session's capture, and the
  native release still refuses to clear a hot control it does not own. A capture with no recorded element owner is
  deliberately not reconciled at the pass boundary - nothing owns its draw - and is still released by `Dispose`.
  Continuation of a drag is a separate question from a new press: it follows the capture, never this frame's hover,
  and the release is not gated by position either.
- *The menu is bounded and scrolls.* `RectFor` returns a height of whole option rows that fit the Host viewport
  (`VisibleRowCount`) instead of `optionCount * OptionHeight`, both option lists draw/hover/hit-test only the rows
  inside that rect, offset by `UiSession.OpenPopupScrollRows`, and the wheel is routed by priority at the engine's
  pass boundary: a notch that lands inside the open menu moves the menu and is consumed before any scroll container
  under it can take it. Geometry, hit test and scroll read one rect and one offset. The notch is owned wherever the
  menu's rect is, **including at its own ends**: a saturated first or last row still swallows the event, because
  handing it back would start the page scrolling under the menu the player is reading - the clamp is the menu's
  edge, not a release of the priority. The one-row floor is the single place the bound is not absolute: a viewport
  shorter than one row still shows one row, since a zero-row menu would be the same unreachable-option defect with
  less paint, and that case is a playtest question rather than a product blocker.

**Public surface, all of it inside `0.7.0` under the standing temporary exemption, none of it promoted.** Seven
members on existing public-unstable types, no new type, no new kind, no XML vocabulary, and no signature changed
on an existing member: `UiNative.CanReceivePointerPress`, `UiNative.WindowPointer`, `UiNative.PointerWheelNotches`,
`UiPopup.VisibleRowCount`, the `UiPopup.WheelRowsPerNotch` constant, `UiSession.OpenPopupScrollRows` and
`UiSession.OwnedHotControlOwner`. `UiNative`'s public member count moves 28 to 31, which is the number the tier
document now reports. `docs/api-tiers.md` carries the per-entry statements, including the one a reader must not
miss: `IsPointerOverHigherLayer(UiNode, Vector2, string?)` keeps its signature and no longer grants an exemption,
and removing the parameter is a signature change that waits for a minor boundary.

**Behaviour changes on existing surface, stated as breaks rather than as additions.** Three, and a consumer that
recompiled against the previous carrier changes observed behaviour in all three: a menu that covers its own
trigger now selects instead of closing; a `chart/line` press under an open menu, outside the effective clip or on a
disabled element now does nothing at all instead of capturing and emitting; and a menu longer than the viewport is
now shorter and scrolls. None of them needs a code edit at the call site: every one goes through `UiPopup` or
`UiNative`, and the consumer's own composite dropdown already reads `RectFor` plus `DrawOptionList`, which is the
reason the fix was put there rather than in either caller. The wheel's **sign is not a library convention**:
`PointerWheelNotches` answers `Event.delta.y` exactly as the native IMGUI scroll view reads it (a positive delta
asks for the later rows), because a menu scrolling the other way round from the `Scroll` containers beside it would
be a second protocol inside one window. Three rows per notch (`UiPopup.WheelRowsPerNotch`) IS a library default and
is named as one. What the stub cannot show is a real device's weight, and whether Unity's own scroll view under a
menu consumes the notch before the pass-boundary rule; both stay human-pass items.

**Event timing, because the ordering is the contract.** One IMGUI event is one host pass. The pass opens, the
previous pass's complete paint order (menu layer included) becomes the stack dispatch reads, and the wheel question
is answered at that moment - before any element drew, so nothing had consumed the notch yet. Content then draws: a
context-bearing control asks eligibility against those layers, and a covering menu makes it refuse without touching
the event. The pass closes, releasing an orphaned popup and a capture whose owner did not report drawing. The
deferred popup pass then paints the bounded rows and hit-tests them, and it is the pass that consumes the click an
option takes. The consequence a reader has to hold: the popup geometry dispatch consults is always ONE event old,
which is what makes a press taken by a control drawn under the menu impossible, and what makes a menu that moved
between the two passes (a scroll, a resize) apply its previous rect for that one frame. That is the existing
published-layer rule, not a new one, and the audit records it as a known boundary.

**What these lanes do not show, kept explicit.** Real Unity wheel handling (weight, direction, and whether a
native `BeginScrollView` under the menu takes the notch first in the real client), a real native
`HorizontalSlider` drag, text input and IME, and the game's `WindowStack` input authority are outside the stub
harness. The lanes here drive the faithful stub pump - separate Layout/MouseDown/MouseUp/ScrollWheel events, the
stub's hot-control capture and `Use()` contract, group-local pointer read-back - and say which claim they rest on.
The paired human pass covers the in-game half: overlap selection, long-menu reachability and wheel, chart drag,
Enter/Esc and the narrow Chinese layout, in one short session.

**Lanes.** `KernelInputEligibilityTests` (thirteen scenarios: string and typed overlap selection, the uncovered
close strip, the forty-option bound-and-scroll with the round trip made inside ONE open menu and both ends
selectable, the same round trip measured on the typed list against the string list's own numbers, wheel priority
inside and outside the menu and at the saturated end, the covered chart's refusal and the option row that takes the
click instead, the normal drag after the menu closes, continuation and release outside the rect, a disabled chart,
a clipped band, the pass-boundary capture release beside another session's untouched capture, and the released
identity's capture). Every wheel assertion reads whether the event came back consumed, so "the menu owns this
notch" and "the notch fell through" are two observable results rather than a scroll position that merely happens
to be unchanged, and every press-pumping lane leaves the shared native hot control as it found it - the doubles
clear it only for the control id that holds it, so a lane ending mid-press would poison the next. `KernelPopupTests` keeps its nine
earlier scenarios green, and `KernelDevGeometryTests.VerifyEventSemantics` now pins `EventType.ScrollWheel`
between the double and the reference constant.

**Faithful reverts.** `tools/mutation/batches/fl-ic1-20261008.ps1`, five cases on single-line anchors: the
owner-id exemption restored WITH the two decisions it used to guard (the retired rule reached both the trigger's
own coverage test and the context-carrying button behind it, so a mutation narrowing only the first would be
caught by the second and redden nothing; the case restores both in one block, and the same-node sibling lanes stay
green on both sides precisely because the restored rule is keyed on the dropdown id and not on node identity -
which is the difference between reverting this slice and reverting the 0.4.0 fix underneath it); the chart's press
gate removed; the menu height unbounded; the capture's owner left unrecorded; and the published scroll range
zeroed. Each names the assertion it must redden, restores by bytes, rebuilds to the baseline fingerprint and
watches the root carrier, and the batch was run against the committed tree so each log binds that HEAD.

One rule in this slice has **no** mutation case, and the batch header says so rather than hiding it: the
wheel-ownership-at-the-ends rule replaces a two-statement shape (`if (moved) { Consume(); }`) that one anchored
region cannot narrow without leaving the unconditional consume behind. That claim rests on its lane - a notch
pumped at the saturated end must return consumed with the scroll unmoved - which is also the reading the PM's
independent probe measured as failing before the correction.

**Review integrated.** The PM's in-progress review of this slice (`evidence/interaction-development-20261008/fl/pm-in-progress-review1.md`
and the probe under `pm-wheel-probe/`, built from a copy of the changed sources with executable stubs) named four
defects in the first version: the negated wheel sign, which the slice's own lanes could not see because they
pumped the same negation; a covered notch falling through to the page below once the menu hit either end; a
"back to the first option" lane that reopened instead of travelling backward inside one open menu; and a
`RunAll` that printed FAIL without summing it, so a red lane could sit inside a green run. All four are corrected
above and in the code, the honest sign/ownership and round-trip readings are now asserted where they were
assumed, and the probe's cited Unity source is protocol evidence, not a game or device measurement - the
real-window half remains the short human pass.

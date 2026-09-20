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

## The selected ordinary-authoring path (B)

The recommended author route is existing surface only: one layout manifest over public atoms/containers
(`input/slider`, `input/number-field`, `input/dropdown`, text/section/wrap/scroll containers), typed
bindings through `UiBindings` (`BindValue`, `BindReadOnly`, `BindOptions`), explicit `NotifyChanged` from an
owned mutation path on an authoritative plain C# model, and `UiWindowHost`/`UiPageWindow` for the window.
`UiNotifyAdapter` + `INotifyPropertyChanged` stays optional. No VM base class, no builder DSL, no second
state store becomes part of the contract. The runnable reference lives in the harness fixture area and the
prose guide in `docs/consumers/ordinary-settings.md`.

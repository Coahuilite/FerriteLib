# Change plan — placement and alignment (0.7.x line)

> **Status: a live plan, nothing implemented.** This file is the single home for the plan opened on
> 2026-09-18; later discussion edits it in place — add, remove, amend items — rather than opening another
> document. Authority order: **code > `MEMORY.md` > this file**, and the architecture it changes is mapped
> in `docs/architecture.md`. Every item below is `proposed` until it has a contract entry and a lane, and
> no line here may be read as implemented or verified.
>
> **Version axis — settled (maintainer ruling 2026-09-18): the 0.7.x line, no minor move.** New manifest
> vocabulary is a public addition, which normally moves the pre-1.0 minor, but the rule that decides it is
> the one already written down: *an rc that never shipped has no goalpost to move* (`MEMORY.md`, the
> 0.4.0 → 0.3.0 refile). Nothing is consumer-compiled against `[0.7.0,0.8.0)` and no package has been handed
> over, so the axis stays `0.7.0` and the consumer range stays `[0.7.0,0.8.0)`. This work is recorded as a
> **second amendment** to the line's contract before it ships — the `UiTheme.Vanilla` amendment is the
> precedent. The contrast is the `0.5.0 → 0.6.0` move, which turned on a delivery fact ("the 0.5.0 dev
> package and its `[0.5.0,0.6.0)` handoff were delivered"), not on the number being unreleased.

## 0. What this closes, and what it does not

`docs/architecture.md` §3.2 and §6.3 register two layout gaps — **alignment: none** and **relations: none**
— and one cross-cutting fact this plan makes load-bearing: **density cannot reach the layout layer**. The
plan closes the first, half of the second (parent-relative placement; sibling-relative placement stays
out), and the density half as a prerequisite.

**Not closed here**, and still registered in §6.3: behaviour/painter separation, published interaction
state, the declared input contract, control-composed parts, keyboard focus, the image outlet, per-part
style keys, and the L1 (orphan-name) closure lane.

## 1. Items

### CP-0 — density reaches container spacing  *(prerequisite; blocks CP-1)*

**Goal.** A theme's geometry moves the space **between** containers, not only the space inside a control.

**Why.** `UiLayoutEngine.cs` never reads `theme.Geometry`; container `Padding`/`Gap` are XML-only with a
default of zero (`ParsePadding` → `Padding.Zero`, `ReadGap` → `0f`), while every density token is consumed
inside widget code. A theme change therefore has no page-level effect — and CP-1's reference frame (the
parent's inner box) would be frozen in XML.

**Change.** `ParsePadding` and `ReadGap` fall back to `theme.Geometry.Padding` / `theme.Geometry.Gap` when
the attribute is absent; an explicit attribute still wins, so `Padding="0"` keeps the old result.

**Migration (breaking).** A container declaring neither attribute gets the token default instead of zero.
The contract states it and names the escape hatch (`Padding="0"`).

**Clock.** Reuse the existing one: `UiHost.MeasureAndArrange` turns a `theme.LayoutRevision` move into
`BumpContentRevision`, so a density change already re-arranges. No second clock is introduced.

**Verification.** One lane changes only the theme's geometry and asserts the arranged rects move; a second
asserts `Padding="0"` arranges exactly as before.

**Status.** `proposed`.

### CP-1 — placement vocabulary inside a placement container

**Goal.** An element can state where it sits in its parent's box: which edge or centre it references, how
far along the parent's span, and a pixel nudge.

**Why.** No alignment vocabulary exists anywhere in the engine, and a relation exists only as a
consequence of flow. Measured in experiment 3: centring is two equal flex spacers and a fixed bottom edge
is whatever a `Fill` sibling leaves behind.

**Model — one rule, both axes.**

```text
x = parentRefX(inner, AlignX) + offsetX - pivotFraction(AlignX) * self.width
```

`AlignX` selects **both** the parent's reference point and the element's own pivot, so one name carries the
pair: `Left` = (inner.x, self left), `Center` = (inner.centre, self centre), `Right` = (inner.xMax, self
right), `Stretch` = today's full-width behaviour and the default. `OffsetX` is `N%` (a fraction of
`inner.width`) or a bare number (pixels). The vertical axis is the same rule with `AlignY`/`OffsetY`.

**Home.** `MeasureOverlay` (`UiLayoutEngine.cs:1260`) already puts every child at `(padding.Left, innerY)`
with the full inner width — a placement container with no placement vocabulary. **No new container kind and
no new structural concept.**

**Attribute set — four names.** `AlignX`, `OffsetX`, `AlignY`, `OffsetY`.

**Refused at creation** (fail-closed, the A2/A3 precedent): an unknown alignment value; a ratio outside
0..100%; a malformed number; percentage placement on a child of a flow container; `Stretch` together with
a numeric `Width`; placement together with `Fill` on the same element.

**Deliberately cheap properties.** No expression and no reference, so the manifest's existing non-goal
holds. Single-pass: the only unknown is the element's own width, and `Measure` already ran, so there is no
solver, no second pass and no cycle.

**Status.** `proposed`; blocked by CP-0.

### CP-2 — cross-axis alignment in flow containers

**Goal.** In a `Row` a child can sit top / middle / bottom; in a `Column`, left / centre / right.

**Why.** Split from CP-1 because it lands in different code and has a different risk: the main axis stays
flow, and the default must not move a single existing manifest.

**Boundary.** Percentage offsets are **not** accepted here — the main axis already belongs to the flow, and
a second owner for it is what this plan exists to avoid.

**Status.** `proposed`; shares `ResolvePlacement` with CP-1.

### CP-3 — the skin-source axis  *(registered; not implemented)*

**Goal.** A look can be defined by data, not only by C# factories, so "a different skin" is a file rather
than a recompile — and so a consumer that wants only the visual core can obtain a skin without a host.

**Why now.** CP-1's predecessor package made `new UiTheme()` an unpainted bag and made the theme a
required parameter, so the library now *forces* the question "where does a skin come from" on every
consumer. Today the only answers are the two static factories, whose literals live in C#.

**Shape.** One parser, one vocabulary, two text origins (already true for `<Styles>`), extended so a
document can define the base tokens a palette is made of — not only the `Scheme`/`Density` overrides.

**Not this.** No registry, no Def, no directory scan, no process-wide mutable static.

**Status.** `proposed`.

### CP-4 — the document boundary: role handling  *(revised 2026-09-18; blocked by D5)*

**Goal.** One file owns Appearance, so that "replace the skin" is a one-file operation.

**Original shape, and why it was withdrawn.** This item first read "move `Tone`/`Emphasis` out of the page
file and into the style document". That half is **withdrawn**, because the decisive evidence points the other
way and the benefit was uncited:

- `Tone` is the only appearance reference in the system that **cannot dangle**. `Scheme="dark"` names
  something the *style document* defines, so a skin that does not define `dark` leaves the reference
  unresolved (fallback recorded); `Tone="Danger"` names a member of a **code** vocabulary, so every skin
  must be able to honour it, and a skin's freedom is what `Danger` *looks like*, not whether it exists.
  A page therefore never breaks because a skin changed. Moving roles into the document would trade that
  property away for "a skin may define new role names" — a capability with **zero citations**.
- The file-boundary complaint is still real, but it is not about roles: it is that *who may write what* is
  not stated.

**What the measurement actually found.** `UiStatusTone` conflates three different things, and one member is
reachable from two directions:

| Member | What it really is | Evidence |
|---|---|---|
| `Neutral` | absence of a role | `AtomVocabulary.ParseTone` returns it for an empty attribute |
| `Active` | an **interaction state** | `ButtonWidget` resolves it while the pointer holds the element down |
| `Success`/`Warning`/`Danger` | authored **semantic roles** | — |
| `Disabled` | a **data-derived state** | `UiStyleTable.Resolve`: `writable == false → Disabled` |
| `Disabled` again | **also authorable** | `ParseTone` accepts `"Disabled"` |

So the same member is derived from the bindings *and* writable by an author, with the derived value
silently winning. And `Warning` and `Danger` resolve to one treatment, because the role→surface mapping
is code (`UiStyleTable.Cell`), not data — a skin can change what `Danger` is painted with, but cannot
make the two roles differ.

**What survives.** The boundary question reduces to three narrower, now-motivated items, carried by CP-6.

**Status.** **Withdrawn (2026-09-18) — D5 chose (a).** The role-move half is not planned, and the
remaining boundary question is carried by CP-6; CP-4 owns no work of its own and is closed rather than
left as a zombie item.

### CP-5 — regional scope in the style document  *(registered; not implemented)*

**Goal.** A named region of the page can be re-skinned without repeating an attribute on every element in
it, and without a selector language.

**Shape.** A scheme/density declaration may name the region it applies to (`For="region-id"`), matched by
the region's declared name — nearest-wins stays the only precedence mechanism. Not a selector: no
combinators, no specificity arithmetic, no pseudo-classes, no media queries.

**Status.** `proposed`; shares CP-4's boundary decision.

## 2. Rules the contract must state

| # | Rule |
|---|---|
| R1 | Placement vocabulary is valid only inside a placement container; a flow container's child accepts the cross-axis subset only. |
| R2 | Absent vocabulary means today's behaviour — zero migration for existing manifests. |
| R3 | The reference frame is the parent's **inner box** (after its `Padding`). |
| R4 | The ratio is a fraction of the parent's inner span; the element's own reference point comes from `AlignX`/`AlignY` (the pivot), never from the painted shape. |
| R5 | `OffsetX`/`OffsetY` accept `N%` or a bare number. Both are **layout data**, not density tokens — no third mechanism. |
| R6 | Contradictory combinations are refused at creation; there is no implicit precedence. |
| R7 | This line adds no responsive variant of the new vocabulary. Existing `Breakpoint`/`Narrow`/`NarrowHidden` cover today's cases. |

## 3. The envelope rule

R4 needs "the element's own box" defined. It is the arranged rect, and this is a rule about the existing
mechanism rather than a new one:

- **E1** The envelope is the arranged rect (`UiNode.Rect`) — not `ContentRect`, not the painted shape.
- **E2** No arrangement, no envelope: an element that was not arranged (hidden, Tab-hidden) keeps its node
  and its state and takes no placement.
- **E3** Content exceeding the envelope is an **overflow finding** on the existing fit-audit channel
  (`UiOverflowAxis`), never a larger envelope.

E3 is a hard constraint rather than tidiness: `UiInvalidation.Paint` deliberately does not re-arrange, so
an appearance-driven envelope would let a paint-class change move geometry — and make the invalidation
classes lie.

## 4. Refusals — what this plan will not do

- no paint-side inset or overhang that can change the envelope (E3)
- no expressions, no `calc`, no reference to a named sibling
- no percentage placement inside a flow container
- no new public type, no new style token, no appearance change at all
- no responsive variants of the new vocabulary in this line
- no solver and no second arrangement pass

## 5. Change surface

| Layer | File | Change | Size |
|---|---|---|---|
| Parser | `UiLayoutManifest.cs` | **nothing** — `UiElementSpec` attributes are generic key/value; vocabulary is checked by schema | 0 |
| Vocabulary | `UiHost.cs:880-891` | the four names into `CommonWidgetAttributes` / `ContainerAttributes` | ~4 lines |
| Vocabulary (mirror) | `UiLayoutEngine.cs:105-116` | the same names in the engine's mirrored lists (a lane reflects them and fails on drift) | ~4 lines |
| Engine | `UiLayoutEngine.cs` `MeasureOverlay` (:1260) | replace `OffsetBox(childBox, padding.Left, innerY)` with the resolved placement | main work |
| Engine | same file, new helper | `ResolvePlacement(spec, innerRect, size)` — one rule, both axes | ~30 lines |
| Engine | `ParsePadding` (:2312), `ReadGap` (:2344) | the CP-0 fallback | ~4 lines |
| Engine | `MeasureRow` / `MeasureStack` | CP-2, cross-axis only | moderate |
| Validation | beside the `Height` validator (A2) | the refusal matrix, located `UiContractException` | ~40 lines |
| Public types | — | **none** — values are attribute strings, so `docs/api-tiers.md` does not move | 0 |
| Appearance | — | **none** — alignment is a relation, not a look; no token, so no closure debt | 0 |
| Tests | a new lane + `KernelLayoutTests` | §6 | ~200 lines |
| Gate | `Program.cs` | lane registration (the existing lane forces it) | 2 lines |
| Docs | `05-api-contract.md` (second amendment, before it ships), then `docs/architecture.md` §3.2/§6.3, the consumer guide, `MEMORY.md`, `TODO.md` | contract before code, the 0.7 precedent | — |
| Version | — | **no move**: the axis stays `0.7.0` and the consumer range stays `[0.7.0,0.8.0)`; the vocabulary is a second amendment to `05-api-contract.md`, recorded before it ships | 0 |

**Obligations on the round's own records.** Two existing statements become false when CP-1 lands, and both
are amended in the same commit as CP-1 — recorded here now so neither is discovered as stale after the fact:

- `docs/development/0.7/README.md` states that "no public type, kind or XML vocabulary was added or removed
  on this line"; the README's statement and its package table take this line's second amendment.
- `Source/FerriteLib.UiKit/Kernel/FerriteLibVersion.cs`'s XML comment describes the `0.6.0 → 0.7.0` move
  as "one bounded supported contract, two peer built-in palettes … and the first validation tightenings".
  It gains the placement vocabulary. The axis **value** does not move: `Api` stays `new Version(0, 7, 0)`,
  which is what the ruling in the header above means by "no minor move".

## 6. Verification plan

| Lane | Asserts |
|---|---|
| placement matrix | every origin × ratio × pivot combination against an `Overlay`, including ratio 0 and 100% |
| refusal matrix | each refused combination throws at creation with the element path (the A2/A3 shape) |
| flow boundary | a flow container's child arranges exactly as before, and CP-2's cross-axis subset works |
| envelope guard | the probe kind that paints a 3px rail inside a 120px rect is placed by its rect, not by its paint; an overflowing element moves no sibling |
| density reach (CP-0) | a theme-only geometry change moves arranged rects; `Padding="0"` keeps the old result |
| registration | the new lane file is wired in `Program.cs` (already enforced) |

## 7. Open decisions

- **D1** CP-0's migration: accept it in one minor with the documented escape hatch, or make the token
  fallback opt-in (and pay a second mechanism)?
- **D2** Vocabulary shape: four attributes on the child (chosen: smallest vocabulary) against a `<Place>`
  sub-element (more verbose, room for per-edge data later).
- **D3** Ratio unit: parent inner span only, or allow a second ratio against the child's own span?
- **D4** Sibling-relative placement: keep out of this line (default), or register it as a named follow-up?
- **D6** CP-6 item 1: which `Tone` members stay authorable? Recommended — `{Neutral, Success, Warning,
  Danger}` remain authored **meanings**, while `Active` becomes a selected **state** and `Disabled` a
  data-derived **state**. The alternative that costs nothing to weigh: keep `Active` authorable (a page
  may want a highlighted row without a model behind it) and remove only `Disabled`, which is the one
  member reachable from two directions. Migration for the authored case: one minor of acceptance with a
  redirect, or an immediate refusal with a located contract error (the A2/A3 shape).
- **D7** CP-6 item 4: the accent's derived steps. One stored accent plus a lighten rule, or keep the hover
  step as an authored token while deleting only the duplicate *storage*? `HoverPoint` has one consumer
  (`LineChartWidget`), so the cheap answer is a derivation; the open part is what the rule is.
- **D5 — RESOLVED (maintainer confirmation 2026-09-18): (a).** A closed, code-owned role vocabulary on
  the element; vocabulary and meaning live in code, mapping and colour move to data. The alternatives are
  recorded below because they were weighed, not to keep them live. *What is `Tone`?* (a) A closed,
  code-owned **role vocabulary on the element**, with the three
  conflations fixed (CP-6): state separated from role, the role→surface mapping moved into the style
  document, and `Tone="Disabled"` no longer authorable. **Sharpened 2026-09-18** by the maintainer's
  "the theme must not change this, and the accent is one colour whose value is free" position: the split
  that position implies is *vocabulary and meaning in code* (any skin must honour every tone) against
  *mapping and colour in data* (what each tone is painted with, and what the accent is). Today the first
  half is right and the second is wrong: the mapping is code (`UiStyleTable.Cell`), which is what makes
  `Warning` and `Danger` indistinguishable. Provenance worth knowing: `UiStatusTone` first appears in
  this repository's root commit **only as a drawing-outlet parameter** (`UiThemeDraw`'s
  `StatusTreatment`/`StatusBadge`), and its definition was moved out of that outlet into the new
  `UiResolvedStyle.cs` by the "add the resolved style table" commit — so the vocabulary was born as a
  **paint argument** and later promoted to a **meaning**, which is exactly why it still carries
  `Active` and `Disabled` beside `Danger`. (b) **Named roles defined by the style document**,
  the element referencing one by name — accepting that role references become danglable like `Scheme`, and
  that the author vocabulary then differs from the stable audit vocabulary (`UiStatusTone`). (c) Leave the
  conflations as they are. Recommended: **(a)** — it keeps the one appearance reference that cannot dangle,
  keeps `UiStatusTone` stable for the audit surface, and still lets a skin separate `Warning` from
  `Danger`; (b)'s only gain, new role names, has no citation yet.

### CP-6 — split the tone axes and make the role mapping data  *(registered; blocked by D5)*

**Goal.** Fix the three conflations found while evaluating CP-4, without moving roles into the style
document:

1. **State leaves the authorable set.** `Disabled` and `Active` become states (derived from the bindings
   and from interaction), not values an author writes. Migration for the one authored case: a page writing
   `Tone="Disabled"` moves to its data side (`BindReadOnly` / `IsWritable`), or the library accepts the
   old name for one minor and redirects it to the derived answer.
2. **The role→surface mapping becomes data.** A style document may state which surfaces a tone resolves to,
   so a skin can separate `Warning` from `Danger` — the L2 debt recorded in `docs/architecture.md` §6.3 —
   without adding an enum member.
3. **A dangling-role rule is stated once**: a role is code-owned and must resolve in every skin; a
   `Scheme`/`Density` name is document-owned and may dangle with the existing recorded fallback.
4. **One accent, and it is a value not a system.** The accent is today **two stored tokens**:
   `AccentGold` and `HoverPoint`. `HoverPoint` has exactly one consumer in the whole tree — the line
   chart's hovered point (`LineChartWidget.cs:262`) — while everything else that names it is a copy, a
   token-list entry or a palette literal. `AccentWith(alpha)` already shows the derive-instead-of-store
   path exists, so the single-accent position is cheap here: keep one stored accent, derive its steps.
   Its contact with the tone system is also exactly one line: `SelectedSurface`'s default edge
   (`UiTheme.cs:201`, `SelectedBorder ?? AccentGold`) — the tone system does not otherwise depend on the
   accent, and the accent does not depend on the tone system.

**Status.** `proposed`; **unblocked by D5 = (a)**, but not yet implementable as written — two
sub-decisions are open (D6, D7) and **item 2 is blocked by CP-3**, because making the role→surface mapping
data requires the document to be able to express a *surface*, which is CP-3's vocabulary extension. Items
1, 3 and 4 are independent of CP-3 and can proceed first.

## 8. Registered elsewhere

The unclosed items in `docs/architecture.md` §6.3 stay there and are not this plan's: behaviour/painter
separation, published interaction state, declared input contract, control parts, focus, the image outlet,
per-part style keys, and the L1 closure lane.

## 9. Status log

| Date | Item | Change |
|---|---|---|
| 2026-09-18 | — | plan opened from the placement/alignment discussion; CP-0/CP-1/CP-2 `proposed`, nothing implemented |
| 2026-09-18 | D5 (resolved), CP-4 (closed), D6/D7 | **D5 = (a)** confirmed by the maintainer: vocabulary and meaning in code, mapping and colour in data. CP-4 is therefore closed with no work of its own. CP-6 is unblocked but not fully implementable: D6 (which members stay authorable) and D7 (the accent's derived steps) are open, and CP-6 item 2 waits on CP-3's vocabulary |
| 2026-09-18 | D5, CP-6 | Sharpened by the maintainer's position ("the theme must not change this; the accent is one colour whose value is free"): vocabulary and meaning stay in code, mapping and colour move to data. Provenance recorded — `UiStatusTone` was born as a **drawing-outlet parameter** and promoted to a meaning, which is why it conflates state with role. CP-6 gains the accent item: two stored accent tokens today, one consumer for the second |
| 2026-09-18 | CP-4, D5, CP-6 | CP-4 revised: the "role moves into the style document" half is **withdrawn** — `Tone` is the only appearance reference that cannot dangle, and the capability it would buy has no citation. The measurement (a role vocabulary that conflates state, interaction and meaning, with `Disabled` reachable from two directions) became D5, and its fixes became CP-6 |
| 2026-09-18 | CP-3..CP-5 | registered from the "layout file + style file" discussion: the skin-source axis, moving the role half of appearance into the style document, and regional scope. All `proposed`; the maintainer allowed splitting and breaking changes in this fast-development window |
| 2026-09-18 | CP-0..CP-2 | **version axis settled**: the work stays on the 0.7.x line (no minor move; range stays `[0.7.0,0.8.0)`), because the line has never shipped and nothing is consumer-compiled against it. The plan moved from `docs/development/0.8/` to `docs/development/0.7/10-change-plan.md`, and the round-README amendment became an obligation of CP-1 |

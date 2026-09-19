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

## 8. Registered elsewhere

The unclosed items in `docs/architecture.md` §6.3 stay there and are not this plan's: behaviour/painter
separation, published interaction state, declared input contract, control parts, focus, the image outlet,
per-part style keys, and the L1 closure lane.

## 9. Status log

| Date | Item | Change |
|---|---|---|
| 2026-09-18 | — | plan opened from the placement/alignment discussion; CP-0/CP-1/CP-2 `proposed`, nothing implemented |
| 2026-09-18 | CP-0..CP-2 | **version axis settled**: the work stays on the 0.7.x line (no minor move; range stays `[0.7.0,0.8.0)`), because the line has never shipped and nothing is consumer-compiled against it. The plan moved from `docs/development/0.8/` to `docs/development/0.7/10-change-plan.md`, and the round-README amendment became an obligation of CP-1 |

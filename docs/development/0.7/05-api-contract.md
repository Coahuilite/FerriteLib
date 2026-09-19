# 0.7 — the supported contract for the subtraction-and-stabilization line

Written before any behavior change on this branch, per the 0.7 plan. This file records what 0.7.x commits
to; `docs/api-tiers.md` keeps the per-type tier membership, which this line does not change. The consumer
assertion range for the line is `[0.7.0, 0.8.0)`.

## Public surface

No new public types, widget kinds, or XML vocabulary land in 0.7. No signature changes on existing
members. The API-tier *type* list stays the 0.6 pin.

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

## The selected ordinary-authoring path (B)

The recommended author route is existing surface only: one layout manifest over public atoms/containers
(`input/slider`, `input/number-field`, `input/dropdown`, text/section/wrap/scroll containers), typed
bindings through `UiBindings` (`BindValue`, `BindReadOnly`, `BindOptions`), explicit `NotifyChanged` from an
owned mutation path on an authoritative plain C# model, and `UiWindowHost`/`UiPageWindow` for the window.
`UiNotifyAdapter` + `INotifyPropertyChanged` stays optional. No VM base class, no builder DSL, no second
state store becomes part of the contract. The runnable reference lives in the harness fixture area and the
prose guide in `docs/consumers/ordinary-settings.md`.

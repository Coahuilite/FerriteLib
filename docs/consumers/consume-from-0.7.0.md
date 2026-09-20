# Consume FerriteLib from another mod — 0.7.0

**Read this first if you are wiring another project against the 0.7 line.** It replaces
`consume-from-0.6.0.md` as the entry document (the 0.6 file stays as versioned history). Companion docs:
`docs/api-tiers.md` (compat promise), `ordinary-settings.md` (the recommended authoring recipe),
`docs/development/0.7/05-api-contract.md` (what 0.7.x commits to),
`docs/development/0.5/20-api-and-xml.md` + `docs/development/0.6/20-api-and-xml.md` (the surface itself).

## 1. Which artefact to take

| | |
| --- | --- |
| Dev package folder | `dist/dev/FerriteLib/` (5 files) — staged by `scripts/verify-local.ps1 -PackDev` |
| version.txt | `FerriteLib 0.7.0-dev / build=dev / commit=88095fb3cbed` |
| DLL | `1.6/Assemblies/FerriteLib.UiKit.dll`, SHA-256 `271128299A9CFF2C4CB5EBDFBB9246C3F6BEC2791A5D0117B894E6F96FC8F82F` |

Published integration goes through the GitHub Release asset once the maintainer cuts one; the dev folder is
the rehearsal, not the publication. **Do not copy the DLL into your package** — `<HintPath>` +
`<Private>false</Private>`; only `coahuilite.ferritelib` ships it. Registry-scope, stale-build and
machine-path traps are unchanged from 0.6 (§3–4 of `consume-from-0.6.0.md` still apply verbatim).

## 2. Assert the version range

```csharp
// Mod constructor, before touching any UiKit type:
FerriteLibVersion.Require(new Version(0, 7, 0), new Version(0, 8, 0));
```

Axes: `FerriteLibVersion.Api` = `<modVersion>` = `<VersionPrefix>` = 0.7.0. Pre-1.0, a minor bump is the
breaking signal; 0.7.x will not move a public signature or a documented behavior again without the next
appropriate minor.

## 3. What actually changed for you at 0.7.0

- **Public surface:** one kind was **added** inside `0.7.0` (B7): `input/text-field`, the single-line
  string sibling of `input/number-field`, carried by a new public-unstable funnel member
  `UiNative.TextField(rect, key, session, value, out committed)`; and `input/mode-row` gained **per-option
  hover help** — the new manifest attribute `HoverHelpKey` plus `UiNative.IsMouseOver(rect, ctx)`. Nothing was
  removed and no existing member signature changed. `UiTheme` (public-unstable) gained one named factory,
  `Vanilla`, so the two built-in palettes are peers.
  **Added XML vocabulary (Batch 1, §4b):** the four placement attribute names `AlignX`, `OffsetX`,
  `AlignY`, `OffsetY`. **Removed:** the stored token `UiTheme.HoverPoint`, replaced by the derived
  `UiTheme.AccentHover`.
- **Two peer palettes, no constructor default.** Pass `UiTheme.Vanilla` (neutral surfaces, reserved
  yellow) or `UiTheme.DarkGold` (warm gold, shared-border edges) at the existing required theme
  parameter. They are two looks, not a default and its history. `new UiTheme()` is an unpainted token
  bag — do not use it as a product skin. Start from a named factory and apply overrides.
- **Two validations tightened**: a malformed/non-finite `Height` now throws an attributable
  `UiContractException` at host creation and at reload-candidate validation instead of a `FormatException`
  at arrange time; `Cols`/`NarrowCols` on a non-`Wrap` container are refused at creation (they never did
  anything there — remove the attribute or switch to `wrap`).
- **Four behavior fixes**: a Row child with `Width="Auto"` that cannot be label-measured now shares space
  like an unsized child instead of collapsing to a 1-unit stub;
  **the migration for that one, stated properly because the obvious advice is wrong** — if what you relied on
  was not the stub but the fact that *the same element behaves differently in the two container shapes*
  (a collapsed filler in the wide `Row` that becomes full width once `Narrow="Column"` swaps the container),
  then giving it a fixed `Width` pins the narrow state too and does **not** reproduce it. The supported route
  is **two mutually exclusive presentations plus `VisibleKey`** — one child for the wide shape, one for the
  narrow, each visible under its own binding. A narrow-only *child* has no single-attribute form today; that
  gap is registered as `WideHidden` (symmetric with the existing `NarrowHidden`) and is not in this line;
  a dropdown shows the option whose **value**
  equals the bound value even when an earlier option's display text matched; a dropdown whose options
  key was registered as a value (`BindReadOnly<IReadOnlyList<T>>`) says so and names `BindOptions<T>`;
  and a `input/mode-row` with `Width="Auto"` now measures its **titles only** — `Description1..8` stay legal
  attributes that still create, but they left the kind's *label set* (never its schema) because nothing draws
  them, so they no longer widen the column. If an Auto mode-row was relying on a description for its width,
  declare the width explicitly.
  Migration detail for each: `docs/development/0.7/05-api-contract.md`.

## 4. The recommended way to author a settings page

`ordinary-settings.md` — existing atoms and containers, typed bindings, a plain C# model with one explicit
`NotifyChanged` path, and the existing window host. The compile-checked reference is
`tools/FerriteLib.UiKit.Tests/KernelOrdinarySettingsRecipeTests.cs`. MVVM and `UiNotifyAdapter` stay
optional, not prerequisites.

## 4b. Batch 1 — the breaks to absorb before you test

Recorded in `docs/development/0.7/05-api-contract.md` under "Batch 1", all inside `0.7.0`. Batch 1's three
were one visual review, one page edit and one member swap; the added kind below is an addition, so it needs no
migration at all.

| Change | What you do |
|---|---|
| Container `Padding` **and** `Gap` now default to the theme's geometry instead of 0 | Review every page's spacing. Both built-in palettes carry `Padding = Gap = 6`, so an undeclared container moves from 0 to 6. An attribute you already declare keeps winning — a container with an explicit `Gap` is unaffected by the `Padding` fallback and vice versa. Add `Padding="0"` / `Gap="0"` where you want the previous result exactly. **Only those two tokens participate**: `Spacing`, `RowHeight` and `Hairline` stay widget-internal and never become a container default. |
| `Tone` accepts only `Neutral`/`Success`/`Warning`/`Danger` | If a page wrote `Tone="Active"` or `Tone="Disabled"`, express the **state** instead: a read-only value binding for disabled, the control's own selected state for active. Both old names still work **for this minor only** and are refused at the next minor boundary (0.8). The only observable consequence today is **one appearance record per runtime element** (a collection row is its own element) — never one per frame, and the rendered treatment is unchanged. |
| `UiTheme.HoverPoint` removed, replaced by `UiTheme.AccentHover` | Read `UiTheme.AccentHover` (read-only, derived from `AccentGold`). A scheme that declared `HoverPoint` now reports it as an unknown token instead of applying it. |
| New: `input/text-field` | Optional, no migration — an addition, not a break. A single-line string field whose focus, draft and commit rule belong to the element: `Bind` (required), `Live` (default `true`; `false` defers the write until focus leaves), `Placeholder`/`PlaceholderKey`, `Label`/`LabelKey`, `Height`. A read-only binding refuses the write and the engine's disabled rule refuses the input, exactly as on `input/number-field`. If you hand-rolled one over `UiNative.TextField(rect, text)`, this is the supported shape now. |
| New: `HoverHelpKey` on `input/mode-row` | Optional, no migration — and no tooltip: declare `HoverHelpKey="my-help"`, bind it with `BindValue<string>`, and the row writes **which option is hovered** there — the option's `DescriptionN`, or its `ValueN` when it declares none — clearing it (empty string) when the pointer leaves. You render the help yourself, wherever your page wants it; the row publishes only when the answer changes. One tightening to know: a `TitleN`/`DescriptionN` whose index has no `ValueN` used to be ignored and is now refused at creation. |
| New: `AlignX`/`OffsetX`/`AlignY`/`OffsetY` | Optional, no migration. Inside an `Overlay` they place a child by an edge or the centre plus a percentage of the parent's usable width; a flow container's child gets its cross axis with a pixel nudge only. |

Nothing else in the compiled surface moved: no type or kind was **removed**, no existing member signature changed,
and **no `UiStatusTone` member was removed** (that type is stable — `Active` and `Disabled` remain members the
library uses internally as states).

## 5. What is still open (do not over-claim)

- **In-game and consumer acceptance for 0.7 is pending** — the four checks in
  `docs/development/0.7/README.md` §External acceptance are owned by the maintainer/consumer operator and
  include one comparable Vanilla/DarkGold/custom state sheet. Library evidence is automated only.
- Known limits inherited from 0.6: main-thread-only notification delivery; IME composition outside the
  input deferral; no cross-sibling-window atomicity for consumer hooks; catalogue is a snapshot.
- If the public surface forces you to hand-roll something, report it — a cited consumer need is what earns
  a capability; a request is not.

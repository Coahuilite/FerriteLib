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

- **Public surface:** no types, kinds or XML vocabulary added or removed; tiers unchanged. `UiTheme`
  (public-unstable) gained one named factory, `Vanilla`, so the two built-in palettes are peers.
- **Two peer palettes, no constructor default.** Pass `UiTheme.Vanilla` (neutral surfaces, reserved
  yellow) or `UiTheme.DarkGold` (warm gold, shared-border edges) at the existing required theme
  parameter. They are two looks, not a default and its history. `new UiTheme()` is an unpainted token
  bag — do not use it as a product skin. Start from a named factory and apply overrides.
- **Two validations tightened**: a malformed/non-finite `Height` now throws an attributable
  `UiContractException` at host creation and at reload-candidate validation instead of a `FormatException`
  at arrange time; `Cols`/`NarrowCols` on a non-`Wrap` container are refused at creation (they never did
  anything there — remove the attribute or switch to `wrap`).
- **Three behavior fixes**: a Row child with `Width="Auto"` that cannot be label-measured now shares space
  like an unsized child instead of collapsing to a 1-unit stub; a dropdown shows the option whose **value**
  equals the bound value even when an earlier option's display text matched; and a dropdown whose options
  key was registered as a value (`BindReadOnly<IReadOnlyList<T>>`) says so and names `BindOptions<T>`.
  Migration detail for each: `docs/development/0.7/05-api-contract.md`.

## 4. The recommended way to author a settings page

`ordinary-settings.md` — existing atoms and containers, typed bindings, a plain C# model with one explicit
`NotifyChanged` path, and the existing window host. The compile-checked reference is
`tools/FerriteLib.UiKit.Tests/KernelOrdinarySettingsRecipeTests.cs`. MVVM and `UiNotifyAdapter` stay
optional, not prerequisites.

## 4b. Batch 1 — the breaks to absorb before you test

Recorded in `docs/development/0.7/05-api-contract.md` under "Batch 1", all inside `0.7.0`. One is visual
review, one is a page edit, one is a member swap.

| Change | What you do |
|---|---|
| Container `Padding`/`Gap` now default to the theme's geometry (`6`) instead of 0 | Review every page's spacing. Add `Padding="0"` / `Gap="0"` where you want the previous result exactly. |
| `Tone` accepts only `Neutral`/`Success`/`Warning`/`Danger` | If a page wrote `Tone="Active"` or `Tone="Disabled"`, express the **state** instead: a read-only value binding for disabled, the control's own selected state for active. Both old names still work **for this minor only** and are refused at the next minor boundary (0.8). The only observable consequence today is **one appearance record per runtime element** (a collection row is its own element) — never one per frame, and the rendered treatment is unchanged. |
| `UiTheme.HoverPoint` removed, replaced by `UiTheme.AccentHover` | Read `UiTheme.AccentHover` (read-only, derived from `AccentGold`). A scheme that declared `HoverPoint` now reports it as an unknown token instead of applying it. |
| New: `AlignX`/`OffsetX`/`AlignY`/`OffsetY` | Optional, no migration. Inside an `Overlay` they place a child by an edge or the centre plus a percentage of the parent's usable width; a flow container's child gets its cross axis with a pixel nudge only. |

Nothing else in the compiled surface moved: no type or kind was added or removed, no member signature changed,
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

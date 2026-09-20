# Consume FerriteLib from another mod — 0.7.0

**Read this first if you are wiring another project against the 0.7 line.** It replaces
`consume-from-0.6.0.md` as the entry document (the 0.6 file stays as versioned history). Companion docs:
`docs/api-tiers.md` (compat promise), `ordinary-settings.md` (the recommended authoring recipe),
`docs/development/0.7/05-api-contract.md` (what 0.7.x commits to),
`docs/development/0.5/20-api-and-xml.md` + `docs/development/0.6/20-api-and-xml.md` (the surface itself).

> **Coming from 0.4 (or older)?** §4c is the one behaviour change in this line that does not fail to
> compile: since 0.5, removing an element from the definition releases its state. Read it before you delete an
> element from a page.

## 1. Which artefact to take

| | |
| --- | --- |
| Dev package folder | `dist/dev/FerriteLib/` (5 files) — staged by `scripts/verify-local.ps1 -PackDev` |
| version.txt | `FerriteLib 0.7.0-dev / build=dev / commit=88095fb3cbed` — **a rehearsal identity of one working tree, not a target** |
| DLL | `1.6/Assemblies/FerriteLib.UiKit.dll`, SHA-256 `271128299A9CFF…FC8F82F` — **the same rehearsal, quoted as history** |

**Which payload is authoritative, because more than one exists (FL-11).** The **GitHub Release asset** published
from `coahuilite.ferritelib` is the only payload identity a consumer can verify: each release body names the
commit it was built from and the asset's SHA-256. The dev folder above, a **sibling checkout's copy**, and
whatever happens to sit in `1.6/Assemblies/` after a build are all rehearsals: their identity is a property of
one machine's tree at one moment, and two of them are *not* two identities for one artifact. Concretely, the 0.6
docs recorded a **Dev-channel** rehearsal (`build=dev`, one commit) while a consumer's `<HintPath>` bound a
**Release** build of a different commit — the byte difference is the configuration and the commit, not a
different artifact. So: take the artifact from the Release page, and **verify what you actually bound at
runtime** — the assembly's `AssemblyConfiguration`, its embedded commit, and the `Require` verdict — rather
than matching a hash written in a document. **Do not copy the DLL into your package** — `<HintPath>` +
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
  `UiNative.TextField(rect, key, session, value, out committed)`; and the **help vocabulary** grew: the
  engine-wide element attribute `HelpKey`, the option-level `HoverHelpKey` (now on `input/dropdown` as well
  as `input/mode-row`), `input/mode-row`'s `TitleKey1..8`, and `UiNative.IsMouseOver(rect, ctx)`. Nothing was
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
| New: `HelpKey` on any widget element | Optional, no migration. Declare the identity your help catalog is keyed by (a literal, never translated) and the **engine** claims it while the pointer is over that element: read `Session.HoverClaim` for the identity and `Session.HoverClaimElement` for which element claimed it — the same pair you already read. Refused on containers and template roots, which are not hit surfaces. A disabled element still claims: the help explains why it is unavailable. |
| New: `input/mode-row` `TitleKey1..8` | Optional, no migration. The translated sibling of `Title1..8` (key wins), and `Width="Auto"` now measures the translated title. An orphan `TitleKeyN`/`TitleN`/`DescriptionN` (no `ValueN`) is refused at creation. |
| `HoverHelpKey` now works on `input/dropdown` too | Optional, no migration. Same attribute, same rule, applied to the popup's rows: the hovered row's **value** is published, cleared when nothing is hovered. |
| `Tab` now works on containers | Optional, no migration — a coherence fix, not new surface: the engine always read `Tab` when deciding visibility, and only the container contract left the name out. A container can now be declared to appear on one tab, and it takes its whole subtree with it when the tab is inactive (a hidden element keeps its node and state, as with `Visible`/`Hidden`). Per-row `Tab` inside a template stays inexpressible by design — a tab is a page-level answer. |
| New: `PayloadKey` on `input/button` | Optional, no migration. Hands the command the bound payload (inside a template it is scoped per item, so a row reports its own key). With it, bind the command as `BindAction<string>`; without it everything is unchanged. |
| New: `Chrome="none"` + `Height="Auto"` on `input/button` | Optional, no migration. A bare hit area: no surface is painted, the hit test still fires, and `Auto` takes the measured content band. Any other `Chrome` value is refused at creation. |
| New: `WideHidden` | Optional, no migration. The mirror of `NarrowHidden`: the element is arranged only while the state is wide. Refused at creation when its parent carries no `Breakpoint`, exactly like `NarrowHidden`. |
| New: `WidthKey` | Optional, no migration. A float value binding answers the declared width when no numeric `Width` is written (a written `Width` wins); announcing the key re-arranges the element, and a key that cannot answer records one appearance note and leaves the element unsized. |
| New: `SelectedKey` | Optional, no migration. A bool binding that paints the element's **active** treatment while it answers true, so a selected row does not need its `Tone` re-authored per state. Honoured by the role-resolving kinds (the atoms and `chrome/banner`). |
| `chrome/banner` takes `Tone`/`Emphasis` | Optional, no migration — an untone banner paints the same secondary ink it always did; an authored `Tone` now moves it. |
| New: `AlignX`/`OffsetX`/`AlignY`/`OffsetY` | Optional, no migration. Inside an `Overlay` they place a child by an edge or the centre plus a percentage of the parent's usable width; a flow container's child gets its cross axis with a pixel nudge only. |

Nothing else in the compiled surface moved: no type or kind was **removed**, no existing member signature changed,
and **no `UiStatusTone` member was removed** (that type is stable — `Active` and `Disabled` remain members the
library uses internally as states).

## 4c. Removing an element releases its state — the behaviour change that cannot fail to compile

**This is a migration section, not a 0.7 change.** The semantic inversion arrived in the **0.4 → 0.5** move and
has continued through 0.6 and 0.7; it is **not** a 0.6 regression and it is not a reason to restore the old
behaviour. Verified at the tips, not asserted: `PruneNodesExcept` is present in `0.5.x`
(`git show 0.5.x:Source/FerriteLib.UiKit/Kernel/UiSession.cs` → 2 hits) and absent in `0.4.x` (0 hits), and the
introducing commit `0ab9015` is an ancestor of `0.5.x`.

**(a) What changed.** In 0.4, taking an element out of the definition (or changing its kind under an Id that used
to be stable) left its node alive and merely unpainted. Since 0.5 the arrange **releases** every identity the
definition no longer declares, and everything that node owned goes with it: its sub-nodes, its state slots, its
scroll position, its dirty and recovery records, any hit layer it still occupied, and the widget instance keyed by
it. Source: `UiSession.PruneNodesExcept`, `Source/FerriteLib.UiKit/Kernel/UiSession.cs:477-513` (the release
list at `:504-513`), called from the arrange at `UiLayoutEngine.cs:304` and, for a collection, through the same
pass (`UiLayoutEngine.cs:1631`). A **declared-but-hidden** element is explicitly *not* released — hidden is not
removed (`UiSession.cs:463-468`).

**(b) What it affects.** Anything you kept in the tree under that element's identity: a text draft, a scroll
offset, focus/selection, a per-element recovery record, a sub-node's state, a re-subscription. After a removal and
a re-add the element exists again with **fresh** state. Nothing warns you: the page compiles, the manifest is
valid, and the only symptom is state that resets — which is exactly why this section exists.

**(c) How to detect it in your own page.** Compare node/state on either side of the removal with
`session.GetNodeByElementId("your-id")`: it answers **non-null while the element is still declared** (including
while `Visible`/`VisibleKey`/`Tab` hides it) and **null once the identity is gone from the definition** — the
lookup is about identity, not arrangement (`UiSession.cs:405-419`). If you need the state to survive, (c) is
where you find out that it will not.

**(d) The supported paths.**
- **Temporarily hiding an element** — use `Visible`, `VisibleKey` or `Tab`. A declared-but-hidden element keeps
  its node and its state on purpose, so a draft or a scroll position stays reachable (`UiSession.cs:463-468`,
  `UiSession.cs:411-413`).
- **Really removing an element** — accept the release: that *is* the semantic, and the prune exists because the
  old behaviour leaked a stale node and orphaned state on every reload (`UiSession.cs:457-462`). Do not restore
  the leak to preserve state.
- **State that must survive a real removal** — hold it in **your model**, keyed by your own business identity
  (an item key, a def name, a settings key), not by the tree path, and re-seed the element when you add it back.
  The tree is a projection of your model; it is not the model's store.

## 5. What is still open (do not over-claim)

- **In-game and consumer acceptance for 0.7 is pending** — the four checks in
  `docs/development/0.7/README.md` §External acceptance are owned by the maintainer/consumer operator and
  include one comparable Vanilla/DarkGold/custom state sheet. Library evidence is automated only.
- Known limits inherited from 0.6: main-thread-only notification delivery; IME composition outside the
  input deferral; no cross-sibling-window atomicity for consumer hooks; catalogue is a snapshot.
- **Reading an overflow verdict (FL-21).** `UiOverflowReport.Available` is the **inset label rect** the widget
  measured into and `Needed` is the measured content it could not hold — an overflow is about the **label band**,
  not about the element's height. A consumer that renders "has N px" is quoting the band; say so, or name the
  element's own rect separately.
- **A fit-audit count is not a census (FL-22).** Findings are deduplicated by key (path, text, font, axis,
  needed, available), capped at `MaxReports = 48` with `Saturated` published when the cap is hit, and
  `Reset()` starts a new collection. Log lines count **distinct findings**, never elements, so an "xN" you print
  is a count of distinct reports and nothing else.
- If the public surface forces you to hand-roll something, report it — a cited consumer need is what earns
  a capability; a request is not.

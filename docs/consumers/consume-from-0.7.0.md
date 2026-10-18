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

**0.7.x is frozen.** Select an explicitly identified package, not an arbitrary DLL whose API number is
0.7.0. A published GitHub Release asset is the public distribution; a named Dev rehearsal is suitable for
its agreed local validation only. The conditional `v0.7.0-rc1` route was decided on 2026-10-09; the
[historical record](../development/0.7/README.md) is not proof that an asset has been published.

The installable payload is `1.6/Assemblies/FerriteLib.UiKit.dll` inside the selected package. Its
`version.txt`, configuration and embedded source identify that build. Dev and Release can share the API
number and still be different builds. Source HEAD, a held package and the root compatibility carrier are
not interchangeable. Build/export commands are in [build-and-debug.md](../build-and-debug.md).

Reference the chosen DLL with `<Private>false</Private>` and never copy it into the consumer package.
After replacing a path-resolved DLL, rebuild the consumer with `--no-incremental`. Reference setup and
registry scope are described in [the 0.6 guide](consume-from-0.6.0.md) §3–4 and remain applicable.

## 2. Assert the version range

At the consumer's early initialization boundary, inspect the result before constructing the page:

```csharp
bool compatible = FerriteLibVersion.Require(
    new Version(0, 7, 0), new Version(0, 8, 0), "your.mod.packageid", out string diagnostic);
// If false, report diagnostic and do not construct the UiKit page.
```

`Require` returns a verdict; it is not a two-parameter assertion that throws on mismatch. The game cannot
express this dependency range. API tiers are the compatibility promise; the coordinated 0.7 development
window allowed documented additions without moving the minor and does not transfer that exemption to 0.8.

## 3. Absorb the changes that affect existing consumers

- **Theme:** `new UiTheme()` now falls back per unassigned token to `UiTheme.Vanilla`. Explicit transparent
  colours stay transparent. The earlier unpainted-bag/two-peer-factory contract was superseded: the product
  `UiTheme.DarkGold` factory was removed; a consumer wanting that palette supplies its own token values.
  `AccentHover` replaces the removed `HoverPoint`. See contract §R2.
- **Bindings:** a hand-written `IUiBindings` implementation must implement `TryInvokeCommand`; adding that
  interface member is a source compatibility change. The library's `UiBindings` implements it. See §FL-IC2.
- **Input:** popup coverage is geometric, including over the popup's own trigger. The window's Accept answers
  an active edit; Cancel answers menu, capture, edit, then the nearest executable `CancelBind`, one layer per
  press. No `CancelBind` declaration does not mean native close wins while a library interaction is open.
  Custom shell cancel policy uses `TryHandleUnansweredCancel()` after the page declines. A shell with
  `closeOnCancel=false` must opt into Verse's `forceCatchAcceptAndCancelEventEvenIfUnfocused` if it needs those
  hooks. Multi-window attribution remains subject to the real window stack. See §FL-IC1/FL-IC2.
- **Deferred editing:** `Live="false"` fields publish a valid draft on the edit-ending pass; an invalid
  numeric draft is not coerced into a value. The frozen 0.7 behavior is not the 0.8 proposal for retaining
  invalid drafts or handling multiline/IME composition.
- **Layout and vocabulary:** the table below retains the migration details for spacing, authored tones,
  placement and measured sizing. Invalid `Height` is rejected at creation; `Cols`/`NarrowCols` are Wrap-only.
  A Row Auto child without a measurable label now shares space rather than collapsing to a 1-unit stub.
  A fixed width cannot reproduce separate wide/narrow behavior: use visibility variants when that is required.
- **Options and labels:** exact option value beats display-text fallback; wrong-kind options registration
  names `BindOptions<T>`. Mode-row `Description1..8` carry help identity, not measured title width.
  `SelectedKey` inside a repeated template resolves in the item's scope, not against a page-level decoy.

Details and refusal rules: [0.7 contract](../development/0.7/05-api-contract.md).

## 4. The recommended way to author a settings page

`ordinary-settings.md` — existing atoms and containers, typed bindings, a plain C# model with one explicit
`NotifyChanged` path, and the existing window host. The compile-checked reference is
`tools/FerriteLib.UiKit.Tests/KernelOrdinarySettingsRecipeTests.cs`. MVVM and `UiNotifyAdapter` stay
optional, not prerequisites.

## 4b. Batch 1 — the breaks to absorb before you test

The table collects optional vocabulary and the migration-sensitive changes across the line. Exact refusal
rules live in [the contract](../development/0.7/05-api-contract.md); later corrections in §3 take precedence
over earlier batch descriptions.

| Change | What you do |
|---|---|
| Container `Padding` **and** `Gap` now default to the theme's geometry instead of 0 | Review every page's spacing. The default geometry carries `Padding = Gap = 6`, so an undeclared container moves from 0 to 6. An attribute you already declare keeps winning — a container with an explicit `Gap` is unaffected by the `Padding` fallback and vice versa. Add `Padding="0"` / `Gap="0"` where you want the previous result exactly. **Only those two tokens participate**: `Spacing`, `RowHeight` and `Hairline` stay widget-internal and never become a container default. |
| `Tone` accepts only `Neutral`/`Success`/`Warning`/`Danger` | If a page wrote `Tone="Active"` or `Tone="Disabled"`, express the **state** instead: a read-only value binding for disabled, the control's own selected state for active. Both old names still work **for this minor only** and are refused at the next minor boundary (0.8). The only observable consequence today is **one appearance record per runtime element** (a collection row is its own element) — never one per frame, and the rendered treatment is unchanged. |
| `UiTheme.HoverPoint` removed, replaced by `UiTheme.AccentHover` | Read `UiTheme.AccentHover` (read-only, derived from `AccentGold`). A scheme that declared `HoverPoint` now reports it as an unknown token instead of applying it. |
| New: `input/text-field` | Optional, no migration — an addition, not a break. A single-line string field whose focus, draft and commit rule belong to the element: `Bind` (required), `Live` (default `true`; `false` defers the write until focus leaves), `Placeholder`/`PlaceholderKey`, `Label`/`LabelKey`, `Height`. A read-only binding prevents model writes; the current wrapper checks this after native editing, so it does not guarantee a non-editable but selectable draft. If you hand-rolled one over `UiNative.TextField(rect, text)`, this is the supported shape now. |
| New: `HoverHelpKey` on `input/mode-row` | Optional, no migration — and no tooltip: declare `HoverHelpKey="my-help"`, bind it with `BindValue<string>`, and the row writes **which option is hovered** there — the option's `DescriptionN`, or its `ValueN` when it declares none — clearing it (empty string) when the pointer leaves. You render the help yourself, wherever your page wants it; the row publishes only when the answer changes. One tightening to know: a `TitleN`/`DescriptionN` whose index has no `ValueN` used to be ignored and is now refused at creation. |
| New: `HelpKey` on any widget element | Optional, no migration. Declare the identity your help catalog is keyed by (a literal, never translated) and the **engine** claims it while the pointer is over that element: read `Session.HoverClaim` for the identity and `Session.HoverClaimElement` for which element claimed it — the same pair you already read. Refused on containers and template roots, which are not hit surfaces. A disabled element still claims: the help explains why it is unavailable. |
| New: `input/mode-row` `TitleKey1..8` | Optional, no migration. The translated sibling of `Title1..8` (key wins), and `Width="Auto"` now measures the translated title. An orphan `TitleKeyN`/`TitleN`/`DescriptionN` (no `ValueN`) is refused at creation. |
| `HoverHelpKey` now works on `input/dropdown` too | Optional, no migration. Same attribute, same rule, applied to the popup's rows: the hovered row's **value** is published, cleared when nothing is hovered. |
| `Tab` now works on containers | Optional, no migration — a coherence fix, not new surface: the engine always read `Tab` when deciding visibility, and only the container contract left the name out. A container can now be declared to appear on one tab, and it takes its whole subtree with it when the tab is inactive (a hidden element keeps its node and state, as with `Visible`/`Hidden`). Per-row `Tab` inside a template stays inexpressible by design — a tab is a page-level answer. |
| New: `UiOption` pairs on `input/dropdown` | Optional, no migration. `BindOptions<UiOption>` gives each dynamic option a display text and a committed value (`new UiOption(text, value)`); `BindOptions<string>` keeps working with display == value. The later typed-choice path also accepts `UiChoice<T>` options beside a matching `T` value binding; see `api-tiers.md` for `UiChoice` and `IUiTypedChoices`. |
| New: `PayloadKey` on `input/button` | Optional, no migration. Hands the command the bound payload (inside a template it is scoped per item, so a row reports its own key). With it, bind the command as `BindAction<string>`; without it everything is unchanged. |
| New: `Chrome="none"` + `Height="Auto"` on `input/button` | Optional, no migration. A bare hit area: no surface is painted, the hit test still fires, and `Auto` takes the measured content band. Any other `Chrome` value is refused at creation. |
| New: `WideHidden` | Optional, no migration. The mirror of `NarrowHidden`: the element is arranged only while the state is wide. Refused at creation when its parent carries no `Breakpoint`, exactly like `NarrowHidden`. |
| New: `WidthKey` | Optional, no migration. A float value binding answers the declared width when no numeric `Width` is written (a written `Width` wins); announcing the key re-arranges the element, and a key that cannot answer records one appearance note and leaves the element unsized. |
| New: `SelectedKey` | Optional, no migration. A bool binding that paints the element's **active** treatment while it answers true, so a selected row does not need its `Tone` re-authored per state. Honoured by the role-resolving kinds (the atoms and `chrome/banner`). **Inside a `<Repeat>` template it is item-scoped** like `Bind`/`ActionBind`/`OptionsBind`/`VisibleKey`/`PayloadKey`: declare `SelectedKey="selected"` in the template and bind `items.<key>.selected` per row, so the row that answers true — and only that row — goes active. The `Active` treatment takes `TextOnGold` and **ignores `Emphasis`**, so declare it on the elements that should turn gold (typically the row title) and not on the row container or on a detail line meant to stay secondary. |
| `chrome/banner` takes `Tone`/`Emphasis` | Optional, no migration — an untone banner paints the same secondary ink it always did; an authored `Tone` now moves it. |
| New: `AlignX`/`OffsetX`/`AlignY`/`OffsetY` | Optional, no migration. Inside an `Overlay` they place a child by an edge or the centre plus a percentage of the parent's usable width; a flow container's child gets its cross axis with a pixel nudge only. |
| New: `Height="MatchContent"` | Optional, no migration. The element's height becomes the height its **parent's content** resolved to, and the element contributes nothing to that computation — so a hit area can match the text beside it instead of being hand-sized to a guess. Valid on a child of a `Row` or an `Overlay` (the two containers whose content height is the MAXIMUM over their children) that has a sibling not declaring it; refused at creation anywhere else, because a vertical container's content height is the SUM of its children and would include the declaring element. A `Row` that narrows into a column degrades it to the element's own content. |
| `Chrome="none"` on `section/header` | Optional, no migration. Suppresses the 1px divider this kind paints along its bottom edge and leaves the title, for a design that separates sections by whitespace. Any other `Chrome` value is refused at creation. The line's colour is the theme's `Divider` token, so a style document can restyle it — or make it transparent — without the switch. |
| New: `VisibleRows` on `Scroll` | Optional, no migration, and inert when absent. A positive finite row count (fractions allowed, e.g. `4.5`) that sizes the viewport from the SAME arrange round's measured rows: full entries up to floor(k), then the fraction of the NEXT entry's own measured height taken from its real position (the full gap before it stays, so a half row shows half a row), counted per materialized Repeat row - the Repeat expands transparently (nested repeats inside a row stay inside its span), never counted as one block, never double-counting a row's inner parts - while a hidden sibling (`VisibleKey`) contributes nothing, fewer rows than k answer natural (everything visible, no scrollbar reserved), and nothing countable answers natural too. A declared numeric `Height` - zero included - keeps its static priority exactly as before. The budget recomputes after the scrollbar reserves its width, so wrapped text is counted at the width its rows actually get; content, `VisibleKey`, order, language/font width and window resize re-budget through the existing invalidation. `4.5` is the page's number, never a library default; bad numbers and non-Scroll declarations are creation refusals. |
| New: `UiTheme.SwitchThumbOff` colour token | Optional, no migration. The switch thumb's OFF ink, declarable in the palette document exactly like every other colour role: `<Color Token="SwitchThumbOff" Value="…"/>` inside a scheme (grammar, resolver application and scoped clones all wired; the declaration is consumed by the same document->resolver->clone path the US palette already runs). Unset it answers the theme's own `TextPrimary` - the historical neutral thumb, unchanged for every bag and every scope that never declares it. Assign it to grey the OFF thumb without darkening any label; the ON half keeps answering `AccentGold` whatever this token says. |
| New: `Appearance` on `input/dropdown` (`field` / `selector`) | Optional, no migration, and pixel-identical when omitted — the default `field` draws exactly the plain status-toned field it always did. `Appearance="selector"` is the reserved-rail selector plane: a 3px left accent, an independent right arrow zone behind its own divider hairline, and the value text in what remains, all colours from the same resolved status ladder (`UiThemeDraw.SelectorAccentWidth` 3 / `SelectorArrowZoneWidth` 18 are the shape constants). An unknown name is refused at creation naming both accepted looks. A consumer's own dropdown-shaped composite paints the identical field through the ONE shared entry `UiThemeDraw.SelectorField(field, display, theme, style, font?)` — the core kind draws through that same method — plus `UiNative.DropdownButton` for the trigger: one popup, one anchor source, and no reserved slot for a help button in either look. |
| New (development builds only): the outline now covers everything the library draws (DT1) | No migration, additive to `GeometryOverlay`. A `Scroll`/`Clip` viewport's OWN band is outlined at its own entry (after its content, same rect, same retained rule), and the shell's own bands — window frame, title band, close affordance — outline through the same switch from the shell's single chrome arithmetic. Chrome also RECORDS into the same capture the moment it draws: the dump gains a `chrome space=window outer=… title=… close=… content=…` line (explicit none-state otherwise) and `UiDevGeometrySnapshot.ShellChrome` carries the completed pass — reports quote the record, never a present-tense read spliced onto an older dump. Nothing about existing paint behavior changed; Release payloads and unsubscribed hosts are untouched. |
| New: `UiWindowHost.ShellChrome` / `UiWindowChrome` | Public read-only geometry of the shell's own bands, window space, computed where the chrome is painted — the PRESENT measurement: use it to retire mirrored `TitleBarHeight`/`SidePadding` constants and to act on the window as it is now (read again after a drag/resize; it cannot be constructed outside the assembly). For a completed pass's chrome, read `UiDevGeometrySnapshot.ShellChrome` / the dump's `chrome` line instead. A small draggable developer overlay needs no new FL API — subclass `UiWindowHost`, apply `UiWindowOptions` (`Draggable`/`Resizeable`/`NormalSize`/`AllowMultipleInstances`); per-window outline/capture toggles are each subscription's own switch. |
| New (development builds only): the geometry instrument | Optional, no migration, and nothing to adopt in a release build. In a development build, `host.Diagnostics.GeometryEnabled = true` makes `host.Diagnostics.DumpGeometry()` return a diffable text dump: one line per arranged node with its arranged rect, the rect its widget was handed, that rect in the pointer's window space, the origin between those two spaces, its height mode (`Fixed`/`Auto`/`MatchContent`) and the height that mode resolved to, plus a `Scroll`/`Clip` viewport and its real content extent — then one line per sampled press (`hit`/`miss`/`covered`/`disabled`, with the element's path, the pointer and the queried rect). `host.Diagnostics.GeometryOverlay = true` outlines the drawn rect of every element that reaches the engine's per-entry draw step — since DT1 including scoped viewports' own bands and the shell's own chrome (see the DT1 row above). Since SA1.5 the switch is **independent**: with no capture it paints those draw-step entries while storing nothing (the dump and snapshot reader keep honestly reporting nothing), with a capture live it paints exactly the retained entries, and switching the capture off afterwards does not silence the picture. **On a release payload `GeometryEnabled` throws** rather than accepting the opt-in and then reporting nothing. |
| `Scheme`/`Density` names resolve from the document, and a missing name is now a located finding | No behaviour change; one new member (`UiStyleIssue.ElementPath`). A scheme is defined by the style document the Host was built with — a standalone `<Styles>` file or the manifest's `<Styles>` section. A palette applied to a theme *instance* is **not** a definition site: a scope naming a scheme only that palette defines looks applied while the resolver never sees it. An unknown name still falls back (fail-soft) and the finding now names the element that declared it, not just the document. |

The stable `UiStatusTone` enum retains `Active` and `Disabled`; the authored vocabulary and the enum are
separate contracts. Later interface and palette migrations are listed in §3.

**One theming coupling worth a line, because a track measurement has already tripped on it.**
`UiTheme.SelectedSurface.Border` is `SelectedBorder ?? AccentGold` — an *outline* whose fallback is the accent — so
a scope that assigns `SelectedBorder` (a flat scheme sets it equal to `Selected`, to paint no frame) **aliases that
fallback away**, and the border then reads as the fill rather than as gold. **If you mean the accent, ask for the
accent**: `theme.AccentGold`, or `UiThemeDraw.AccentRail(rect, theme, active, width)` with no colour — its own
fallback is `theme.AccentGold` (`UiThemeDraw.cs:150`), not the selected surface's border. The alias is introduced
by passing `SelectedSurface.Border` in **explicitly**; the rail itself is not the trap.

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
lookup is about identity, not arrangement. A surviving node proves that its storage was not pruned;
it does not prove that an edit or another live interaction continues unchanged.

**(d) The supported paths.**
- **Temporarily hiding an element** — use `Visible`, `VisibleKey` or `Tab`. Its declared identity, value slots
  and scroll-position storage are not pruned. **This is not a 0.7 draft-preservation guarantee:**
  `UiSession.ReconcileInteractionRecords` ends the hidden field's active edit without committing it, and
  `UiNative` resets an unfocused field's edit text from the model when it is drawn again. The same node can
  therefore return with a reset draft. Preserving an unfinished draft across temporary hiding is a
  **0.8 target contract**, not existing 0.7 behavior.
- **Really removing an element** — accept the release: that *is* the semantic, and the prune exists because the
  old behaviour leaked a stale node and orphaned state on every reload (`UiSession.cs:457-462`). Do not restore
  the leak to preserve state.
- **State that must survive a real removal** — hold it in **your model**, keyed by your own business identity
  (an item key, a def name, a settings key), not by the tree path, and re-seed the element when you add it back.
  The tree is a projection of your model; it is not the model's store.

## 5. What is still open (do not over-claim)

- **A bounded 2026-10-09 candidate received a maintainer game pass.** This is not blanket acceptance of
  every capability; see [the verification record](../development/0.7/40-verification.md) for scope and gaps.
- Known limits inherited from 0.6: main-thread-only notification delivery; IME composition outside the
  input deferral; no cross-sibling-window atomicity for consumer hooks; catalogue is a snapshot.
- **Reading an overflow verdict (FL-21).** `UiOverflowReport.Available` is the **inset label rect** the widget
  measured into and `Needed` is the measured content it could not hold — an overflow is about the **label band**,
  not about the element's height. A consumer that renders "has N px" is quoting the band; say so, or name the
  element's own rect separately.
- **A wrong-type binding read is reported as well as refused (FL-23).** Reading an options list or an action
  payload as a different element type still throws, and it now also records one deduplicated finding on the
  audit surface (the binding key is the path, the two type names are the authored/resolved pair), so a caught
  exception is no longer the only trace of it.
- **A fit-audit count is not a census (FL-22).** Findings are deduplicated by key (path, text, font, axis,
  needed, available), capped at `MaxReports = 48` with `Saturated` published when the cap is hit, and
  `Reset()` starts a new collection. Log lines count **distinct findings**, never elements, so an "xN" you print
  is a count of distinct reports and nothing else.
- Report gaps in the public surface. General capabilities can proceed from a confirmed contract;
  stabilization requires defined behavior, verification and a compatibility commitment, not a consumer
  count. Promoting a **new specialized widget kind** still requires cited real-consumer use and the kind's
  promotion checks. Missing integration evidence remains explicit; it is not a blanket veto on general APIs.

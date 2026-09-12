# Public API tiers

The library is referenceable by strangers (maintainer ruling 2026-09-10, `AGENTS.md` "Purpose and
non-goals"), so "we will not break you" has to be a statement about specific types rather than about the
assembly. Three tiers, one home for the promise:

- **Stable** — inside `[FerriteLibVersion.Api min, max)` a consumer compiles against, no member of these
  types is removed and no signature changes. Anything that would change one is either re-scoped or lands as
  a documented breaking change with a migration in its own minor.
- **Public-unstable** — usable today, and expected to change shape. Compile against the version you tested
  against; expect to recompile at a minor bump. This is not a warning label, it is the honest status of a
  surface that has been forced by one consumer and never by two.
- **Internalize-candidate** — public today and needed by nobody outside the assembly. The 0.4.x window
  removes them from the surface; until then a consumer that names one is on its own.

Enforcement is a harness lane (`tools/FerriteLib.UiKit.Tests/FerriteLibApiTierTests.cs`): every public type
in the payload must be listed here exactly once, no entry may name a type that no longer exists, and the
stable list is additionally pinned inside the test, so promoting or demoting a type takes two deliberate
edits. `UiWidgetRegistry.Clear` is the precedent for what an unlisted change does: it went internal in 0.3.0
(item D) and no lane here would have noticed.

**Why the stable tier is thin, and that is the point.** Three known debts each block a specific group of
types from stability, and they are the reason a 0.4.x exists rather than a 0.3.5:

| Debt (see `TODO.md`) | Blocks from stable |
|---|---|
| Per-surface theme tokens (`§4`) | `UiTheme`, `UiThemeDraw`, `UiFitAudit` |
| Element identity layer (`§3`) | `UiElementSpec`, `UiValueState`, `UiSession`, `UiSessionGuard`, `UiWidgetContext`, `UiLayoutSnapshot` |
| Bindings announce / invalidation (`§3`) | `IUiBindings`, `UiBindings` |
| Owned hit stack, popup as a stack (`§3`) | `UiNative`, `UiPopup` |
| Window shell provisional (`MEMORY.md` round-1 P2) | `UiWindowHost`, `UiWindowNotice` |

`IUiWidget` is called stable *with that constraint visible*: the identity work may want to hand a widget a
node object instead of a spec plus a context path, and if it does, this file changes first and the breaking
consequence is paid in the open rather than discovered by a stranger.

## Stable

- `FerriteLibVersion` — the version contract itself; a mismatch report that can only be read by the version
  that wrote it is worthless.
- `ITextMetrics` — the injected measurement seam; proven by the wired consumer's pages and by round 3's
  text-natural `Width="Auto"`.
- `VerseFerriteTextMetrics` — the production seam, shipped so measurement and drawing cannot disagree about a
  font size.
- `UiFont` — the font vocabulary every other seam names.
- `UiKitFonts` — the one `UiFont` to `GameFont` mapping in the series.
- `IUiTranslation` — the translation seam; the library owns no strings, and this is how it says so. A
  missing key is drawn as the key on purpose: the library's choice is between a blank label and the key the
  consumer asked for, and it draws the key, because a visible wrong-ish word is diagnosable from a
  screenshot while a blank rectangle has no author to blame. The cost is written down with the policy — the
  library cannot see a consumer's language files, so the dev-only check ("resolve the keys my chrome uses,
  log the ones that come back equal to the key") is the host's to run, and a host that prefers a
  placeholder or an empty string is free to answer that way.
- `IUiWidget` — the custom-kind interface; sixteen consumer kinds implement it, which is the only proven
  extension point the library has.
- `UiWidgetRegistry` — kind registration carrying the per-kind attribute schema and label set; consumers
  register their own scope through it.
- `UiContractException` — creation-time contract failure, the type a consumer catches to survive its own
  manifest.
- `UiUnknownWidgetKindException` — the same contract's kind half.
- `UiStatusTone` — the status vocabulary shared by the audit surface and consumer copy.
- `UiOverflowAxis` — the fit-audit axis vocabulary; two axes because a width overflow and a height overflow
  are different bugs.

## Public-unstable

- `UiHost` — per-window engine entry; identity and the manifest load-path fork both reach into it. The
  style document enters here too (`UiStyleDocument? document` on the constructor, the manifest's own
  `<Styles>` section when none is handed in): the page level is applied to the injected theme inside the
  constructor — the resolve-before-Measure slot, riding the theme's existing `LayoutRevision` clock rather
  than a second one — and every drop the parser and the resolver recorded is published on `UiFitAudit`'s
  appearance channel, so a dropped style value is the host's to surface and not something a consumer has to
  remember to ask for. `StyleResolver` is never null; a page with no document still needs one to report an
  element naming a scheme nobody declared. Two sources at once — a handed-in document and a manifest
  section that carried something — is the same rule applied to the choice itself: the document wins and the
  displaced section is reported, because quietly picking one of two authored sources is the silent fallback
  this library refuses.
- `UiSession` — session state; focus traversal and per-key invalidation land here when they are built.
- `UiValueState` — per-element state bag; its key is the path string the identity layer replaces.
- `UiElementSpec` — the spec a widget reads; additions are breaking pre-1.0 by definition.
- `UiWidgetContext` — what a widget is handed per pass; may carry a node instead of a path, and since
  batch B a nearest-first `StyleChain` — the scheme/density declarations from the element outward, the
  element's own declaration at index 0, roles deliberately absent because they never inherit. The engine
  builds it while descending and appends a link only for an element that declares something, so an element
  that styles nothing reuses its parent's list; `WithTheme`, `WithStyleDeclaration` and `WithStyleChain`
  are the hand-offs, and a widget is handed the same theme instance in Measure and in Draw.
- `IUiBindings` — get, set and the writability read (`IsWritable`, landed with the disabled treatment's
  first producer); announce is the next operation the roadmap asks for, and an interface member is a
  breaking addition for every implementer.
- `UiBindings` — the reference implementation, same reason.
- `UiNative` — the backend funnel's public face (26 members today); hit-stack and focus work will move
  members across its boundary.
- `UiPopup` — one popup per session by design today; the owned hit stack generalises exactly that.
- `UiSessionGuard` — the recovery wrapper; the recovery key is an arranged path today.
- `UiTheme` — per-surface (fill, border) pairs and a density bundle landed (`BaseSurface` through
  `DangerSurface`, `Geometry`, `LayoutRevision`, `Styles`); the table's third key slot now carries the
  bindings' read side (`IUiBindings.IsWritable`, landed with the disabled treatment's first producer),
  and the density tokens reach every core widget that used to hold its own numbers — the dropdown, the
  mode row, the two text composites and the five leaf atoms — while `chrome/banner` and `state/empty`
  keep their own type size by design, so a density font change moves the atoms and not those bands. The
  region/page carriers the scheme and density classes waited for landed in batch B (`Scheme`/`Density` as
  engine vocabulary on every kind, plus the per-element style chain the engine carries and resolves), so
  what is left of that debt is the type size those two pinned composites would need before density can
  reach them.
- `UiThemeDraw` — the single text and panel outlet; per-surface tokens change what it takes to draw.
- `UiFitAudit` — the audit surface; entry attribution follows the identity layer.
- `UiLayoutManifest` — the `Schema="2"` slot and the uncalled `ParseFile` are an open fork, and either
  branch changes this type.
- `UiLayoutSnapshot` — the measure-then-draw halves are exercised only by the harness; no wired consumer or
  the shell names them (`UiWindowHost` drives `DrawFrame`), so they either earn a cited use or go internal.
- `UiWindowHost` — the largest freeze surface this library has ever shipped, landed before a second consumer
  existed; provisional in a way a theme token is not. The close affordance is sized from its own label by
  default (`CloseButtonSize` = `max(110, measured label + padding)`, measured through the `Metrics`
  seam a host also hands the fit audit) rather than from the fixed 110x30 box it used to ship, because the
  label is the consumer's string and a library must not clip the wording it was handed; overriding the
  property still replaces the computation. The shell's own text is attributed in the audit as
  `<windowType>/chrome` and `<windowType>/notice` — the concrete type is the only identity the shell has
  before it holds a manifest — so two windows on screen at once no longer produce indistinguishable
  `(unscoped)` findings.
- `UiWindowNotice` — the shell's notice vocabulary, same reason.
- `LineChartWidget` — named by the consumer's own composition, so it cannot go internal yet; that use is
  also the specimen behind the tree-membership metric, and the debt list would rather it be a kind string.
- `UiChartPointChange` — the typed drag result of `chart/line`, consumed by the attenuation editor today; it
  travels with that widget's shape, which the metric wants expressed as a kind plus bindings instead.
- `UiOverflowReport` — the fit audit's overflow record, consumed by the wired consumer's own audit sink; its
  fields follow the audit's entry attribution, which the identity layer changes. It carries two
  **non-content discriminators** — `TextLength` and `RectWidth` (`RectWidth` was already there) — so a
  finding outside any element scope is still decidable by a caller whose record may not carry UI text:
  length separates the candidates, the rect width says which box the label was drawn into.

- `UiSurfaceStyle` — a surface's (fill, border) pair; what the per-surface restructure replaces the single
  global border with, and what `UiTheme.BaseSurface` through `UiTheme.DangerSurface` hand back.
- `UiGeometry` — the density bundle (padding / spacing / gap / row height / hairline) read from
  `UiTheme.Geometry`; the numbers widgets used to hard-code, in one knob.
- `UiEmphasis` — the second axis of a treatment key (`Normal` form-control text, `Muted` badge text); it
  carries the one measured difference between those two coordinates, not a general emphasis scale.
- `UiResolvedStyle` — one immutable answer (fill, border, text plus its pair view) handed out by the table.
- `UiStyleTable` — the resolved-value store reached through `UiTheme.Styles`; one per theme instance, never
  shared, and the single place the painting outlets and the widgets get their values from.

- `UiNodeId` — the element identity the 0.4.0 identity layer adds, and the key of `UiSession`'s node
  table: unique per element inside one tree, the same value in Measure and in Draw, unchanged across a
  re-arrange (`Id`, or `Kind[declaredIndex]` when the element has none). Since the node step it carries two
  strings, and the difference is the point: `Key` is the canonical identity — segments joined by a separator
  no XML text can hold; the creation-time guards are load-bearing for that injectivity, not a legacy rule the
  node step supersedes — while `Path` is the display path every diagnostic has always printed.
  **Debt and residual aliases, stated rather than hidden.** Closed by the node step: recovery slots
  (`UiSession.Trip`/`IsTripped`/`TryGetTripLog`/`TrippedNodes`, and the engine's guard calls now take a
  node), `ScrollPositions` with `Get/SetScrollPosition`, and scroll targeting — `SetScrollTarget(string)`
  plus `UiLayoutSnapshot.RectById` stays consumer vocabulary (a declared Id is globally unique, so it was
  never a display-path key) while the engine resolves it to a node, writes that node's scroll position and
  consumes the request once. Still open, because the display path is what a human reads and existing
  consumers assert on: `VisibleIds`, the fit audit and the recovery band can still print one text for two
  elements whose `Kind` contains `/` (`input/stepper-slider`); `UiNative`'s hot-control id stays
  string-keyed. The popup owner key is no longer a yield input: since the owned hit stack, cover decisions
  are layer comparisons, and the popup's own state (`OpenPopupId`/`OpenPopupAnchor`/`IsPopupOpen`/
  `ClosePopup`) only says whether a popup is open. Identity and per-element state never depend on the
  display path: `GetNode`, `GetNodeByElementId`, `GetValueStates`, `ActiveElement`, `ActiveNode`,
  `TrippedNodes`, `ScrollPositions`, `HitLayers` and `HoverClaimElement` are all node- or
  identity-keyed.
- `UiNode` — one arranged element's identity carrier: it owns the element's `State` (plus its named slots,
  reachable with `GetOrCreateState`), its `Kind`, its declared `ElementId` and `Ordinal`, the `IsDirty`
  flag whose writer is `MarkDirty`, and - since the node step's second half - the tree and the geometry:
  `Parent`, `Children` (arranged elements plus minted sub-nodes, in declared order), `Rect`,
  `ContentRect` and `IsArranged`. `UiSession` caches it by `UiNodeId` for the host's lifetime, so a
  re-arrange reuses the node and its state survives a frame, and `BeginArrange` clears children and geometry
  so "not arranged this pass" is readable instead of a stale rect. `UiWidgetContext.Node`/`WithNode` is the
  per-pass hand-off, and `UiWidgetContext.Child(name)` (which replaces the 0.4.0-window `ForChild`, and the
  path-only `WithElement` before it) mints a sub-node under the element so a widget's own controls get their
  own identity and state. Public-unstable: the hit stack and focus are the next steps that read this tree.

- `UiHitLayer` — one entry of the owned hit stack: the element whose paint covers a window-space rect,
  and whether the layer is a popup. `UiSession.HitLayers` is the stack in paint order (bottom-to-top) and
  `UiSession.IsPointerOverHigherLayer` is the one dispatch rule: the topmost layer containing the pointer
  decides, and a layer belonging to the caller keeps the click. This replaces the single-popup yield branch
  (`UiNative.YieldsToCoveringPopup`, `UiSession.OpenPopupRect`, `SetPopupRect`, `IsPointOverPopup` -
  all removed in the same 0.4.0 window), so every primitive answers the same question the same way.
  **Contract for the context-free overload:** `UiNative.Button(Rect)` has no session and no draw origin, and
  a static primitive cannot reach a session without a process-wide mutable static (which the promotion gate
  forbids); it therefore keeps the pre-stack behaviour and is **not** covered by the stack. Migration is one
  argument: pass the context (`UiNative.Button(rect, ctx)`), which is what every library widget now does.
  The window shell's own chrome button is the one library call site left raw, because the chrome draws
  outside the tree; that count is measured rather than asserted - the containment lane allowlists this
  call site by name and fails if a second raw call site appears or if this sentence stops saying so.
  **Recorded boundary and its recovery condition:** content layers are in the stack and ordered by paint,
  but two content layers do not arbitrate each other yet - only popup-over-content does, because IMGUI
  already serialises content input by draw order and a rect lookup cannot tell a real pointer from an
  injected one. Recover it the moment a consumer needs topmost-content dispatch inside an `Overlay`: the
  data is already here, so the work is a consumption rule (which layer wins on the pointer, and what the
  loser does), not a new structure.
- `UiStyleFallbackReport` — one appearance fallback: an authored `Tone`/`Emphasis` value outside the
  vocabulary, carrying the element path, the kind, the attribute, the authored text and the value it
  resolved to. Produced by the atom vocabulary, and since batch B by `UiHost` too — one report per dropped
  style-document declaration, attributed to the page's style origin — and delivered through `UiFitAudit`'s
  appearance half (`AttachStyleFallback`, `StyleFallbackCount`, `LastStyleFallbackDiagnostic`), which stays
  live even when the text half is off, because fail-soft must not mean silent.

- `UiStyleDocument` — the parsed style document (named schemes, named densities, page-level defaults) from
  either text origin: the standalone `<Styles>` file and the manifest's `<Styles>` section share one parser
  and one vocabulary. All of its failures are appearance-class; the one page-level case is a section that
  is not well-formed, because then the manifest itself does not parse (co-location's real cost).
- `UiStyleResolver` — the written precedence chain `state > element > container > page > theme > default`
  and the region themes built once per effective scheme/density pair. Scheme and density inherit; roles do
  not, and that asymmetry is the design rather than a gap. The engine resolves each element's chain through
  `ThemeFor` and reuses the cached instance in Measure and Draw, while an empty chain is the page level and
  answers with the injected theme itself; `UiHost.StyleResolver` is the live instance for a page, and its
  `Issues` are the resolution-time half of the same record the document keeps. The cache is bound to the
  injected theme's own `UiTheme.LayoutRevision` — the revision the engine's band cache already compares —
  so a layout-bearing token moving drops the cached clones and the next scope lookup rebuilds them, while a
  colour-only re-tint leaves the instances alone, exactly as it leaves the band cache alone.
- `UiStyleDeclaration` — one node's own style attributes as the resolver reads them (scheme, density, tone,
  emphasis), handed in nearest first along the tree; the engine's chain carries the two that inherit and
  leaves the roles on the element's own spec.
- `UiStyleIssue` — one appearance value a document dropped, so that fail-soft is never silent.

## Internalize-candidate

- `KernelCoreWidgetRegistrar` — called from inside the assembly by the registry itself; no consumer names it.
- `UiLayoutEngine` — reached through `UiHost`; naming it from a consumer means bypassing the session.
- `ChromeBannerWidget` — a kind string in the manifest is the only supported way to use it.
- `DropdownWidget` — the consumer references it in a comment, not in code.
- `EmptyStateWidget` — kind string only.
- `InputModeRowWidget` — kind string only, and the consumer ships its own `us/mode-row` anyway.
- `SectionHeaderWidget` — kind string only.
- `StepperSliderWidget` — kind string only.
- `WrappedTextWidget` — kind string only (`text/wrapped`); the measure contract over its own wrapped
  content replaces the copy inside `chrome/banner` and `state/empty` when those are rebuilt over it.
- `ButtonWidget` — kind string only (`input/button`); the command binding and the three interaction
  appearances are reached from the manifest.
- `RuleWidget` — kind string only (`chrome/rule`).
- `SliderWidget` — kind string only (`input/slider`).
- `NumberFieldWidget` — kind string only (`input/number-field`).

Removing them needs a place for the kind names: a stable container of `const string` kind identifiers, so
`["Kind"] = "core/state/empty"` stays writable without a type reference. That is a 0.4.x item, and until it
lands these classes stay public.

## Breaking changes inside the open 0.4 window

This minor is not released yet, which is why a shape change here is still cheap — but "cheap to change" is
not "free to discover by compiling". A consumer that re-pins to 0.4 is owed the call that no longer binds
and the call that replaces it, in the same place the promise lives. One entry per breaking change, written
by the commit that made it.

- **Session scroll state is keyed by a node, not by a string id.** `UiSession.GetScrollPosition` and
  `SetScrollPosition` take a `UiNode`, `ScrollPositions` is an `IReadOnlyDictionary<UiNode, Vector2>`,
  and no string overload was kept: a shim would be the second key space the node step exists to remove. The
  recovery surface moved in the same step — `TrippedComponentIds` became `TrippedNodes`, and
  `IsTripped`/`Trip`/`TryGetTripLog` take a node.
  **Migration:** a caller holding the scroll container's declared `Id` bridges with
  `UiSession.GetNodeByElementId(string)` — a lookup from the one string a page owns to the node identity
  everything else keys on, not a second key space — and then reads or writes the position on that node.
  Unchanged on purpose: `UiLayoutSnapshot.Viewports`/`ScrollContents` stay string-keyed because they are
  diagnostic geometry keyed by scroll container id/display path rather than session state, and
  `UiLayoutSnapshot.RectById` with `SetScrollTarget(string)` keep the declared-`Id` strings the engine
  resolves to nodes internally.

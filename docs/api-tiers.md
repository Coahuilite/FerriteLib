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
- `IUiTranslation` — the translation seam; the library owns no strings, and this is how it says so.
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

- `UiHost` — per-window engine entry; identity and the manifest load-path fork both reach into it.
- `UiSession` — session state; focus traversal and per-key invalidation land here when they are built.
- `UiValueState` — per-element state bag; its key is the path string the identity layer replaces.
- `UiElementSpec` — the spec a widget reads; additions are breaking pre-1.0 by definition.
- `UiWidgetContext` — what a widget is handed per pass; may carry a node instead of a path.
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
  open debt is the region/page carriers the scheme and density classes still wait for, and the type size
  those two pinned composites would need before density can reach them.
- `UiThemeDraw` — the single text and panel outlet; per-surface tokens change what it takes to draw.
- `UiFitAudit` — the audit surface; entry attribution follows the identity layer.
- `UiLayoutManifest` — the `Schema="2"` slot and the uncalled `ParseFile` are an open fork, and either
  branch changes this type.
- `UiLayoutSnapshot` — the measure-then-draw halves are exercised only by the harness; no wired consumer or
  the shell names them (`UiWindowHost` drives `DrawFrame`), so they either earn a cited use or go internal.
- `UiWindowHost` — the largest freeze surface this library has ever shipped, landed before a second consumer
  existed; provisional in a way a theme token is not.
- `UiWindowNotice` — the shell's notice vocabulary, same reason.
- `LineChartWidget` — named by the consumer's own composition, so it cannot go internal yet; that use is
  also the specimen behind the tree-membership metric, and the debt list would rather it be a kind string.
- `UiChartPointChange` — the typed drag result of `chart/line`, consumed by the attenuation editor today; it
  travels with that widget's shape, which the metric wants expressed as a kind plus bindings instead.
- `UiOverflowReport` — the fit audit's overflow record, consumed by the wired consumer's own audit sink; its
  fields follow the audit's entry attribution, which the identity layer changes.

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
  **Debt and residual aliases, stated rather than hidden:** the display path can still name two elements
  alike when a `Kind` itself contains `/` (`input/stepper-slider`), so `VisibleIds`, the fit audit, the
  recovery band and the string-keyed state surfaces — `UiSession.Trip`/`TrippedComponentIds`,
  `ScrollPositions`, `SetScrollTarget` with `UiLayoutSnapshot.RectById` — can still print or key on one
  text. Identity and per-element state no longer depend on it: `GetNode`, `GetValueStates`,
  `ActiveElement`, `ActiveNode` and `HoverClaimElement` are identity-keyed. `UiNative`'s hot-control id
  and the popup owner key stay string-keyed for the same reason; both are named as the owned-hit-stack
  step's business.
  **Recovery condition:** when widgets receive nodes for their own sub-controls and the hit stack lands, the
  string-keyed surfaces above move to node identity and this paragraph shrinks accordingly.
- `UiNode` — one arranged element's identity carrier, and the object the node step adds: it owns the
  element's `State` (plus its named slots), its `Kind`, its declared `Ordinal` and the `IsDirty` flag whose
  writer is `MarkDirty`; `UiSession` caches it by `UiNodeId` for the host's lifetime, so a re-arrange
  reuses the node and its state survives a frame. `UiWidgetContext.Node` (with `WithNode`, which replaces
  the 0.4.0-window `WithElement(UiNodeId)`) is the per-pass hand-off. Public-unstable: the next step grows
  parent/child links and geometry onto it — today the arranged rect still lives on the engine's entry.

- `UiStyleFallbackReport` — one appearance fallback: an authored `Tone`/`Emphasis` value outside the
  vocabulary, carrying the element path, the kind, the attribute, the authored text and the value it
  resolved to. Produced by the atom vocabulary and delivered through `UiFitAudit`'s appearance half
  (`AttachStyleFallback`, `StyleFallbackCount`, `LastStyleFallbackDiagnostic`), which stays live even
  when the text half is off, because fail-soft must not mean silent.

- `UiStyleDocument` — the parsed style document (named schemes, named densities, page-level defaults) from
  either text origin: the standalone `<Styles>` file and the manifest's `<Styles>` section share one parser
  and one vocabulary. All of its failures are appearance-class; the one page-level case is a section that
  is not well-formed, because then the manifest itself does not parse (co-location's real cost).
- `UiStyleResolver` — the written precedence chain `state > element > container > page > theme > default`
  and the region themes built once per effective scheme/density pair. Scheme and density inherit; roles do
  not, and that asymmetry is the design rather than a gap.
- `UiStyleDeclaration` — one node's own style attributes as the resolver reads them (scheme, density, tone,
  emphasis), handed in nearest first along the tree.
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

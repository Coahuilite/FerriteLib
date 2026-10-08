# Public API tiers

The library is referenceable by strangers (maintainer ruling 2026-09-10, `AGENTS.md` "Purpose and
non-goals"), so "we will not break you" has to be a statement about specific types rather than about the
assembly. Three tiers, one home for the promise:

- **Stable** — inside `[FerriteLibVersion.Api min, max)` a consumer compiles against, no member of these
  types is removed and no signature changes. Anything that would change one is either re-scoped or lands as
  a documented breaking change with a migration in its own minor.
- **Public-unstable** — usable today, and expected to change shape. Compile against the version you tested
  against; expect to recompile at a minor bump. This classification records unresolved contract or
  verification decisions; a second consumer is not a prerequisite for stabilizing a surface.
- **Internalize-candidate** — public today and needed by nobody outside the assembly. A minor window removes
  them from the surface; the 0.4 line shipped without finishing that sweep, so it continues in the open 0.5
  window. Until then a consumer that names one is on its own.

Enforcement is a harness lane (`tools/FerriteLib.UiKit.Tests/FerriteLibApiTierTests.cs`): every public type
in the payload must be listed here exactly once, no entry may name a type that no longer exists, and the
stable list is additionally pinned inside the test, so promoting or demoting a type takes two deliberate
edits. `UiWidgetRegistry.Clear` is the precedent for what an unlisted change does: it went internal in 0.3.0
(item D) and no lane here would have noticed.

**Why the stable tier is thin, and that is the point.** Known debts each block a specific group of types
from stability; they are why the 0.4 line existed and they are what the open 0.5 window is paying down:

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
- `IUiWidget` — the custom-kind interface; **seventeen core kinds** implement it (counted 2026-09-20: the
  classes in `Source/FerriteLib.UiKit/Kernel/Widgets/` that declare `IUiWidget`, internal ones included — it was
  sixteen until `input/text-field` arrived), which is the only proven extension point the library has. The count
  is of the LIBRARY's own kinds: a consumer's widget count is a different number answering a different question.
  **The 17/13/12 relation, stated because three counts of one fact read as a disagreement:** four of the
  seventeen classes are `internal` (`RepeatTemplateWidget`, `CheckboxWidget`, `ProgressWidget`, `TreeWidget`), so
  **thirteen** widget classes are exported; of those, **twelve** are classified `internalize-candidate` below and
  the thirteenth, `LineChartWidget`, is `public-unstable`. A count of the internalize list is therefore not a
  count of the library's kinds, and neither is a count of its exported classes.
- `UiWidgetRegistry` — kind registration carrying the per-kind attribute schema and label set; consumers
  register their own scope through it. **Member-level additions to a `stable` type are part of this promise
  too, so they are listed here rather than left to the type name.** Two `Register` overloads have been added
  beside the original five-parameter one, and the five-parameter signature is unchanged: the **six-parameter**
  overload declares the kind's non-text natural-width contribution (its body — a box, a track, a thumb — which
  a label-set measurement cannot see), and a **seven-parameter** overload additionally carries the kind's
  appearance look set. The seven-parameter overload is `internal`, because the appearance seam is this
  library's own shape and no consumer has asked for an appearance axis on a kind of its own; exposing it would
  freeze a vocabulary a stranger's kind would then be bound to, so it waits for a citation. The read side is
  `GetNaturalBody(scope, kind)` (public, the body half of `Width="Auto"`) and `GetAppearanceResolver`
  (`internal`). A kind registered through the original overloads behaves exactly as before.
- `UiContractException` — creation-time contract failure, the type a consumer catches to survive its own
  manifest.
- `UiUnknownWidgetKindException` — the same contract's kind half.
- `UiStatusTone` — the status vocabulary shared by the audit surface and consumer copy. **Batch 1
  (0.7.x):** the authored *manifest* vocabulary shrank to four accepted values, which is a manifest change
  only — this type stays stable and `Active`/`Disabled` remain members used internally as states.
- `UiOverflowAxis` — the fit-audit axis vocabulary; two axes because a width overflow and a height overflow
  are different bugs.

## Public-unstable

- `UiOption` — one option of a dynamic options binding: the display text and the value to commit (0.7.x, FL-16).
  Plain strings on purpose, and the same separation the static `OptionN`/`ValueN` pairs always had;
  `BindOptions<string>` keeps working with display == value, so this is an addition, not a reshape.
- `UiChoice` — the typed sibling of `UiOption`: one option of a TYPED options binding, carrying the display
  text and the real `T` to commit (`UiChoice<T>`, 0.7.x, R4-A). Bound with `BindOptions<UiChoice<T>>` beside
  `BindValue<T>`, so the value travels to the consumer's own `Action<T>` setter as the instance the choice
  carried — no string round-trip, no parse, no conversion anywhere on that path. `UiOption` and every
  string-typed registration keep their own paths untouched; the declared element type is what selects the
  read, so this too is an addition, not a reshape.
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
- `UiSession` — session state. Since FL-IC1 (2026-10-08) it also owns the two facts the interaction contract
  needs an owner for: the open menu's scroll (`OpenPopupScrollRows`, reset when a popup opens or closes and
  clamped by `UiPopup`, which is the only frame that knows how many rows the bounded rect shows) and the
  pointer capture with the element that took it (`OwnedHotControlOwner`). A capture whose recorded owner
  stopped being drawn is released at the pass boundary, and a node the definition releases releases its own
  capture through the same owner-scoped door; a capture taken outside any element draw is deliberately NOT
  reconciled, because nothing owns its draw, and is still released by `Dispose`. `ContentRevision`/`BumpContentRevision` are the engine's
  deliberately not the library's invalidation API: the consumer path is `IUiBindings.NotifyChanged` plus
  per-key revisions, and a Paint-class announcement leaves this clock alone. Focus traversal still lands
  here when it is built. The hover surface (`ClaimHover(string claim)`, `HoverClaim`, `HoverClaimElement`,
  `HoverGraceFrames`, `BeginHoverClaimFrame`) takes an **opaque claim token**, not an element id: the engine
  passes an element's declared `HelpKey` and a kind passes the identity its own help surface is keyed by
  (the consumer's catalog keys, a mode row's option help). The parameter was named `elementId` until the
  element-help round, which is what made readers assume the wrong thing; the rename is a source-level change
  only for a caller that used a named argument, on a public-unstable type.
- `UiValueState` — per-element state bag; its key is the path string the identity layer replaces.
- `UiElementSpec` — the spec a widget reads; additions are breaking pre-1.0 by definition.
- `UiWidgetContext` — what a widget is handed per pass; may carry a node instead of a path, and since
  batch B a nearest-first `StyleChain` — the scheme/density declarations from the element outward, the
  element's own declaration at index 0, roles deliberately absent because they never inherit. The engine
  builds it while descending and appends a link only for an element that declares something, so an element
  that styles nothing reuses its parent's list; `WithTheme`, `WithStyleDeclaration` and `WithStyleChain`
  are the hand-offs, and a widget is handed the same theme instance in Measure and in Draw.
- `IUiBindings` — get, set and the writability read (`IsWritable`, landed with the disabled treatment's
  first producer). Announce has landed as the third operation: `NotifyChanged(key...)` says the
  authoritative model moved, `GetRevision(key)` is the per-key revision that replaces one global counter,
  `GetInvalidation(key)` is the class the binding declared, `CanExecute(actionId)` is the command-state
  veto the disabled path consults, `TryGetBool(key, out bool)` is the non-throwing probe the `VisibleKey`
  query needs, and the `Bind*` registrations take an optional `UiInvalidation` (command registration also
  takes the executability predicate). Every one is a breaking addition for every implementer, which is why
  they land once in this window.
- `IUiTypedChoices` — the OPTIONAL typed half of the bindings surface (0.7.x, R4-A): the declared value type
  of a key, a boxed read of the current value, the acceptance test the write applies, a write that refuses a
  value of the wrong type **without writing anything**, and the key's typed choices as labels plus boxed
  values. `UiBindings` implements it and a widget probes for it, which is the whole reason it is a companion
  interface rather than new members here: net472 has no default interface members, so a required addition
  would stop every existing `IUiBindings` implementation from compiling. An implementation without it keeps
  building and keeps working string-only.
- `UiBindings` — the reference implementation, same reason: it carries the per-key revisions and declared
  invalidation classes, moves a revision only on `NotifyChanged` (never on `Set`), refuses to run a
  command whose predicate answers false, and answers `TryGetBool` without throwing for a key bound to
  another type. It also implements `IUiTypedChoices`, and recognises a `UiChoice<T>` options registration at
  **bind time** (`default(T) is IUiChoice`) rather than by reflection, so the erased view exists for exactly
  the registrations that declared it and no other list shape is affected.
- `UiNative` — the backend funnel's public face (31 members counted 2026-10-08: 28 before FL-IC1 plus
  `CanReceivePointerPress`, `WindowPointer` and `PointerWheelNotches`); hit-stack and focus work will move
  members across its boundary.
  FL-IC1 put the receive-eligibility rule in this type rather than in each kind: `CanReceivePointerPress(rect, ctx)`
  answers the four questions a control that takes the pointer itself must ask - an interactive node (the element
  or an ancestor is not disabled), the native window and scroll authority (the raw hover answer, which is what
  refuses a point the enclosing scroll view or the obscuring stack rejects), the element's own rect, and the
  effective clip the engine published - and `Button(rect, ctx)` consults the first two of those before it hands a
  press to the native control, so one answer serves every kind instead of one branch per widget. **It answers a
  NEW press only:** continuing a drag is a question of capture ownership, and a caller that gated its release with
  this method would decide an in-progress hold by where the pointer happens to be this frame. The wheel is read
  in one place too (`PointerWheelNotches`), read with the sign the native IMGUI scroll view uses - a positive
  `delta.y` asks for the later rows, so a menu in a scrolled page cannot scroll the opposite way to the page
  beside it. The funnel's popup-priority rule hands a notch that lands inside the open menu to that menu before
  any content draws, and it OWNS that notch whether or not the scroll moved: a clamped menu at its first or last
  row still swallows the event, because handing it back would start the page scrolling under the menu the player
  is reading. The clamp is the menu's edge, not a release of the priority.
  It also owns the one disabled-input rule: `Button(rect, ctx)` and
  `DropdownButton` refuse the pointer for an element the engine published disabled, and the
  session-plus-key primitives now do the same — `Slider`, `NumberField` and the identity-bearing
  `TextField(Rect, string, UiSession, string, out bool)` resolve the element through the
  declared-Id bridge and never reach the native control while it is disabled, so a command-bound slider or
  field takes no drag, no edit and no focus, while a key that names no arranged element keeps the
  pre-guard behaviour exactly. The identity-free `TextField(Rect, string)` remains for a caller with no
  element to name, and the gap this entry used to record — "the one interactive primitive that cannot
  consult the disabled state" — is **closed** by the identity-bearing overload (0.7.0, B7) rather than by
  widening the raw form. The hover primitive follows the same shape: the raw `IsMouseOver(Rect)` stays for a
  caller with no element, and `IsMouseOver(Rect, UiWidgetContext)` (0.7.0) applies the **higher-layer** rule so
  a kind or the engine can publish "this part of me is hovered" without consuming the click and without
  re-deriving the arbitration. It deliberately does **not** apply the disabled rule: disabled-ness refuses
  input (the paragraph above), while hover also drives inspection — the engine claims a hovered element's
  declared `HelpKey` — and a control that is unavailable is exactly when a player needs the help explaining
  it. Corrected in the element-help round, with the consumer's unavailable-state help entries as the evidence.
- `UiPopup` — one popup per session by design today; the owned hit stack generalises exactly that.
  `DrawOptionList` returns the **hovered** option's value (or an empty string), so a caller can publish an
  option-level fact — the dropdown's `HoverHelpKey` identity — without re-deriving the row geometry; the
  return value is additive, so a caller that ignored the old `void` still compiles unchanged.
  `DrawChoiceList` (0.7.x, R4-A) is its typed sibling: the same panel, hit layer, viewport rules and row
  hit for `UiChoice<object>` rows, comparing values by identity and handing the chosen option's real
  instance back, and returning the hovered row's **index** (or -1) because a typed value is not a string a
  help catalog could be keyed by. Both lists paint and publish through the same private helpers, so the
  hit-layer rule still exists in one place.
  **FL-IC1 (2026-10-08) changed the height rule, not the signatures.** `RectFor(anchor, optionCount, viewport)`
  now bounds the menu to whole rows that fit the viewport (`VisibleRowCount`) instead of returning
  `optionCount * OptionHeight` whatever the window said, and both option lists draw, hover and hit-test only the
  rows inside that bounded rect, offset by `UiSession.OpenPopupScrollRows`; `WheelRowsPerNotch` says how far one
  notch moves them. A consumer that already goes through `RectFor` + a list (the only supported route) needs no
  edit: a forty-option menu that used to run off the window now scrolls inside it, which is the behaviour the
  old signature always promised. The escape hatch is unchanged: a viewport the host never published (zero
  height) bounds nothing. `OptionHeight` stays the shared row constant the fitting audit reads.
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
  reach them. **0.7 amendment:** one named peer factory, `Vanilla`; `new UiTheme()` is a PARTIAL bag whose
  every unset colour token answers `Vanilla`, and an explicitly assigned colour — including a transparent
  one — is a value the fallback must not replace. **Batch 1 amendment:** the accent is one stored colour,
  `AccentGold`, plus the read-only derived `AccentHover`; the stored `HoverPoint` member is removed.
  **R2 amendment (0.7.x):** `Vanilla` is the only shipped look (the peer `DarkGold` factory is **removed**,
  with no alias and no shim), and a second clock joins `LayoutRevision`: `ColourRevision` moves on any colour
  assignment that changes a value and on nothing else. The pair is the point — a colour token must be able to
  invalidate a cache of cloned themes without re-arranging the page, and `LayoutRevision` still means "a rect
  moved". A consumer that re-tints a theme it handed to a `UiHost` gets refreshed region/scoped themes; a
  consumer that wants the retired warm-gold look assigns the tokens on its own bag. SA1.1 adds one
  assignable colour token: `SwitchThumbOff` - the switch thumb's OFF ink, which answers the theme's own
  `TextPrimary` until assigned (the historical thumb, unchanged for every unset bag), so a consumer can grey
  the thumb without darkening any label, and the assignment cannot reach the ON half (`AccentGold`).
- `UiThemeDraw` — the single text and panel outlet; per-surface tokens change what it takes to draw. R4-B adds
  the library's only image outlet beside it: `Image(Rect rect, Texture? texture, Color tint)` draws one bound
  texture tinted through `GUI.color` exactly as a label is, and answers false when there is nothing to paint
  (the caller decides what that means), and `FitImage(Rect slot, Vector2 naturalSize)` is the letterbox rule it
  uses — the largest centred rect with the texture's aspect that fits the slot, total for a non-positive size.
  SA1.1 adds the selector field's shared shape outlets beside them: `SelectorAccentWidth` (3) and
  `SelectorArrowZoneWidth` (18) are the reserved slots, `SelectorArrowZone(field)` / `SelectorTextOutlet(field,
  theme)` compute them from one rule so the core `input/dropdown` look and a consumer composite cannot diverge,
  and `SelectorArrow(zone, theme, color?)` paints a procedural triangle through the existing `Solid` outlet —
  no texture, no font glyph, no second backend path. SA1.1(r2) lands the COMPLETE field as one entry:
  `SelectorField(field, display, theme, style, font?)` composes plane, rail, divider, arrow and text outlet in
  the one order, and the core dropdown kind draws through this method - core and composite share the
  composition itself, not two spellings of it. Shape and colour stay separated: these members own rects,
  every colour is a token argument.
  No exported type is added by this change (the members sit on classified types), so no tier moves; the
  0.7.x temporary public-addition exemption applies, which means no minor bump either.
- `UiFitAudit` — the audit surface; entry attribution follows the identity layer. The ruler moved with
  the routing: a subscribed host is measured with the `ITextMetrics` its own diagnostic scope carries, and
  the process-wide slot bound by `Attach` serves only the legacy (unsubscribed) channel. That split is a
  fix, not a nuance — while subscribing wrote the slot, a second host with a different measurement adapter
  silently changed the first host's overflow verdict without ever drawing.
- `UiLayoutManifest` — the `Schema="2"` slot and the `ParseFile` file entry point. The fork this entry used
  to describe is **decided as of the R7 fix**: the document service reads one snapshot (`ReadAllBytes`) and
  parses that text, because hashing one read and parsing another let a concurrently replaced file record
  version A while the tree came from B. `ParseFile` therefore has **no production caller** and stays as a
  convenience for a consumer that wants the one-shot parse; it is *not* a second read path in the service, and
  a lane asserts the single-read pipeline. In-process text entry is `Parse(string)`.
- `UiLayoutSnapshot` — the measure-then-draw halves are exercised only by the harness; no wired consumer or
  the shell names them (`UiWindowHost` drives `DrawFrame`), so they either earn a cited use or go internal.
- `UiWindowHost` — a broad lifecycle and window-geometry surface, currently classified public-unstable;
  its stability decision must name the supported behavior and verification, not wait for another consumer.
  The close affordance is sized from its own label by
  default (`CloseButtonSize` = `max(110, measured label + padding)`, measured through the `Metrics`
  seam a host also hands the fit audit) rather than from the fixed 110x30 box it used to ship, because the
  label is the consumer's string and a library must not clip the wording it was handed; overriding the
  property still replaces the computation. The shell's own text is attributed in the audit as
  `<windowType>[<window-key>]/chrome` and `.../notice` for a shell a catalog attached, and as the bare
  `<windowType>/chrome` when none did — the concrete type plus the window key is the identity, extended in
  the 0.5.x window round because one generic page shell now serves every ordinary page, and type-only
  identity would put two open panels on one audit path and merge two instances into one finding. Two
  windows on screen at once therefore no longer produce indistinguishable `(unscoped)` findings either.
  The same round adds `Key`, `IsActiveTarget`, `Session`, the
  `PreOpen`/`PostOpen`/`OnCloseRequest`/`PreClose`/`PostClose` hooks and the protected `CanClose`
  veto to this type; all of it is additive, and a window no catalog attached behaves exactly as before.
  DT1 adds `ShellChrome` (a `UiWindowChrome` snapshot of the shell's own frame/title-band/close/content
  rects in window space, answered by the same arithmetic that paints the chrome and drives the Dev
  outline) as the PRESENT measurement - so a consumer stops mirroring `TitleBarHeight`/`SidePadding` into
  its own constants; a report of a COMPLETED pass instead quotes the capture's own chrome record
  (`UiDevGeometrySnapshot.ShellChrome`, written by the shell when the chrome drew). Additive, no behavior
  change; the 0.7.x temporary public-addition exemption applies.
- `UiWindowChrome` — the read-only geometry snapshot `UiWindowHost.ShellChrome` answers with: outer, title
  band, close affordance, content box, plus the two inset values it was built from. Classified
  public-unstable with the shell it describes; the constructor is internal, so the only source of a value
  is a real shell computing itself.
- `UiWindowNotice` — the shell's notice vocabulary, same reason.
- `LineChartWidget` (`chart/line`) — named by the consumer's own composition, so it cannot go internal yet; that
  use is also the specimen behind the tree-membership metric, and the debt list would rather it be a kind string.
  **Its input contract changed in FL-IC1 (2026-10-08) and its shape did not.** A NEW press now goes through
  `UiNative.CanReceivePointerPress` before the widget touches the hot control, because this kind reads the pointer
  itself and no native control answers the window, the clip or a covering menu on its behalf: a chart under an open
  option menu captured the press and committed the drag anyway (the confirmed D2 witness, reachable from the
  consumer's distance page). Continuation and release are deliberately NOT gated that way - they follow the
  capture the session owns, not the pointer's position this frame - so a drag that runs past the plot edge keeps
  running and ends on its own MouseUp; the session records the drawing element as the capture's owner, and a
  capture whose owner stopped being drawn is released at the pass boundary. No attribute, no kind string, no
  binding and no signature moved. **Not shown by any lane:** a real native slider drag, real IMGUI clipping of the
  native group, and the game's window-input authority - the harness drives the library's routing through the stub
  pump, and the in-game half belongs to the paired human pass.
- `UiChartPointChange` — the typed drag result of `chart/line`, and it stays public because a consumer still names
  the **type**: `coahuilite/UniversalSqueaker@a13f8af:Source/UniversalSqueaker/UI/UsKernelSettingsHost.cs:335`
  binds it as `BindAction<UiChartPointChange>("attenuation-point", …)` (the receiving method is `:944`), and that
  repo's harness names it the same way at
  `…@a13f8af:tools/UniversalSqueakerKernelHostTests/Program.cs:938,1351,1715`. A generic type argument is a
  compile-time dependency, so the type is a consumer contract rather than history: the composition file that used
  to name it (there, `ValidateAction<UiChartPointChange>` at `:63`, together with `LineChartWidget`) was deleted at
  `f378715` (S4-3b, 2026-09-22), and the declaration now reaches the kind by the string `chart/line`
  (`…@a13f8af:Source/UniversalSqueaker/UI/Layout.Schema2.xml:236`) — but the payload type is still named in code.
  It travels with that widget's shape, which the metric wants expressed as a kind plus bindings instead.
  *(Reason corrected 2026-09-24: it read "consumed by the attenuation editor today", a file that no longer exists.
  Tier unchanged — this was accuracy, not a layering decision; the review is §C.2 of
  `docs/development/0.7/60-capability-dispositions.md`.)*
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
  string-keyed. Cover decisions use the owned hit stack; a popup layer also carries `PopupOwnerId` to
  distinguish independent dropdowns inside one composite node. Only the matching trigger may claim that
  owner-id exemption. Popup state remains session-owned. A drawn dropdown refreshes its open anchor in
  window coordinates each hit pass. If its trigger no longer intersects the effective nested clip, or
  the owner is absent by the end of the pass, the popup closes and its stale hit layers are cleared.
  Opening a popup without drawing the matching dropdown does not create an owner-independent overlay.
  These are internal lifecycle rules, not new exported APIs. Identity and per-element state never depend on the
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
  whether the layer is a popup, and its optional `PopupOwnerId`. The existing three-argument constructor
  remains available. `UiSession.HitLayers` is the stack in paint order (bottom-to-top) and
  `UiSession.IsPointerOverHigherLayer` is the one dispatch rule: the topmost popup layer whose rect contains
  the pointer decides, and nothing else. **FL-IC1 (2026-10-08) retired the owner-id exemption**: a dropdown's
  own menu used to be unable to cover its own trigger, which is what made an option row drawn over that trigger
  unselectable (the F09/D1 witness, reported from the real game). Coverage is geometric for every caller now,
  and the trigger keeps its toggle-to-close by geometry instead - the menu's rect is not the trigger's rect, so a
  click on the part of the bar the menu leaves free still closes it. The three-argument
  `IsPointerOverHigherLayer(UiNode, Vector2, string?)` overload keeps its signature and now answers exactly what
  the two-argument form does; `PopupOwnerId` is still recorded on the layer, because the wheel-priority rule and
  the diagnostics need to know which dropdown a covering menu belongs to, and a reader of the stack needs the
  attribution. Dropping the parameter is a signature removal and waits for a minor boundary; until then this
  paragraph is the statement that the id is a record, not a licence. Context-bearing buttons and session-bearing
  sliders/fields yield to any covering popup, as before. This replaces the single-popup yield branch
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
  **R12-T amendment (0.7.x):** a `<Scheme>` may carry the density metric vocabulary
  (`<Metric Token="…" Value="…"/>`), and **typography is its own token of that vocabulary**:
  `<Metric Token="Font" Value="Tiny|Small|Medium"/>` selects a font class, which is what
  `UiTheme.DefaultFont` receives and therefore what text measurement, the fit audit and `UiThemeDraw.Label`
  all read — one selection, obeyed by measure and paint. The legacy `<Font Value="…"/>` declaration keeps
  working by being **redirected to that same font path**, never ignored and never translated into a distance:
  the parser sets the same typography selection, records one issue naming both the legacy spelling and its
  successor, and reports the redirect through the appearance channel. A scheme that declares both is resolved
  by the metric, and says so. Density/`RowHeight` stays an **independent** axis — a font change is never
  implemented as a spacing change, and selecting a scheme does not silently move density.
- `UiStyleResolver` — the written precedence chain `state > element > container > page > theme > default`
  and the region themes built once per effective scheme/density pair. Scheme and density inherit; roles do
  not, and that asymmetry is the design rather than a gap. The engine resolves each element's chain through
  `ThemeFor` and reuses the cached instance in Measure and Draw, while an empty chain is the page level and
  answers with the injected theme itself; `UiHost.StyleResolver` is the live instance for a page, and its
  `Issues` are the resolution-time half of the same record the document keeps. The cache expires on **two**
  of the injected theme's own clocks, and they answer two different questions: `UiTheme.LayoutRevision` (the
  revision the engine's band cache already compares) means a layout-bearing token moved, while
  `UiTheme.ColourRevision` is the paint-side peer — a cached region theme is a CLONE, so a re-tint has to
  drop it or the region keeps painting the palette it was built with, and a re-tint must not re-arrange the
  page. Reading both, and only dropping the clones, is what makes a scoped palette refresh free of layout
  damage.
- `UiStyleDeclaration` — one node's own style attributes as the resolver reads them (scheme, density, tone,
  emphasis), handed in nearest first along the tree; the engine's chain carries the two that inherit and
  leaves the roles on the element's own spec.
- `UiStyleIssue` — one appearance value a document dropped, so that fail-soft is never silent.
- `UiWindowKey` — the instance identity the 0.5.x window round adds: `(consumer, window-kind,
  context-key)` as a value type with ordinal equality over all three parts, an empty context key meaning
  the kind's one context-free instance. It exists because the C# type cannot be the identity: the vanilla
  add path removes same-typed windows by exact type, so a shared page shell would make every ordinary page
  a sibling of every other one. Usable as a dictionary key and through `==`/`!=`; `ToString` is a
  diagnostic rendering and is never parsed back.
- `UiWindowOptions` — per-kind window policy, applied by `UiWindowCatalog` before the window enters the
  stack. Every modality switch is a nullable `bool?` and null writes nothing: `ForcePause` and
  `PreventCameraMotion` are any-true over all windows, so a library-chosen default there would change
  another consumer's game — this type ships no product default for either. `AllowMultipleInstances` is
  the one non-nullable switch and defaults to true, setting the window's `onlyOneOfTypeAllowed` false so
  the library's key, not the C# type, is what deduplicates instances; a false is the vanilla exact-type
  rule, exact C# type included, so two kinds sharing one window class would evict each other.
  `InitialSize` feeds the shell's size provider, `NormalSize` is re-applied at `PreOpen`, `CanClose`
  is the close veto and `SetFocusOnActivate` lets activation call the game's own focus setter.
- `UiWindowCatalog` — the registry that composes `Verse.WindowStack` instead of scheduling windows
  itself: `Register`, `Open`/`Close`/`CloseAll`, `TryGet`, `Instances`,
  `ActiveKey`/`ActiveWindow`/`IsActive`, `Activate`, `FocusPolicy`, and an
  `AnyForcesPause`/`AnyPreventsCameraMotion` view of its own instances (the game's aggregate is still
  the authority, because it also counts vanilla windows). It is constructed with the stack it drives, so
  it holds no process-wide lookup and two catalogs in one process cannot touch each other. Its lifecycle
  sits on the vanilla hooks `PreOpen`/`PostOpen`/`OnCloseRequest`/`PreClose`/`PostClose`, and a
  consumer's refusal to close is preserved because `Close` reports the stack's own answer instead of
  removing the instance itself.
- `UiPageWindow` — the concrete page shell an ordinary XML page no longer needs a C# `Window` subclass
  for. It takes a key, the manifest, typed bindings, a theme, a translation seam and every visible word
  (title, close label and the notice text for each `UiWindowNotice`), and it participates in a catalog by
  key; options come from the registration rather than the constructor so a kind has one policy in one
  place. It inherits the shell's deferred failure-notice contract unchanged. Its one addition to the base
  shell is `PageHost`: the `UiHost` this page owns, or null before the first draw pass and after a failure
  disposed it. The inherited host is protected and this class is sealed, so without that accessor a consumer
  holding the flagship XML-only shape could not reach its own page's engine - and therefore could not opt
  the page into per-host diagnostics (`UiHost.Diagnostics`) or attach a reload report subscription, which
  left two ordinary XML pages sharing the one process-wide channel. The accessor is read-only and
  side-effect free: it never creates a host and never subscribes anything, so diagnostics stay opt-in and
  a page nobody subscribed to stays as cheap and quiet as before.
- `UiFocusPolicy` — when a window becomes the one active target: `FollowClicks` (default: opening,
  reopening and a pointer-down inside a window move it, and a click outside every instance clears it),
  `OpenOnly`, and `Manual`. It states the library's own rule only: it makes no claim about z-order,
  and the vanilla focus call an activation may make is not a bring-to-front.

- `UiDocumentKind` — which of the two document vocabularies a file is read as (layout or style). Part of
  the source identity rather than an extension guess, because the two parsers have two different failure
  policies.
- `UiDocumentSource` — `{ Id, Kind, Path }`: the consumer names the file, nothing scans a directory, and
  the service is what later re-reads it.
- `UiDocumentService` — the bounded, disposable owner of the document half: dependency tracking from host
  to file, the watcher whose worker thread only posts a change signal, candidate parse/validation on the
  main-thread commit boundary, atomic per-document batches across every affected host, last-known-good with
  an embedded-fallback first load, and the manual reload that recovers a dropped signal. No static state,
  and every collection is capped.
- `UiReloadReport` — one reload attempt's outcome: file, element, reason, content identity, and the
  committed/skipped/duplicate axes; failures are recorded once per refusing version.

- `UiInvalidation` — the class a binding declares for its own announcements: `Paint` reuses the arranged
  snapshot and lets the next paint read the value; `Measure` re-arranges the page (the arrangement cache is
  page-wide, so this is **not** an element-level re-measure — what is per-key is the targeting of the
  announcement); `Structure` additionally covers the container that owns the element's slot. Combinable, and
  an undeclared binding answers `Everything`. The class is declared at binding **registration**, so every
  element sharing one key shares one class and there is no per-element override — a page that needs two
  classes for one key must use two keys. Public-unstable for the same reason `IUiBindings` is: the class set
  may grow when collection reconciliation has to name what a keyed insert or removal invalidates.

- `UiDiagnosticHub` — the per-host / per-session routing that replaces the one shared diagnostic slot:
  `Subscribe(UiHost, budget)`, `ForSession`, `Release` and `SubscriptionCount`, over a registry capped at
  `MaxSubscriptions` that holds subscriptions rather than hosts or sessions. `UiHost.Diagnostics` is the
  opt-in door, and a host that never opens it pays a struct scope push/pop and a null check. Subscribing
  binds no process-wide ruler: the host's `ITextMetrics` travels in the same ambient scope as its
  subscription, so two hosts using different measurement adapters cannot change each other's overflow
  verdict. Public-unstable because the channel set and the sampling policy are this round's shape, not a
  promise.
- `UiDiagnosticSubscription` — one subscriber's bounded ring (`Budget`, clamped to `MaxBudget`,
  `DefaultBudget` when unnamed) with a dropped-count marker, a dedup table bounded by that same ring, and
  per-subscription `Count`/`Dropped`/`Suppressed`/`Published`. `Dispose` releases it, and so does
  disposing the owning host or session; it carries the host's identity string and the session id, never the
  objects. It also carries the **development-only numeric instrument**: `GeometryEnabled` (off by default),
  `GeometryOverlay` (off by default; since SA1.5 an **independent switch** - with no capture live it outlines
  every entry that reaches the engine's per-entry draw step while storing nothing, and with a capture live it
  outlines exactly the retained entries at that step; since DT1 the scoped container's OWN viewport band is
  outlined too - at its own entry, after its nested walk, same rect and same retained answer - and the
  shell's own frame/title/close bands both outline through the same switch and RECORD into the same capture
  the moment they draw (dump `chrome` line, snapshot `ShellChrome` field; a pass whose window drew no
  chrome says so explicitly; `UiWindowHost.ShellChrome` stays the present-tense read, distinct from the
  completed-pass record); turning `GeometryEnabled` off does not turn it off) and
  `DumpGeometry()` (diffable
  text: one line per arranged node with its arranged, drawn and window rects, the origin between them, its
  height mode and the height that mode resolved to, plus a scoped container's viewport and content extent,
  then one line per sampled press with its verdict, then the pass's `chrome` line). **All three are
  compiled out of a release payload**
  (`FER_DEV`), where each enable path throws instead of accepting the request and answering every later
  question with emptiness. The instrument observes only: no member of it is read back by the engine, the
  session, the hit stack or the fit audit. **R3-A addition:** `TryGetGeometrySnapshot(out UiDevGeometrySnapshot?)`
  is the machine-readable rendering of the SAME capture the text dump renders - built from the capture's own
  stored samples in the same call, never from a second collection or a replayed pass. It is the one reader
  that does **not** throw in a release payload (unlike the enable path): a call a
  consumer makes unconditionally must not become an exception in the shipped configuration, and because
  "nothing was captured" is the truthful answer there rather than an empty object that reads like an empty
  page.
- `UiDevGeometrySnapshot` — one captured pass as data: `Host`, `SessionId`, `Pass`, `EntryEvent`, the
  immutable `Nodes` / `Inputs` lists, their `NodesDropped` / `InputsDropped` markers, the DT1 `ShellChrome`
  record (the shell's own bands as drawn in THIS pass - window frame/title/close/content in window space,
  null when no shell chrome drew), and the
  `NodeByKey` / `NodeByPath` lookups (key first, because a display path can be ambiguous when a kind itself
  contains the separator). It is a **fresh copy per call**: every call allocates a new snapshot and new node
  and input objects, no member is a view over the capture's buffers, an old snapshot is detached from later
  draws, and it holds no node, session or host object - only strings, value types and the two lists. What is
  **not** claimed is a caller-level immutable collection: the lists ARE the arrays that call allocated, so a
  caller who down-casts its own copy and mutates it changes that copy (and nothing else). A consumer cannot
  construct one, and there is no "empty snapshot" state: the reader answers `false` instead, so an obtained
  snapshot always describes a real pass.
  **Tier: public-unstable**, and the reason it is public at all rather than internal is the packaging
  constraint the demo packer establishes: `InternalsVisibleTo` is banned there, so a structured export a
  consumer must call has to be genuinely public. **The four TYPES are deliberately NOT inside `#if FER_DEV`**,
  unlike the capture behind them. The lane that decides that is the **RELEASE** stale-entry check,
  `FerriteLibApiTierTests.VerifyNoTierEntryIsStale`: this document is one file read in BOTH configurations,
  and a Dev-only public type must either be listed - which fails that Release lane, because the entry names a
  type the release payload does not export - or omitted, which fails
  `VerifyAllPublicTypesAreClassified` in a **Dev** build. There is no third option, so a public type that
  exists in one configuration only cannot satisfy the tier harness at all; it is also a
  compile-against-Dev / ship-against-Release trap for a consumer. These four types are therefore
  **callable but inert in a release payload**: a consumer can name them and the assembly exports them, and
  nothing in that payload ever produces an instance - the reader answers `false`, the capture is compiled out,
  and the enable path still throws. No version bump: the 0.7.x temporary public-addition rule applies.
- `UiDevNodeSnapshot` — one arranged element as the instrument observed it: `NodeKey` (the canonical
  `UiNodeId.Key`, not a re-derived display string) with `Path`, `Ordinal` and `ElementId` beside it; `Kind`,
  the container facts and `IsHitSurface`; the four spaces (`Arranged`, `Draw`, `Window`, `Origin`) plus the
  `Content` boundary and the `HeightMode` / `ResolvedHeight` pair; the appearance and its provenance
  (`ScopeFont`, `PaintedFont`, `Scheme`, `Density`, `ThemeOrigin`, `ThemeLayoutRevision`,
  `ThemeColourRevision`, the AUTHORED `Tone` / `Emphasis` text, `Disabled`); the **effective appearance**
  (`Appearance`, `AppearanceDeclared`, `AppearanceDefault`, `AppearanceSupported`, `AppearanceSource`,
  `AppearancePairRegistered`) resolved through the registry's own seam
  (`GetAppearanceResolver(scope, kind)` → `UiAppearanceResolver.Resolve(spec)`) with the core-versus-scope
  provenance proved by instance identity; and the boundaries `Clip`, `Hover`, `Focus`, `OpenPopupId`,
  `PopupAnchor`, `OwnsOpenPopup`. Three values are **always unknown with a documented reason**, and the reason
  strings are `const` members so a consumer can compare against them: hover is published during an element's
  own draw, which runs after the sample; this library publishes no focus owner at all (the session's active
  node is the draw-time element stack); and the font a label finally paints with is chosen inside that same
  draw, so `PaintedFont` is null while `ScopeFont` reports the scope's selection - the font measurement and
  the shared text vocabulary use, and never a stand-in for what a kind's own constant paints. `Tone` /
  `Emphasis` are the authored text rather than the resolved treatment, because the resolution happens inside a
  widget - which may also be applying hover/armed state - and calling the role parser here would record a
  deprecation note that the instrument itself caused. Public-unstable for the same reason as the snapshot
  above.
- `UiDevInputSnapshot` — one hit query as observed: `NodeKey` / `Path` / `Kind` / `ElementId`, the queried
  `Rect` and `Point` (both Host window space), `Verdict`, `EventBefore` / `EventAfter`, whether the query
  `Consumed` the event (the phase transition to `Used`, derived from the two phases beside it rather than
  asked of the funnel), and `OriginUnknown` / `EntryEvent` for a pass that entered already `Used`, where the
  originating input is not observable and is not guessed. Public-unstable.
- `UiDevBoundary` — one boundary the instrument tried to observe: `Known(rect)` or `Unknown(reason)`, with
  `IsKnown`, `Rect` and `UnknownReason`. The type exists so that "not measured" cannot be rendered as a zero
  rect a reader would take for a measurement, and `Unknown` refuses an empty reason for the same purpose.
  Public-unstable.
- `UiDiagnosticEvent` — one attributed record: `Kind` plus a stable `Code`, `Host`, `SessionId`,
  `Node`/`ElementPath`, and the original record for the channel that produced it (`Report` for reload,
  `Overflow`/`Fallback` for the fit audit's two halves, `Timing` for a sampled aggregate). No field names a
  host, session or node type.
- `UiDiagnosticKind` — the channel vocabulary (`Reload`, `Fit`, `Recovery`, `Timing`).
- `UiDiagnosticTiming` — one sampling window's aggregate (name, samples, total, max, average), which is what
  makes timing a rate rather than a per-frame line.
- `UiTreeRow` — one row of a `container/tree` row set: the consumer's stable business key, the level it is
  presented at, its label (literal or translation key) and the model's expansion answer. A data carrier, not a
  control: the kind owns the band geometry and the hit rule, the consumer owns the ordering and the answer.
  Public-unstable because the collection vocabulary is new in 0.5 and this type's fields follow the first real
  consumer's shape. The key is the only identity the kind uses — a blank or duplicated one is refused rather
  than replaced by a position. R4-B appends the composed row's own answers — `Selected`, `Checkable`,
  `Checked`, `Image` (the consumer's `Texture2D`, which the library never loads) and `ImageSize` — as
  optional constructor parameters, so every existing construction keeps compiling and every new part is
  opt-in per row.
- `UiRowBand` — the composed tree-row band, published so a consumer's own composite can drive it per row
  (R4-B): `Measure(UiTreeRow row, UiTheme theme, float rowHeight)` for the height a band needs, and
  `Draw(UiTreeRow row, Rect band, UiWidgetContext ctx, float rowHeight, UiResolvedStyle style,
  UiRowBandActions actions, UiRowBandLayout layout = default)` for the indent/disclosure/checkbox/image/label
  band and its per-part hit. `container/tree` is its first driver and calls these two members itself, so there
  is exactly one implementation of the band's measure, its painted order and its hit rule; the primitive holds
  no state and clamps everything it paints into the band it is handed.
- `UiRowBandLayout` — the placement inputs that call lets a caller override: `IndentStep` (the distance one
  `UiTreeRow.Depth` level indents by; null keeps the theme's `Geometry.Spacing`, zero is a real flat answer) and
  `CheckboxRightInset` (when set, the checkbox's far edge sits that far inside the band's right edge and the
  label's room ends before it; null keeps the box immediately after the indent), and `ControlSize` (optional
  disclosure/checkbox visual side, clamped to the band height; null keeps the theme-derived size). `default(UiRowBandLayout)` is
  the library's own behaviour, so a caller with no convention of its own passes nothing and gets exactly what
  `container/tree` gets. Both are caller-SUPPLIED inputs to the band's one placement decision, never a
  replacement for it.
- `UiRowBandActions` — the three optional per-part targets one band reports through (`Body`, `Disclosure`,
  `Checkbox`, each `Action<string>` carrying the row's stable key). A null part is painted but not a target,
  and the click falls through to the body; no index and no part name is ever parsed, so a consumer dispatches
  to its own typed action with its own data.

### Added in the open 0.6 window

- `IUiMainThread` — the "may I touch the game's objects from here" seam. One member, answering only that
  question, because the alternative is a process-wide notion of "main" a test cannot answer either way.
- `VerseFerriteMainThread` — the production answer, delegated to `Verse.UnityData.IsInMainThread` rather
  than re-derived: a second thread comparison inside this library could disagree with the game's own, and
  every other subsystem that checks its thread checks that property.
- `UiNotifyAdapter` — the optional `INotifyPropertyChanged` bridge. Explicit mapping only (one property to
  many binding keys, plus the empty-name "the whole object moved" convention), a bounded pending set, a
  nesting batch scope, and deterministic unsubscribe. Currently public-unstable; a stability decision
  must review this explicit mapping/lifecycle contract, not wait for another consumer or speculate about
  adding attributes or another mapping mechanism.
  **Boundary, stated as a boundary:** it delivers on the main thread only. An off-thread notification is
  refused, counted and surfaced, never queued for later. There is no base class to inherit and no container to
  configure - a plain C# object that implements the BCL interface is a view model here.
- `IUiTimeSource` — the reload scheduler's clock. The seam exists because frame counts, host counts and pump
  counts are not time: a paused game pumps many frames per second and a minimised one pumps few, so a
  scheduler built on them coalesces differently on every machine.
- `VerseFerriteTimeSource` — real time, deliberately not the simulation clock, so a paused game still
  notices a saved file.
- `UiReloadPolicy` — when a signalled file may be read and committed: the quiet period that merges a save
  burst, the retry interval and bound after a transient read failure, and the ceiling on deferring to the
  user's own input. It says nothing about *whether* files are watched - that stays
  `UiDocumentService.AutoWatch` and its build-configuration default.
- `UiReloadSchedulerState` — a value snapshot of what the scheduler is holding (pending, deferred,
  retrying, oldest wait). Public so a diagnostic readout and a lane can both observe the timing contract
  rather than assert it against source text.
- `UiWidgetDescriptor` — one registered kind as the registry can describe it without being asked for an
  instance: the scope it was declared in, the kind, and the declared attribute and label sets. Identity is the
  pair, because a kind name is not unique across scopes. `HasAttributeSchema`/`HasLabelSet` keep
  "not declared" from being rendered as "no attributes allowed".
- `UiWidgetCatalog` — the read-only description surface: `Snapshot()`, `TryGet`, `Scopes()`. It never
  calls a factory (a catalogue that resolved kinds in order to list them would run every registered consumer's
  constructor on open), never hands back a writable registry, and returns a fresh deterministic copy each
  call, so listing stays safe while other mods are still registering.

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
- `TextFieldWidget` — kind string only (`input/text-field`); the single-line field's focus, draft and
  commit rule are reached from the manifest, not from the type.

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

## Breaking changes inside the open 0.5 window

Same rule as the section above, one minor later: the 0.4 line was cut as `v0.4.0-rc1`, so 0.5.0 is the
window in which the public foundation this round is built lands **once**, as one minor, rather than as a
train of breaking releases. A consumer re-pinning to 0.5 is owed, in this file, the call that no longer
binds and its replacement. Entries are written by the commit that makes them.

What the 0.5 window is for (working packages, ownership and status: `docs/development/0.5/`):

- **Window instances.** A generic XML window shell already exists (`UiWindowHost`); what it never had is
  `consumer + window-kind + context-key` instance identity, same-key reopen-as-activate, and an explicit
  active-target/focus rule. All three land here, and `UiWindowHost`/`UiWindowNotice` move with them.
- **Invalidation.** `IUiBindings` has get, set and `IsWritable`, and no notification, so "the model moved"
  is a single global `ContentRevision` counter. A per-key revision plus a batch commit lands here, and the
  source-text assertions that currently police the counter retire with it.
- **Collections and common controls.** The engine has no per-item template; a keyed repeater with a local
  item binding scope, node reuse and removal cleanup lands here, together with the checkbox, a basic
  progress bar and a hierarchy-only tree surface.
- **Documents.** `UiLayoutManifest.ParseFile`/`UiStyleDocument.ParseFile` exist and have no production
  caller (harness lanes call them; no shipped path does). A
  bounded document service with dependency tracking, candidate validation, atomic batch commit and
  last-known-good fallback lands here, which is what makes "edit the XML and the open window updates" true
  rather than implied. C# kind changes stay outside hot reload on purpose.
- **Diagnostics.** One shared diagnostic slot becomes per-host/per-session subscriptions with bounded,
  attributed events (reload / fit / recovery), released when the host closes.

### Landed breaking changes (0.5)

- **Bindings announce per key.** `IUiBindings` gains `NotifyChanged(params string[])`,
  `GetRevision(string)`, `GetInvalidation(string)`, `CanExecute(string)` and
  `TryGetBool(string, out bool)`; `BindValue`/`BindReadOnly`/`BindOptions`/`BindAction` gain an
  optional `UiInvalidation invalidates` parameter, and `BindCommand` gains optional `canExecute` and
  `invalidates` parameters. An interface addition is breaking for every implementer; the reference
  implementation is `UiBindings`, and `UiInvalidation` is a new public type.
  **Migration:** declare what a key moves where you register it (`Paint` for a fixed-size readout,
  `Measure`/`Structure` for text, spacing or structure), then call `bindings.NotifyChanged(key)` when
  the authoritative model behind that key changes. `Set` deliberately does not announce - only the owner
  of the model can say the model moved.
- **A disabled command does not run and does not take input.** `BindCommand(key, action, canExecute)` is
  the enablement path: `Invoke` no-ops when the predicate answers false, the engine publishes the state on
  the element's node, and the funnel's context-carrying interactive entry points (`UiNative.Button(rect,
  ctx)`, `UiNative.DropdownButton`) refuse the pointer for that element. The disabled look keeps coming
  from the one writability funnel (`UiResolvedStyle.Resolve(..., writable: false)` →
  `UiStatusTone.Disabled`), so there is still exactly one disabled treatment.
- **`Visible`/`VisibleKey` join the engine-wide vocabulary.** `Visible="true|false"` is static and
  defaults true; `VisibleKey="key"` reads a bool value binding through `TryGetBool`; `Hidden` keeps its
  meaning as the legacy static form. An unresolvable `VisibleKey` is not fatal - the element stays visible
  and one deduplicated appearance fallback is recorded - so a page whose model key is missing or not yet
  bound still exists.
- **`UiSession.ContentRevision`/`BumpContentRevision` are demoted to the arrangement-cache clock.** No
  signature changed; what changed is the contract. A consumer must not use them as its refresh channel
  (that is `NotifyChanged`), and a Paint-class announcement must not move the clock.
- **Collections: `<Templates>`/`<Repeat>`, item-local binding scope, and three common kinds.** A layout may
  now carry one `<Templates>` section whose named subtrees a `<Repeat Items="..." Template="..."/>`
  materializes once per consumer-supplied item key. Each row's identity is `<declaredId>#<itemKey>`, and each
  binding key inside a template resolves in the item's scope as `<Items>.<itemKey>.<declaredKey>` — for
  `Bind`, `ActionBind`, `OptionsBind`, `VisibleKey`, `PayloadKey` and `SelectedKey`; `Tab` stays
  page-level on purpose. (This is the current set, and it grew after 0.5 - `PayloadKey` in G2 and
  `SelectedKey` in B1, both inside `0.7.0`; `docs/development/0.7/05-api-contract.md` records each one.)
  A `Repeat` that names a template nothing declares, carries children of its own, or appears inside a template is refused
  at parse time; a row whose key is blank, duplicated or carries a reserved identity character is refused with
  one bounded report rather than reconciled onto another row's state. New kinds: `input/checkbox`,
  `display/progress`, `container/tree`. `UiLayoutManifest` gains `Templates`, and
  `UiLayoutEngine`'s constructor takes the template table (a host passes `manifest.Templates`; the engine
  enforces the table's kinds and attribute vocabulary at creation, because a template's binding keys cannot
  exist as page bindings).
  **Migration:** nothing existing changes meaning. A consumer that wants a row set projects its collection to
  stable keys through an `IReadOnlyList<string>` value binding, declares one binding per item under
  `<Items>.<key>.…`, announces that items key with `UiInvalidation.Structure`, and never writes a rectangle,
  a node identity or an input rule for a row.

## The visual core, and what "without the page model" means

`AGENTS.md` "The two layers" promises a visual core reachable **without** adopting the page model, and
`KernelContractTests.VerifyVisualCoreIsPageModelFree` enforces it over an explicit file list:
`UiTheme.cs`, `UiThemeDraw.cs`, `UiResolvedStyle.cs`, `UiStyleDocument.cs`, `UiStyleResolver.cs`,
`UiKitFonts.cs`, `UiFont.cs`, `ITextMetrics.cs`, `VerseFerriteTextMetrics.cs`.

**What the list does and does not prove.** It proves no file on it names a page-model type. It does **not**
prove that a type is *usable* standalone: `UiStyleDocument`'s own API is page-model free and it is listed,
but **consuming** a parsed document without a `UiHost` is exercised by no lane, and in production the
document is reached through `UiHost`'s constructor. So the file list is a purity guarantee, while the
"reachable without the page model" claim is honest for `UiTheme`, `UiThemeDraw`, `ITextMetrics` and the
font types and **unverified for the style-document family**. Recorded rather than fixed: closing it needs a
lane that drives a document with no host, which is a decision for the maintainer.

## Breaking changes inside the open 0.6 window

`FerriteLibVersion.Api` moved 0.5.0 → 0.6.0 for this round. The rule is unconditional for additions
(`FerriteLibVersion.Api`'s own summary: while the major is 0, ANY change to the compiled surface, addition
or break, bumps the minor), so a purely additive round is still a minor. The competing reading - "0.5.0 was
never released, so its window is still open" - is the precedent the 0.4.0 → 0.3.0 refile used, and it does not
apply here: the 0.5.0 dev package and its `[0.5.0,0.6.0)` handoff were delivered, so the number has a
goalpost a consumer may already have pinned. Re-pinning a consumer is a one-line change in its constructor;
silently growing the surface under a number somebody tested is the failure this rule exists to prevent.

- **Additive only, and source-compatible.** Every new type is listed above, and the two changed public
  signatures are both optional-parameter additions: `UiDocumentService`'s constructor gains
  `UiReloadPolicy? policy` and `IUiTimeSource? timeSource` after the existing `bool? autoWatch`, and
  `UiPageWindow`-`UiWindowHost` gains the `HostAttached`/`HostDetached` events plus `UiHost`'s
  lifecycle wiring. Existing call sites keep compiling and keep their behaviour; no member was removed, moved
  or renamed in this round.
  **Migration:** none required to keep working. To use the round: subscribe `HostAttached` instead of
  polling `PageHost`/`Session`, hand a `UiReloadPolicy` and an `IUiTimeSource` to the document
  service, and list kinds through `UiWidgetCatalog` (which keeps scope, unlike `KnownKinds`).

**Maintainer ruling, 2026-09-17:** the second-wired-consumer prerequisite for API stabilization is
removed. Establish a documented, verified compatibility commitment so another consumer can adopt it;
do not make adoption and stabilization wait on each other. Real-consumer results remain evidence, not
permission to stabilize. Existing tier memberships are unchanged by this ruling: promotion still
requires an explicit contract/verification decision and the paired document/test update described above.

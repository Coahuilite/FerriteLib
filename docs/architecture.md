# FerriteLib UiKit — the four questions, and where the implementation answers them

> Authority order: **code > `MEMORY.md` > this document.** This file is a map, not a plan: it records
> what each architectural question owns, and what the current implementation answers, as measured by
> `tools/FerriteLib.UiKit.Tests/KernelArchitectureProbeTests.cs`. Nothing here proposes a change, and a
> "gap" below means *this question has no owner yet* — not *this is scheduled work*. Every claim carries
> its evidence class: **measured** (a lane asserts it), **read** (a code reference), or **inferred**.

## 1. The four questions

A decomposition earns its keep only if every part is necessary and none can answer another's question:
remove one and the result is impossible or meaningless; keep one that duplicates another and it is not a
question but a copy.

| Question | What it answers | What answers it today |
|---|---|---|
| **Structure** | Which elements exist, and who is whose child | element names, containment rules, identity |
| **Layout** | Where each element goes, and how it relates to its neighbours | flow, sizing, responsive variants |
| **Semantics** | What a control *is* and how it is *used* | kind, value/command/input contract, state |
| **Appearance** | What it looks like | tokens, roles, resolved values, drawing outlets |

Removing any one makes the page impossible: without structure there is no relationship to speak of,
without layout nothing knows where to go, without semantics nothing knows how to be used, without
appearance nothing is visible. That is the necessity test, and it is why these four are **four parallel
questions about one tree**, not four layers of a stack.

Two rules cut across all four and belong to none of them:

- **Resolution** — when several sources answer the same question, which one wins. Today: `Scheme` and
  `Density` resolve nearest-first along the element's style chain, roles do not inherit
  (`UiWidgetContext.StyleChain`).
- **Ownership** — at runtime, who holds the state. Today: the session and the arranged node hold hover,
  capture, focus, drafts and selection; a widget owns none of it (`IUiWidget` doc contract).

Naming these two as *rules* rather than as a fifth and sixth question matters: a question that has an
owner can be answered from data, while a rule about precedence cannot be a place where things live.

## 2. Two layers, two different axes

`AGENTS.md` "The two layers" splits the **delivery**: the declarative page engine and the visual core,
where the second is reachable without adopting the first. That is a packaging axis — *what may I compile
against* — and it is enforced by `KernelContractTests.VerifyVisualCoreIsPageModelFree`.

The four questions are a **concern** axis — *who owns which answer*. The two are orthogonal and both are
true; confusing them makes the "two layers" sound like the four questions and vice versa. A type can sit
in the visual core (delivery) while belonging to Appearance (concern); the page engine spans all four.

## 3. Where the implementation answers each question today

### 3.1 Structure — answered, and the strongest of the four

**Who answers.** A closed element vocabulary — `UiPage`, `Stack`, `Row`, `Column`, `Wrap`, `Overlay`, `Section`,
`Surface`, `Scroll`, `Clip`, `Widget`, `Repeat` (`UiLayoutManifest.cs:56`) — plus containment rules
enforced at parse time (a `Widget` may not carry children, `UiLayoutManifest.cs:542`; a `Repeat` may
carry none, `UiLayoutManifest.cs:439`), `Id` grammar and uniqueness, the `<Templates>` section, and a runtime identity
per arranged element (`UiNodeId`, `UiNode`). *(read)*

**Complete?** Yes, for what the question asks. Structure failures are fail-closed.

**Notes.** Two details worth knowing rather than fixing:

- Structure and layout share one element vocabulary: a container's *name* selects its arrangement
  algorithm. That is the Qt-QML positioner family, not the CSS property family. *(read)*
- The container and widget attribute lists exist twice — `UiLayoutEngine.cs:105-116` copies the host's
  private lists, and `KernelRepeatTests` reflects the originals so the copies cannot drift. It is a
  known duplication with a guard, not a hole. *(read)*

### 3.2 Layout — answered for flow, silent about relations

**Who answers.** Container kinds plus a fixed attribute set: `Gap`, `Padding`, `Height`, `Width`, `Fill`,
`MinWidth`, `MaxWidth`, `Breakpoint`, `Narrow`, `Cols`, `NarrowCols`, `NarrowHidden`, with `Title`/`TitleKey`
for bands. Responsiveness is real and is **container-scoped**: `Breakpoint` is measured against the
container's own inner width (`UiLayoutEngine.cs:966-982`), and `Narrow` can swap a `Row` for a
`Column` (:984-1005). *(read + measured)*

**Complete?** For "where does it go" in a flow: yes. For "what is the spatial relation": no.

**The gap.** There is no alignment property of any kind, and no relation between named elements. The only
explicit relation in the library is hand-written in C#: `UiPopup.RectFor` places a popup below its
anchor, flips it above when it does not fit, and clamps it into the viewport — with `OptionHeight = 24f`
as a constant beside it (`UiPopup.cs:19,28`). Relations are expressible today only as *consequences of
flow*: a fixed bottom edge is what a `Fill` sibling leaves behind, and horizontal centring is achieved
with two equal flex spacers. Both were used in the measurement and both work; neither is a declaration.
*(measured — see §4.3)*

**Text alignment is not owned here either.** It is a `TextAnchor` literal inside each widget
(`ButtonWidget.cs:113`, `DropdownWidget.cs:85`, …), so "which side is this text on" is answered
inside Appearance rather than by either of the two questions that claim spatial answers. *(read)*

### 3.3 Semantics — half answered; the half that is missing is the interesting one

**Who answers.** `IUiWidget` (`Configure` / `Validate` / `Measure` / `Draw`), the per-kind
attribute schema and label set registered with the kind (`UiWidgetRegistry.cs:38-68`), the typed binding
contract, creation-time validation, and one engine-published state (`UiNode.IsDisabled`). *(read)*

**What already works.** The hard part of input is factored out and shared: `UiNative.Button(rect, ctx)`
performs topmost-first hit-stack arbitration and refuses input for a disabled element, once, for every kind
including kinds the library has never seen (`UiNative.cs:97-110`). A widget's input rule is therefore
one call, and the disabled rule is not paint-dependent — measured: a disabled, fully transparent button
takes no click. *(measured)*

**Gap A — the behavior has no home other than the painter.** `ButtonWidget.Draw` performs the hit test,
decides the visual state *and* paints, in one method (`ButtonWidget.cs:64-85`). Consequence,
measured: the only way to keep a button's behavior and delete its painting is to write a different kind,
and that kind retypes the behavior statements and the contract around them. *(measured)*

**Gap B — per-element state is computed, not held.** Hover and armed are derived from the funnel inside
`Draw` and never published; a node exposes `IsDisabled` and nothing else. A second renderer of the
same behavior must re-derive them, and no consumer can read them. *(measured — the probe asserts the
published property set)*

**Gap C — "what input does this control take" has no declaration site.** It is implicit in which funnel
call each widget happens to make. The containment lane can prove *where* backend contact happens; it
cannot state what a kind's input contract is. *(read)*

### 3.4 Appearance — answered for colour and density, silent about shape

**Who answers.** `UiTheme` (26 colour tokens, a five-metric `UiGeometry`, fonts), the resolved-value
store `UiStyleTable` keyed by `(tone, emphasis, writability)`, an independent `UiStyleDocument`, the
nearest-first `Scheme`/`Density` chain, and a closed set of **14 drawing outlets**
(`UiThemeDraw`). *(read + measured)*

**What already works.** Colours and density are reachable from data with no compiler: an element declares
`Tone`/`Emphasis`/`Scheme`/`Density`, the document defines the schemes, and a per-element
scheme can change every colour the element reads. Measured: a button under a zero-alpha scheme keeps its
geometry, keeps its behaviour, draws nothing visible, and does not affect its sibling. Failures are
fail-soft **and loud** — an unknown tone falls back to the default treatment and is recorded on the fit
audit's appearance channel. *(measured + read)*

**Gap A — there is no shape vocabulary.** The 14 outlets paint rectangles, borders, text and solids. None
paints an image, a layer or a transform, so an icon-only control has no primitive to draw with, and no
style property could ask for one. *(measured — the outlet list is pinned in the lane)*

**Gap B — appearance decides its own geometry limits.** `Measure` reads `Height` and the theme's
row height, never the resolved colours, which is exactly why making a button transparent does not move
it. The one exception is the font, which carries geometry and therefore makes density a layout input —
already handled by resolving the style document before the first arrange. *(measured + read)*

## 4. The measurement (2026-09-18)

`KernelArchitectureProbeTests` runs three experiments with the shipped API, and registers consumer-side
probe kinds only where the shipped API cannot express the experiment. It asserts measured facts, so a
future change to the split shows up as a changed measurement.

### 4.1 Experiment 1 — a headless button

Three measurements, because "headless" has three different meanings here:

| Route | Result | Evidence class |
|---|---|---|
| **1a** keep `input/button`, resolve every colour token to zero alpha through a per-element `Scheme` | geometry kept; solids emitted with alpha 0; caption outlet still called; click fires; a click on the sibling visual fires nothing; disabled blocks the click; positive control (no scheme) paints opaque | measured |
| **1b** a probe kind whose `Draw` paints nothing at all | zero solids, zero text, click fires, outside click does not | measured |
| **1c** the core button with no scheme | always paints at least one solid and always writes a caption, whatever is configured | measured |

**Answer to the asked question** — *"if all of the button's own visual drawing logic is deleted, is it
still a complete button?"* — **the behaviour survives, the structure does not.** Behaviour does not need
pixels: 1b proves a kind can take clicks and paint nothing. But 1b also proves the cost: the behaviour is
*two statements inside a painter*, so removing the painting means writing another kind and retyping the
contract around those statements. There is no configuration of `input/button` that reaches 1b.

Against the failure shapes the request listed, two are real and three are not:

| Expected failure | Verdict |
|---|---|
| the button must call `Widgets.ButtonText` to get a click | **does not hold** — it goes through `UiNative.Button(rect, ctx)` → `Widgets.ButtonInvisible` |
| the hit test is written inside `Draw` and cannot leave it | **holds** |
| hover/armed are computed inside the painter and never become component state | **holds** |
| the caption and the behaviour cannot be separated | **half** — `Text` is optional (only `ActionBind` is required), so behaviour never depends on a caption; the paint always writes one |
| removing the background also removes the layout size | **does not hold** — `Measure` reads `Height` or the density row height, never the text |

### 4.2 Experiment 2 — one behaviour, two completely different visuals

**Result: not supported by the library.** Visual A (surface + caption, the core atom) and visual B (one
3px rail, no text, a probe kind) both fire through the same funnel call and the same binding mechanism,
and the measurement confirms one click mechanism and two different painted structures. But "the same
behaviour implementation" is true only in the sense that the *same two statements* were typed twice.

The four couplings that force it, each measured:

1. **`<Widget>` cannot carry a child** — the requested shape `<Button><Label/></Button>` is refused
   at creation (`FormatException` from the manifest parser, not `UiContractException`: structure
   failures are fail-closed but not one type).
2. **`ButtonWidget` is `sealed`** — the paint cannot be replaced by inheriting the behaviour.
3. **The outlet vocabulary is a closed 14 names and none of them paints an image** — an icon-only button
   has no primitive.
4. **`UiNode` publishes `IsDisabled` and no hover/armed state** — a second renderer must
   re-derive the state it is supposed to be a renderer *of*.

**Minimal structural change to support it** (stated because it was asked for, not proposed): the
behaviour must become something a renderer can hold — one axis that answers *what it does* and one that
answers *how it is drawn* — instead of one `kind → factory → painter` axis. Today the second axis does
not exist, so a second visual is always a second kind, and a second kind is vocabulary that the
promotion gate asks to justify.

### 4.3 Experiment 3 — parent size changes continuously, business code stays out

**The page**, expressed in today's vocabulary only: a `Column` with `Gap` and `Padding`, a title band,
a `Fill` container that eats the remaining height, and a footer `Row` whose button is centred by two
equal flex spacers.

**Result: the caller declares nothing but the size, and everything is correct.** Measured across
400x300 → 800x600 → 500x350 → 1000x500: the title tracks the parent's width through padding alone;
content sits below the title by the declared gap; the button is centred; it ends on the parent's bottom
padding; it keeps its declared width. Per pass: the frame paints the button at *this* pass's rect; the
hit test uses *this* pass's rect; the previous size's centre is no longer a hit target; a changed size
produces a new arrangement; the same size twice returns the same snapshot instance; and no published
snapshot property is writable, so the caller *cannot* set a child rect even if it wanted to.

**Which model is it?** **B — parameters are saved and the whole tree is re-evaluated when the size
changes**, with the arrangement cached on `(available size, content/definition/translation revisions,
theme layout revision)` (`UiHost.MeasureAndArrange`). Not A: there is no relation graph and no
propagation. Not C: the caller never computes a rect. B was explicitly allowed by the request, and on
this evidence it is not the problem.

**The qualification.** "The caller only declares relations" is true in the weak sense. Three of the four
requested relations are consequences of flow — *left/right margin* is `Padding`, *below the title* is
flow order plus `Gap`, *fill the rest* is `Fill` — and the two that are genuinely relational are
**idioms, not declarations**: centring is two equal spacers, and the fixed bottom edge is whatever the
`Fill` sibling leaves behind. Remove the `Fill` sibling and the button floats. Also still absent:
alignment, edge anchors, and any reference to a named sibling.

## 5. The coupling map as measured

```
              manifest XML (structure + layout attributes + role attributes)
                     │                                   │
        ┌────────────┴────────────┐          ┌───────────┴────────────┐
        │   UiLayoutEngine        │          │  UiStyleDocument       │
        │   flow · sizing ·       │          │  + UiTheme + UiStyleTable
        │   breakpoints · Narrow  │          │  (26 colours, 5 metrics)
        └────────────┬────────────┘          └───────────┬────────────┘
                     └──────────────┬────────────────────┘
                                    ▼
                        UiWidgetContext (+ resolved theme, style chain)
                                    │
                     IUiWidget.Draw(rect, ctx)   ← everything below is one method
                     ┌──────────────┼───────────────────────┐
                     ▼              ▼                       ▼
              hit test        state decision            painting
        UiNative.Button   hover/armed computed     UiThemeDraw outlets
        (arbitration +    inside Draw, published   (14, closed; no image,
         disabled rule)   nowhere                  no shape, no transform)
                     └──────────────┬───────────────────────┘
                                    ▼
                    the five funnel files → Verse IMGUI
                                    ▲
                     IUiWidget.Measure — geometry only, paint-independent
```

## 6. Three categories

### 6.1 Already at target — do not change

- **Structure** is a closed vocabulary with fail-closed containment, identity and a prune lifecycle.
- **`Measure` is independent of the paint.** Geometry survives any appearance change; a transparent
  button keeps its height. This is the load-bearing property that makes Appearance safe to change.
- **Colour and density are reachable from data.** `Tone`/`Emphasis`/`Scheme`/`Density` plus
  the resolved table: no compiler needed to re-skin a page, and unknown values fall back loudly.
- **Input arbitration and the disabled rule are shared, not per-widget.** One funnel call carries the
  hit stack, the popup yield and the disabled refusal for every kind, including unknown ones.
- **A parent resize needs no business code and leaves nothing stale.** Model B, measured.
- **The backend is closed.** Only the five funnel files touch IMGUI, and a lane with a positive control
  enforces it.

### 6.2 Coupled, but cheap today — leave it

- **Hover/armed are recomputed each `Draw`.** With exactly one renderer per kind the cost is two funnel
  calls; publishing them would add a lifetime and invalidation question nobody has asked yet.
- **`UiNode` publishes `IsDisabled` and nothing else.** It is the only state shared across
  widgets today, and it is published by the engine rather than by a widget, which is the right direction.
- **The 14 outlets are pinned rather than extensible.** An addition needs two edits; no consumer has
  needed one.

### 6.3 Coupled in a way that blocks reuse or declarative layout — the complete inventory

Grouped by the question that is short an answer, each row with its evidence and what it blocks. These are
observations: none of them is a proposal, and a row becomes work only when a maintainer decides it does.
§6.1 is the other half of this picture — this is a gap inventory, not a verdict on the library.

**Structure**

| Missing | Evidence | What it blocks |
|---|---|---|
| A control cannot compose its own visual parts | `<Widget>` may not carry children (`UiLayoutManifest.cs:542`, refused as a `FormatException`); `ctx.Child(name)` mints a sub-node that carries identity and state but **no arranged geometry** and **no stylable kind/role** (`UiWidgetContext.cs:139-151`) | Restructuring a control's visuals needs C#; Appearance cannot address a control's internals |

**Layout**

| Missing | Evidence | What it blocks |
|---|---|---|
| Alignment: none | no `Align`/`Justify`/`Margin` name exists in the engine at all; text alignment is a `TextAnchor` literal inside each widget (`ButtonWidget.cs:113`) | "Which side is this on" is answered by Appearance or by nobody |
| Relations: none | no anchor, no named-sibling reference, no distance-from-edge. The only real relation is hand-written: `UiPopup.RectFor` (below/above, viewport flip and clamp) beside the constant `OptionHeight = 24f` (`UiPopup.cs:19,28`) | Experiment 3's centring and bottom edge are *idioms* — two equal spacers, a `Fill` sibling that ate the rest — not declarations |
| Density cannot reach the layout layer | `UiLayoutEngine.cs` never reads `theme.Geometry` (zero references); container `Padding`/`Gap` are XML-only with a default of zero (`ParsePadding` → `Padding.Zero`, `ReadGap` → `0f`), while every density token is consumed **inside widget code** (`Geometry.Padding`/`Gap`/`Spacing`/`RowHeight` across the core atoms) | A theme change moves what happens *inside* a control but not the *relationships between* containers: the page's rhythm is frozen in XML. Also no per-part inset |
| Responsiveness: one dimension | one `Breakpoint` per container, one `Narrow` replacement kind, `Cols`/`NarrowCols`, `NarrowHidden` | No named breakpoints shared by a page, no multi-step variants, no per-element variant table |

**Semantics**

| Missing | Evidence | What it blocks |
|---|---|---|
| Behaviour is not separable from painting | `ButtonWidget.Draw` hits, decides state and paints in one method; experiment 1c (no configuration of the core kind paints nothing) and 1b (painting nothing costs a new kind) | One behaviour rendered two ways; replacing a look without freezing vocabulary |
| Behaviour is not a shareable unit | `kind → factory → painter` is one axis (`UiWidgetRegistry.cs:38`); experiment 2a | The second visual retypes the hit/invoke pair and the contract around it |
| State has no public read side | a node publishes `IsDisabled` and no hover/armed (measured); both are computed inside `Draw` | A second renderer cannot read the state it renders; no consumer can observe it |
| The input contract has no declaration site | implicit in which funnel call a widget happens to make; the containment lane sees backend contact, not a contract | "What input does this kind take" is unstateable |
| Focus is not an engine concept | `UiSession` has no focus surface; only the number field holds a private `Focused` in its value state | **Disclosure:** experiment 1's request listed focus and the lane did not verify it — there is nothing to verify |
| The semantic axes are thin | `Tone` (five values) plus `Emphasis` (two values, one of which moves only the `neutral` cell) | No prominence or size axis for a style-free semantic vocabulary |

**Appearance**

| Missing | Evidence | What it blocks |
|---|---|---|
| No shape vocabulary | the 14 outlets paint rectangles, borders, text and solids, and the list is pinned by the probe lane | No rounded, 9-slice or transformed surface; bounded by the backend, so the vocabulary must be closed over it |
| No image outlet | none of the pinned outlets paints an image | Icon-only controls and any texture-based skin |
| No per-part or per-state-per-part style key | the resolved table is keyed `(tone, emphasis, writability)` (`UiStyleTable.Resolve`) | A kind cannot declare "these are my styleable slots"; Appearance can only answer "what colour is this surface" |
| Promises wider than delivery (L2) | `Emphasis` differs only on `neutral` (`UiResolvedStyle.cs:9-13`); `Warning` and `Danger` share one treatment | The names read broader than they are |

**The two cross-cutting rules**

- **Resolution** covers only `Scheme`/`Density` inheritance. XML `Gap` and the `Gap` density token are
  not in conflict today because they never meet — the engine reads only the attribute and the widgets read
  only the token (see the Layout row above) — while `Height` against `Fill` is settled by ad-hoc engine
  code rather than by a stated rule.
- **Ownership** is real but not queryable: the session owns hover, capture, focus, drafts and selection,
  and only `IsDisabled` is ever published.

**Closure**

- **L0 is done** — three parsers refuse an unknown name at creation.
- **L1 has no owner.** No lane asserts that every declared name has a live consumer, or that every
  attribute a kind declares is actually read. A token can be added to the parser and the token struct,
  be accepted, be stored, do nothing, and report nothing.
- **L2** has two known instances (above) and no contract either way.

**Verification and contract**

- No lane treats the four vocabularies *as* vocabularies: element names, attributes, style tokens and
  kind contracts each have partial coverage, and nothing answers "which names does this library promise
  an author".
- `UiLayoutSnapshot`'s measure/draw halves are exercised only by the harness.
- This document is a map, not a contract: it states no refusal rule and no owner per question, so by
  itself it cannot constrain the next change.

## 7. What each vocabulary promises, and what "closed" means here

The four questions each expose a vocabulary, and a vocabulary is only trustworthy when its promise and
its delivery are the same size. Three failure levels, all observed in this tree:

| Level | Shape | Today |
|---|---|---|
| **L0 — unknown name** | a name no vocabulary declares | **refused at creation, loudly** (attribute schema, manifest parser, style parser) |
| **L1 — orphan name** | declared, parsed, stored, and read by nothing | **not checked by any lane.** A name can be added to the token list and simply do nothing |
| **L2 — promise wider than delivery** | a name that works, but far more narrowly than it reads | **two known instances**: `Emphasis` is declared on every text-bearing kind and changes only the `neutral` cell (`UiResolvedStyle.cs:9-13`); `Warning` and `Danger` resolve to one treatment |

L0 is already the library's habit. L1 is the one with no owner: nothing prevents a vocabulary from
growing a name that no code consumes, and nothing would report it. L2 is a naming debt rather than a bug —
either the delivery widens or the name narrows, and leaving it is a decision, not an accident.

## 8. Re-running the measurement

```powershell
dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release   # the whole harness
pwsh -NoProfile -File scripts/verify-local.ps1                              # the gate suite
```

The probe block prints under `Kernel architecture probes`. Its assertions are the measurements above;
its `note:` lines record costs that are real but not pass/fail (the retyped contract, the cache
behaviour). If a future change splits behaviour from painting, experiments 1b, 1c and 2a are the lanes
that will change their verdict first.

## 9. Not answered here

- No target architecture, no migration, no schedule. §6.3 lists observations by request; acting on them
  is a separate maintainer decision.
- No statement about the game: every measurement is over the harness stubs. The in-game behaviour of
  these paths is unverified here, as everywhere in this repository that has not run a session.
- No consumer evidence. Nothing in this document raises the validated-surface count.

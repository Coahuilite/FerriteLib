# FerriteLib UiKit — concerns and the frozen 0.7 implementation

This map describes the source inspected on **2026-10-18**. It does not promote historical harness
measurements into current runtime acceptance. The [0.7 contract](development/0.7/05-api-contract.md) and
[API tiers](api-tiers.md) define existing consumption; the [0.8 specification](development/0.8/README.md)
defines the next design.

## Four parallel questions about one UI

| Concern | Question | Current implementation homes |
| --- | --- | --- |
| **Structure** | Which elements exist, and who contains whom? | `UiElementSpec`, `UiLayoutManifest`, templates, `UiNode` identity |
| **Layout** | How much space is needed, who allocates it, and where does it go? | `UiLayoutEngine`, `UiPlacement`, `ITextMetrics`, layout snapshots |
| **Semantics** | What capability is offered, and under what interaction rules? | bindings, kind validation, `UiNative`, widget behavior |
| **Appearance** | How is the result presented? | `UiTheme`, `UiStyleDocument`, `UiStyleResolver`, `UiThemeDraw`, widget paint |

These are concerns, not a stack or four assemblies. **Resolution** (which source wins) and **Ownership**
(which live instance holds and changes state) apply across them. A headless capability still has semantics;
removing paint does not make the other three questions meaningless.

The existing delivery split is different: the page engine owns retained trees and sessions, while the
visual core offers themes, drawing, measurement, fit audit and the version contract without a page host.
Both ship in `FerriteLib.UiKit.dll`. `KernelContractTests.VerifyVisualCoreIsPageModelFree` is a source-name
boundary check, not a proof of arbitrary transitive dependency freedom. The directory name `Kernel` is not
the definition of stable semantic core in the 0.8 design.

## What the current source provides

### Structure, state and lifecycle

- The manifest has a closed structural vocabulary with creation-time validation. `Widget` does not accept
  arbitrary child elements; repeated rows are materialized through templates.
- A host owns a session. Nodes and named value slots provide instance identity and state; widget kinds
  are not process-wide stores of interaction state.
- `UiSession.PruneNodesExcept` releases removed identities and their owned state. Hiding a declared
  element does not prune its node or value slots, but 0.7 ends its active edit and may reset the draft from
  the model when it returns. Node retention is not draft preservation. The
  [consumer migration](consumers/consume-from-0.7.0.md) separates these cases in §4c; preserving a draft
  across temporary hiding belongs to the 0.8 target contract.
- `UiWindowHost` supplies a window shell. `UiHost` is also the page engine entry for an existing host;
  it does not turn application models into library-owned business state.

### Layout

Flow containers, intrinsic text measurement, `Width="Auto"`, explicit bounds, `Fill`, responsive
`Breakpoint`/`Narrow`, visibility rules and scrolling already remove routine rectangle arithmetic from
page authors. `Height="MatchContent"` and scroll `VisibleRows` cover particular measured-size needs.

`UiPlacement` implements parent-relative placement:

```text
origin = parentOrigin + fraction * (parentSpan - selfSpan) + offset
```

`AlignX`/`AlignY` and `OffsetX`/`OffsetY` operate in the parent's inner box. `Overlay` can use both axes;
flow owns its main axis and admits only the permitted cross-axis placement. Invalid combinations are
creation-time failures. Absent container `Padding`/`Gap` use theme geometry; explicit values, including
zero, win. Named sibling relations and shared label columns are **0.8 scope**, not existing 0.7 features.

### Behavior and input

`UiNative` centralizes native contact and important input rules. `CanReceivePointerPress` accounts for
eligibility and popup coverage; the session tracks capture and an active edit. `UiHost` handles the current
Accept/Cancel ladder before the field's draw applies its pending edit request.

The split is incomplete: `ButtonWidget.Draw` still derives its visual hover/armed state, paints and invokes
the bound command. `Chrome="none"` can suppress its chrome, but that is not a reusable, presentation-free
Button behavior object. Sharing a low-level press helper is narrower than sharing the complete capability
contract. The [0.8 specification](development/0.8/README.md) addresses this seam without adding a second
headless state machine.

The current string wrapper is single-line and calls the native text field. Its `Live` default is `true`.
The generic Accept path and read-only checks do not establish a complete multiline/IME/read-only editing
contract. The next specification's native-based single-line/basic-multiline scope is not a claim that those
gaps have already been implemented.

### Appearance and diagnostics

- `UiPalette` owns sparse neutral colour declarations over immutable per-role defaults. `UiTheme` is its
  platform paint adapter and retains existing typography/density settings. Ordinary authors can configure
  the palette directly; selected/success and warning/danger meanings are independent. See the0.8 theme
  migration guide for the removed historical colour bag and XML syntax; no compatibility double path remains.
- Colour and layout invalidation are distinct (`ColourRevision` / `LayoutRevision`). Font selection and
  density are separate inputs. Style resolution refreshes scoped clones when either relevant baseline
  revision changes.
- The drawing surface includes `UiThemeDraw.Image` and `FitImage`; the old claim that there is no image
  outlet is obsolete. Current appearance selection for checkbox/dropdown is narrower than arbitrary
  replacement of a control's entire presentation and internal arrangement.
- The per-host Dev instrument has a text dump, structured snapshot and optional outline. Its observations
  do not govern input or layout. The [operations guide](build-and-debug.md) states what a report can show.

## Source anchors

All paths below are repository-local and describe source, not installed bytes:

- [Identity and state](../Source/FerriteLib.UiKit/Kernel/UiNode.cs),
  [session lifecycle](../Source/FerriteLib.UiKit/Kernel/UiSession.cs).
- [Layout engine](../Source/FerriteLib.UiKit/Kernel/UiLayoutEngine.cs),
  [placement rule](../Source/FerriteLib.UiKit/Kernel/UiPlacement.cs).
- [Button](../Source/FerriteLib.UiKit/Kernel/Widgets/ButtonWidget.cs),
  [text wrapper](../Source/FerriteLib.UiKit/Kernel/Widgets/TextFieldWidget.cs),
  [native boundary](../Source/FerriteLib.UiKit/Kernel/UiNative.cs),
  [host input](../Source/FerriteLib.UiKit/Kernel/UiHost.cs).
- [Theme](../Source/FerriteLib.UiKit/Kernel/UiTheme.cs),
  [style resolution](../Source/FerriteLib.UiKit/Kernel/UiStyleResolver.cs),
  [drawing outlets](../Source/FerriteLib.UiKit/Kernel/UiThemeDraw.cs).

## What the 2026-09-18 architecture probe established

`KernelArchitectureProbeTests` originally compared transparent standard buttons, a no-paint custom kind,
two different visuals using the same press helper, and resize-driven layout. At that revision, transparency
preserved hit behavior and geometry; no-paint operation was possible through a custom kind; replacing the
whole visual still retyped wrapper behavior; parent resizing re-evaluated and cached layout without
business-side rectangle updates. These were **stub measurements**, not game or consumer acceptance.

That record motivated the separation questions. Its old outlet counts, absence-of-image assertion,
absence-of-alignment assertion, line numbers and exhaustive gap inventory are not current facts. The probe
source has evolved too; a new run measures that run's revision, not the historical experiment. For the
bounded later game observations, use the [0.7 verification record](development/0.7/40-verification.md).

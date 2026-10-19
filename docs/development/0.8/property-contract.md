# Per-property appearance declarations

Declare a font or semantic colour directly, or apply one named Scheme and Density:

```xml
<Stack Id="form" Scheme="muted" Density="compact" Font="Medium">
  <Widget Id="title" Kind="text/wrapped" Text="Settings" Font="Tiny" />
  <Widget Id="name" Kind="input/text-field" Bind="name" Label="Name" Palette.Foreground="#FFFFFF" />
</Stack>
```

The default wrappers need no palette configuration. An ordinary author can change one semantic colour
on the host, or use `Palette.Role` on one declaration, without subclassing or finding a private part:

```csharp
var theme = new UiTheme();
theme.Palette[UiPaletteRole.Accent] = new UiColour(.25f, .6f, .9f);
// Pass theme through the existing UiHost / UiPageWindow constructor.
```

`UiColour`, `UiPaletteRole` and `UiPalette` are neutral production contracts compiled unchanged by the
runtime and engine-free target. Thirty finite paint meanings cover window/panel/control/hover/selected,
workspace/section, foregrounds, edges, accent, switch-off, success, warning and danger. Warning and danger
have independent surface/foreground/border declarations; success text is independent of selection text.
The shipped defaults deliberately preserve the previous rendered colours, font and geometry. Equal default
colours do not alias declarations. Defaults are returned as immutable values, never a mutable shared theme.
`UiTheme` adapts that palette to platform paint and retains existing font/density settings; it has no old
26-bit colour bag. Its platform colour indexer and surface queries read the same palette result.

Named styles use `<Palette Role="WarningSurface" Value="#802010"/>` inside the existing Scheme. The neutral
parser accepts #RGB/#RGBA/#RRGGBB/#RRGGBBAA and invariant r,g,b[,a], with finite channels in0..1. Transparent
is an explicit colour; null is not an edge-fallback command. Unknown roles are reported rather than
translated from old names. `Color.Token`, `<Color Token="...">` and old colour properties are removed in
this 0.8 development change; see [the exact migration](theme-migration.md). Standalone styles retain their
recorded fail-soft policy; inline manifest attributes retain located creation-time validation.

P1's colour direction and ordinary-author access are approved. This change does not open new internal
RowHeight/Density/structure configuration. The previously available font/five-metric inputs and same-level
Scheme/Density ordering are preserved, with their unapproved opening-set details still separate. Business
values, bindings, tone, drafts, interaction policy, capture and final geometry do not inherit as colours.
No selector, expression language, second component system or theme compatibility path is introduced.
## Keep written intent separate from resolved values

For each property, resolution is: runtime instance declaration, explicit inline instance declaration;
runtime local/derived-wrapper declaration, the explicitly named local style; nearest ancestor's same
written sources; page-style declaration; host declaration; applicable wrapper/library fallback.
Runtime and inline declarations are deliberately distinct: removing a runtime override reveals an
inline declaration if the current component definition still contains it. Removing an inline declaration
uses the normal immutable definition/update path. A resolved parent theme is never copied into the
child's declaration store. Sparse named styles leave missing properties open to the remaining sources.

P1 also proposes one Scheme and one Density at the same level. One Scheme supplies its declared colours/font/metrics. One named Density applies after that Scheme on
a metric both explicitly define, preserving the old deterministic same-level ordering. A runtime local
property takes precedence over the named local property. No arbitrary style-list combination is built.
Font is independent of all density metrics. A local Gap does not replace an ancestor RowHeight with a
host fallback. Container Padding/Gap still own their explicit layout overrides, rather than becoming
blanket inherited geometry declarations.

```csharp
UiNode field = host.Session.GetNodeByElementId("name")!;
field.Properties.Font.Set(UiFont.Tiny);
field.Properties.Palette(UiPaletteRole.Foreground).Set(new UiColour(1, .9f, .7f));
field.Properties.Font.Remove();                 // reveal the current remaining font source
field.Properties.Palette(UiPaletteRole.Foreground).Remove(); // remove just this colour override
```

`ResolvedFont`, `ResolvedMetrics` and `ResolvedColours` expose the current effective source and value;
individual metrics can come from different sources, in which case ResolvedDensity reports Mixed and
the metric facts carry the details. Named-source facts identify Scheme/Density; ancestor facts identify
the actual node. Equal-valued explicit declarations retain their source/following intent.
Colours affect paint and do not dirty measurement. Font/density changes dirty measurement. Draw resolves
paint from current declarations even when the layout snapshot remains cached. Each unassigned role reads its own immutable default; shared default values do not imply coupled overrides.

Host page-style application preserves declaration intent: a sparse document metric is not promoted to
a host declaration for the rest of its density bundle. The retained public UiStyleResolver.ApplyTo API
still explicitly applies a document to a caller-owned theme, including its established downstream use
as an independently supplied theme. Plain page-level effective themes retain the original theme object
identity when no effective node values differ. Failed arrangement restores derived authoring facts with
geometry, without undoing runtime declaration changes or any business side effects.

## Complete labelled-field participation

All five independent label+field wrappers participate in SharedLabels: Slider, NumberField, TextField,
StepperSlider and Dropdown. Dropdown keeps its legacy80px column when the group has not opted in;
inside a shared group it consumes exactly the same natural column/narrow stacking rule. Its trigger's
established operation now precedes pure field painting; a paint exception cannot swallow a qualified
release. Dropdown/ModeRow/popup text consumes the effective font, including both Dropdown looks.
Checkbox whole-content labels and descriptive headings are not misrepresented as label+field pairs.

Evidence: actual host sparse-style/programmatic-ancestor/inline/equal-value/removal/source/paint-only
and independent-role regressions in KernelStyleScopeTests; five-wrapper actual labels/font/narrow geometry,
failed-authoring recovery and real Event/paint-failing Dropdown in KernelLayoutTests. Existing full
style/layout/input contracts remain tested. Unapproved E/N and typography/density opening-set details remain reversible Dev candidates (see editing-contract.md for B1/N1 approvals); these
engineering source-resolution rules do not reopen approved F1/F2/F3. Native1.6 appearance acceptance
is still pending.

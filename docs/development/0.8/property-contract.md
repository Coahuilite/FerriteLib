# Per-property appearance declarations

Declare a font or semantic colour directly, or apply one named Scheme and Density:

```xml
<Stack Id="form" Scheme="muted" Density="compact" Font="Medium">
  <Widget Id="title" Kind="text/wrapped" Text="Settings" Font="Tiny" />
  <Widget Id="name" Kind="input/text-field" Bind="name" Label="Name" Color.TextPrimary="#FFFFFF" />
</Stack>
```

P1 implementation remains a Dev candidate. The latest product direction requires a new theme decoupled from the legacy26 slots, with compatibility adapters preserving old entry points and data meaning; that redesign is not implemented or frozen by this candidate. The current implementation covers typography, the five existing density metrics, and all26 existing
colour tokens (including nullable per-surface edges). It does not inherit business values, bindings,
roles, drafts, interaction policy, capture, final geometry or container-specific layout declarations.
`Color.Token` uses the existing document colour parser; invalid fonts/colours at the host/manifest
creation boundary are located declarations, not ignored input. Named appearance documents keep their
existing fail-soft recorded-issue policy. No selectors, style-expression language or general policy
registry is introduced.

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
field.Properties.Colour("TextPrimary").Set(new Color(1, 0.9f, 0.7f, 1));
field.Properties.Font.Remove();                 // reveal the current remaining font source
field.Properties.Colour("TextPrimary").Remove(); // remove just this colour override
```

`ResolvedFont`, `ResolvedMetrics` and `ResolvedColours` expose the current effective source and value;
individual metrics can come from different sources, in which case ResolvedDensity reports Mixed and
the metric facts carry the details. Named-source facts identify Scheme/Density; ancestor facts identify
the actual node. Equal-valued explicit declarations retain their source/following intent.
Colours affect paint and do not dirty measurement. Font/density changes dirty measurement. Draw resolves
paint from current declarations even when the layout snapshot remains cached. Derived ink such as an
undeclared SwitchThumbOff follows the effective TextPrimary instead of a frozen copied fallback.

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
and derived-ink regressions in KernelStyleScopeTests; five-wrapper actual labels/font/narrow geometry,
failed-authoring recovery and real Event/paint-failing Dropdown in KernelLayoutTests. Existing full
style/layout/input contracts remain tested. Unapproved E/N details and P1 remain reversible Dev candidates (see editing-contract.md for B1/N1 approvals); these
engineering source-resolution rules do not reopen approved F1/F2/F3. Native1.6 appearance acceptance
is still pending.

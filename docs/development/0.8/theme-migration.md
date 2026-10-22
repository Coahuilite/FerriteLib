# Use the default look, then override one colour

All default widgets and retained visual helpers work with `new UiTheme()` without palette assignments.
The default appearance is unchanged. For a host-wide tint:

```csharp
var theme = new UiTheme();
theme.Palette[UiPaletteRole.Accent] = new UiColour(.25f, .6f, .9f);
```

For one ordinary default widget, keep its content/bindings and add a local declaration:

```xml
<Widget Id="apply" Kind="input/button" ActionBind="apply" Text="Apply"
        Palette.ControlSurface="#203040" Palette.Foreground="#FFFFFF" />
```

A reusable sparse Scheme uses the existing style document service:

```xml
<Styles Schema="1" Scheme="quiet">
  <Scheme Name="quiet">
    <Palette Role="WarningSurface" Value="#804020" />
    <Palette Role="WarningForeground" Value="#FFFFFF" />
  </Scheme>
</Styles>
```

No custom class, registration, private-part lookup or behavior assembly is required. A runtime override
uses `host.ElementProperties("elementId").Palette(UiPaletteRole.Foreground).Set(new UiColour(...))`; `Remove()` reveals the
current inline/local/ancestor/page/host/default source. `LocalPalette(role)` is the corresponding explicit
local declaration. Source facts remain available in `ResolvedColours`, keyed by the semantic role name.
The platform `UiTheme[role]` indexer accepts/returns Unity colours for standalone visual callers; it adapts
the same neutral palette, not a second store. New author code can remain neutral by using `theme.Palette`.

## What changed from the old development theme

This is an intentional 0.8 theme break, not a compatibility redirect. The old26-bit bag, old named colour
properties, Warning→Danger alias, nullable per-surface fallback and old colour XML vocabulary are removed.
No old/new synchronization or fallback parser remains. Other widget kinds, bindings, font/density inputs,
layout, editing, pointer timing, host lifecycle and visual-helper uses retain their contracts.

| Old declaration | New role |
| --- | --- |
| Base / Panel / Raised | WindowSurface / PanelSurface / ControlSurface |
| Hover / Selected / Success / Danger | HoverSurface / SelectedSurface / SuccessSurface / DangerSurface |
| Warning alias | WarningSurface (independent) |
| WorkspacePlane / SectionBand | WorkspaceSurface / SectionSurface |
| TextPrimary / TextSecondary / TextDisabled | Foreground / SecondaryForeground / DisabledForeground |
| TextOnGold | SelectedForeground; explicitly use SuccessForeground for success |
| TextOnDanger | DangerForeground; explicitly use WarningForeground for warning |
| AccentGold / SwitchThumbOff | Accent / SwitchOff |
| Border / BorderStrong / Divider | Border / StrongBorder / Divider |
| BaseBorder / PanelBorder / RaisedBorder | WindowBorder / PanelBorder / ControlBorder |
| HoverBorder / SelectedBorder / SuccessBorder / DangerBorder | same semantic role names; add WarningBorder independently |
| `Color.Name="..."` | `Palette.Role="..."` |
| `<Color Token="Name" Value="..."/>` | `<Palette Role="Role" Value="..."/>` |
| `theme.Name = UnityColor` | `theme.Palette[role] = neutralColour` (or platform `theme[role]`) |
| `Properties.Colour("Name")` / `LocalColour(...)` | `Properties.Palette(role)` / `LocalPalette(role)` |

An old null edge meant “use another token”; new roles always contain a concrete colour. Declare the
chosen border explicitly, use alpha0 to hide it, or remove the declaration to restore its own default.
Changing Accent no longer implicitly changes SelectedBorder; changing Foreground no longer implicitly
changes SwitchOff. Assign both roles when that coupling is wanted. Defaults preserve the old effective
colours while allowing those meanings to vary independently. Font and five density metrics are not new
palette roles. Their confirmed finite opening uses `Font` and `Metric.Padding/Spacing/Gap/RowHeight/Hairline`,
or individual `Properties.Metric(name)` / `theme.Metric(name)` declarations. Set/Remove affects only that
property and exposes the current remaining source. Bare container Padding/Gap still allocate its own
contents; they are not copied into each child control. Private default structure remains closed.

## Consumer handoff and verification boundary

The sole existing consumer has not adopted this Dev. Its owner must migrate actual old theme assignments
and colour XML using the table, then compile against the fixed0.8 package. Its existing explicit Live=true
requirement is independent of palette migration. No consumer checkout was edited here.

The engine-free target compiles the actual neutral parser/palette source. Platform fixtures retain the
full widget/visual inventory and exercise source/removal, sparse styles, current-host declarations,
same-kind/two-host isolation, tint without geometry/request changes, real held capture and existing draft
preservation. GUI state is restored by the existing outlets. Exact native1.6 pixels, fonts and window-stack
behavior remain a separate pending acceptance; fixture colour records do not certify native appearance.

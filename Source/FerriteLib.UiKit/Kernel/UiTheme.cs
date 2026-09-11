using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// A surface treatment: the plane a surface paints and the colour of the edge it is outlined with.
/// The bag used to carry one global border for every plane, which cannot express a chrome family whose
/// planes are outlined differently — including the game's own look, where a window edge and a button
/// edge are not one grey.
/// </summary>
public readonly struct UiSurfaceStyle
{
    public UiSurfaceStyle(Color fill, Color border)
    {
        Fill = fill;
        Border = border;
    }

    /// <summary>The colour a painted surface fills its rect with.</summary>
    public Color Fill { get; }

    /// <summary>The colour of the edge every painted surface carries.</summary>
    public Color Border { get; }
}

/// <summary>
/// The density axis in one bundle: the numbers a kind needs to place content, kept together so a
/// consumer can change how dense the whole tree is without editing a widget. The five values are the
/// literals the widgets used to hold privately (28f, 6f, 4f, 1f and their copies), named here once.
/// <para>
/// Negative inputs are clamped to zero instead of refused: these are appearance values, and appearance
/// failures stay soft — a density never takes a page down. Deliberately absent is a corner radius: the
/// series' look is flat by ruling (no gradient, no rounding, no texture, no drop shadow), so a radius
/// token would be surface nobody consumes.
/// </para>
/// </summary>
public readonly struct UiGeometry : IEquatable<UiGeometry>
{
    public UiGeometry(float padding, float spacing, float gap, float rowHeight, float hairline)
    {
        Padding = Math.Max(0f, padding);
        Spacing = Math.Max(0f, spacing);
        Gap = Math.Max(0f, gap);
        RowHeight = Math.Max(0f, rowHeight);
        Hairline = Math.Max(0f, hairline);
    }

    /// <summary>Distance from a painted surface's edge to the content it wraps (a field's text inset).</summary>
    public float Padding { get; }

    /// <summary>Distance a composite leaves between its own edge and the cells it lays out.</summary>
    public float Spacing { get; }

    /// <summary>Distance between two sibling cells of a row or a wrap.</summary>
    public float Gap { get; }

    /// <summary>Height of a control that declares no <c>Height</c> attribute of its own.</summary>
    public float RowHeight { get; }

    /// <summary>Width of a rule or of a surface edge.</summary>
    public float Hairline { get; }

    /// <summary>The shipped density: the numbers every widget hard-coded before they moved here.</summary>
    public static UiGeometry Default => new(6f, 4f, 6f, 28f, 1f);

    public bool Equals(UiGeometry other)
    {
        return Padding == other.Padding
            && Spacing == other.Spacing
            && Gap == other.Gap
            && RowHeight == other.RowHeight
            && Hairline == other.Hairline;
    }

    public override bool Equals(object? obj)
    {
        return obj is UiGeometry other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + Padding.GetHashCode();
            hash = hash * 31 + Spacing.GetHashCode();
            hash = hash * 31 + Gap.GetHashCode();
            hash = hash * 31 + RowHeight.GetHashCode();
            hash = hash * 31 + Hairline.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(UiGeometry left, UiGeometry right) => left.Equals(right);

    public static bool operator !=(UiGeometry left, UiGeometry right) => !left.Equals(right);
}

/// <summary>
/// Theme token bag injected by the host. It is the value source for every appearance decision the
/// library makes: the painting outlets read it, and <see cref="Styles"/> is the one table that turns
/// its tokens into the fill/border/text answer a surface actually paints.
/// </summary>
public sealed class UiTheme
{
    private UiFont defaultFont = UiFont.Small;
    private UiGeometry geometry = UiGeometry.Default;

    public UiTheme()
    {
        // One resolved-value store per theme instance, built here and never replaced. The injecting
        // host holds one theme per window, so a store is per-window in practice and two windows never
        // share one; the store is a read-through view of this bag, so re-tinting the theme still moves
        // the answer and no second copy of a colour exists.
        Styles = new UiStyleTable(this);
    }

    // Surfaces: restrained near-black planes with small luminance steps.
    public Color Base { get; set; } = new(0.065f, 0.065f, 0.063f, 1f);
    public Color Panel { get; set; } = new(0.095f, 0.095f, 0.090f, 1f);
    public Color Raised { get; set; } = new(0.135f, 0.135f, 0.128f, 1f);
    public Color Hover { get; set; } = new(0.17f, 0.17f, 0.16f, 1f);
    public Color Selected { get; set; } = new(0.17f, 0.14f, 0.08f, 1f);
    public Color Success { get; set; } = new(0.11f, 0.24f, 0.15f, 1f);
    public Color Danger { get; set; } = new(0.38f, 0.14f, 0.11f, 1f);

    // Named planes for reusable multi-column chrome primitives.
    public Color WorkspacePlane { get; set; } = new(0.045f, 0.045f, 0.044f, 1f);
    public Color SectionBand { get; set; } = new(0.085f, 0.085f, 0.080f, 1f);

    // Text
    public Color TextPrimary { get; set; } = new(0.88f, 0.88f, 0.84f, 1f);
    public Color TextSecondary { get; set; } = new(0.58f, 0.58f, 0.55f, 1f);
    public Color TextOnGold { get; set; } = new(1f, 0.84f, 0.55f, 1f);
    public Color TextOnDanger { get; set; } = new(1f, 0.65f, 0.46f, 1f);
    public Color TextDisabled { get; set; } = new(0.42f, 0.42f, 0.40f, 1f);

    // Accents: reserve gold for activity and focus states.
    public Color AccentGold { get; set; } = new(0.82f, 0.60f, 0.22f, 1f);
    public Color HoverPoint { get; set; } = new(0.96f, 0.80f, 0.42f, 1f);

    /// <summary>
    /// The accent at a reduced alpha. Derived rather than stored: a consumer that re-tints
    /// <see cref="AccentGold"/> must not be left with alpha variants still holding the old hue.
    /// </summary>
    public Color AccentWith(float alpha) => new Color(AccentGold.r, AccentGold.g, AccentGold.b, alpha);

    // Shared borders. These stay the defaults for every surface that does not override its edge, so a
    // consumer that re-tints the bag the old way still moves every plane it used to move.
    public Color Border { get; set; } = new(0.21f, 0.21f, 0.20f, 1f);
    public Color BorderStrong { get; set; } = new(0.32f, 0.32f, 0.30f, 1f);
    public Color Divider { get; set; } = new(0.14f, 0.14f, 0.13f, 1f);

    // Per-surface edge overrides. Null — the default — means "use the shared token above"; a value
    // gives exactly one surface an edge of its own, which is what the per-surface restructure is for.
    public Color? BaseBorder { get; set; }
    public Color? PanelBorder { get; set; }
    public Color? RaisedBorder { get; set; }
    public Color? HoverBorder { get; set; }
    public Color? SelectedBorder { get; set; }
    public Color? SuccessBorder { get; set; }
    public Color? DangerBorder { get; set; }

    /// <summary>Substrate surface: the plane a disabled or recovering element sits on.</summary>
    public UiSurfaceStyle BaseSurface
    {
        get => new(Base, BaseBorder ?? Border);
        set { Base = value.Fill; BaseBorder = value.Border; }
    }

    /// <summary>Panel surface: the standard window/box plane.</summary>
    public UiSurfaceStyle PanelSurface
    {
        get => new(Panel, PanelBorder ?? Border);
        set { Panel = value.Fill; PanelBorder = value.Border; }
    }

    /// <summary>Raised surface: fields, option cells and other controls above the panel plane.</summary>
    public UiSurfaceStyle RaisedSurface
    {
        get => new(Raised, RaisedBorder ?? Border);
        set { Raised = value.Fill; RaisedBorder = value.Border; }
    }

    /// <summary>Hover surface: the plane a chrome control takes under the pointer.</summary>
    public UiSurfaceStyle HoverSurface
    {
        get => new(Hover, HoverBorder ?? BorderStrong);
        set { Hover = value.Fill; HoverBorder = value.Border; }
    }

    /// <summary>Selected surface: the active/checked treatment, outlined with the accent by default.</summary>
    public UiSurfaceStyle SelectedSurface
    {
        get => new(Selected, SelectedBorder ?? AccentGold);
        set { Selected = value.Fill; SelectedBorder = value.Border; }
    }

    /// <summary>Success surface.</summary>
    public UiSurfaceStyle SuccessSurface
    {
        get => new(Success, SuccessBorder ?? BorderStrong);
        set { Success = value.Fill; SuccessBorder = value.Border; }
    }

    /// <summary>
    /// Alarm surface. It is the one treatment behind both <c>UiStatusTone.Warning</c> and
    /// <c>UiStatusTone.Danger</c>: the bag carried those as two names for one RGB with no user in
    /// either repository, and a token nobody shapes apart is debt, so one name went. A citation that
    /// needs them distinguishable adds the second surface and the second table row.
    /// </summary>
    public UiSurfaceStyle DangerSurface
    {
        get => new(Danger, DangerBorder ?? Danger);
        set { Danger = value.Fill; DangerBorder = value.Border; }
    }

    // Typography. DefaultFont is geometry-bearing (it feeds text measurement); the colour tokens
    // above are not, and KernelContractTests holds that line. Both this and Geometry are the two
    // tokens that move a rect, which is what LayoutRevision reports.
    public UiFont DefaultFont
    {
        get => defaultFont;
        set
        {
            if (value == defaultFont) return;
            defaultFont = value;
            LayoutRevision++;
        }
    }

    /// <summary>The density bundle a widget reads instead of holding its own literals.</summary>
    public UiGeometry Geometry
    {
        get => geometry;
        set
        {
            if (value == geometry) return;
            geometry = value;
            LayoutRevision++;
        }
    }

    /// <summary>
    /// Bumped whenever a layout-bearing token moves: the font size or the density. Colour tokens
    /// deliberately do not bump it — a lane holds that no colour can move a rect, and re-measuring a
    /// page because somebody re-tinted it would be cost for nothing. The band cache cannot see the
    /// theme, so the host reads this and turns it into the one cache clock the engine compares.
    /// </summary>
    public int LayoutRevision { get; private set; }

    /// <summary>
    /// The theme's resolved-value store. Queries are answered from the current tokens, so a consumer
    /// that re-tints this theme sees the new value on the next query; the store itself never changes
    /// after construction and is never shared between two themes.
    /// </summary>
    public UiStyleTable Styles { get; }

    /// <summary>
    /// A copy of this bag with the same token values and its own resolved-value store. A region style
    /// builds one so two regions can differ while the injected theme stays exactly what the consumer
    /// handed in. It copies values, not identity: the copy shares no mutable state with the original
    /// (each has its own <see cref="Styles"/>), and moving a token on either one afterwards moves only
    /// that one.
    /// </summary>
    public UiTheme Clone()
    {
        return new UiTheme
        {
            Base = Base,
            Panel = Panel,
            Raised = Raised,
            Hover = Hover,
            Selected = Selected,
            Success = Success,
            Danger = Danger,
            WorkspacePlane = WorkspacePlane,
            SectionBand = SectionBand,
            TextPrimary = TextPrimary,
            TextSecondary = TextSecondary,
            TextOnGold = TextOnGold,
            TextOnDanger = TextOnDanger,
            TextDisabled = TextDisabled,
            AccentGold = AccentGold,
            HoverPoint = HoverPoint,
            Border = Border,
            BorderStrong = BorderStrong,
            Divider = Divider,
            BaseBorder = BaseBorder,
            PanelBorder = PanelBorder,
            RaisedBorder = RaisedBorder,
            HoverBorder = HoverBorder,
            SelectedBorder = SelectedBorder,
            SuccessBorder = SuccessBorder,
            DangerBorder = DangerBorder,
            DefaultFont = defaultFont,
            Geometry = geometry
        };
    }

    /// <summary>
    /// The shipped default palette, handed out as a fresh instance on every call. It is a template,
    /// not a shared singleton: two consumer mods re-tinting "the default" must never repaint each
    /// other. A caller that wants one theme for a whole window keeps the returned instance.
    /// </summary>
    public static UiTheme DarkGold => new();
}

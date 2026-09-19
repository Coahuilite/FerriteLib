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

    // C (0.7, amended 2026-09-18): the constructor is a token bag, not a product. Colour fields
    // start unpainted (default Color / null edges). The two built-in palettes are named peers —
    // <see cref="Vanilla"/> and <see cref="DarkGold"/> — neither is selected by constructing the
    // bag, and neither is history of the other. Geometry and fonts stay on the bag because they
    // are density, not a look. Provenance for Vanilla's substrate/edge/option references: the
    // earlier vanilla <c>Verse.Widgets</c> investigation whose service build identity was NOT
    // established; every other Vanilla value is DERIVED (neutral surfaces, one reserved yellow).
    // The exact vanilla button yellow is not established, so <see cref="AccentGold"/> on Vanilla
    // is a stated candidate.

    public Color Base { get; set; }
    public Color Panel { get; set; }
    public Color Raised { get; set; }
    public Color Hover { get; set; }
    public Color Selected { get; set; }
    public Color Success { get; set; }
    public Color Danger { get; set; }

    public Color WorkspacePlane { get; set; }
    public Color SectionBand { get; set; }

    public Color TextPrimary { get; set; }
    public Color TextSecondary { get; set; }
    public Color TextOnGold { get; set; }
    public Color TextOnDanger { get; set; }
    public Color TextDisabled { get; set; }

    public Color AccentGold { get; set; }
    public Color HoverPoint { get; set; }

    /// <summary>
    /// The accent at a reduced alpha. Derived rather than stored: a consumer that re-tints
    /// <see cref="AccentGold"/> must not be left with alpha variants still holding the old hue.
    /// </summary>
    public Color AccentWith(float alpha) => new Color(AccentGold.r, AccentGold.g, AccentGold.b, alpha);

    // Shared borders. Null per-surface edges mean "use the shared token"; a value gives exactly
    // one surface an edge of its own. The bag starts with unclaimed edges; each named palette
    // decides whether to claim them.
    public Color Border { get; set; }
    public Color BorderStrong { get; set; }
    public Color Divider { get; set; }

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
    /// <c>UiStatusTone.Danger</c>: the bag carried those as two names for one RGB, a token nobody shapes
    /// apart is debt, and the 0.4 window collapsed them. <b>The census that justified the collapse
    /// measured this repository only</b> -- a consumer did compile against the name, so
    /// <see cref="Warning"/> survives as a redirect for one minor rather than disappearing. A citation
    /// that needs the two distinguishable adds the second surface and the second table row.
    /// </summary>
    public UiSurfaceStyle DangerSurface
    {
        get => new(Danger, DangerBorder ?? Danger);
        set { Danger = value.Fill; DangerBorder = value.Border; }
    }

    /// <summary>
    /// The alarm fill under its pre-0.4 name. A consumer compiled against that name while the 0.4 window
    /// collapsed it into <see cref="Danger"/> -- one name for a fill and the other for a border inside one
    /// expression -- so deleting it broke a build instead of a pixel. The citation is transcribed in this
    /// repository's own ledger, which is where cross-repo evidence belongs; nothing here names the
    /// consumer, because the neutrality invariant forbids product vocabulary in the payload. The two names
    /// always held one RGB, so this is a redirect and not a second token: reading or assigning here reads
    /// or assigns <see cref="Danger"/>. Retires at the next minor boundary, once the consumer has moved.
    /// </summary>
    public Color Warning
    {
        get => Danger;
        set => Danger = value;
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
    /// Neutral-surface, yellow-accent palette. A built-in look, a peer of <see cref="DarkGold"/>,
    /// not the constructor and not a ranked default. Fresh independent instance every call.
    /// <para>
    /// Substrate/edge/option values are references from an earlier vanilla <c>Verse.Widgets</c>
    /// investigation whose service build identity was not established. Derived roles (hover,
    /// success, danger, accent, text) are labeled derived. The window and section planes claim
    /// their own edges; remaining surfaces use the shared border tokens.
    /// </para>
    /// </summary>
    public static UiTheme Vanilla => new()
    {
        Base = new Color(21f / 255f, 25f / 255f, 29f / 255f, 1f),
        Panel = new Color(42f / 255f, 43f / 255f, 44f / 255f, 1f),
        Raised = new Color(0.21f, 0.21f, 0.21f, 1f),
        Hover = new Color(0.27f, 0.27f, 0.27f, 1f),
        Selected = new Color(0.32f, 0.28f, 0.21f, 1f),
        Success = new Color(0.13f, 0.30f, 0.18f, 1f),
        Danger = new Color(0.42f, 0.15f, 0.12f, 1f),
        WorkspacePlane = new Color(0.055f, 0.066f, 0.078f, 1f),
        SectionBand = new Color(0.12f, 0.125f, 0.13f, 1f),
        TextPrimary = new Color(0.91f, 0.91f, 0.91f, 1f),
        TextSecondary = new Color(0.62f, 0.64f, 0.67f, 1f),
        TextOnGold = new Color(0.99f, 0.87f, 0.55f, 1f),
        TextOnDanger = new Color(0.99f, 0.72f, 0.64f, 1f),
        TextDisabled = new Color(0.45f, 0.46f, 0.48f, 1f),
        AccentGold = new Color(0.93f, 0.77f, 0.22f, 1f),
        HoverPoint = new Color(0.99f, 0.87f, 0.45f, 1f),
        Border = new Color(0.33f, 0.35f, 0.38f, 1f),
        BorderStrong = new Color(0.47f, 0.51f, 0.57f, 1f),
        Divider = new Color(0.20f, 0.21f, 0.23f, 1f),
        BaseBorder = new Color(97f / 255f, 108f / 255f, 122f / 255f, 1f),
        PanelBorder = new Color(135f / 255f, 135f / 255f, 135f / 255f, 1f),
        RaisedBorder = null,
        HoverBorder = null,
        SelectedBorder = null,
        SuccessBorder = null,
        DangerBorder = null,
    };

    /// <summary>
    /// Warm-gold palette. A built-in look, a peer of <see cref="Vanilla"/>, not a compatibility
    /// alias for the constructor and not frozen history of another theme. Fresh independent
    /// instance every call. Per-surface edges stay unclaimed — DarkGold paints through the shared
    /// border tokens.
    /// </summary>
    public static UiTheme DarkGold => new()
    {
        Base = new Color(0.065f, 0.065f, 0.063f, 1f),
        Panel = new Color(0.095f, 0.095f, 0.090f, 1f),
        Raised = new Color(0.135f, 0.135f, 0.128f, 1f),
        Hover = new Color(0.17f, 0.17f, 0.16f, 1f),
        Selected = new Color(0.17f, 0.14f, 0.08f, 1f),
        Success = new Color(0.11f, 0.24f, 0.15f, 1f),
        Danger = new Color(0.38f, 0.14f, 0.11f, 1f),
        WorkspacePlane = new Color(0.045f, 0.045f, 0.044f, 1f),
        SectionBand = new Color(0.085f, 0.085f, 0.080f, 1f),
        TextPrimary = new Color(0.88f, 0.88f, 0.84f, 1f),
        TextSecondary = new Color(0.58f, 0.58f, 0.55f, 1f),
        TextOnGold = new Color(1f, 0.84f, 0.55f, 1f),
        TextOnDanger = new Color(1f, 0.65f, 0.46f, 1f),
        TextDisabled = new Color(0.42f, 0.42f, 0.40f, 1f),
        AccentGold = new Color(0.82f, 0.60f, 0.22f, 1f),
        HoverPoint = new Color(0.96f, 0.80f, 0.42f, 1f),
        Border = new Color(0.21f, 0.21f, 0.20f, 1f),
        BorderStrong = new Color(0.32f, 0.32f, 0.30f, 1f),
        Divider = new Color(0.14f, 0.14f, 0.13f, 1f),
        BaseBorder = null,
        PanelBorder = null,
        RaisedBorder = null,
        HoverBorder = null,
        SelectedBorder = null,
        SuccessBorder = null,
        DangerBorder = null,
    };
}

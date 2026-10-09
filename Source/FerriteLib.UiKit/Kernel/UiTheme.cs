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
/// <para>
/// <b>The bag is PARTIAL, and an unset token answers the Vanilla palette.</b> A consumer that wants one
/// different plane assigns that one token and keeps a complete, drawable look everywhere else; it does not
/// have to restate twenty colours to change four. The fallback is per token and resolved on read, so
/// <c>new UiTheme()</c> is exactly the shipped look and "the consumer overrode nothing" and "the consumer
/// overrode it with the same colour" are indistinguishable to every reader.
/// </para>
/// <para>
/// <b>An explicit transparent is a value, not an absence.</b> Assigning <c>Colors.clear</c> or any other
/// zero-alpha colour - or clearing a per-surface edge to <c>null</c> - marks the token as claimed, and the
/// Vanilla default must not come back and paint it. A UI that wants a gap where a plane would be has to be
/// able to say so. This is the one asymmetry in the bag, and it is the reason the fallback is tracked with a
/// flag per token rather than by comparing the stored colour against the default.
/// </para>
/// <para>
/// <b>One shipped palette.</b> The library ships <see cref="Vanilla"/> and nothing else: a second built-in
/// look is a product decision no consumer has asked for, and a dormant peer would be inventory rather than a
/// contract. A consumer that wants a different look assigns tokens on its own bag, which is what the partial
/// fallback above is for.
/// </para>
/// </summary>
public sealed class UiTheme
{
    // One bit per colour token, in declaration order. A setter claims its bit; a reader answers the stored
    // colour when the bit is set and the Vanilla default when it is not. A uint is deliberate (net472's
    // Enum/HashSet paths are the boxing traps this repo has recorded); 24 tokens fit with room to spare.
    private const uint BaseBit = 1u << 0;
    private const uint PanelBit = 1u << 1;
    private const uint RaisedBit = 1u << 2;
    private const uint HoverBit = 1u << 3;
    private const uint SelectedBit = 1u << 4;
    private const uint SuccessBit = 1u << 5;
    private const uint DangerBit = 1u << 6;
    private const uint WorkspacePlaneBit = 1u << 7;
    private const uint SectionBandBit = 1u << 8;
    private const uint TextPrimaryBit = 1u << 9;
    private const uint TextSecondaryBit = 1u << 10;
    private const uint TextOnGoldBit = 1u << 11;
    private const uint TextOnDangerBit = 1u << 12;
    private const uint TextDisabledBit = 1u << 13;
    private const uint AccentGoldBit = 1u << 14;
    private const uint BorderBit = 1u << 15;
    private const uint BorderStrongBit = 1u << 16;
    private const uint DividerBit = 1u << 17;
    private const uint BaseBorderBit = 1u << 18;
    private const uint PanelBorderBit = 1u << 19;
    private const uint RaisedBorderBit = 1u << 20;
    private const uint HoverBorderBit = 1u << 21;
    private const uint SelectedBorderBit = 1u << 22;
    private const uint SuccessBorderBit = 1u << 23;
    private const uint DangerBorderBit = 1u << 24;

    /// <summary>SA1.1: the switch's OFF-thumb token. 25 is the first free slot after the 24 declaration-order
    /// tokens, and the uint budget named in the block comment above has room for it.</summary>
    private const uint SwitchThumbOffBit = 1u << 25;

    private uint claimed;
    private Color baseColour, panel, raised, hover, selected, success, danger;
    private Color workspacePlane, sectionBand;
    private Color textPrimary, textSecondary, textOnGold, textOnDanger, textDisabled;
    private Color accentGold;
    private Color switchThumbOff;
    private Color border, borderStrong, divider;
    private Color? baseBorder, panelBorder, raisedBorder, hoverBorder, selectedBorder, successBorder, dangerBorder;
    private UiFont defaultFont = UiFont.Small;
    private UiGeometry geometry = UiGeometry.Default;

    /// <summary>
    /// The Vanilla values every unset token answers. Held apart from the instance so the fallback is one
    /// definition rather than a copy per theme, and so <see cref="Vanilla"/> and a bag with no assignments
    /// can never drift into two slightly different looks.
    /// </summary>
    private sealed class VanillaTemplate
    {
        internal static readonly VanillaTemplate Instance = new();
        internal static VanillaTemplate Defaults => Instance;

        internal Color Base = new(21f / 255f, 25f / 255f, 29f / 255f, 1f);
        internal Color Panel = new(42f / 255f, 43f / 255f, 44f / 255f, 1f);
        internal Color Raised = new(0.21f, 0.21f, 0.21f, 1f);
        internal Color Hover = new(0.27f, 0.27f, 0.27f, 1f);
        internal Color Selected = new(0.32f, 0.28f, 0.21f, 1f);
        internal Color Success = new(0.13f, 0.30f, 0.18f, 1f);
        internal Color Danger = new(0.42f, 0.15f, 0.12f, 1f);
        internal Color WorkspacePlane = new(0.055f, 0.066f, 0.078f, 1f);
        internal Color SectionBand = new(0.12f, 0.125f, 0.13f, 1f);
        internal Color TextPrimary = new(0.91f, 0.91f, 0.91f, 1f);
        internal Color TextSecondary = new(0.62f, 0.64f, 0.67f, 1f);
        internal Color TextOnGold = new(0.99f, 0.87f, 0.55f, 1f);
        internal Color TextOnDanger = new(0.99f, 0.72f, 0.64f, 1f);
        internal Color TextDisabled = new(0.45f, 0.46f, 0.48f, 1f);
        internal Color AccentGold = new(0.93f, 0.77f, 0.22f, 1f);
        internal Color Border = new(0.33f, 0.35f, 0.38f, 1f);
        internal Color BorderStrong = new(0.47f, 0.51f, 0.57f, 1f);
        internal Color Divider = new(0.20f, 0.21f, 0.23f, 1f);
        internal Color? BaseBorder = new Color(97f / 255f, 108f / 255f, 122f / 255f, 1f);
        internal Color? PanelBorder = new Color(135f / 255f, 135f / 255f, 135f / 255f, 1f);
        internal Color? RaisedBorder = null;
        internal Color? HoverBorder = null;
        internal Color? SelectedBorder = null;
        internal Color? SuccessBorder = null;
        internal Color? DangerBorder = null;
    }

    public UiTheme()
    {
        // One resolved-value store per theme instance, built here and never replaced. The injecting
        // host holds one theme per window, so a store is per-window in practice and two windows never
        // share one; the store is a read-through view of this bag, so re-tinting the theme still moves
        // the answer and no second copy of a colour exists.
        Styles = new UiStyleTable(this);
    }

    /// <summary>
    /// The copy constructor: same claimed set, same stored values, its own resolved-value store. Private
    /// because a caller builds a peer through <see cref="Clone"/>, which is the documented way to get an
    /// independent bag with these values.
    /// </summary>
    private UiTheme(UiTheme source)
    {
        claimed = source.claimed;
        baseColour = source.baseColour;
        panel = source.panel;
        raised = source.raised;
        hover = source.hover;
        selected = source.selected;
        success = source.success;
        danger = source.danger;
        workspacePlane = source.workspacePlane;
        sectionBand = source.sectionBand;
        textPrimary = source.textPrimary;
        textSecondary = source.textSecondary;
        textOnGold = source.textOnGold;
        textOnDanger = source.textOnDanger;
        textDisabled = source.textDisabled;
        accentGold = source.accentGold;
        switchThumbOff = source.switchThumbOff;
        border = source.border;
        borderStrong = source.borderStrong;
        divider = source.divider;
        baseBorder = source.baseBorder;
        panelBorder = source.panelBorder;
        raisedBorder = source.raisedBorder;
        hoverBorder = source.hoverBorder;
        selectedBorder = source.selectedBorder;
        successBorder = source.successBorder;
        dangerBorder = source.dangerBorder;
        defaultFont = source.defaultFont;
        geometry = source.geometry;
        Styles = new UiStyleTable(this);
    }

    public Color Base { get => ColourOf(BaseBit, baseColour, VanillaTemplate.Defaults.Base); set => SetColour(BaseBit, ref baseColour, value); }
    public Color Panel { get => ColourOf(PanelBit, panel, VanillaTemplate.Defaults.Panel); set => SetColour(PanelBit, ref panel, value); }
    public Color Raised { get => ColourOf(RaisedBit, raised, VanillaTemplate.Defaults.Raised); set => SetColour(RaisedBit, ref raised, value); }
    public Color Hover { get => ColourOf(HoverBit, hover, VanillaTemplate.Defaults.Hover); set => SetColour(HoverBit, ref hover, value); }
    public Color Selected { get => ColourOf(SelectedBit, selected, VanillaTemplate.Defaults.Selected); set => SetColour(SelectedBit, ref selected, value); }
    public Color Success { get => ColourOf(SuccessBit, success, VanillaTemplate.Defaults.Success); set => SetColour(SuccessBit, ref success, value); }
    public Color Danger { get => ColourOf(DangerBit, danger, VanillaTemplate.Defaults.Danger); set => SetColour(DangerBit, ref danger, value); }

    public Color WorkspacePlane { get => ColourOf(WorkspacePlaneBit, workspacePlane, VanillaTemplate.Defaults.WorkspacePlane); set => SetColour(WorkspacePlaneBit, ref workspacePlane, value); }
    public Color SectionBand { get => ColourOf(SectionBandBit, sectionBand, VanillaTemplate.Defaults.SectionBand); set => SetColour(SectionBandBit, ref sectionBand, value); }

    public Color TextPrimary { get => ColourOf(TextPrimaryBit, textPrimary, VanillaTemplate.Defaults.TextPrimary); set => SetColour(TextPrimaryBit, ref textPrimary, value); }
    public Color TextSecondary { get => ColourOf(TextSecondaryBit, textSecondary, VanillaTemplate.Defaults.TextSecondary); set => SetColour(TextSecondaryBit, ref textSecondary, value); }
    public Color TextOnGold { get => ColourOf(TextOnGoldBit, textOnGold, VanillaTemplate.Defaults.TextOnGold); set => SetColour(TextOnGoldBit, ref textOnGold, value); }
    public Color TextOnDanger { get => ColourOf(TextOnDangerBit, textOnDanger, VanillaTemplate.Defaults.TextOnDanger); set => SetColour(TextOnDangerBit, ref textOnDanger, value); }
    public Color TextDisabled { get => ColourOf(TextDisabledBit, textDisabled, VanillaTemplate.Defaults.TextDisabled); set => SetColour(TextDisabledBit, ref textDisabled, value); }

    public Color AccentGold { get => ColourOf(AccentGoldBit, accentGold, VanillaTemplate.Defaults.AccentGold); set => SetColour(AccentGoldBit, ref accentGold, value); }

    /// <summary>
    /// The switch thumb's OFF colour (SA1.1). Unset it answers THIS theme's own <see cref="TextPrimary"/> -
    /// which is the historical behaviour, so a bag that never assigns it paints the same neutral thumb it
    /// always did, and a theme that re-tints its primary ink still moves the thumb with it. A consumer that
    /// wants a dedicated grey for the OFF state declares it in its palette document
    /// (<c>&lt;Color Token="SwitchThumbOff"/&gt;</c> - grammar, resolver and scoped clones wired) or assigns
    /// this property, and leaves every important text at full weight: it is the thumb-side peer of
    /// <see cref="AccentGold"/>, which is the on-state meaning, and neither assignment can reach the other half.
    /// </summary>
    public Color SwitchThumbOff
    {
        get => (claimed & SwitchThumbOffBit) != 0 ? switchThumbOff : TextPrimary;
        set => SetColour(SwitchThumbOffBit, ref switchThumbOff, value);
    }

    /// <summary>
    /// The accent's hover step: every channel of <see cref="AccentGold"/> lifted <see cref="HoverLift"/> of
    /// the remaining distance toward white, with the accent's alpha kept. Computed on every read, never
    /// stored and never settable, because there is exactly one accent token: a consumer that re-tints
    /// <see cref="AccentGold"/> gets the matching hover step immediately, and no second colour can be left
    /// holding the old hue (which is what the two stored tokens used to do).
    /// <para>
    /// The rule is a fixed fraction rather than an adaptive lighten, so the result is predictable for an
    /// author who re-tints the accent; no requirement has asked for the adaptive form.
    /// </para>
    /// </summary>
    public Color AccentHover => new Color(
        Lift(AccentGold.r),
        Lift(AccentGold.g),
        Lift(AccentGold.b),
        AccentGold.a);

    /// <summary>The fraction of the remaining distance to white the derived hover step travels.</summary>
    private const float HoverLift = 0.3f;

    private static float Lift(float channel) => channel + (1f - channel) * HoverLift;

    /// <summary>
    /// The accent at a reduced alpha. Derived rather than stored: a consumer that re-tints
    /// <see cref="AccentGold"/> must not be left with alpha variants still holding the old hue.
    /// </summary>
    public Color AccentWith(float alpha) => new Color(AccentGold.r, AccentGold.g, AccentGold.b, alpha);

    // Shared borders, with the same claim rule as the fills above: an unset shared edge answers the Vanilla
    // value. A per-surface edge is a nullable token whose unset value means "use the shared token" — which is
    // why claiming one with null is a no-op rather than a way to express transparency, and why assigning a
    // transparent COLOUR is how a caller removes an edge.
    public Color Border { get => ColourOf(BorderBit, border, VanillaTemplate.Defaults.Border); set => SetColour(BorderBit, ref border, value); }
    public Color BorderStrong { get => ColourOf(BorderStrongBit, borderStrong, VanillaTemplate.Defaults.BorderStrong); set => SetColour(BorderStrongBit, ref borderStrong, value); }
    public Color Divider { get => ColourOf(DividerBit, divider, VanillaTemplate.Defaults.Divider); set => SetColour(DividerBit, ref divider, value); }

    public Color? BaseBorder { get => EdgeOf(BaseBorderBit, baseBorder, VanillaTemplate.Defaults.BaseBorder); set => SetEdge(BaseBorderBit, ref baseBorder, value); }
    public Color? PanelBorder { get => EdgeOf(PanelBorderBit, panelBorder, VanillaTemplate.Defaults.PanelBorder); set => SetEdge(PanelBorderBit, ref panelBorder, value); }
    public Color? RaisedBorder { get => EdgeOf(RaisedBorderBit, raisedBorder, VanillaTemplate.Defaults.RaisedBorder); set => SetEdge(RaisedBorderBit, ref raisedBorder, value); }
    public Color? HoverBorder { get => EdgeOf(HoverBorderBit, hoverBorder, VanillaTemplate.Defaults.HoverBorder); set => SetEdge(HoverBorderBit, ref hoverBorder, value); }
    public Color? SelectedBorder { get => EdgeOf(SelectedBorderBit, selectedBorder, VanillaTemplate.Defaults.SelectedBorder); set => SetEdge(SelectedBorderBit, ref selectedBorder, value); }
    public Color? SuccessBorder { get => EdgeOf(SuccessBorderBit, successBorder, VanillaTemplate.Defaults.SuccessBorder); set => SetEdge(SuccessBorderBit, ref successBorder, value); }
    public Color? DangerBorder { get => EdgeOf(DangerBorderBit, dangerBorder, VanillaTemplate.Defaults.DangerBorder); set => SetEdge(DangerBorderBit, ref dangerBorder, value); }

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
    /// Bumped whenever any COLOUR token is assigned a different value - the paint-side peer of
    /// <see cref="LayoutRevision"/>, and the clock a cache of cloned themes compares so a re-tint cannot
    /// leave a region painting the palette it was built with.
    /// <para>
    /// It is deliberately not <see cref="LayoutRevision"/>: a colour must not re-measure a page, and folding
    /// the two together would make every re-tint pay for a full arrange. It counts ASSIGNMENTS that change a
    /// value, so a scope applying a scheme whose colour happens to equal the current one does not invalidate
    /// anything - and, like the layout clock, it is a compare-and-rebuild signal rather than a public
    /// invalidation API.
    /// </para>
    /// </summary>
    public int ColourRevision { get; private set; }

    /// <summary>
    /// The theme's resolved-value store. Queries are answered from the current tokens, so a consumer
    /// that re-tints this theme sees the new value on the next query; the store itself never changes
    /// after construction and is never shared between two themes.
    /// </summary>
    public UiStyleTable Styles { get; }

    /// <summary>
    /// A copy of this bag with the same token values - claimed and inherited alike - and its own
    /// resolved-value store. A region style builds one so two regions can differ while the injected theme
    /// stays exactly what the consumer handed in. It copies values, not identity: the copy shares no mutable
    /// state with the original (each has its own <see cref="Styles"/>), and moving a token on either one
    /// afterwards moves only that one.
    /// </summary>
    public UiTheme Clone()
    {
        return new UiTheme(this);
    }

    /// <summary>
    /// Neutral-surface, yellow-accent palette: the look the library ships and the value every unset token
    /// answers. Fresh independent instance every call.
    /// <para>
    /// Substrate/edge/option values are references from an earlier vanilla <c>Verse.Widgets</c>
    /// investigation whose service build identity was not established. Derived roles (hover,
    /// success, danger, accent, text) are labeled derived. The window and section planes claim
    /// their own edges; remaining surfaces use the shared border tokens.
    /// </para>
    /// </summary>
    public static UiTheme Vanilla => new()
    {
        Base = VanillaTemplate.Defaults.Base,
        Panel = VanillaTemplate.Defaults.Panel,
        Raised = VanillaTemplate.Defaults.Raised,
        Hover = VanillaTemplate.Defaults.Hover,
        Selected = VanillaTemplate.Defaults.Selected,
        Success = VanillaTemplate.Defaults.Success,
        Danger = VanillaTemplate.Defaults.Danger,
        WorkspacePlane = VanillaTemplate.Defaults.WorkspacePlane,
        SectionBand = VanillaTemplate.Defaults.SectionBand,
        TextPrimary = VanillaTemplate.Defaults.TextPrimary,
        TextSecondary = VanillaTemplate.Defaults.TextSecondary,
        TextOnGold = VanillaTemplate.Defaults.TextOnGold,
        TextOnDanger = VanillaTemplate.Defaults.TextOnDanger,
        TextDisabled = VanillaTemplate.Defaults.TextDisabled,
        AccentGold = VanillaTemplate.Defaults.AccentGold,
        Border = VanillaTemplate.Defaults.Border,
        BorderStrong = VanillaTemplate.Defaults.BorderStrong,
        Divider = VanillaTemplate.Defaults.Divider,
        BaseBorder = VanillaTemplate.Defaults.BaseBorder,
        PanelBorder = VanillaTemplate.Defaults.PanelBorder,
    };

    private Color ColourOf(uint bit, Color stored, Color fallback)
    {
        return (claimed & bit) != 0 ? stored : fallback;
    }

    private Color? EdgeOf(uint bit, Color? stored, Color? fallback)
    {
        return (claimed & bit) != 0 ? stored : fallback;
    }

    private void SetColour(uint bit, ref Color field, Color value)
    {
        bool wasClaimed = (claimed & bit) != 0;
        if (wasClaimed && field == value) return;

        claimed |= bit;
        field = value;

        // A colour assignment moves the paint clock and never the layout clock.
        ColourRevision++;
    }

    private void SetEdge(uint bit, ref Color? field, Color? value)
    {
        bool wasClaimed = (claimed & bit) != 0;
        if (wasClaimed && Nullable.Equals(field, value)) return;

        claimed |= bit;
        field = value;
        ColourRevision++;
    }
}

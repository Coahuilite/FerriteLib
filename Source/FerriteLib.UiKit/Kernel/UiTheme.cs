using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Platform fill and border values handed to a visual outlet.</summary>
public readonly struct UiSurfaceStyle
{
    public UiSurfaceStyle(UiColour fill, UiColour border)
    {
        Fill = fill;
        Border = border;
    }

    /// <summary>The colour a painted surface fills its rect with.</summary>
    public UiColour Fill { get; }

    /// <summary>The colour of the edge every painted surface carries.</summary>
    public UiColour Border { get; }
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

/// <summary>Platform paint adapter and existing typography/density settings. Colours live only in Palette.</summary>
public sealed class UiTheme
{
    private readonly Dictionary<UiPaletteRole, UiColour> appliedColours = new();
    private readonly Dictionary<string, float> styleMetrics = new(StringComparer.Ordinal);
    private UiFont? styleFont;
    private UiFont defaultFont = UiFont.Small;
    private UiGeometry geometry = UiGeometry.Default;
    private bool fontDeclared;
    private bool densityDeclared;
    private readonly Dictionary<string, UiPropertyDeclaration<float>> metrics = new(StringComparer.Ordinal);
    public UiPropertyDeclaration<float> Metric(string name)
    {
        if (!metrics.TryGetValue(name, out var declaration))
        {
            declaration = UiAppearanceProperties.MetricDeclaration(name, () => { LayoutRevision++; PropertyRevision++; });
            metrics.Add(name, declaration);
        }
        return declaration;
    }
    internal bool HasMetricOverride(string name) => densityDeclared || (metrics.TryGetValue(name, out var value) && value.IsDeclared);
    private int applicationRevision;
    internal int PropertyRevision { get; private set; }
    public UiTheme()
    {
        Palette = new UiPalette();
        Styles = new UiStyleTable(this);
    }
    public UiPalette Palette { get; }
    public UiStyleTable Styles { get; }
    public static UiTheme Vanilla => new();
    public UiColour this[UiPaletteRole role]
    {
        get => Colour(role);
        set => Palette[role] = value;
    }
    internal UiColour Colour(UiPaletteRole role) => appliedColours.TryGetValue(role, out UiColour value) ? value : Palette[role];
    internal bool HasColourOverride(string role) => Palette.IsDeclared(ParseRole(role));
    private static UiPaletteRole ParseRole(string name)
        => UiPalette.TryRole(name, out UiPaletteRole role) ? role : throw new ArgumentException("Unknown palette role '" + name + "'.");
    public int ColourRevision => Palette.Revision + applicationRevision;
    public int LayoutRevision { get; private set; }
    public bool HasFontOverride => fontDeclared;
    public bool HasDensityOverride => densityDeclared;
    public UiFont DefaultFont
    {
        get => styleFont ?? defaultFont;
        set
        {
            if (!fontDeclared) { fontDeclared = true; PropertyRevision++; }
            if (value == defaultFont) return;
            defaultFont = value;
            LayoutRevision++;
        }
    }
    public UiGeometry Geometry
    {
        get => new(StyleMetric("Padding", geometry.Padding), StyleMetric("Spacing", geometry.Spacing),
            StyleMetric("Gap", geometry.Gap), StyleMetric("RowHeight", geometry.RowHeight), StyleMetric("Hairline", geometry.Hairline));
        set
        {
            if (!densityDeclared) { densityDeclared = true; PropertyRevision++; }
            if (value == geometry) return;
            geometry = value;
            LayoutRevision++;
        }
    }
    private float StyleMetric(string name, float fallback) => styleMetrics.TryGetValue(name, out float value) ? value
        : metrics.TryGetValue(name, out var declaration) && declaration.IsDeclared ? declaration.Value : fallback;
    internal void ApplyStyleFont(UiFont value)
    {
        UiFont prior = DefaultFont;
        styleFont = value;
        if (DefaultFont != prior) LayoutRevision++;
    }
    internal void ApplyStyleMetric(string name, float value)
    {
        UiGeometry prior = Geometry;
        styleMetrics[name] = value;
        if (Geometry != prior) LayoutRevision++;
    }
    internal void ApplyStyleGeometry(UiGeometry value)
    {
        ApplyStyleMetric("Padding", value.Padding);
        ApplyStyleMetric("Spacing", value.Spacing);
        ApplyStyleMetric("Gap", value.Gap);
        ApplyStyleMetric("RowHeight", value.RowHeight);
        ApplyStyleMetric("Hairline", value.Hairline);
    }
    internal void ApplyStyleColour(string name, UiColour? value)
    {
        if (!value.HasValue) throw new ArgumentException("Use a transparent palette colour, not null.");
        UiPaletteRole role = ParseRole(name);
        UiColour colour = value.Value;
        if (appliedColours.TryGetValue(role, out UiColour prior) && prior == colour) return;
        appliedColours[role] = colour;
        applicationRevision++;
    }
    internal void ClearStyleApplications()
    {
        UiFont priorFont = DefaultFont;
        UiGeometry priorGeometry = Geometry;
        if (appliedColours.Count > 0) applicationRevision++;
        appliedColours.Clear();
        styleFont = null;
        styleMetrics.Clear();
        if (DefaultFont != priorFont || Geometry != priorGeometry) LayoutRevision++;
    }
    public void RemoveFontOverride()
    {
        if (!fontDeclared) return;
        fontDeclared = false;
        PropertyRevision++;
        if (defaultFont != UiFont.Small) { defaultFont = UiFont.Small; LayoutRevision++; }
    }
    public void RemoveDensityOverride()
    {
        if (!densityDeclared) return;
        densityDeclared = false;
        PropertyRevision++;
        if (geometry != UiGeometry.Default) { geometry = UiGeometry.Default; LayoutRevision++; }
    }
    public UiTheme Clone()
    {
        var clone = new UiTheme();
        clone.RestoreFrom(this);
        return clone;
    }
    internal void RestoreFrom(UiTheme source)
    {
        UiFont priorFont = DefaultFont;
        UiGeometry priorGeometry = Geometry;
        Palette.RestoreFrom(source.Palette);
        appliedColours.Clear();
        foreach (var pair in source.appliedColours) appliedColours.Add(pair.Key, pair.Value);
        styleMetrics.Clear();
        foreach (var pair in source.styleMetrics) styleMetrics.Add(pair.Key, pair.Value);
        defaultFont = source.defaultFont;
        geometry = source.geometry;
        fontDeclared = source.fontDeclared;
        densityDeclared = source.densityDeclared;
        foreach (var pair in metrics) pair.Value.Remove();
        foreach (var pair in source.metrics) if (pair.Value.IsDeclared) Metric(pair.Key).Set(pair.Value.Value);
        styleFont = source.styleFont;
        applicationRevision++;
        PropertyRevision++;
        if (DefaultFont != priorFont || Geometry != priorGeometry) LayoutRevision++;
    }
    public UiColour AccentHover
    {
        get
        {
            UiColour accent = this[UiPaletteRole.Accent];
            return new UiColour(Lift(accent.Red), Lift(accent.Green), Lift(accent.Blue), accent.Alpha);
        }
    }
    private static float Lift(float channel) => channel + (1 - channel) * .3f;
    public UiColour AccentWith(float alpha)
    {
        UiColour accent = this[UiPaletteRole.Accent];
        return new UiColour(accent.Red, accent.Green, accent.Blue, alpha);
    }
    public UiSurfaceStyle Surface(UiPaletteRole fill, UiPaletteRole border) => new(this[fill], this[border]);
    public UiSurfaceStyle BaseSurface => Surface(UiPaletteRole.WindowSurface, UiPaletteRole.WindowBorder);
    public UiSurfaceStyle PanelSurface => Surface(UiPaletteRole.PanelSurface, UiPaletteRole.PanelBorder);
    public UiSurfaceStyle RaisedSurface => Surface(UiPaletteRole.ControlSurface, UiPaletteRole.ControlBorder);
    public UiSurfaceStyle HoverSurface => Surface(UiPaletteRole.HoverSurface, UiPaletteRole.HoverBorder);
    public UiSurfaceStyle SelectedSurface => Surface(UiPaletteRole.SelectedSurface, UiPaletteRole.SelectedBorder);
    public UiSurfaceStyle SuccessSurface => Surface(UiPaletteRole.SuccessSurface, UiPaletteRole.SuccessBorder);
    public UiSurfaceStyle WarningSurface => Surface(UiPaletteRole.WarningSurface, UiPaletteRole.WarningBorder);
    public UiSurfaceStyle DangerSurface => Surface(UiPaletteRole.DangerSurface, UiPaletteRole.DangerBorder);
}

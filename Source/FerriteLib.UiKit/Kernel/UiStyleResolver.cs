using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Turns the written precedence chain into values. Sources, nearest first:
/// <c>state &gt; element &gt; container &gt; page &gt; theme &gt; default</c>.
/// <para>
/// Not every property may be declared at every level, and that asymmetry is the design, not a gap:
/// <list type="bullet">
/// <item>scheme and density inherit — element, container and page may declare them, the nearest
/// declaration wins, and the injected theme is the baseline under all of them. A scope that declares
/// nothing gets its nearest ancestor's values.</item>
/// <item>roles (tone and emphasis) do <b>not</b> inherit — only <c>state</c> (a value the player cannot
/// write is painted disabled) and <c>element</c> (the node's own Tone/Emphasis attribute) speak for a
/// node. A container tagged danger does not make the controls inside it read as dangerous, because that
/// would destroy the information the tag exists to carry.</item>
/// <item>theme is the consumer-injected bag, and default is what the library ships; a scope with no
/// document at all resolves exactly like the theme says.</item>
/// </list>
/// </para>
/// <para>
/// Region themes are built once and reused: the theme for a given effective (scheme, density) pair is
/// created on first use and the same instance is handed out on every later frame, so a region costs one
/// theme, not one per frame. The instance is a clone of the injected theme with that pair applied -
/// values only, its own resolved-value store - and it is never mutated afterwards.
/// </para>
/// <para>
/// That reuse expires on the injected theme's own <see cref="UiTheme.LayoutRevision"/>: a layout-bearing
/// token (the default font, the density bundle) moving means every band measured against the old value is
/// stale, so the cached clones are dropped and rebuilt lazily on the next lookup. This is not a second
/// clock - it is the revision the engine's band cache already compares, read here so both caches expire
/// together.
/// </para>
/// <para>
/// <b>A colour-only re-tint expires the same cache through its own clock, and moves no layout.</b> A cached
/// clone copies token VALUES, so a palette change on the injected theme would otherwise leave every region
/// painting the old colours forever - the resolver would keep handing out the instances it built under the
/// previous palette. <see cref="UiTheme.ColourRevision"/> is the peer clock that closes it: a colour
/// assignment drops the cached clones and the next lookup rebuilds them, while
/// <see cref="UiTheme.LayoutRevision"/> stays exactly where it was, so the engine's band cache and the
/// arranged page are untouched. Reading two clocks rather than one is the point - the whole reason a colour
/// does not move the layout clock is that re-arranging the page to repaint it would be cost for nothing.
/// </para>
/// <para>
/// Resolution-time drops (a scope naming a scheme nobody declared, a scope budget overrun) are recorded
/// in <see cref="Issues"/> next to the document's own parse-time issues; the page keeps rendering with
/// the values it would have had without the document.
/// </para>
/// </summary>
public sealed class UiStyleResolver
{
    /// <summary>Distinct region themes one resolver will hold before it stops caching.</summary>
    public const int MaxScopes = 64;

    private const int MaxIssues = UiStyleDocument.MaxIssues;

    private readonly UiTheme baseline;
    private readonly UiStyleDocument document;
    private readonly Dictionary<string, UiTheme> regionThemes = new(StringComparer.Ordinal);
    private readonly List<UiStyleIssue> issues = new();

    // The baseline's paint-side revision as this resolver last saw it. It exists because a cached clone
    // holds values, not a reference: a palette change on the injected theme has to drop the clones or every
    // region keeps the colours it was built with.
    private int observedBaselineColourRevision;

    // The baseline's layout revision as this resolver last saw it. Deliberately not a clock of its own: it
    // is a read head over the revision the engine's band cache compares, so a move expires both caches.
    private int observedBaselineRevision;

    public UiStyleResolver(UiTheme baseline, UiStyleDocument document)
    {
        this.baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
        this.document = document ?? throw new ArgumentNullException(nameof(document));
        observedBaselineRevision = this.baseline.LayoutRevision;
        observedBaselineColourRevision = this.baseline.ColourRevision;
    }

    /// <summary>The consumer-injected theme every scope starts from.</summary>
    public UiTheme Baseline => baseline;

    /// <summary>The document these values come from.</summary>
    public UiStyleDocument Document => document;

    /// <summary>
    /// Resolution-time drops, in the order they happened. Parse-time drops are on
    /// <see cref="UiStyleDocument.Issues"/>; neither list is ever allowed to stay silent about a fallback.
    /// </summary>
    public IReadOnlyList<UiStyleIssue> Issues => issues;

    /// <summary>How many distinct region themes have been built (bounded by <see cref="MaxScopes"/>).</summary>
    public int ScopeCount => regionThemes.Count;

    /// <summary>
    /// Applies the document's page-level scheme to <paramref name="theme"/> before the first arrange.
    /// Typography and density advance <see cref="UiTheme.LayoutRevision"/>; colour assignments advance
    /// <see cref="UiTheme.ColourRevision"/> without invalidating the arranged bands.
    /// <para>
    /// The legacy <c>&lt;Font&gt;</c> declaration selects typography through the same path as Metric Font.
    /// It changes the font used for measurement and drawing independently of density such as RowHeight.
    /// </para>
    /// </summary>
    public void ApplyTo(UiTheme theme)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));

        ApplyNamedScopes(theme, document.DefaultScheme, document.DefaultDensity, null);
    }

    /// <summary>
    /// The theme a scope draws with, built once per effective (scheme, density) pair and reused - the same
    /// instance on every later frame, until either of the baseline's revisions moves and the cached instances
    /// are dropped. Unknown names fall back to the nearest declared ancestor's values (or the baseline)
    /// and are recorded.
    /// </summary>
    public UiTheme ThemeFor(IReadOnlyList<UiStyleDeclaration>? nearestFirst)
    {
        return ThemeFor(nearestFirst, null);
    }

    /// <summary>
    /// The same lookup with the element that asked for it. The tree is the only layer that knows where a scope
    /// name was written, so it hands its path in and an unknown name is recorded against that element instead of
    /// against the document - which is the difference between "some scheme is missing" and "this column's scheme
    /// is missing". Caching is unchanged: the record lands on the first lookup that builds the pair.
    /// </summary>
    internal UiTheme ThemeFor(IReadOnlyList<UiStyleDeclaration>? nearestFirst, string? elementPath)
    {
        // A cached scope is only as good as the baseline it was cloned from, and a clone carries VALUES, so
        // two clocks have to be watched and they answer two different questions. The layout clock is the
        // revision the engine's band cache compares, so a font or density move expires both caches together.
        // The colour clock is the paint-side peer: a re-tint leaves every arranged rect exactly where it was
        // (and the band cache untouched) while the cached clones, which would otherwise keep painting the
        // palette they were built under, are dropped and rebuilt on the next lookup.
        int baselineRevision = baseline.LayoutRevision;
        int baselineColourRevision = baseline.ColourRevision;
        if (baselineRevision != observedBaselineRevision || baselineColourRevision != observedBaselineColourRevision)
        {
            observedBaselineRevision = baselineRevision;
            observedBaselineColourRevision = baselineColourRevision;
            regionThemes.Clear();
        }

        string? scheme = ResolveScheme(nearestFirst);
        string? density = ResolveDensity(nearestFirst);
        string key = (scheme ?? "") + "\u0001" + (density ?? "");

        if (regionThemes.TryGetValue(key, out UiTheme? cached)) return cached;

        UiTheme theme = baseline.Clone();
        ApplyNamedScopes(theme, scheme, density, elementPath);

        if (regionThemes.Count >= MaxScopes)
        {
            Record("More than " + MaxScopes.ToString(CultureInfo.InvariantCulture)
                + " distinct scheme/density scopes; this scope's theme is rebuilt per use instead of cached.",
                0, elementPath);
            return theme;
        }

        regionThemes.Add(key, theme);
        return theme;
    }

    /// <summary>
    /// Applies the two inheriting names in the documented order. A scheme contributes its colours and its
    /// premeasure metrics (the density vocabulary, including a redirected legacy font); a named density
    /// contributes metrics after it, so the more specific name wins on a token both declare. Both go through
    /// the same <see cref="ApplyMetric"/> funnel, which is why one fact - a row height - cannot be expressed
    /// twice in two different ways.
    /// <para>
    /// Typography is the one part of a scheme that is not a metric: <see cref="ApplyFont"/> writes the selected
    /// typeface to <see cref="UiTheme.DefaultFont"/>, which is the single value text measurement, the fit
    /// audit and the label outlet all read. A font change therefore moves the layout clock (a glyph run really
    /// does occupy a different width) while a RowHeight metric moves the same clock as a DISTANCE - the two
    /// axes are independent, and neither is implemented in terms of the other.
    /// </para>
    /// </summary>
    private void ApplyNamedScopes(UiTheme theme, string? scheme, string? density, string? elementPath)
    {
        if (scheme != null && scheme.Length > 0)
        {
            if (document.TryGetScheme(scheme, out UiStyleDocument.SchemeDefinition definition))
            {
                foreach (KeyValuePair<string, Color> pair in definition.Colours)
                {
                    ApplyColour(theme, pair.Key, pair.Value);
                }

                if (definition.Font.HasValue) ApplyFont(theme, definition.Font.Value);

                foreach (KeyValuePair<string, float> pair in definition.Metrics)
                {
                    ApplyMetric(theme, pair.Key, pair.Value);
                }
            }
            else
            {
                Record(
                    "Unknown scheme '" + scheme + "'; the scope keeps the values it would have had without it.",
                    0, elementPath);
            }
        }

        if (density == null || density.Length == 0) return;
        if (!document.TryGetDensity(density, out UiStyleDocument.DensityDefinition densityDefinition))
        {
            Record(
                "Unknown density '" + density + "'; the scope keeps the values it would have had without it.",
                0, elementPath);
            return;
        }

        foreach (KeyValuePair<string, float> pair in densityDefinition.Metrics)
        {
            ApplyMetric(theme, pair.Key, pair.Value);
        }
    }

    /// <summary>
    /// Applies a scheme's typography selection. It writes <see cref="UiTheme.DefaultFont"/> and nothing else,
    /// so the selected font is what <c>ctx.Theme.DefaultFont</c> answers, what
    /// <see cref="ITextMetrics.MeasureWidth"/>/<c>MeasureText</c> is called with, and what
    /// <see cref="UiThemeDraw.Label"/> paints with when the caller names no font - one selection, obeyed by
    /// measurement and paint. The theme's own setter moves the layout clock, which is correct: a different
    /// typeface measures differently.
    /// </summary>
    private static void ApplyFont(UiTheme theme, UiFont font)
    {
        theme.DefaultFont = font;
    }

    /// <summary>
    /// The role answer for one node: the state-beats-author rule, then the node's own declaration, then
    /// the library default. Container and page levels are deliberately absent - see the type remarks.
    /// </summary>
    public UiResolvedStyle Resolve(UiTheme theme, IReadOnlyList<UiStyleDeclaration>? nearestFirst, bool? writable = null)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));
        UiStyleDeclaration element = nearestFirst is { Count: > 0 } ? nearestFirst[0] : default;
        return theme.Styles.Resolve(ResolveTone(element, writable), ResolveEmphasis(element), writable);
    }

    /// <summary>
    /// Written precedence for a role: <c>state &gt; element &gt; default</c>. A value the player cannot
    /// write is painted disabled whatever tone was authored; otherwise the node's own tone stands; with
    /// neither, the default treatment. A container's or a page's role never reaches a child.
    /// </summary>
    public static UiStatusTone ResolveTone(UiStyleDeclaration element, bool? writable)
    {
        if (writable == false) return UiStatusTone.Disabled;
        return element.Tone ?? UiStatusTone.Neutral;
    }

    /// <summary>Written precedence for the second axis, same shape as the tone: state, then element.</summary>
    public static UiEmphasis ResolveEmphasis(UiStyleDeclaration element)
    {
        return element.Emphasis ?? UiEmphasis.Normal;
    }

    /// <summary>Nearest declared scheme: element, then containers outward, then the document's page level.</summary>
    public string? ResolveScheme(IReadOnlyList<UiStyleDeclaration>? nearestFirst)
    {
        if (nearestFirst != null)
        {
            for (int i = 0; i < nearestFirst.Count; i++)
            {
                if (!string.IsNullOrEmpty(nearestFirst[i].Scheme)) return nearestFirst[i].Scheme;
            }
        }

        return document.DefaultScheme;
    }

    /// <summary>Nearest declared density, same walk as <see cref="ResolveScheme"/>.</summary>
    public string? ResolveDensity(IReadOnlyList<UiStyleDeclaration>? nearestFirst)
    {
        if (nearestFirst != null)
        {
            for (int i = 0; i < nearestFirst.Count; i++)
            {
                if (!string.IsNullOrEmpty(nearestFirst[i].Density)) return nearestFirst[i].Density;
            }
        }

        return document.DefaultDensity;
    }

    private static void ApplyColour(UiTheme theme, string token, Color value)
    {
        switch (token)
        {
            case "Base": theme.Base = value; break;
            case "Panel": theme.Panel = value; break;
            case "Raised": theme.Raised = value; break;
            case "Hover": theme.Hover = value; break;
            case "Selected": theme.Selected = value; break;
            case "Success": theme.Success = value; break;
            case "Danger": theme.Danger = value; break;
            case "WorkspacePlane": theme.WorkspacePlane = value; break;
            case "SectionBand": theme.SectionBand = value; break;
            case "TextPrimary": theme.TextPrimary = value; break;
            case "TextSecondary": theme.TextSecondary = value; break;
            case "TextOnGold": theme.TextOnGold = value; break;
            case "TextOnDanger": theme.TextOnDanger = value; break;
            case "TextDisabled": theme.TextDisabled = value; break;
            case "AccentGold": theme.AccentGold = value; break;
            // Batch 1 (CP-6④): there is no second accent token. A document that declares HoverPoint is
            // reported as an unknown token by UiStyleDocument, so it never reaches this switch; the hover
            // step is read from the accent through UiTheme.AccentHover.
            case "Border": theme.Border = value; break;
            case "BorderStrong": theme.BorderStrong = value; break;
            case "Divider": theme.Divider = value; break;
            case "BaseBorder": theme.BaseBorder = value; break;
            case "PanelBorder": theme.PanelBorder = value; break;
            case "RaisedBorder": theme.RaisedBorder = value; break;
            case "HoverBorder": theme.HoverBorder = value; break;
            case "SelectedBorder": theme.SelectedBorder = value; break;
            case "SuccessBorder": theme.SuccessBorder = value; break;
            case "DangerBorder": theme.DangerBorder = value; break;
        }
    }

    private static void ApplyMetric(UiTheme theme, string token, float value)
    {
        UiGeometry current = theme.Geometry;
        switch (token)
        {
            case "Padding":
                theme.Geometry = new UiGeometry(value, current.Spacing, current.Gap, current.RowHeight, current.Hairline);
                break;
            case "Spacing":
                theme.Geometry = new UiGeometry(current.Padding, value, current.Gap, current.RowHeight, current.Hairline);
                break;
            case "Gap":
                theme.Geometry = new UiGeometry(current.Padding, current.Spacing, value, current.RowHeight, current.Hairline);
                break;
            case "RowHeight":
                theme.Geometry = new UiGeometry(current.Padding, current.Spacing, current.Gap, value, current.Hairline);
                break;
            case "Hairline":
                theme.Geometry = new UiGeometry(current.Padding, current.Spacing, current.Gap, current.RowHeight, value);
                break;
        }
    }

    private void Record(string message, int line = 0, string? elementPath = null)
    {
        if (issues.Count >= MaxIssues) return;
        issues.Add(new UiStyleIssue(message, line, elementPath));
    }
}

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

    public UiStyleResolver(UiTheme baseline, UiStyleDocument document)
    {
        this.baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
        this.document = document ?? throw new ArgumentNullException(nameof(document));
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
    /// Applies the document's page-level scheme and density to <paramref name="theme"/> in place. This is
    /// the resolve-before-Measure slot: the consumer (or the host, once it holds a document) runs it
    /// before the first arrange, and because every applied token moves <see cref="UiTheme.LayoutRevision"/>
    /// the existing band cache is invalidated by the clock it already compares - no second clock, and the
    /// first arrangement already sees the document's values.
    /// </summary>
    public void ApplyTo(UiTheme theme)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));
        ApplyScheme(theme, document.DefaultScheme);
        ApplyDensity(theme, document.DefaultDensity);
    }

    /// <summary>
    /// The theme a scope draws with, built once per effective (scheme, density) pair and reused - the same
    /// instance on every later frame. Unknown names fall back to the nearest declared ancestor's values
    /// (or the baseline) and are recorded.
    /// </summary>
    public UiTheme ThemeFor(IReadOnlyList<UiStyleDeclaration>? nearestFirst)
    {
        string? scheme = ResolveScheme(nearestFirst);
        string? density = ResolveDensity(nearestFirst);
        string key = (scheme ?? "") + "\u0001" + (density ?? "");

        if (regionThemes.TryGetValue(key, out UiTheme? cached)) return cached;

        UiTheme theme = baseline.Clone();
        ApplyScheme(theme, scheme);
        ApplyDensity(theme, density);

        if (regionThemes.Count >= MaxScopes)
        {
            Record("More than " + MaxScopes.ToString(CultureInfo.InvariantCulture)
                + " distinct scheme/density scopes; this scope's theme is rebuilt per use instead of cached.");
            return theme;
        }

        regionThemes.Add(key, theme);
        return theme;
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

    private void ApplyScheme(UiTheme theme, string? name)
    {
        // Explicit null/empty test rather than string.IsNullOrEmpty: net472's overload carries no
        // [NotNullWhen(false)], so flow analysis cannot narrow through it (the repo's recorded trap).
        if (name == null || name.Length == 0) return;
        if (!document.TryGetScheme(name, out UiStyleDocument.SchemeDefinition definition))
        {
            Record("Unknown scheme '" + name + "'; the scope keeps the values it would have had without it.");
            return;
        }

        foreach (KeyValuePair<string, Color> pair in definition.Colours)
        {
            ApplyColour(theme, pair.Key, pair.Value);
        }

        if (definition.Font.HasValue) theme.DefaultFont = definition.Font.Value;
    }

    private void ApplyDensity(UiTheme theme, string? name)
    {
        if (name == null || name.Length == 0) return;
        if (!document.TryGetDensity(name, out UiStyleDocument.DensityDefinition definition))
        {
            Record("Unknown density '" + name + "'; the scope keeps the values it would have had without it.");
            return;
        }

        foreach (KeyValuePair<string, float> pair in definition.Metrics)
        {
            ApplyMetric(theme, pair.Key, pair.Value);
        }
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
            case "HoverPoint": theme.HoverPoint = value; break;
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

    private void Record(string message, int line = 0)
    {
        if (issues.Count >= MaxIssues) return;
        issues.Add(new UiStyleIssue(message, line));
    }
}

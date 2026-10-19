using System;
using System.Collections.Generic;


namespace FerriteLib.UiKit.Kernel;

/// <summary>Written per-property intent. Colours affect paint; font/density changes invalidate measure.</summary>
public sealed class UiElementProperties
{
    internal UiElementProperties(Action changed)
    {
        Font = new UiPropertyDeclaration<UiFont>(changed);
        LocalFont = new UiPropertyDeclaration<UiFont>(changed);
        Density = new UiPropertyDeclaration<UiDensity>(changed);
        LocalDensity = new UiPropertyDeclaration<UiDensity>(changed);
    }

    public UiPropertyDeclaration<UiFont> Font { get; }
    public UiPropertyDeclaration<UiFont> LocalFont { get; }
    public UiPropertyDeclaration<UiDensity> Density { get; }
    public UiPropertyDeclaration<UiDensity> LocalDensity { get; }
    private readonly Dictionary<string, UiPropertyDeclaration<UiColour?>> colours = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiPropertyDeclaration<UiColour?>> localColours = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiResolvedProperty<UiColour?>> resolvedColours = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiResolvedProperty<float>> resolvedMetrics = new(StringComparer.Ordinal);
    public IReadOnlyList<string> ColourNames => Array.AsReadOnly(UiAppearanceProperties.Colours);
    public IReadOnlyDictionary<string, UiResolvedProperty<UiColour?>> ResolvedColours => resolvedColours;
    public IReadOnlyDictionary<string, UiResolvedProperty<float>> ResolvedMetrics => resolvedMetrics;
    public UiPropertyDeclaration<UiColour?> Palette(UiPaletteRole role) => ColourDeclaration(colours, role.ToString());
    public UiPropertyDeclaration<UiColour?> LocalPalette(UiPaletteRole role) => ColourDeclaration(localColours, role.ToString());

    private static UiPropertyDeclaration<UiColour?> ColourDeclaration(Dictionary<string, UiPropertyDeclaration<UiColour?>> declarations, string token)
    {
        if (!UiAppearanceProperties.IsColour(token)) throw new ArgumentException("Unknown colour token '" + token + "'.", nameof(token));
        if (!declarations.TryGetValue(token, out UiPropertyDeclaration<UiColour?>? declaration))
        {
            declaration = new UiPropertyDeclaration<UiColour?>(null, value =>
            {
                if (!value.HasValue) throw new ArgumentException(token + " cannot be null.");
            });
            declarations.Add(token, declaration);
        }
        return declaration;
    }


    private readonly UiPropertyDeclaration<UiFont> inlineFont = new();
    private readonly UiPropertyDeclaration<UiFont> namedFont = new();
    private readonly Dictionary<string, UiPropertyDeclaration<UiColour?>> inlineColours = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiPropertyDeclaration<UiColour?>> namedColours = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiPropertyDeclaration<float>> namedMetrics = new(StringComparer.Ordinal);
    private UiElementSpec? authoredSpec;
    private UiStyleDocument? authoredDocument;

    internal Action CaptureAuthoring()
    {
        UiElementSpec? priorSpec = authoredSpec;
        UiStyleDocument? priorDocument = authoredDocument;
        UiResolvedProperty<UiFont> priorFont = ResolvedFont;
        UiResolvedProperty<UiDensity> priorDensity = ResolvedDensity;
        var priorColours = new Dictionary<string, UiResolvedProperty<UiColour?>>(resolvedColours, StringComparer.Ordinal);
        var priorMetrics = new Dictionary<string, UiResolvedProperty<float>>(resolvedMetrics, StringComparer.Ordinal);
        return () =>
        {
            authoredSpec = null;
            if (priorSpec != null) Authoring(priorSpec, priorDocument);
            else
            {
                authoredDocument = priorDocument;
                inlineFont.Remove();
                namedFont.Remove();
                inlineColours.Clear();
                namedColours.Clear();
                namedMetrics.Clear();
            }
            ResolvedFont = priorFont;
            ResolvedDensity = priorDensity;
            resolvedColours.Clear();
            foreach (var pair in priorColours) resolvedColours.Add(pair.Key, pair.Value);
            resolvedMetrics.Clear();
            foreach (var pair in priorMetrics) resolvedMetrics.Add(pair.Key, pair.Value);
            lastHost = null;
        };
    }
    internal void Authoring(UiElementSpec spec, UiStyleDocument? document)
    {
        if (ReferenceEquals(spec, authoredSpec) && ReferenceEquals(document, authoredDocument)) return;
        authoredSpec = spec;
        authoredDocument = document;
        inlineFont.Remove();
        namedFont.Remove();
        inlineColours.Clear();
        namedColours.Clear();
        namedMetrics.Clear();
        if (spec.TryGetAttribute("Font", out string font) && Enum.TryParse(font, true, out UiFont parsed)) inlineFont.Set(parsed);
        foreach (string token in UiAppearanceProperties.Colours)
            if (spec.TryGetAttribute("Palette." + token, out string raw) && UiStyleDocument.TryParseColour(raw, out UiColour color))
                ColourDeclaration(inlineColours, token).Set(color);
        if (document == null) return;
        if (spec.TryGetAttribute("Scheme", out string scheme) && document.TryGetScheme(scheme.Trim(), out UiStyleDocument.SchemeDefinition definition))
        {
            if (definition.Font.HasValue) namedFont.Set(definition.Font.Value);
            foreach (KeyValuePair<string, UiColour> pair in definition.Colours) ColourDeclaration(namedColours, pair.Key).Set(pair.Value);
            foreach (KeyValuePair<string, float> pair in definition.Metrics) namedMetrics[pair.Key] = Declared(pair.Value);
        }
        if (spec.TryGetAttribute("Density", out string density) && document.TryGetDensity(density.Trim(), out UiStyleDocument.DensityDefinition dense))
            foreach (KeyValuePair<string, float> pair in dense.Metrics) namedMetrics[pair.Key] = Declared(pair.Value);
    }

    private static UiPropertyDeclaration<T> Declared<T>(T value)
    {
        var declaration = new UiPropertyDeclaration<T>();
        declaration.Set(value);
        return declaration;
    }
    private static UiPropertyDeclaration<T>? Prefer<T>(UiPropertyDeclaration<T>? first, UiPropertyDeclaration<T>? second)
        => first?.IsDeclared == true ? first : second;
    private static UiPropertyDeclaration<T>? Find<T>(Dictionary<string, UiPropertyDeclaration<T>> source, string token)
        => source.TryGetValue(token, out UiPropertyDeclaration<T>? declaration) ? declaration : null;

    internal static IEnumerable<KeyValuePair<string, UiPropertyDeclaration<T>>> Ancestors<T>(UiNode node,
        Func<UiElementProperties, UiPropertyDeclaration<T>?> instance, Func<UiElementProperties, UiPropertyDeclaration<T>?> local,
        Func<UiElementProperties, string>? localSource = null)
    {
        for (UiNode? parent = node.Parent; parent != null; parent = parent.Parent)
        {
            UiPropertyDeclaration<T>? direct = instance(parent.Properties);
            UiPropertyDeclaration<T>? style = local(parent.Properties);
            if (direct != null) yield return new KeyValuePair<string, UiPropertyDeclaration<T>>(parent.Id.Key, direct);
            if (style != null) yield return new KeyValuePair<string, UiPropertyDeclaration<T>>(parent.Id.Key + ":" + (localSource?.Invoke(parent.Properties) ?? "local-style"), style);
        }
    }
    private static IEnumerable<KeyValuePair<string, UiPropertyDeclaration<T>>> WithPage<T>(
        IEnumerable<KeyValuePair<string, UiPropertyDeclaration<T>>> ancestors, UiPropertyDeclaration<T>? page)
    {
        foreach (var ancestor in ancestors) yield return ancestor;
        if (page != null) yield return new KeyValuePair<string, UiPropertyDeclaration<T>>("page-style", page);
    }
    private string NamedSource(string token, bool metric = false)
    {
        if (metric && authoredSpec?.TryGetAttribute("Density", out string densityName) == true
            && authoredDocument?.TryGetDensity(densityName.Trim(), out UiStyleDocument.DensityDefinition density) == true
            && density.Metrics.ContainsKey(token)) return "Density=" + densityName.Trim();
        return authoredSpec?.TryGetAttribute("Scheme", out string scheme) == true ? "Scheme=" + scheme.Trim() : "local-style";
    }
    private UiPropertyDeclaration<float>? Metric(string token, bool local)
    {
        UiPropertyDeclaration<UiDensity> bundle = local ? LocalDensity : Density;
        if (bundle.IsDeclared) return Declared(UiAppearanceProperties.ReadMetric(bundle.Value, token));
        return local ? Find(namedMetrics, token) : null;
    }
    private UiStyleDocument.SchemeDefinition? PageScheme
        => authoredDocument?.DefaultScheme is string name && authoredDocument.TryGetScheme(name, out UiStyleDocument.SchemeDefinition scheme) ? scheme : null;
    private UiPropertyDeclaration<float>? PageMetric(string token)
    {
        float value;
        if (authoredDocument?.DefaultDensity is string name && authoredDocument.TryGetDensity(name, out UiStyleDocument.DensityDefinition density)
            && density.Metrics.TryGetValue(token, out value)) return Declared(value);
        return PageScheme?.Metrics.TryGetValue(token, out value) == true ? Declared(value) : null;
    }
    private UiPropertyDeclaration<UiColour?>? PageColour(string token)
        => PageScheme?.Colours.TryGetValue(token, out UiColour value) == true ? Declared<UiColour?>(value) : null;

    private UiTheme? lastHost;
    private UiTheme? appliedTheme;
    private int hostColourRevision;
    private int hostLayoutRevision;
    private int hostPropertyRevision;
    private UiFont lastFont;
    private UiDensity lastDensity;
    private readonly Dictionary<string, UiColour?> lastColours = new(StringComparer.Ordinal);

    internal UiTheme Apply(UiNode node, UiTheme host, UiFont fallbackFont = UiFont.Small)
    {
        UiPropertyDeclaration<UiFont>? pageFont = PageScheme?.Font is UiFont page ? Declared(page) : null;
        ResolvedFont = UiPropertyResolver.Resolve(Prefer(Font, inlineFont), Prefer(LocalFont, namedFont),
            WithPage(Ancestors(node, p => Prefer(p.Font, p.inlineFont), p => Prefer(p.LocalFont, p.namedFont), p => p.LocalFont.IsDeclared ? "local-style" : p.NamedSource("Font")), pageFont),
            host.HasFontOverride ? Declared(host.DefaultFont) : null, fallbackFont);
        if (ResolvedFont.Origin == UiPropertyOrigin.LocalStyle && !LocalFont.IsDeclared)
            ResolvedFont = new UiResolvedProperty<UiFont>(ResolvedFont.Value, ResolvedFont.Origin, NamedSource("Font"));
        UiDensity defaults = DensityOf(host);
        foreach (string token in UiAppearanceProperties.Metrics)
        {
            resolvedMetrics[token] = UiPropertyResolver.Resolve(Metric(token, false), Metric(token, true),
                WithPage(Ancestors(node, p => p.Metric(token, false), p => p.Metric(token, true), p => p.LocalDensity.IsDeclared ? "local-style" : p.NamedSource(token, true)), PageMetric(token)),
                host.HasDensityOverride ? Declared(UiAppearanceProperties.ReadMetric(defaults, token)) : null,
                UiAppearanceProperties.ReadMetric(new UiDensity(6, 4, 6, 28, 1), token));
            UiResolvedProperty<float> resolved = resolvedMetrics[token];
            if (resolved.Origin == UiPropertyOrigin.LocalStyle && !LocalDensity.IsDeclared)
                resolvedMetrics[token] = new UiResolvedProperty<float>(resolved.Value, resolved.Origin, NamedSource(token, true));
        }
        UiDensity d = new(resolvedMetrics["Padding"].Value, resolvedMetrics["Spacing"].Value, resolvedMetrics["Gap"].Value,
            resolvedMetrics["RowHeight"].Value, resolvedMetrics["Hairline"].Value);
        UiResolvedProperty<float> first = resolvedMetrics["Padding"];
        bool uniformSource = true;
        foreach (var metric in resolvedMetrics.Values)
            uniformSource &= metric.Origin == first.Origin && metric.Source == first.Source;
        ResolvedDensity = new UiResolvedProperty<UiDensity>(d, uniformSource ? first.Origin : UiPropertyOrigin.Mixed, uniformSource ? first.Source : "per-metric");
        bool paletteChanged = false;
        foreach (string token in UiAppearanceProperties.Colours)
        {
            UiResolvedProperty<UiColour?> resolved = UiPropertyResolver.Resolve(Prefer(Find(colours, token), Find(inlineColours, token)),
                Prefer(Find(localColours, token), Find(namedColours, token)),
                WithPage(Ancestors(node, p => Prefer(Find(p.colours, token), Find(p.inlineColours, token)),
                    p => Prefer(Find(p.localColours, token), Find(p.namedColours, token)),
                    p => Find(p.localColours, token)?.IsDeclared == true ? "local-style" : p.NamedSource(token)), PageColour(token)),
                host.HasColourOverride(token) ? Declared(UiAppearanceProperties.ReadColour(host, token)) : null,
                UiAppearanceProperties.ReadColour(host, token));
            if (resolved.Origin == UiPropertyOrigin.LocalStyle && Find(localColours, token)?.IsDeclared != true)
                resolved = new UiResolvedProperty<UiColour?>(resolved.Value, resolved.Origin, NamedSource(token));
            resolvedColours[token] = resolved;
            if (!lastColours.TryGetValue(token, out UiColour? prior) || !Nullable.Equals(prior, resolved.Value)) paletteChanged = true;
        }
        bool equalsHost = ResolvedFont.Value == host.DefaultFont && d.Equals(defaults);
        foreach (var colour in resolvedColours)
            equalsHost &= Nullable.Equals(colour.Value.Value, UiAppearanceProperties.ReadColour(host, colour.Key));
        if (equalsHost)
        {
            // Preserve the existing page-level theme identity, without promoting its values to declarations.
            lastHost = null;
            appliedTheme = null;
            return host;
        }
        bool reuse = ReferenceEquals(lastHost, host) && hostColourRevision == host.ColourRevision
            && hostLayoutRevision == host.LayoutRevision && hostPropertyRevision == host.PropertyRevision
            && lastFont == ResolvedFont.Value && lastDensity.Equals(d) && !paletteChanged;
        if (!reuse)
        {
            UiTheme theme = host.Clone();
            theme.ApplyStyleFont(ResolvedFont.Value);
            theme.ApplyStyleGeometry(new UiGeometry(d.Padding, d.Spacing, d.Gap, d.RowHeight, d.Hairline));
            lastColours.Clear();
            foreach (var colour in resolvedColours)
            {
                lastColours[colour.Key] = colour.Value.Value;
                if (colour.Value.Origin != UiPropertyOrigin.Fallback)
                    theme.ApplyStyleColour(colour.Key, colour.Value.Value);
            }
            appliedTheme = theme;
            lastHost = host;
            hostColourRevision = host.ColourRevision;
            hostLayoutRevision = host.LayoutRevision;
            hostPropertyRevision = host.PropertyRevision;
            lastFont = ResolvedFont.Value;
            lastDensity = d;
        }
        return appliedTheme!;
    }

    public UiResolvedProperty<UiFont> ResolvedFont { get; private set; }
    public UiResolvedProperty<UiDensity> ResolvedDensity { get; private set; }
    internal static UiDensity DensityOf(UiTheme theme) => new(theme.Geometry.Padding, theme.Geometry.Spacing, theme.Geometry.Gap, theme.Geometry.RowHeight, theme.Geometry.Hairline);
}

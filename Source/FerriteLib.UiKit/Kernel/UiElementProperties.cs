using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;
/// <summary>Per-instance declarations. Changes invalidate measurement, not business values or capture.</summary>
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

    internal static IEnumerable<KeyValuePair<string, UiPropertyDeclaration<T>>> Ancestors<T>(UiNode node, Func<UiElementProperties, UiPropertyDeclaration<T>> instance, Func<UiElementProperties, UiPropertyDeclaration<T>> local)
    {
        for (UiNode? parent = node.Parent; parent != null; parent = parent.Parent)
        {
            yield return new KeyValuePair<string, UiPropertyDeclaration<T>>(parent.Id.Key, instance(parent.Properties));
            yield return new KeyValuePair<string, UiPropertyDeclaration<T>>(parent.Id.Key, local(parent.Properties));
        }
    }

    private UiTheme? lastHost;
    private UiTheme? appliedTheme;
    private int hostColourRevision;
    private UiFont lastFont;
    private UiDensity lastDensity;
    internal UiTheme Apply(UiNode node, UiTheme host, UiFont fallbackFont = UiFont.Small)
    {
        var fontHost = new UiPropertyDeclaration<UiFont>();
        if (host.HasFontOverride)
            fontHost.Set(host.DefaultFont);
        UiDensity defaults = DensityOf(host);
        var densityHost = new UiPropertyDeclaration<UiDensity>();
        if (host.HasDensityOverride)
            densityHost.Set(defaults);
        ResolvedFont = UiPropertyResolver.Resolve(Font, LocalFont, Ancestors(node, p => p.Font, p => p.LocalFont), fontHost, fallbackFont);
        ResolvedDensity = UiPropertyResolver.Resolve(Density, LocalDensity, Ancestors(node, p => p.Density, p => p.LocalDensity), densityHost, new UiDensity(6, 4, 6, 28, 1));
        UiDensity d = ResolvedDensity.Value;
        if (ResolvedFont.Value == host.DefaultFont && d.Equals(defaults))
            return host;
        if (ReferenceEquals(lastHost, host) && hostColourRevision == host.ColourRevision && lastFont == ResolvedFont.Value && lastDensity.Equals(d))
            return appliedTheme!;
        hostColourRevision = host.ColourRevision;
        lastHost = host;
        lastFont = ResolvedFont.Value;
        lastDensity = d;
        var theme = host.Clone();
        appliedTheme = theme;
        theme.DefaultFont = ResolvedFont.Value;
        theme.Geometry = new UiGeometry(d.Padding, d.Spacing, d.Gap, d.RowHeight, d.Hairline);
        return theme;
    }

    public UiResolvedProperty<UiFont> ResolvedFont { get; private set; }
    public UiResolvedProperty<UiDensity> ResolvedDensity { get; private set; }

    internal static UiDensity DensityOf(UiTheme theme) => new UiDensity(theme.Geometry.Padding, theme.Geometry.Spacing, theme.Geometry.Gap, theme.Geometry.RowHeight, theme.Geometry.Hairline);
}

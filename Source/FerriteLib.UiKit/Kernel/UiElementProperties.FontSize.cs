using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

public sealed partial class UiElementProperties
{
    private UiPropertyDeclaration<int>? instanceFontSize, localFontSize;
    private readonly UiPropertyDeclaration<int> inlineFontSize = new(), namedFontSize = new();
    internal UiResolvedProperty<int?> ResolvedFontSize { get; private set; }
    internal readonly Dictionary<UiTextPurpose, UiTextFontFact> TextRuns = new();
    internal readonly Dictionary<UiTextPurpose, UiTextPaintFact> TextPaints = new();
    private int? lastFontSize;
    internal UiElementSpec? TextStyleSpec => authoredSpec;
    internal UiPropertyDeclaration<int> FontSizeDeclaration(bool local)
    {
        if (local) return localFontSize ??= UiTextFonts.Declaration(LayoutDeclarationChanged);
        return instanceFontSize ??= UiTextFonts.Declaration(LayoutDeclarationChanged);
    }
    private void AuthorFontSize(UiElementSpec spec, UiStyleDocument? document)
    {
        inlineFontSize.Remove(); namedFontSize.Remove();
        if (spec.TryGetAttribute("FontSize", out string raw) && UiTextFont.TrySize(raw, out int size)) inlineFontSize.Set(size);
        if (spec.TryGetAttribute("Scheme", out string scheme) && document?.TryGetScheme(scheme.Trim(), out UiStyleDocument.SchemeDefinition definition) == true
            && definition.FontSize.HasValue) namedFontSize.Set(definition.FontSize.Value);
    }
    private IEnumerable<(UiPropertyDeclaration<UiFont>? Font, UiPropertyDeclaration<int>? Size, UiPropertyOrigin Origin, string Source)> FontLayers(UiNode node, UiTheme host)
    {
        yield return (Font, instanceFontSize, UiPropertyOrigin.Instance, "instance");
        yield return (inlineFont, inlineFontSize, UiPropertyOrigin.Instance, "inline");
        yield return (LocalFont, localFontSize, UiPropertyOrigin.LocalStyle, "local-style");
        yield return (namedFont, namedFontSize, UiPropertyOrigin.LocalStyle, NamedSource("Font"));
        for (UiNode? parent = node.Parent; parent != null; parent = parent.Parent)
        {
            UiElementProperties p = parent.Properties;
            yield return (p.Font, p.instanceFontSize, UiPropertyOrigin.Ancestor, parent.Id.Key + ":instance");
            yield return (p.inlineFont, p.inlineFontSize, UiPropertyOrigin.Ancestor, parent.Id.Key + ":inline");
            yield return (p.LocalFont, p.localFontSize, UiPropertyOrigin.Ancestor, parent.Id.Key + ":local-style");
            yield return (p.namedFont, p.namedFontSize, UiPropertyOrigin.Ancestor, parent.Id.Key + ":" + p.NamedSource("Font"));
        }
        yield return (PageScheme?.Font is UiFont page ? Declared(page) : null,
            PageScheme?.FontSize is int pageSize ? Declared(pageSize) : null, UiPropertyOrigin.Ancestor, "page-style");
        yield return (host.HasFontOverride ? Declared(host.DefaultFont) : null,
            host.HasFontSizeOverride ? Declared(host.DeclaredFontSize) : null, UiPropertyOrigin.Host, "host");
    }
    private void ResolveFontSize(UiNode node, UiTheme host)
    {
        foreach (var layer in FontLayers(node, host))
            if (layer.Font?.IsDeclared == true || layer.Size?.IsDeclared == true)
            {
                ResolvedFontSize = new UiResolvedProperty<int?>(layer.Size?.IsDeclared == true ? layer.Size.Value : null,
                    layer.Origin, layer.Source + (layer.Size?.IsDeclared == true ? "" : ":Font-reset"));
                return;
            }
        ResolvedFontSize = new UiResolvedProperty<int?>(host.ReadFontSize, UiPropertyOrigin.Fallback, "library");
    }
}

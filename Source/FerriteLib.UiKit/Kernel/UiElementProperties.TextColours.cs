using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

public sealed partial class UiElementProperties
{
    private readonly Dictionary<string, UiPropertyDeclaration<UiColour?>> textColours = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiPropertyDeclaration<UiColour?>> localTextColours = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiPropertyDeclaration<UiColour?>> inlineTextColours = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiPropertyDeclaration<UiColour?>> namedTextColours = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiResolvedProperty<UiColour?>> resolvedTextColours = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiColour?> lastTextColours = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, UiResolvedProperty<UiColour?>> resolvedTextColoursView;
    internal IReadOnlyDictionary<string, UiResolvedProperty<UiColour?>> TextColourFacts => resolvedTextColoursView;

    internal UiPropertyDeclaration<UiColour?> TextColourDeclaration(string property, bool local)
        => UiTextColourProperties.Declaration(local ? localTextColours : textColours, property, InvalidateResolution);

    private void AuthorTextColours(UiElementSpec spec, UiStyleDocument? document)
    {
        inlineTextColours.Clear();
        namedTextColours.Clear();
        foreach (string property in UiTextColourProperties.Names)
            if (spec.TryGetAttribute(property, out string raw) && UiStyleDocument.TryParseColour(raw, out UiColour colour))
                UiTextColourProperties.Declaration(inlineTextColours, property).Set(colour);
        if (spec.TryGetAttribute("Scheme", out string scheme)
            && document?.TryGetScheme(scheme.Trim(), out UiStyleDocument.SchemeDefinition definition) == true)
            foreach (var pair in definition.TextColours)
                UiTextColourProperties.Declaration(namedTextColours, pair.Key).Set(pair.Value);
    }
    private UiPropertyDeclaration<UiColour?>? PageTextColour(string property)
        => PageScheme?.TextColours.TryGetValue(property, out UiColour value) == true ? Declared<UiColour?>(value) : null;

    private bool ResolveTextColours(UiNode node, UiTheme host)
    {
        bool changed = false;
        foreach (string property in UiTextColourProperties.Names)
        {
            UiResolvedProperty<UiColour?> resolved = UiPropertyResolver.Resolve(
                Prefer(Find(textColours, property), Find(inlineTextColours, property)),
                Prefer(Find(localTextColours, property), Find(namedTextColours, property)),
                WithPage(Ancestors(node,
                    p => Prefer(Find(p.textColours, property), Find(p.inlineTextColours, property)),
                    p => Prefer(Find(p.localTextColours, property), Find(p.namedTextColours, property)),
                    p => Find(p.localTextColours, property)?.IsDeclared == true ? "local-style" : p.NamedSource(property)), PageTextColour(property)),
                host.HasTextColourOverride(property) ? Declared(host.DeclaredTextColour(property)) : null,
                host.ReadTextColour(property));
            if (resolved.Origin == UiPropertyOrigin.LocalStyle && Find(localTextColours, property)?.IsDeclared != true)
                resolved = new UiResolvedProperty<UiColour?>(resolved.Value, resolved.Origin, NamedSource(property));
            resolvedTextColours[property] = resolved;
            if (!lastTextColours.TryGetValue(property, out UiColour? prior) || prior != resolved.Value) changed = true;
        }
        return changed;
    }
    private bool TextColoursEqual(UiTheme host)
    {
        foreach (var pair in resolvedTextColours)
            if (pair.Value.Value != host.ReadTextColour(pair.Key)) return false;
        return true;
    }
    private void ApplyTextColours(UiTheme theme)
    {
        lastTextColours.Clear();
        foreach (var pair in resolvedTextColours)
        {
            lastTextColours[pair.Key] = pair.Value.Value;
            theme.ApplyStyleTextColour(pair.Key, pair.Value.Value);
        }
    }
}

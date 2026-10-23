using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The shared appearance seam: one kind declares, at registration, the complete set of looks it can draw,
/// and this resolver answers which of them an element asked for.
/// <para>
/// <b>Why it is shared rather than per-kind.</b> A look used to live in the widget as a private attribute
/// read, which meant the same three questions - what may be declared, which value is the default, and what
/// the value means - were answered inside one widget and nowhere else: nothing could report a kind's
/// supported looks, and the natural-size half of <c>Width="Auto"</c> (which is measured by the registry's
/// natural-body entry point, outside any widget instance) had to re-derive the look from raw XML. Here the
/// kind declares its looks once, the resolver reads the element's own declaration against that set, and
/// every consumer of the answer - the widget's own measure, the natural-body entry point, draw and the
/// creation-time contract - reads one resolved value, so the measured look and the drawn look cannot drift
/// apart.
/// </para>
/// <para>
/// <b>The value is a NAME, never a colour and never a measurement.</b> An appearance selects which shape a
/// kind paints; the tokens that fill and outline it come from <see cref="UiResolvedStyle"/> and the theme,
/// and the numbers come from the look's own constants or the theme's geometry. A palette change therefore
/// cannot move a rect, and a look cannot pick a colour.
/// </para>
/// <para>
/// <b>Fail-soft, and never silent at creation.</b> An absent, blank or unrecognised declaration resolves to
/// the kind's declared default, because a look that cannot be read must not take a page down mid-frame;
/// the <see cref="ResolvedAppearance.Declared"/> flag is what lets the creation-time contract refuse a
/// genuinely unknown value once, located on the element and its authored text, instead of degrading it on
/// every frame.
/// </para>
/// </summary>
internal sealed class UiAppearanceResolver
{
    /// <summary>The attribute an element declares its look with. One name for every kind that has looks.</summary>
    internal const string Attribute = "Appearance";

    private readonly Dictionary<string, string> supported = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The resolver for one kind: <paramref name="defaultLook"/> is the look an element draws with when it
    /// declares nothing (or declares something unreadable), and every other name is an accepted alternative.
    /// The default is always part of the set, so "declare the default explicitly" needs no special case.
    /// </summary>
    internal UiAppearanceResolver(string defaultLook, params string[] otherLooks)
    {
        if (string.IsNullOrEmpty(defaultLook))
        {
            throw new ArgumentException("An appearance resolver needs a default look name.", nameof(defaultLook));
        }

        Default = defaultLook;
        supported[defaultLook] = defaultLook;

        if (otherLooks == null) return;
        foreach (string look in otherLooks)
        {
            if (string.IsNullOrEmpty(look)) continue;
            supported[look] = look;
        }
    }

    /// <summary>The look an element draws with when it declares none, or declares one the kind does not implement.</summary>
    internal string Default { get; }

    /// <summary>
    /// Every accepted spelling, canonical and case-insensitive. The registry publishes this as the kind's
    /// supported-look set, so the contract the engine validates against is the same object the widget
    /// resolves through.
    /// </summary>
    internal IReadOnlyCollection<string> Supported => supported.Keys;

    /// <summary>
    /// The look this element draws with. Absent, blank and unrecognised declarations all answer the kind's
    /// default; <see cref="ResolvedAppearance.Declared"/> is false in all three cases, which is what
    /// separates "took the default by omission" from "declared something this kind cannot draw". It IS the
    /// set-driven overload below, handed this instance's own set: one implementation, so a second copy of the
    /// rule cannot answer differently about the same declaration.
    /// </summary>
    internal ResolvedAppearance Resolve(UiElementSpec spec)
    {
        return Resolve(spec, Default, supported.Keys);
    }

    /// <summary>
    /// The same normalization against a set a caller holds as data - the registry's own declaration, or a
    /// lane driving the seam without a registered kind. One implementation, so a second copy of the rule
    /// cannot answer differently about the same declaration.
    /// </summary>
    internal static ResolvedAppearance Resolve(UiElementSpec spec, string defaultLook, IReadOnlyCollection<string> supports)
    {
        string? authored = Read(spec);
        if (authored == null) return new ResolvedAppearance(defaultLook, declared: false);

        foreach (string candidate in supports)
        {
            if (string.Equals(candidate, authored, StringComparison.OrdinalIgnoreCase))
            {
                return new ResolvedAppearance(candidate, declared: true);
            }
        }

        return new ResolvedAppearance(defaultLook, declared: false);
    }

    /// <summary>The authored text, trimmed, or null when nothing usable was declared.</summary>
    private static string? Read(UiElementSpec spec)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));
        if (!spec.TryGetAttribute(Attribute, out string raw)) return null;

        string value = (raw ?? "").Trim();
        return value.Length == 0 ? null : value;
    }
}

/// <summary>
/// One element's resolved look: the canonical name of the appearance it will draw with, plus whether the
/// element DECLARED one this kind supports.
/// <para>
/// The two facts are separate on purpose. Every frame answers with a usable name - an unreadable
/// declaration degrades to the kind's default rather than stopping the page - while <see cref="Declared"/>
/// is what the creation-time contract reads once, so a mistyped look is refused with a location instead of
/// being repainted as the default forever.
/// </para>
/// </summary>
internal readonly struct ResolvedAppearance
{
    internal ResolvedAppearance(string value, bool declared)
    {
        Value = value ?? "";
        Declared = declared;
    }

    /// <summary>The canonical look name the element draws with.</summary>
    internal string Value { get; }

    /// <summary>True when the element declared a look this kind implements.</summary>
    internal bool Declared { get; }

    /// <summary>True when nothing was drawn for the declaration - absent, blank or unrecognised.</summary>
    internal bool IsDefault => !Declared;

    public override string ToString() => Value;
}

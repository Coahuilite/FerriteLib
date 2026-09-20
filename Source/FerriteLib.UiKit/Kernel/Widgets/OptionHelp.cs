using System;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// The option-level help contract shared by the kinds whose options are parts of ONE element rather than
/// elements of their own: <c>input/mode-row</c>'s cells and <c>input/dropdown</c>'s popup rows. The engine's
/// element-level <c>HelpKey</c> cannot reach these — an option is not an element, so no manifest attribute can
/// sit on it and the draw walk has nothing to walk — which is why the option level owns its own attribute and
/// its own rule, and why the two kinds that have options share one implementation instead of two copies.
/// <para>
/// The contract: the element declares <c>HoverHelpKey</c>, a <b>writable string</b> value binding, and the kind
/// writes the hovered option's identity there — clearing it (an empty string) when no option is hovered, and
/// only when the answer changes, so a consumer sees one write per hover transition rather than one per frame.
/// The identity itself is the kind's own business and is documented on each kind: the mode row publishes the
/// option's declared help text (<c>DescriptionN</c>), or its value when it declares none; the dropdown
/// publishes the option's value, because a dropdown's options come from the consumer's data and the value is
/// the machine token a help catalog is keyed by.
/// </para>
/// <para>
/// Why a binding rather than the session claim the element level uses: the claim carries ONE live topic for the
/// page, which is what a help panel wants, while this binding carries a per-element fact the consumer's own
/// model can read, diff and invalidate. They are complementary reads of the same hover, not two mechanisms for
/// one meaning — and the option level cannot use the claim's element attribution anyway, because the hovered
/// thing is a cell inside an element, not an element.
/// </para>
/// </summary>
internal static class OptionHelp
{
    /// <summary>The one literal, declared where the option-level contract started so the two kinds cannot drift.</summary>
    internal const string Attribute = InputModeRowWidget.HoverHelpKeyAttribute;

    /// <summary>The declared sink key, trimmed, or "" when the element asks for no option-level help.</summary>
    internal static string SinkKey(UiElementSpec spec)
    {
        return spec.TryGetAttribute(Attribute, out string key) ? key.Trim() : "";
    }

    /// <summary>
    /// The creation-time contract for the sink: it must name a string value binding and it must be writable,
    /// because the publication is a write. A read-only or unbound key would refuse that write mid-frame and
    /// trip the recovery band, so it is refused where the author can be told instead of discovered at draw.
    /// </summary>
    internal static void Validate(IUiBindings bindings, UiElementSpec spec, string elementPath, string kindName)
    {
        string key = SinkKey(spec);
        if (key.Length == 0) return;

        bindings.ValidateValue<string>(key, elementPath);
        if (!bindings.IsWritable(key))
        {
            throw new InvalidOperationException(
                $"{kindName} at '{elementPath}' declares '{Attribute}' as '{key}', which is not writable: the kind"
                + " publishes the hovered option's help identity through it, so a read-only or unbound key would"
                + " refuse the write. Bind it with BindValue<string>.");
        }
    }

    /// <summary>
    /// Publishes the hovered option's identity, or clears the sink when <paramref name="identity"/> is empty.
    /// Change-detected, so the consumer's binding moves on a hover transition rather than every frame.
    /// </summary>
    internal static void Publish(UiWidgetContext ctx, string sinkKey, string identity)
    {
        if (sinkKey.Length == 0) return;

        if (ctx.Bindings.TryGet(sinkKey, out string published)
            && string.Equals(published ?? "", identity, StringComparison.Ordinal))
        {
            return;
        }

        ctx.Bindings.Set(sinkKey, identity);
    }
}

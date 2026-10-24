using System;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Optional observable option output for mode-row cells and dropdown popup rows. Session HelpRef/source
/// publication does not require this binding. Explicit Choice.HelpRef is opaque; empty means no specific
/// option topic, never Value/Text inference. General element help remains independent.
/// A writable HoveredOptionHelpOutputBind receives changed references through its access-supplied value
/// endpoint. The node retains that existing owner-bound endpoint for withdrawal on empty/no hover,
/// close, hide, removal and backing replacement. Cleanup withdraws only its own still-current publication;
/// it never erases another publisher or a consumer-written/announced value at a shared address.
/// </summary>
internal static class OptionHelp
{
    /// <summary>The one literal, declared where the option-level contract started so the two kinds cannot drift.</summary>
    internal const string Attribute = InputModeRowWidget.HoveredOptionHelpOutputBindAttribute;

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
    internal static void Validate(IUiBindingAccess bindings, UiElementSpec spec, string elementPath, string kindName)
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
    /// Publishes the explicit reference, or withdraws this node's owned output when identity is empty.
    /// Change-detected writes preserve notification/subscription semantics without per-frame churn.
    /// </summary>
    internal static void Publish(UiWidgetContext ctx, string sinkKey, string identity, bool popup = false)
    {
        UiNode node = ctx.Node;
        UiBindings owner = UiBindings.OwnerOf(ctx.Bindings);
        if (node.OptionHelpOwner != null && (!ReferenceEquals(node.OptionHelpOwner, owner)
            || node.OptionHelpBind != sinkKey || identity.Length == 0))
            Release(node);
        if (sinkKey.Length == 0 || identity.Length == 0) return;
        node.OptionHelpPass = ctx.Session.Frame;
        node.OptionHelpPopup = popup;
        UiValueEndpoint endpoint = ctx.Bindings.Value(sinkKey);
        if (!ReferenceEquals(endpoint.Owner, owner))
            throw new InvalidOperationException("Option help output endpoint '" + sinkKey + "' does not belong to the access's current BindingOwner at '" + node.Path + "'.");
        owner.PublishOptionHelp(endpoint, node, sinkKey, identity);
    }

    internal static void Release(UiNode node)
    {
        UiBindings? owner = node.OptionHelpOwner;
        UiValueEndpoint? endpoint = node.OptionHelpEndpoint;
        string key = node.OptionHelpBind, helpRef = node.OptionHelpRef;
        // End ownership before calling external code. A failed cleanup is reported, never replayed.
        node.OptionHelpOwner = null;
        node.OptionHelpEndpoint = null;
        node.OptionHelpBind = node.OptionHelpRef = "";
        node.OptionHelpPopup = false;
        if (owner != null && endpoint != null) owner.ReleaseOptionHelp(endpoint, node, key, helpRef);
    }
}

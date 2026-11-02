using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Optional text-colour declarations, separate from the closed semantic palette.</summary>
public static class UiTextColours
{
    /// <summary>Host intent. Remove reveals the current page application or library fallback.</summary>
    public static UiPropertyDeclaration<UiColour?> Theme(UiTheme theme, string property = "TextColor")
        => (theme ?? throw new ArgumentNullException(nameof(theme))).TextColourDeclaration(property);

    /// <summary>Element-instance intent. Remove reveals the next current source.</summary>
    public static UiPropertyDeclaration<UiColour?> Instance(UiElementProperties properties, string property = "TextColor")
        => (properties ?? throw new ArgumentNullException(nameof(properties))).TextColourDeclaration(property, false);

    /// <summary>Element-local style intent, below inline declarations and above named styles.</summary>
    public static UiPropertyDeclaration<UiColour?> Local(UiElementProperties properties, string property = "TextColor")
        => (properties ?? throw new ArgumentNullException(nameof(properties))).TextColourDeclaration(property, true);

    /// <summary>Live read-only per-slot values and sources from the element's last normal resolution.</summary>
    public static IReadOnlyDictionary<string, UiResolvedProperty<UiColour?>> Resolved(UiElementProperties properties)
        => (properties ?? throw new ArgumentNullException(nameof(properties))).TextColourFacts;

    /// <summary>
    /// Resolves owned appearance declarations for an independent session context, without a Host.
    /// Pass the authoritative host theme, not a previously resolved element theme. Use the returned
    /// context for both measurement and drawing; call again after changing declarations.
    /// Retained pages already perform this resolution through their normal Host pipeline.
    /// </summary>
    public static UiWidgetContext Resolve(UiWidgetContext context, UiTheme host)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (host == null) throw new ArgumentNullException(nameof(host));
        if (!context.IsActive) throw new ObjectDisposedException(nameof(UiSession));
        return context.WithTheme(context.Properties.Apply(context.Node, host));
    }

    /// <summary>Read the effective slot; null means fall through to the semantic palette.</summary>
    public static UiColour? Read(UiTheme theme, string property = "TextColor")
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));
        UiTextColourProperties.Validate(property);
        return theme.ReadTextColour(property);
    }
}

internal static class UiTextColourProperties
{
    internal static readonly string[] Names = { "TextColor", "TextColor.Muted", "TextColor.Active", "TextColor.Success", "TextColor.Warning", "TextColor.Danger", "TextColor.Disabled" };
    internal static bool IsSlot(string name) => Array.IndexOf(Names, name) >= 0;
    internal static string? XmlSlot(string name)
    {
        foreach (string slot in Names) if (string.Equals(slot, name, StringComparison.OrdinalIgnoreCase)) return slot;
        return null;
    }
    internal static void Validate(string name)
    {
        if (!IsSlot(name)) throw new ArgumentException("Unknown text colour slot '" + name + "'.", nameof(name));
    }
    internal static UiPropertyDeclaration<UiColour?> Declaration(Dictionary<string, UiPropertyDeclaration<UiColour?>> declarations,
        string property, Action? changed = null)
    {
        Validate(property);
        if (!declarations.TryGetValue(property, out var declaration))
        {
            declaration = new UiPropertyDeclaration<UiColour?>(changed, value =>
            {
                if (!value.HasValue) throw new ArgumentException("Use Remove for an absent text colour, or an explicit transparent colour.", nameof(value));
            });
            declarations.Add(property, declaration);
        }
        return declaration;
    }
}

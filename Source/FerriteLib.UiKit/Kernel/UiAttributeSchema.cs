using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace FerriteLib.UiKit.Kernel;
public enum UiBindingRole
{
    None,
    Value,
    Choices,
    Command,
    Structure
}

public enum UiAttributeKind
{
    Text,
    Number,
    Boolean,
    Length
}

/// <summary>An immutable declaration literal. XML tokenization and C# construction enter the same value path.</summary>
public readonly struct UiAttributeValue
{
    private readonly float number;
    private readonly bool boolean;
    private readonly byte flags;
    private UiAttributeValue(string text, float number, bool boolean, byte flags)
    {
        this.text = text;
        this.number = number;
        this.boolean = boolean;
        this.flags = flags;
    }

    private readonly string? text;
    public string Text => text ?? "";

    public static UiAttributeValue Literal(string text)
    {
        text = text ?? "";
        bool numeric = float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float n) && !float.IsNaN(n) && !float.IsInfinity(n);
        bool logical = bool.TryParse(text, out bool b);
        if (text == "0" || text == "1")
        {
            logical = true;
            b = text == "1";
        }

        return new UiAttributeValue(text, n, b, (byte)((numeric ? 1 : 0) | (logical ? 2 : 0)));
    }

    public static UiAttributeValue Number(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        return new UiAttributeValue(value.ToString("R", CultureInfo.InvariantCulture), value, false, 1);
    }

    public static UiAttributeValue Boolean(bool value) => new(value ? "true" : "false", 0, value, 2);
    public bool TryNumber(out float value)
    {
        value = number;
        return (flags & 1) != 0;
    }

    public bool TryBoolean(out bool value)
    {
        value = boolean;
        return (flags & 2) != 0;
    }
}

public readonly struct UiAttributeDefinition
{
    public UiAttributeDefinition(string name, UiAttributeKind kind = UiAttributeKind.Text, UiBindingRole binding = UiBindingRole.None)
    {
        Binding = binding;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Kind = kind;
    }

    public string Name { get; }
    public UiAttributeKind Kind { get; }
    public UiBindingRole Binding { get; }
}

/// <summary>Immutable attribute grammar. Root and template compilation consult the same descriptor.</summary>
public sealed class UiAttributeSchema
{
    private readonly IReadOnlyDictionary<string, UiAttributeDefinition> attributes;
    public UiAttributeSchema(params UiAttributeDefinition[] declarations)
    {
        var copy = new Dictionary<string, UiAttributeDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in declarations ?? throw new ArgumentNullException(nameof(declarations)))
            copy.Add(d.Name, d);
        attributes = new ReadOnlyDictionary<string, UiAttributeDefinition>(copy);
        Names = Array.AsReadOnly(copy.Keys.ToArray());
    }

    public IReadOnlyCollection<string> Names { get; }

    internal static UiAttributeSchema FromNames(IReadOnlyCollection<string> names) => new(names.Select(n => new UiAttributeDefinition(n, StandardKind(n), StandardBinding(n))).ToArray());
    internal UiBindingRole Binding(string name) => attributes.TryGetValue(name, out var d) ? d.Binding : UiBindingRole.None;
    internal static UiBindingRole StandardBinding(string name)
    {
        switch (name.ToLowerInvariant())
        {
            case "bind":
            case "visiblekey":
            case "widthkey":
            case "selectedkey":
            case "payloadkey":
            case "pointkeysbind":
            case "hoverhelpkey":
                return UiBindingRole.Value;
            case "actionbind":
            case "cancelbind":
            case "disclosurebind":
            case "checkbind":
                return UiBindingRole.Command;
            case "optionsbind":
                return UiBindingRole.Choices;
            case "items":
                return UiBindingRole.Structure;
            default:
                return UiBindingRole.None;
        }
    }

    private static UiAttributeKind StandardKind(string name)
    {
        switch (name.ToLowerInvariant())
        {
            case "hidden":
            case "visible":
            case "fill":
            case "narrowhidden":
            case "widehidden":
            case "live":
            case "multiline":
            case "sharedlabels":
            case "updatescope":
            case "decorative":
            case "aspectheight":
                return UiAttributeKind.Boolean;
            case "height":
            case "width":
                return UiAttributeKind.Length;
            case "gap":
            case "padding":
            case "minwidth":
            case "maxwidth":
            case "breakpoint":
            case "cols":
            case "narrowcols":
            case "visiblerows":
            case "offsetx":
            case "offsety":
            case "min":
            case "max":
            case "step":
                return UiAttributeKind.Number;
            default:
                return UiAttributeKind.Text;
        }
    }

    internal void Validate(UiElementSpec spec, string source, string path)
    {
        foreach (var entry in attributes)
        {
            if (!spec.Values.TryGetValue(entry.Key, out var v))
                continue;
            bool valid = entry.Value.Kind switch
            {
                UiAttributeKind.Number => v.TryNumber(out _),
                UiAttributeKind.Boolean => v.TryBoolean(out _),
                UiAttributeKind.Length => v.TryNumber(out _) || v.Text.Length == 0 || v.Text.Equals("Auto", StringComparison.OrdinalIgnoreCase) || v.Text.Equals("MatchContent", StringComparison.OrdinalIgnoreCase),
                _ => true
            };
            if (!valid)
                throw new UiContractException("Invalid " + entry.Key + " literal '" + v.Text + "'; expected " + entry.Value.Kind + ".", source, spec.Id, spec.Kind, path);
        }
    }
}

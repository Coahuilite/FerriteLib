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
    private readonly IReadOnlyList<float>? numbers;
    private readonly UiLayoutOffset offset;
    private UiAttributeValue(string text, float number, bool boolean, byte flags,
        IReadOnlyList<float>? numbers = null, UiLayoutOffset offset = default)
    {
        this.text = text;
        this.number = number;
        this.boolean = boolean;
        this.flags = flags;
        this.numbers = numbers;
        this.offset = offset;
    }

    private readonly string? text;
    public string Text => text ?? "";

    public static UiAttributeValue Literal(string text)
    {
        text = text ?? "";
        string token = text.Trim();
        bool numeric = Finite(token, out float n);
        bool logical = bool.TryParse(token, out bool b);
        if (token == "0" || token == "1")
        {
            logical = true;
            b = token == "1";
        }

        bool percentage = token.EndsWith("%", StringComparison.Ordinal);
        bool hasOffset = numeric || (percentage && Finite(token.Substring(0, token.Length - 1).Trim(), out _));
        float ratio = 0;
        if (percentage && hasOffset) Finite(token.Substring(0, token.Length - 1).Trim(), out ratio);
        IReadOnlyList<float>? components = numeric ? Array.AsReadOnly(new[] { n }) : null;
        if (!numeric && (token.IndexOf(',') >= 0 || token.IndexOf(';') >= 0))
        {
            string[] parts = token.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var parsed = new float[parts.Length];
            bool valid = parts.Length == 1 || parts.Length == 2 || parts.Length == 4;
            for (int i = 0; i < parts.Length; i++) valid &= Finite(parts[i].Trim(), out parsed[i]);
            if (valid) components = Array.AsReadOnly(parsed);
        }
        return new UiAttributeValue(text, n, b, (byte)((numeric ? 1 : 0) | (logical ? 2 : 0) | (hasOffset ? 4 : 0)),
            components, percentage ? new UiLayoutOffset(0, ratio / 100f) : new UiLayoutOffset(n));
    }

    private static bool Finite(string token, out float number) =>
        float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !float.IsNaN(number) && !float.IsInfinity(number);

    public static UiAttributeValue Number(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        return new UiAttributeValue(value.ToString("R", CultureInfo.InvariantCulture), value, false, 5,
            Array.AsReadOnly(new[] { value }), new UiLayoutOffset(value));
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

    internal bool TryOffset(out UiLayoutOffset value) { value = offset; return (flags & 4) != 0; }
    internal bool TryNumbers(out IReadOnlyList<float> value) { value = numbers ?? Array.Empty<float>(); return numbers != null; }
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
            case "minwidth":
            case "maxwidth":
            case "breakpoint":
            case "cols":
            case "narrowcols":
            case "visiblerows":
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
                UiAttributeKind.Length => v.TryNumber(out _) || v.Text.Trim().Length == 0 || v.Text.Trim().Equals("Auto", StringComparison.OrdinalIgnoreCase) || v.Text.Trim().Equals("MatchContent", StringComparison.OrdinalIgnoreCase),
                _ => true
            };
            if (!valid)
                throw new UiContractException("Invalid " + entry.Key + " literal '" + v.Text + "' at '"+path+"'; expected " + (entry.Value.Kind == UiAttributeKind.Length ? "a finite number, Auto or MatchContent" : entry.Value.Kind.ToString()) + ".", source, spec.Id, spec.Kind, path);
        }
    }
}

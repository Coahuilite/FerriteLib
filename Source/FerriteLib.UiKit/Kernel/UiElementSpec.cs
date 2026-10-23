using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FerriteLib.UiKit.Kernel;
/// <summary>One immutable, typed declaration used by both XML and C# authoring.</summary>
public sealed class UiElementSpec
{
    public string Id { get; }
    public string Kind { get; }
    public IReadOnlyDictionary<string, UiAttributeValue> Values { get; }
    public IReadOnlyDictionary<string, string> Attributes { get; }
    public IReadOnlyList<UiElementSpec> Children { get; }
    public IReadOnlyList<UiChoice<string>> Choices { get; }

    public UiElementSpec(string id, string kind, IReadOnlyDictionary<string, UiAttributeValue>? values = null, IReadOnlyList<UiElementSpec>? children = null, IReadOnlyList<UiChoice<string>>? choices = null)
    {
        Id = id ?? "";
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        if (kind.Length == 0)
            throw new ArgumentException("Widget kind must not be empty.", nameof(kind));
        var copy = new Dictionary<string, UiAttributeValue>(StringComparer.OrdinalIgnoreCase);
        if (values != null)
            foreach (var p in values)
                copy[p.Key] = p.Value;
        Values = new ReadOnlyDictionary<string, UiAttributeValue>(copy);
        Attributes = new ReadOnlyDictionary<string, string>(copy.ToDictionary(p => p.Key, p => p.Value.Text, StringComparer.OrdinalIgnoreCase));
        Children = Array.AsReadOnly((children ?? Array.Empty<UiElementSpec>()).ToArray());
        Choices = Array.AsReadOnly((choices ?? Array.Empty<UiChoice<string>>()).ToArray());
    }

    // XML boundary and internal derived declarations. Public C# authors construct typed values directly.
    internal UiElementSpec(string id, string kind, IReadOnlyDictionary<string, string>? attributes, IReadOnlyList<UiElementSpec>? children = null, IReadOnlyList<UiChoice<string>>? choices = null) : this(id, kind, attributes?.ToDictionary(p => p.Key, p => UiAttributeValue.Literal(p.Value), StringComparer.OrdinalIgnoreCase), children, choices)
    {
    }

    public bool TryGetAttribute(string name, out string value) => Attributes.TryGetValue(name, out value!);
    public bool TryGetNumber(string name, out float value)
    {
        value = 0;
        return Values.TryGetValue(name, out var literal) && literal.TryNumber(out value);
    }

    public bool TryGetBoolean(string name, out bool value)
    {
        value = false;
        return Values.TryGetValue(name, out var literal) && literal.TryBoolean(out value);
    }

    public static UiElementSpec Empty { get; } = new UiElementSpec("", "empty");
}

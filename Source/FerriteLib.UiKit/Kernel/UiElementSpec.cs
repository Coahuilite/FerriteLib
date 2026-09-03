using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Parsed XML element spec for a widget. Attribute lookup is case-insensitive; the stored
/// dictionaries are read-only snapshots.
/// </summary>
public sealed class UiElementSpec
{
    private readonly IReadOnlyDictionary<string, string> attributes;

    /// <summary>Stable XML id; empty for non-interactive elements.</summary>
    public string Id { get; }

    /// <summary>Widget Kind tag as written in the manifest.</summary>
    public string Kind { get; }

    /// <summary>All XML attributes (including Id and Kind) keyed case-insensitively.</summary>
    public IReadOnlyDictionary<string, string> Attributes => attributes;

    /// <summary>Child specs. Flat manifests produce an empty list.</summary>
    public IReadOnlyList<UiElementSpec> Children { get; }

    public UiElementSpec(
        string id,
        string kind,
        IReadOnlyDictionary<string, string>? attributes = null,
        IReadOnlyList<UiElementSpec>? children = null)
    {
        if (kind == null) throw new ArgumentNullException(nameof(kind));
        if (kind.Length == 0) throw new ArgumentException("Widget Kind must not be empty.", nameof(kind));

        Id = id ?? "";
        Kind = kind;

        var attributeCopy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (attributes != null)
        {
            foreach (KeyValuePair<string, string> pair in attributes)
                attributeCopy[pair.Key] = pair.Value;
        }
        this.attributes = new ReadOnlyDictionary<string, string>(attributeCopy);

        Children = children == null
            ? Array.Empty<UiElementSpec>()
            : Array.AsReadOnly(children.ToArray());
    }

    /// <summary>Case-insensitive attribute lookup.</summary>
    public bool TryGetAttribute(string name, out string value)
    {
        if (name == null)
        {
            value = null!;
            return false;
        }
        return attributes.TryGetValue(name, out value);
    }

    public static UiElementSpec Empty { get; } = new UiElementSpec();

    private UiElementSpec()
    {
        Id = "";
        Kind = "";
        attributes = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        Children = Array.Empty<UiElementSpec>();
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// One registered widget kind as the registry can describe it without being asked for an instance.
/// <para>
/// <b>Identity is the pair.</b> A kind name is not unique across scopes - two mods may both register
/// <c>details</c>, which is the whole reason scope exists - so every descriptor carries the scope it was
/// declared in and is meaningless without it. The library's older <see cref="UiWidgetRegistry.KnownKinds(string?)"/>
/// merges names across scopes and therefore cannot answer "which kinds exist, and where".
/// </para>
/// <para>
/// <b>Undeclared is not forbidden.</b> A kind registered without an attribute schema is not attribute-checked;
/// <see cref="HasAttributeSchema"/> is false and <see cref="AllowedAttributes"/> is empty, and a caller that
/// renders this must say "not declared" rather than "no attributes allowed". The two are opposite claims.
/// </para>
/// <para>
/// The attribute collections are copies owned by this value. Mutating whatever collection a caller built the
/// descriptor from cannot reach the registry, and mutating <see cref="AllowedAttributes"/> cannot reach another
/// descriptor.
/// </para>
/// </summary>
public readonly struct UiWidgetDescriptor
{
    public UiWidgetDescriptor(
        string scope,
        string kind,
        IReadOnlyCollection<string>? allowedAttributes,
        IReadOnlyCollection<string>? labelAttributes)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));

        // Declared-ness is read from the arguments, not from the copies: a declared-but-empty collection and a
        // missing one are different claims, and the copies below turn both into non-null empty collections.
        HasAttributeSchema = allowedAttributes != null;
        HasLabelSet = labelAttributes != null;
        AllowedAttributes = Copy(allowedAttributes);
        LabelAttributes = Copy(labelAttributes);
    }

    /// <summary>The scope the kind was declared in - a registration scope, not necessarily the packageId.</summary>
    public string Scope { get; }

    /// <summary>The kind string, unique only within <see cref="Scope"/>.</summary>
    public string Kind { get; }

    /// <summary>The declared allowed-attribute schema, or empty when the kind declared none.</summary>
    public IReadOnlyCollection<string> AllowedAttributes { get; }

    /// <summary>The attributes that carry the kind's own label text, or empty when it declared none.</summary>
    public IReadOnlyCollection<string> LabelAttributes { get; }

    /// <summary>True when the kind declared an attribute schema; false means "not declared", not "none allowed".</summary>
    public bool HasAttributeSchema { get; }

    /// <summary>True when the kind declared which attributes carry its label; false means "not Auto-measurable".</summary>
    public bool HasLabelSet { get; }

    /// <summary>
    /// A private, ordinal-sorted, read-only copy of one declared collection, and the reason the type's own
    /// promise holds: a caller that keeps a list it passed in and mutates it cannot move this descriptor, and a
    /// caller that reads <see cref="AllowedAttributes"/> cannot move the registry, because what it receives is
    /// this copy and not the registry's set. Sorting is part of the description: a HashSet enumerates in
    /// insertion order, and a catalogue that rendered that order would be describing the registration sequence
    /// rather than the kind.
    /// </summary>
    private static IReadOnlyCollection<string> Copy(IReadOnlyCollection<string>? source)
    {
        if (source == null || source.Count == 0)
        {
            return Array.Empty<string>();
        }

        var copy = new List<string>(source.Count);
        foreach (string value in source)
        {
            copy.Add(value ?? "");
        }

        copy.Sort(StringComparer.Ordinal);
        return new ReadOnlyCollection<string>(copy);
    }

    /// <summary>Diagnostic rendering; never parsed back.</summary>
    public override string ToString()
    {
        return Scope + "/" + Kind;
    }
}

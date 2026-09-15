using System;
using System.Collections.Generic;

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
        AllowedAttributes = allowedAttributes ?? Array.Empty<string>();
        LabelAttributes = labelAttributes ?? Array.Empty<string>();
        HasAttributeSchema = allowedAttributes != null;
        HasLabelSet = labelAttributes != null;
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

    /// <summary>Diagnostic rendering; never parsed back.</summary>
    public override string ToString()
    {
        return Scope + "/" + Kind;
    }
}

using System;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Stable identity of one element inside one arranged tree (0.4.0 identity layer). The key is the
/// element's structural path: a declared <c>Id</c> when it has one, otherwise <c>Kind[declaredIndex]</c>
/// - the child's position among its parent's declared children, not among the children that happen to
/// be visible, so hiding a sibling or switching a <c>Tab</c> does not renumber the others. Two unnamed
/// siblings of the same kind therefore receive different identities, which is exactly the alias a bare
/// path string could not express: <c>UiLayoutEngine</c> used to fall back to the kind alone, so the two
/// shared one widget instance, one session state slot, one recovery slot and one scroll key.
/// <para>
/// The identity is built once per element during arrange and stored on the placed entry, so the same
/// element reports the same value in Measure, in Draw and on later passes over an unchanged tree. That
/// stability is what lets per-element state survive a frame, and it is why this type is a value rather
/// than a string regenerated at each call site.
/// </para>
/// <para>
/// Grammar, enforced at creation time by <c>UiLayoutManifest</c>. A key is unique only if every
/// segment is one path element, so two shapes are refused: a declared <c>Id</c> containing the
/// <c>'/'</c> separator (it would spell a deeper path), and a declared <c>Id</c> that spells an
/// unnamed sibling's generated <c>Kind[declaredIndex]</c> segment (the two siblings would share one
/// key). What stays open by construction is a <c>Kind</c> that itself contains <c>'/'</c>: kinds are
/// namespaced vocabulary (<c>input/stepper-slider</c>) and cannot be refused, so a generated segment
/// still carries a separator and a deliberately crafted Id chain can still meet it. Closing that
/// needs identity to stop being a flat string, which is the node-object step; it is written down here
/// rather than left implicit.
/// </para>
/// </summary>
public readonly struct UiNodeId : IEquatable<UiNodeId>
{
    /// <summary>
    /// The session-level identity: "no element". A caller outside any element's Measure/Draw (a host
    /// level call, a popup pass) resolves its state here, and it never equals a real element's identity.
    /// </summary>
    public static readonly UiNodeId None = default;

    private readonly string? key;

    private UiNodeId(string key)
    {
        this.key = key;
    }

    /// <summary>
    /// Canonical key: unique inside one tree and stable across its passes. Empty for <see cref="None"/>.
    /// </summary>
    public string Key => key ?? "";

    /// <summary>True when this identity is <see cref="None"/>, i.e. no element is being arranged or drawn.</summary>
    public bool IsNone => Key.Length == 0;

    /// <summary>Identity of the manifest root declared at <paramref name="declaredIndex"/>.</summary>
    internal static UiNodeId Root(UiElementSpec spec, int declaredIndex)
    {
        return new UiNodeId(Segment(spec, declaredIndex));
    }

    /// <summary>Identity of the child declared at <paramref name="declaredIndex"/>.</summary>
    internal UiNodeId Child(UiElementSpec spec, int declaredIndex)
    {
        string segment = Segment(spec, declaredIndex);
        return new UiNodeId(Key.Length == 0 ? segment : Key + "/" + segment);
    }

    /// <summary>
    /// One identity segment. A declared <c>Id</c> wins; without one the segment is the kind plus the
    /// child's declared ordinal, because the kind alone is shared by every unnamed sibling.
    /// </summary>
    private static string Segment(UiElementSpec spec, int declaredIndex)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));
        return spec.Id.Length > 0 ? spec.Id : GeneratedSegment(spec.Kind, declaredIndex);
    }

    /// <summary>
    /// The identity segment an element without a declared <c>Id</c> contributes: its kind plus its
    /// declared ordinal. One source of truth for this grammar, because the manifest parser has to
    /// refuse a declared <c>Id</c> that spells one of these (see
    /// <c>UiLayoutManifest.ValidateIdentitySegments</c>) and two copies of the format would drift.
    /// </summary>
    internal static string GeneratedSegment(string kind, int declaredIndex)
    {
        if (kind == null) throw new ArgumentNullException(nameof(kind));
        return kind + "[" + declaredIndex.ToString(CultureInfo.InvariantCulture) + "]";
    }

    public bool Equals(UiNodeId other)
    {
        return string.Equals(Key, other.Key, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is UiNodeId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Key);
    }

    public override string ToString()
    {
        return Key;
    }

    public static bool operator ==(UiNodeId left, UiNodeId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(UiNodeId left, UiNodeId right)
    {
        return !left.Equals(right);
    }
}

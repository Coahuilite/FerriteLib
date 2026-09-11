using System;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Stable identity of one element inside one arranged tree (0.4.0 identity layer). It carries two
/// strings, and the difference between them is the point:
/// <list type="bullet">
/// <item><see cref="Key"/> is the canonical identity - the element's segments joined by a separator no
/// XML text can carry, so two different elements cannot produce the same key and identity is structural
/// rather than textual. This is what the session's node table and state are keyed by.</item>
/// <item><see cref="Path"/> is the display path a human reads in the fit audit, the recovery band and
/// the contract diagnostics. It joins the same segments with '/', which is the string this library has
/// always printed - and it is the one that can still be ambiguous when a <c>Kind</c> itself contains
/// '/', because kinds are namespaced vocabulary (<c>input/stepper-slider</c>). That residual is
/// deliberate: display text must stay readable and unchanged for existing consumers, so identity, not
/// display, is what the engine and the session key on.</item>
/// </list>
/// <para>
/// Grammar, enforced at creation time by <c>UiLayoutManifest</c>: a declared <c>Id</c> may not contain
/// the '/' separator (it would spell a deeper path), and may not spell an unnamed sibling's generated
/// <c>Kind[declaredIndex]</c> segment (the two siblings would claim one position). Those guards are what
/// make <see cref="Key"/> injective, so they are load-bearing for the node step and are never removed:
/// segment texts come from XML attribute values, which cannot contain <see cref="KeySeparator"/>, and
/// the one ambiguity that would remain - a declared Id equal to a generated segment - is refused per
/// parent, with global Id uniqueness covering the declared-vs-declared case.
/// </para>
/// </summary>
public readonly struct UiNodeId : IEquatable<UiNodeId>
{
    /// <summary>
    /// The session-level identity: "no element". A caller outside any element's Measure/Draw (a host
    /// level call, a popup pass) resolves its state here, and it never equals a real element's identity.
    /// </summary>
    public static readonly UiNodeId None = default;

    /// <summary>
    /// Segment separator of <see cref="Key"/>. U+001F is not a legal XML character, so neither an Id nor
    /// a Kind - both read out of XML - can contain it, which is what makes the join unambiguous.
    /// </summary>
    internal const char KeySeparator = '\u001F';

    /// <summary>The display separator: the character this library has always joined paths with.</summary>
    public const char PathSeparator = '/';

    private readonly string? key;
    private readonly string? path;

    private UiNodeId(string key, string path)
    {
        this.key = key;
        this.path = path;
    }

    /// <summary>
    /// Canonical identity key: unique inside one tree and stable across its passes; empty for
    /// <see cref="None"/>. It is not meant for humans - log it through <see cref="Path"/>.
    /// </summary>
    public string Key => key ?? "";

    /// <summary>
    /// Display path: the same element named for a human, exactly the string earlier versions printed.
    /// </summary>
    public string Path => path ?? "";

    /// <summary>True when this identity is <see cref="None"/>, i.e. no element is being arranged or drawn.</summary>
    public bool IsNone => Key.Length == 0;

    /// <summary>Identity of the manifest root declared at <paramref name="declaredIndex"/>.</summary>
    internal static UiNodeId Root(UiElementSpec spec, int declaredIndex)
    {
        string segment = Segment(spec, declaredIndex);
        return new UiNodeId(segment, segment);
    }

    /// <summary>Identity of the child declared at <paramref name="declaredIndex"/>.</summary>
    internal UiNodeId Child(UiElementSpec spec, int declaredIndex)
    {
        string segment = Segment(spec, declaredIndex);
        string childKey = Key.Length == 0 ? segment : Key + KeySeparator + segment;
        string childPath = Path.Length == 0 ? segment : Path + PathSeparator + segment;
        return new UiNodeId(childKey, childPath);
    }

    private static string Segment(UiElementSpec spec, int declaredIndex)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));
        return spec.Id.Length > 0 ? spec.Id : GeneratedSegment(spec.Kind, declaredIndex);
    }

    /// <summary>
    /// The identity segment an element without a declared <c>Id</c> contributes: its kind plus its
    /// declared ordinal. One source of truth for this grammar, because the manifest parser refuses a
    /// declared <c>Id</c> that spells one of these (see
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

    /// <summary>The display path - never the canonical key, which can carry control characters.</summary>
    public override string ToString()
    {
        return Path;
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

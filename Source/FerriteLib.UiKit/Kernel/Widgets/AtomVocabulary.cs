using System;
using System.Globalization;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Shared vocabulary of the core leaf atoms. One type owns the two things the atoms must agree on:
/// the creation-time attribute schema each kind registers, and the way their XML values are read.
/// <para>
/// The seam exists for a second, forward-looking reason. The role axis of the 0.4.x style work adds
/// <c>Tone</c>/<c>Emphasis</c> as per-element attributes resolved through the theme's resolved-value
/// table; those two names join <see cref="Schema"/> here, once, and every atom that declared its
/// schema through this helper accepts them together. An atom that kept its own literal array would
/// otherwise refuse the role attributes at creation time while its neighbours accepted them.
/// </para>
/// </summary>
internal static class AtomVocabulary
{
    // The names the engine already reads on every widget regardless of kind (UiHost's common widget
    // vocabulary) plus the two identity names. Listed explicitly so an atom's schema reads as the
    // complete contract instead of "whatever the engine adds on the side" — the shape the six
    // pre-existing core kinds use as well.
    private static readonly string[] EngineWideAttributes = { "Id", "Kind", "Tab", "Hidden" };

    /// <summary>
    /// The allowed-attribute array a core atom registers. <paramref name="kindAttributes"/> are the
    /// names the kind itself reads; anything not listed is refused at Host creation.
    /// </summary>
    internal static string[] Schema(params string[] kindAttributes)
    {
        var allowed = new string[EngineWideAttributes.Length + kindAttributes.Length];
        EngineWideAttributes.CopyTo(allowed, 0);
        kindAttributes.CopyTo(allowed, EngineWideAttributes.Length);
        return allowed;
    }

    /// <summary>Raw attribute value, or "" when absent — never null, so callers do not branch twice.</summary>
    internal static string Read(UiElementSpec spec, string name)
    {
        return spec.TryGetAttribute(name, out string value) ? value : "";
    }

    /// <summary>Declared number, or <paramref name="fallback"/> when absent or malformed.</summary>
    internal static float ReadFloat(UiElementSpec spec, string name, float fallback)
    {
        return spec.TryGetAttribute(name, out string raw)
            && float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }

    /// <summary>Declared boolean ("true"/"1"), or <paramref name="fallback"/> when absent.</summary>
    internal static bool ReadBool(UiElementSpec spec, string name, bool fallback)
    {
        if (!spec.TryGetAttribute(name, out string raw)) return fallback;
        string value = raw.Trim();
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.Ordinal);
    }

    /// <summary>The kind's own band height, or <paramref name="fallback"/> when none is declared.</summary>
    internal static float ReadHeight(UiElementSpec spec, float fallback)
    {
        float height = ReadFloat(spec, "Height", 0f);
        return height > 0f ? height : fallback;
    }

    /// <summary>
    /// The typed binding key every value atom uses: the <c>Bind</c> attribute when present, else the
    /// element Id. Both may be empty, and the atom's Validate is what refuses that at creation.
    /// </summary>
    internal static string ReadBindKey(UiElementSpec spec)
    {
        string bind = Read(spec, "Bind");
        return bind.Length > 0 ? bind : spec.Id;
    }

    /// <summary>
    /// The kind's own display string: the key attribute resolved through the translation seam when
    /// filled, else the literal attribute. The engine's <c>Width="Auto"</c> seam resolves the very
    /// same attribute pair, which is why a kind's declared label set names these two attributes.
    /// </summary>
    internal static string ResolveText(UiElementSpec spec, UiWidgetContext ctx, string literalAttribute, string keyAttribute)
    {
        if (spec.TryGetAttribute(keyAttribute, out string key) && key.Length > 0)
        {
            return ctx.Translation.Translate(key);
        }

        return Read(spec, literalAttribute);
    }
}
